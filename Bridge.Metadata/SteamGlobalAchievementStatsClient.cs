using System.Net.Http;
using System.Text.Json;

namespace Bridge.Metadata;

/// <summary>
/// Fetches global Steam achievement unlock rates (no API key required).
/// Names match the hashed ids stored in local stats schemas.
/// </summary>
public sealed class SteamGlobalAchievementStatsClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyDictionary<string, double>> GetUnlockPercentsAsync(
        string appId,
        CancellationToken cancellationToken = default)
    {
        if (!uint.TryParse(appId, out _))
            return Empty;

        var url =
            $"https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid={appId}";

        using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return Empty;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<GlobalPercentagesResponse>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        var entries = payload?.AchievementPercentages?.Achievements;
        if (entries is null || entries.Count == 0)
            return Empty;

        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            if (double.TryParse(entry.Percent, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var percent))
            {
                result[entry.Name] = percent;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, double> Empty { get; } =
        new Dictionary<string, double>();

    private sealed class GlobalPercentagesResponse
    {
        public GlobalAchievementPercentages? AchievementPercentages { get; set; }
    }

    private sealed class GlobalAchievementPercentages
    {
        public List<GlobalAchievementEntry>? Achievements { get; set; }
    }

    private sealed class GlobalAchievementEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Percent { get; set; } = string.Empty;
    }
}
