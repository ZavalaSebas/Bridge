using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bridge.Metadata;

/// <summary>
/// Unofficial client for howlongtobeat.com's internal API (main / main+extras / completionist).
/// </summary>
public sealed class HowLongToBeatClient(HttpClient httpClient)
{
    private const string BaseUrl = "https://howlongtobeat.com";
    private const string InitUrl = $"{BaseUrl}/api/bleed/init";
    private const string SearchUrl = $"{BaseUrl}/api/bleed";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private HltbAuthToken? _authToken;
    private DateTimeOffset _authTokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public async Task<HowLongToBeatGame?> SearchBestMatchAsync(
        string gameName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return null;

        var auth = await GetAuthTokenAsync(cancellationToken).ConfigureAwait(false);
        var results = await SearchAsync(gameName, auth, limit: 8, cancellationToken).ConfigureAwait(false);
        return PickBestMatch(gameName, results);
    }

    internal static HowLongToBeatGame? PickBestMatch(string query, IReadOnlyList<HowLongToBeatGame> results)
    {
        if (results.Count == 0)
            return null;

        var normalizedQuery = NormalizeName(query);
        HowLongToBeatGame? best = null;
        var bestScore = int.MinValue;

        foreach (var candidate in results)
        {
            var score = ScoreNameMatch(normalizedQuery, NormalizeName(candidate.Name));
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore >= 50 ? best : results[0];
    }

    private async Task<IReadOnlyList<HowLongToBeatGame>> SearchAsync(
        string gameName,
        HltbAuthToken auth,
        int limit,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SearchUrl)
        {
            Content = new StringContent(BuildSearchPayload(gameName, limit, auth), Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Referer", $"{BaseUrl}/");
        request.Headers.TryAddWithoutValidation("Origin", BaseUrl);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("x-auth-token", auth.Token);
        request.Headers.TryAddWithoutValidation("x-hp-key", auth.HpKey);
        request.Headers.TryAddWithoutValidation("x-hp-val", auth.HpVal);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return [];

        var envelope = await response.Content.ReadFromJsonAsync<HltbSearchEnvelope>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (envelope?.Data is not { Count: > 0 } rows)
            return [];

        return rows
            .Select(MapGame)
            .Where(g => g.HasAnyTime)
            .ToList();
    }

    private async Task<HltbAuthToken> GetAuthTokenAsync(CancellationToken cancellationToken)
    {
        if (_authToken is not null && DateTimeOffset.UtcNow < _authTokenExpiry - TimeSpan.FromMinutes(1))
            return _authToken;

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_authToken is not null && DateTimeOffset.UtcNow < _authTokenExpiry - TimeSpan.FromMinutes(1))
                return _authToken;

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{InitUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.TryAddWithoutValidation("Referer", $"{BaseUrl}/");

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<HltbAuthToken>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (token?.Token is not { Length: > 0 } ||
                token.HpKey is not { Length: > 0 } ||
                token.HpVal is not { Length: > 0 })
            {
                throw new InvalidOperationException("HowLongToBeat returned an invalid auth token.");
            }

            _authToken = token;
            _authTokenExpiry = DateTimeOffset.UtcNow.Add(TokenLifetime);
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string BuildSearchPayload(string gameName, int limit, HltbAuthToken auth)
    {
        var payload = new Dictionary<string, object?>
        {
            ["searchType"] = "games",
            ["searchTerms"] = gameName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ["searchPage"] = 1,
            ["size"] = Math.Clamp(limit, 1, 20),
            ["searchOptions"] = new Dictionary<string, object?>
            {
                ["games"] = new Dictionary<string, object?>
                {
                    ["userId"] = 0,
                    ["platform"] = string.Empty,
                    ["sortCategory"] = "popular",
                    ["rangeCategory"] = "main",
                    ["rangeTime"] = new Dictionary<string, object?> { ["min"] = null, ["max"] = null },
                    ["gameplay"] = new Dictionary<string, string>
                    {
                        ["perspective"] = string.Empty,
                        ["flow"] = string.Empty,
                        ["genre"] = string.Empty,
                        ["difficulty"] = string.Empty
                    },
                    ["rangeYear"] = new Dictionary<string, string> { ["min"] = string.Empty, ["max"] = string.Empty },
                    ["modifier"] = string.Empty
                },
                ["users"] = new Dictionary<string, string> { ["sortCategory"] = "postcount" },
                ["lists"] = new Dictionary<string, string> { ["sortCategory"] = "follows" },
                ["filter"] = string.Empty,
                ["sort"] = 0,
                ["randomizer"] = 0
            },
            ["useCache"] = true,
            [auth.HpKey!] = auth.HpVal
        };

        return JsonSerializer.Serialize(payload);
    }

    private static HowLongToBeatGame MapGame(HltbRawGame raw) => new()
    {
        Id = raw.GameId,
        Name = raw.GameName ?? string.Empty,
        MainSeconds = ToSeconds(raw.CompMain),
        ExtraSeconds = ToSeconds(raw.CompPlus),
        CompleteSeconds = ToSeconds(raw.Comp100),
        AllStylesSeconds = ToSeconds(raw.CompAll),
        ProfileUrl = raw.GameId > 0 ? $"{BaseUrl}/game/{raw.GameId}" : string.Empty
    };

    private static ulong? ToSeconds(int seconds) => seconds > 0 ? (ulong)seconds : null;

    internal static string NormalizeName(string value) =>
        new string(value.Where(c => !char.IsPunctuation(c) && !char.IsWhiteSpace(c)).ToArray())
            .ToUpperInvariant();

    internal static int ScoreNameMatch(string query, string candidate)
    {
        if (query.Length == 0 || candidate.Length == 0)
            return 0;

        if (query.Equals(candidate, StringComparison.Ordinal))
            return 100;

        if (candidate.Contains(query, StringComparison.Ordinal) || query.Contains(candidate, StringComparison.Ordinal))
            return 85;

        var overlap = candidate.Count(ch => query.Contains(ch));
        return (int)Math.Round(overlap * 100.0 / Math.Max(query.Length, candidate.Length));
    }
}

public sealed class HowLongToBeatGame
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public ulong? MainSeconds { get; init; }
    public ulong? ExtraSeconds { get; init; }
    public ulong? CompleteSeconds { get; init; }
    public ulong? AllStylesSeconds { get; init; }
    public string ProfileUrl { get; init; } = string.Empty;

    public bool HasAnyTime =>
        MainSeconds is > 0 ||
        ExtraSeconds is > 0 ||
        CompleteSeconds is > 0 ||
        AllStylesSeconds is > 0;
}

internal sealed class HltbAuthToken
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("hpKey")]
    public string? HpKey { get; set; }

    [JsonPropertyName("hpVal")]
    public string? HpVal { get; set; }
}

internal sealed class HltbSearchEnvelope
{
    [JsonPropertyName("data")]
    public List<HltbRawGame>? Data { get; set; }
}

internal sealed class HltbRawGame
{
    [JsonPropertyName("game_id")]
    public int GameId { get; set; }

    [JsonPropertyName("game_name")]
    public string? GameName { get; set; }

    [JsonPropertyName("comp_main")]
    public int CompMain { get; set; }

    [JsonPropertyName("comp_plus")]
    public int CompPlus { get; set; }

    [JsonPropertyName("comp_100")]
    public int Comp100 { get; set; }

    [JsonPropertyName("comp_all")]
    public int CompAll { get; set; }
}
