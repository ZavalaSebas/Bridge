using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Bridge.Metadata;

/// GraphQL client for Epic Games Store achievements (definitions + player progress).
public sealed class EpicAchievementsClient(HttpClient httpClient)
{
    private const string GraphQlUrl = "https://launcher.store.epicgames.com/graphql";
    private const string StoreUserAgent = "EpicGamesLauncher/14.0.8-22004686+++Portal+Release-Live";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<EpicAchievementCatalog?> GetCatalogAsync(
        string accessToken,
        string sandboxId,
        string locale,
        CancellationToken cancellationToken = default)
    {
        var payload = await PostGraphQlAsync(
            accessToken,
            AchievementDefinitionsQuery,
            new { sandboxId, locale },
            cancellationToken).ConfigureAwait(false);

        return EpicAchievementCatalog.TryParse(payload);
    }

    public async Task<EpicPlayerAchievementRecord?> GetPlayerRecordAsync(
        string accessToken,
        string accountId,
        string sandboxId,
        CancellationToken cancellationToken = default)
    {
        var payload = await PostGraphQlAsync(
            accessToken,
            PlayerAchievementsQuery,
            new { epicAccountId = accountId, sandboxId },
            cancellationToken).ConfigureAwait(false);

        return EpicPlayerAchievementRecord.TryParse(payload, sandboxId);
    }

    private async Task<JsonDocument?> PostGraphQlAsync(
        string accessToken,
        string query,
        object variables,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
        request.Headers.TryAddWithoutValidation("User-Agent", StoreUserAgent);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private const string AchievementDefinitionsQuery = """
        query Achievement($sandboxId: String!, $locale: String!) {
          Achievement {
            productAchievementsRecordBySandbox(sandboxId: $sandboxId, locale: $locale) {
              sandboxId
              totalAchievements
              achievements {
                achievement {
                  name
                  hidden
                  unlockedDisplayName
                  lockedDisplayName
                  unlockedDescription
                  lockedDescription
                  unlockedIconLink
                  lockedIconLink
                  rarity { percent }
                }
              }
            }
          }
        }
        """;

    private const string PlayerAchievementsQuery = """
        query PlayerAchievement($epicAccountId: String!, $sandboxId: String!) {
          PlayerAchievement {
            playerAchievementGameRecordsBySandbox(epicAccountId: $epicAccountId, sandboxId: $sandboxId) {
              records {
                totalUnlocked
                playerAchievements {
                  playerAchievement {
                    achievementName
                    unlocked
                    unlockDate
                    progress
                  }
                }
              }
            }
          }
        }
        """;
}

public sealed class EpicAchievementDefinition
{
    public required string Name { get; init; }
    public bool Hidden { get; init; }
    public required string UnlockedDisplayName { get; init; }
    public required string LockedDisplayName { get; init; }
    public required string UnlockedDescription { get; init; }
    public required string LockedDescription { get; init; }
    public string? UnlockedIconLink { get; init; }
    public string? LockedIconLink { get; init; }
    public double? GlobalUnlockPercent { get; init; }
}

public sealed class EpicAchievementCatalog
{
    public required string SandboxId { get; init; }
    public required IReadOnlyList<EpicAchievementDefinition> Achievements { get; init; }

    internal static EpicAchievementCatalog? TryParse(JsonDocument? document)
    {
        if (document is null)
            return null;

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("Achievement", out var achievementRoot) ||
            !achievementRoot.TryGetProperty("productAchievementsRecordBySandbox", out var record))
        {
            return null;
        }

        var sandboxId = record.TryGetProperty("sandboxId", out var sandboxElement)
            ? sandboxElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(sandboxId))
            return null;

        if (!record.TryGetProperty("achievements", out var achievementsElement) ||
            achievementsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var achievements = new List<EpicAchievementDefinition>();
        foreach (var item in achievementsElement.EnumerateArray())
        {
            if (!item.TryGetProperty("achievement", out var achievementElement))
                continue;

            var name = achievementElement.GetStringProperty("name");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            double? globalPercent = null;
            if (achievementElement.TryGetProperty("rarity", out var rarityElement) &&
                rarityElement.TryGetProperty("percent", out var percentElement) &&
                percentElement.TryGetDouble(out var percent))
            {
                globalPercent = percent;
            }

            achievements.Add(new EpicAchievementDefinition
            {
                Name = name,
                Hidden = achievementElement.GetBooleanProperty("hidden"),
                UnlockedDisplayName = achievementElement.GetStringProperty("unlockedDisplayName") ?? name,
                LockedDisplayName = achievementElement.GetStringProperty("lockedDisplayName") ?? name,
                UnlockedDescription = achievementElement.GetStringProperty("unlockedDescription") ?? string.Empty,
                LockedDescription = achievementElement.GetStringProperty("lockedDescription") ?? string.Empty,
                UnlockedIconLink = achievementElement.GetStringProperty("unlockedIconLink"),
                LockedIconLink = achievementElement.GetStringProperty("lockedIconLink"),
                GlobalUnlockPercent = globalPercent,
            });
        }

        if (achievements.Count == 0)
            return null;

        return new EpicAchievementCatalog
        {
            SandboxId = sandboxId,
            Achievements = achievements,
        };
    }
}

public sealed class EpicPlayerAchievementState
{
    public required string AchievementName { get; init; }
    public bool Unlocked { get; init; }
    public DateTime? UnlockedAt { get; init; }
    public double Progress { get; init; }
}

public sealed class EpicPlayerAchievementRecord
{
    public required string SandboxId { get; init; }
    public required IReadOnlyDictionary<string, EpicPlayerAchievementState> ByName { get; init; }
    public int UnlockedCount { get; init; }

    internal static EpicPlayerAchievementRecord? TryParse(JsonDocument? document, string sandboxId)
    {
        if (document is null)
            return null;

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("PlayerAchievement", out var playerRoot) ||
            !playerRoot.TryGetProperty("playerAchievementGameRecordsBySandbox", out var recordsRoot) ||
            !recordsRoot.TryGetProperty("records", out var recordsElement) ||
            recordsElement.ValueKind != JsonValueKind.Array ||
            recordsElement.GetArrayLength() == 0)
        {
            return null;
        }

        var record = recordsElement[0];
        var unlockedCount = record.TryGetProperty("totalUnlocked", out var unlockedElement) &&
                            unlockedElement.TryGetInt32(out var parsedUnlocked)
            ? parsedUnlocked
            : 0;

        var byName = new Dictionary<string, EpicPlayerAchievementState>(StringComparer.OrdinalIgnoreCase);
        if (record.TryGetProperty("playerAchievements", out var playerAchievementsElement) &&
            playerAchievementsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in playerAchievementsElement.EnumerateArray())
            {
                if (!item.TryGetProperty("playerAchievement", out var achievementElement))
                    continue;

                var name = achievementElement.GetStringProperty("achievementName");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                DateTime? unlockedAt = null;
                var unlockDate = achievementElement.GetStringProperty("unlockDate");
                if (!string.IsNullOrWhiteSpace(unlockDate) &&
                    DateTime.TryParse(unlockDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                {
                    unlockedAt = parsed;
                }

                var progress = achievementElement.TryGetProperty("progress", out var progressElement) &&
                               progressElement.TryGetDouble(out var parsedProgress)
                    ? parsedProgress
                    : 0;

                byName[name] = new EpicPlayerAchievementState
                {
                    AchievementName = name,
                    Unlocked = achievementElement.GetBooleanProperty("unlocked"),
                    UnlockedAt = unlockedAt,
                    Progress = progress,
                };
            }
        }

        return new EpicPlayerAchievementRecord
        {
            SandboxId = sandboxId,
            ByName = byName,
            UnlockedCount = unlockedCount,
        };
    }
}

internal static class EpicJsonElementExtensions
{
    internal static string? GetStringProperty(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    internal static bool GetBooleanProperty(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();
}
