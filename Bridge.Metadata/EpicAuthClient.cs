using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bridge.Metadata;

public sealed class EpicAuthSession
{
    public required string AccessToken { get; init; }
    public required string AccountId { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

/// Exchanges Epic launcher refresh tokens for API access tokens.
public sealed class EpicAuthClient(HttpClient httpClient)
{
    private const string OAuthHost = "account-public-service-prod03.ol.epicgames.com";
    private const string ClientId = "34a02cf8f4414e29b15921876da36f9a";
    private const string ClientSecret = "daafbccc737745039dffe53d94fc76cf";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<EpicAuthSession?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://{OAuthHost}/account/api/oauth/token");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ClientId}:{ClientSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["token_type"] = "eg1",
        });

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<EpicOAuthResponse>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.AccessToken) ||
            string.IsNullOrWhiteSpace(payload.AccountId))
        {
            return null;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(payload.ExpiresIn, 60));
        return new EpicAuthSession
        {
            AccessToken = payload.AccessToken,
            AccountId = payload.AccountId,
            ExpiresAt = expiresAt,
        };
    }

    private sealed class EpicOAuthResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("account_id")]
        public string? AccountId { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
