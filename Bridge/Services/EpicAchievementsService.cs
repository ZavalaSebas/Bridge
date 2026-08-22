using System.Collections.Concurrent;
using System.Net.Http;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Import.Epic;
using Bridge.Metadata;
using Bridge.Storage.Repositories;

namespace Bridge.Services;

/// <summary>Loads Epic achievements via the store GraphQL API using the launcher session.</summary>
public sealed class EpicAchievementsService(
    IRepository<GameSource> sourceRepository,
    EpicAuthClient authClient,
    EpicAchievementsClient achievementsClient)
{
    private readonly Guid _epicSourceId = sourceRepository.GetOrCreateByName("Epic").Id;
    private readonly ConcurrentDictionary<(Guid GameId, string Locale), GameAchievementsSnapshot?> _cache = new();
    private readonly Lock _sessionLock = new();
    private EpicAuthSession? _session;

    public bool IsEpicGame(Game game) => game.SourceId == _epicSourceId;

    public bool IsLauncherSessionAvailable() => EpicLauncherSessionReader.TryReadSession() is not null;

    public bool TryGetCached(Game game, out GameAchievementsSnapshot? snapshot)
    {
        snapshot = null;
        if (!IsEpicGame(game))
            return false;

        var locale = MapLocale(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        return _cache.TryGetValue((game.Id, locale), out snapshot);
    }

    public async Task<GameAchievementsSnapshot?> LoadForGameAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        if (!IsEpicGame(game) || string.IsNullOrWhiteSpace(game.ExternalId))
            return null;

        var locale = MapLocale(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        var key = (game.Id, locale);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var sandboxId = EpicManifestLookup.TryGetSandboxId(game.ExternalId);
        if (string.IsNullOrWhiteSpace(sandboxId))
        {
            _cache[key] = null;
            return null;
        }

        var authSession = await EnsureAuthSessionAsync(cancellationToken);
        if (authSession is null)
        {
            _cache[key] = null;
            return null;
        }

        EpicAchievementCatalog? catalog;
        EpicPlayerAchievementRecord? playerRecord;
        try
        {
            catalog = await achievementsClient.GetCatalogAsync(
                authSession.AccessToken,
                sandboxId,
                locale,
                cancellationToken);
            playerRecord = await achievementsClient.GetPlayerRecordAsync(
                authSession.AccessToken,
                authSession.AccountId,
                sandboxId,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            _cache[key] = null;
            return null;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        if (catalog is null || catalog.Achievements.Count == 0)
        {
            _cache[key] = null;
            return null;
        }

        var snapshot = BuildSnapshot(catalog, playerRecord);
        _cache[key] = snapshot;
        return snapshot;
    }

    public void ClearCache()
    {
        _cache.Clear();
        lock (_sessionLock)
        {
            _session = null;
        }
    }

    private async Task<EpicAuthSession?> EnsureAuthSessionAsync(CancellationToken cancellationToken)
    {
        lock (_sessionLock)
        {
            if (_session is not null && _session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return _session;
        }

        var launcherSession = EpicLauncherSessionReader.TryReadSession();
        if (launcherSession is null)
            return null;

        var refreshed = await authClient.RefreshAsync(launcherSession.RefreshToken, cancellationToken);
        if (refreshed is null)
            return null;

        lock (_sessionLock)
        {
            _session = refreshed;
            return _session;
        }
    }

    private static GameAchievementsSnapshot BuildSnapshot(
        EpicAchievementCatalog catalog,
        EpicPlayerAchievementRecord? playerRecord)
    {
        var achievements = new List<GameAchievement>();
        var unlockedCount = 0;

        foreach (var definition in catalog.Achievements)
        {
            EpicPlayerAchievementState? playerState = null;
            if (playerRecord?.ByName.TryGetValue(definition.Name, out var found) == true)
                playerState = found;

            var isUnlocked = playerState?.Unlocked == true;
            if (isUnlocked)
                unlockedCount++;

            var showUnlockedDetails = isUnlocked || !definition.Hidden;
            achievements.Add(new GameAchievement
            {
                ApiName = definition.Name,
                Name = showUnlockedDetails ? definition.UnlockedDisplayName : definition.LockedDisplayName,
                Description = showUnlockedDetails ? definition.UnlockedDescription : definition.LockedDescription,
                IsHidden = definition.Hidden,
                IsUnlocked = isUnlocked,
                UnlockedAt = playerState?.UnlockedAt,
                IconUrl = definition.UnlockedIconLink,
                IconLockedUrl = definition.LockedIconLink ?? definition.UnlockedIconLink,
                GlobalUnlockPercent = definition.GlobalUnlockPercent,
                Rarity = SteamAchievementRarity.FromGlobalPercent(definition.GlobalUnlockPercent),
            });
        }

        if (playerRecord is not null && playerRecord.UnlockedCount > unlockedCount)
            unlockedCount = playerRecord.UnlockedCount;

        return new GameAchievementsSnapshot
        {
            Achievements = achievements,
            UnlockedCount = unlockedCount,
        };
    }

    private static string MapLocale(string twoLetterCode) =>
        twoLetterCode switch
        {
            "es" => "es",
            "de" => "de",
            "fr" => "fr",
            "it" => "it",
            "pt" => "pt",
            "ru" => "ru",
            "ja" => "ja",
            "ko" => "ko",
            "zh" => "zh-CN",
            _ => "en",
        };
}
