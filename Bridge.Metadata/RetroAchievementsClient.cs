using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Bridge.Core.Entities;

namespace Bridge.Metadata;

/// <summary>RetroAchievements web API client (retroachievements.org/API).</summary>
public sealed class RetroAchievementsClient(HttpClient httpClient)
{
    private const string ApiBase = "https://retroachievements.org/API/";
    private const string MediaBase = "https://media.retroachievements.org";

    public async Task<IReadOnlyDictionary<string, int>> GetConsoleIdsAsync(
        string webApiKey,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetJsonAsync(
            $"{ApiBase}API_GetConsoleIDs.php?y={Uri.EscapeDataString(webApiKey)}&a=1&g=1",
            cancellationToken).ConfigureAwait(false);

        if (payload.ValueKind != JsonValueKind.Array)
            return EmptyConsoleMap;

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in payload.EnumerateArray())
        {
            if (!RaJson.TryGetInt32(item, "ID", out var id))
                continue;

            var name = RaJson.TryGetString(item, "Name");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result[name.Trim()] = id;
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetHashIndexAsync(
        string webApiKey,
        int consoleId,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"{ApiBase}API_GetGameList.php?y={Uri.EscapeDataString(webApiKey)}&i={consoleId}&f=1&h=1";
        var payload = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        if (payload.ValueKind != JsonValueKind.Array)
            return EmptyHashMap;

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in payload.EnumerateArray())
        {
            if (!RaJson.TryGetInt32(item, "ID", out var gameId))
                continue;

            if (!RaJson.TryGetProperty(item, "Hashes", out var hashesElement) ||
                hashesElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var hashElement in hashesElement.EnumerateArray())
            {
                var hash = hashElement.GetString()?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(hash))
                    continue;

                result[hash] = gameId;
            }
        }

        return result;
    }

    public async Task<GameAchievementsSnapshot?> GetGameAchievementsAsync(
        string webApiKey,
        string username,
        int gameId,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"{ApiBase}API_GetGameInfoAndUserProgress.php?y={Uri.EscapeDataString(webApiKey)}&u={Uri.EscapeDataString(username)}&g={gameId}";
        var payload = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        if (payload.ValueKind != JsonValueKind.Object)
            return null;

        if (!RaJson.TryGetProperty(payload, "Achievements", out var achievementsElement))
            return null;

        var distinctPlayers = RaJson.TryGetInt32(payload, "NumDistinctPlayers", out var parsedPlayers)
            ? parsedPlayers
            : 0;

        var achievements = new List<GameAchievement>();
        var unlockedCount = 0;
        foreach (var item in EnumerateAchievementElements(achievementsElement))
        {
            var achievement = ParseAchievement(item, distinctPlayers);
            if (achievement is null)
                continue;

            if (achievement.IsUnlocked)
                unlockedCount++;

            achievements.Add(achievement);
        }

        if (achievements.Count == 0)
            return null;

        if (RaJson.TryGetInt32(payload, "NumAwardedToUser", out var parsedUnlocked))
            unlockedCount = parsedUnlocked;

        return new GameAchievementsSnapshot
        {
            Achievements = achievements,
            UnlockedCount = unlockedCount,
            TracksProgress = true,
        };
    }

    private static IEnumerable<JsonElement> EnumerateAchievementElements(JsonElement achievementsElement)
    {
        switch (achievementsElement.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in achievementsElement.EnumerateObject())
                    yield return property.Value;
                yield break;
            case JsonValueKind.Array:
                foreach (var item in achievementsElement.EnumerateArray())
                    yield return item;
                yield break;
        }
    }

    private static GameAchievement? ParseAchievement(JsonElement item, int distinctPlayers)
    {
        var apiName = RaJson.TryGetInt32(item, "ID", out var achievementId)
            ? achievementId.ToString(CultureInfo.InvariantCulture)
            : RaJson.TryGetString(item, "BadgeName") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiName))
            return null;

        var title = RaJson.TryGetString(item, "Title") ?? string.Empty;
        var description = RaJson.TryGetString(item, "Description") ?? string.Empty;
        var dateEarned = RaJson.TryGetString(item, "DateEarned");
        var isUnlocked = !string.IsNullOrWhiteSpace(dateEarned);
        var hidden = LooksHidden(title, description, isUnlocked);

        double? globalPercent = null;
        if (distinctPlayers > 0 &&
            RaJson.TryGetInt32(item, "NumAwarded", out var awardedCount))
        {
            globalPercent = awardedCount * 100.0 / distinctPlayers;
        }

        var badgeName = RaJson.TryGetString(item, "BadgeName");

        return new GameAchievement
        {
            ApiName = apiName,
            Name = title,
            Description = description,
            IsHidden = hidden,
            IsUnlocked = isUnlocked,
            UnlockedAt = TryParseRaDate(dateEarned),
            IconUrl = BuildBadgeUrl(badgeName, locked: false),
            IconLockedUrl = BuildBadgeUrl(badgeName, locked: true),
            GlobalUnlockPercent = globalPercent,
            Rarity = SteamAchievementRarity.FromGlobalPercent(globalPercent),
        };
    }

    private async Task<JsonElement> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return default;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return document.RootElement.Clone();
    }

    private static bool LooksHidden(string title, string description, bool isUnlocked)
    {
        if (isUnlocked)
            return false;

        if (title.Contains('?', StringComparison.Ordinal))
            return true;

        return string.IsNullOrWhiteSpace(title) &&
               string.IsNullOrWhiteSpace(description);
    }

    private static DateTime? TryParseRaDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed;

        return null;
    }

    private static string? BuildBadgeUrl(string? badgeName, bool locked)
    {
        if (string.IsNullOrWhiteSpace(badgeName))
            return null;

        var suffix = locked ? "_Lock" : string.Empty;
        return $"{MediaBase}/Badge/{badgeName}{suffix}.png";
    }

    private static IReadOnlyDictionary<string, int> EmptyConsoleMap { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, int> EmptyHashMap { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

internal static class RaJson
{
    internal static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
            return true;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    internal static string? TryGetString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    internal static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!TryGetProperty(element, propertyName, out var property))
            return false;

        return property.TryGetInt32(out value);
    }
}
