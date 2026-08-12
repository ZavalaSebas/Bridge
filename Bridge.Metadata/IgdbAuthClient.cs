using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Bridge.Metadata;

/// <summary>
/// IGDB authenticates through Twitch's OAuth2 client-credentials flow, not
/// its own login — this is IGDB's real, documented mechanism (IGDB is owned
/// by Twitch/Amazon), not a Bridge invention. Token is cached in memory and
/// refreshed (with a 60s safety margin) only when it's actually expired —
/// callers don't need to think about caching themselves.
/// </summary>
public class IgdbAuthClient(HttpClient httpClient, IgdbSettings settings)
{
    private const string TokenUrl = "https://id.twitch.tv/oauth2/token";

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _accessToken;
        }

        // Guard the refresh so two concurrent callers don't both hit the token
        // endpoint (and so the second one uses the token the first one fetched).
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring the lock — another caller may have
            // refreshed while we waited.
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _accessToken;
            }

            if (!settings.IsConfigured)
            {
                throw new InvalidOperationException("IGDB Client ID/Secret are not configured.");
            }

            var url = $"{TokenUrl}?client_id={Uri.EscapeDataString(settings.ClientId)}" +
                       $"&client_secret={Uri.EscapeDataString(settings.ClientSecret)}" +
                       "&grant_type=client_credentials";

            using var response = await httpClient.PostAsync(url, content: null, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Twitch token endpoint returned an empty response.");

            // A missing/blank token must not be cached as valid — that would make
            // every subsequent request send an empty Bearer header until expiry.
            if (string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                throw new InvalidOperationException("Twitch token endpoint returned a blank token.");
            }

            _accessToken = payload.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn - 60);
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private class TwitchTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
