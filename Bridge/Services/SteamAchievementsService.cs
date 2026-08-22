using System.Collections.Concurrent;
using System.Net.Http;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Import.Steam;
using Bridge.Metadata;
using Bridge.Storage.Repositories;

namespace Bridge.Services;

/// <summary>Loads Steam achievements from local cache files for library detail UI.</summary>
public sealed class SteamAchievementsService(
    IRepository<GameSource> sourceRepository,
    SteamGlobalAchievementStatsClient globalStatsClient,
    SteamCommunityAchievementsClient communityAchievementsClient)
{
    private readonly Guid _steamSourceId = sourceRepository.GetOrCreateByName("Steam").Id;
    private readonly ConcurrentDictionary<(Guid GameId, string Language), GameAchievementsSnapshot?> _cache = new();

    public bool IsSteamGame(Game game) => game.SourceId == _steamSourceId;

    public bool IsLinkedSteamGame(Game game) =>
        !IsSteamGame(game) &&
        GameSource.IsUserManaged(game.SourceId) &&
        GameDetailLinkResolver.TryResolveSteamAppId(game, out _);

    public bool SupportsAchievements(Game game) => IsSteamGame(game) || IsLinkedSteamGame(game);

    public bool IsDefinitionsOnlyGame(Game game) => IsLinkedSteamGame(game);

    public bool TryGetCached(Game game, out GameAchievementsSnapshot? snapshot)
    {
        snapshot = null;
        if (!SupportsAchievements(game))
            return false;

        var language = MapLanguage(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        return _cache.TryGetValue((game.Id, language), out snapshot);
    }

    public async Task<GameAchievementsSnapshot?> LoadForGameAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveAppId(game, out var appId))
            return null;

        var definitionsOnly = IsDefinitionsOnlyGame(game);
        var language = MapLanguage(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        var key = (game.Id, language);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var localSnapshot = await Task.Run(
            () => SteamLocalAchievementsResolver.TryGetAchievements(appId, language: language),
            cancellationToken);

        GameAchievementsSnapshot? snapshot;
        if (localSnapshot is not null)
        {
            snapshot = definitionsOnly ? ToDefinitionsOnly(localSnapshot) : localSnapshot;
        }
        else if (definitionsOnly)
        {
            try
            {
                snapshot = await communityAchievementsClient.GetCatalogAsync(appId, language, cancellationToken);
            }
            catch (HttpRequestException)
            {
                snapshot = null;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
        else
        {
            snapshot = null;
        }

        if (snapshot is null)
        {
            _cache[key] = null;
            return null;
        }

        if (definitionsOnly && localSnapshot is null)
        {
            _cache[key] = snapshot;
            return snapshot;
        }

        try
        {
            var globalPercents = await globalStatsClient.GetUnlockPercentsAsync(appId, cancellationToken);
            snapshot = EnrichWithGlobalStats(snapshot, globalPercents);
            if (definitionsOnly)
                snapshot = ToDefinitionsOnly(snapshot);
        }
        catch (HttpRequestException)
        {
            if (definitionsOnly)
                snapshot = ToDefinitionsOnly(snapshot);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            if (definitionsOnly)
                snapshot = ToDefinitionsOnly(snapshot);
        }

        _cache[key] = snapshot;
        return snapshot;
    }

    public void ClearCache() => _cache.Clear();

    private static bool TryResolveAppId(Game game, out string appId)
    {
        if (GameDetailLinkResolver.TryResolveSteamAppId(game, out var parsedAppId))
        {
            appId = parsedAppId.ToString();
            return true;
        }

        appId = string.Empty;
        return false;
    }

    private static GameAchievementsSnapshot ToDefinitionsOnly(GameAchievementsSnapshot snapshot)
    {
        var achievements = snapshot.Achievements
            .Select(achievement => new GameAchievement
            {
                ApiName = achievement.ApiName,
                Name = achievement.Name,
                Description = achievement.Description,
                IsHidden = achievement.IsHidden,
                IsUnlocked = false,
                UnlockedAt = null,
                IconUrl = achievement.IconUrl,
                IconLockedUrl = achievement.IconLockedUrl,
                GlobalUnlockPercent = achievement.GlobalUnlockPercent,
                Rarity = achievement.Rarity,
            })
            .ToList();

        return new GameAchievementsSnapshot
        {
            Achievements = achievements,
            UnlockedCount = 0,
            TracksProgress = false,
        };
    }

    private static GameAchievementsSnapshot EnrichWithGlobalStats(
        GameAchievementsSnapshot snapshot,
        IReadOnlyDictionary<string, double> globalPercents)
    {
        if (globalPercents.Count == 0)
            return snapshot;

        var achievements = snapshot.Achievements
            .Select(achievement =>
            {
                double? globalPercent = globalPercents.TryGetValue(achievement.ApiName, out var percent)
                    ? percent
                    : achievement.GlobalUnlockPercent;

                return new GameAchievement
                {
                    ApiName = achievement.ApiName,
                    Name = achievement.Name,
                    Description = achievement.Description,
                    IsHidden = achievement.IsHidden,
                    IsUnlocked = achievement.IsUnlocked,
                    UnlockedAt = achievement.UnlockedAt,
                    IconUrl = achievement.IconUrl,
                    IconLockedUrl = achievement.IconLockedUrl,
                    GlobalUnlockPercent = globalPercent,
                    Rarity = SteamAchievementRarity.FromGlobalPercent(globalPercent),
                };
            })
            .ToList();

        return new GameAchievementsSnapshot
        {
            Achievements = achievements,
            UnlockedCount = snapshot.UnlockedCount,
            TracksProgress = snapshot.TracksProgress,
        };
    }

    private static string MapLanguage(string twoLetterCode) =>
        twoLetterCode switch
        {
            "es" => "spanish",
            "de" => "german",
            "fr" => "french",
            "it" => "italian",
            "pt" => "portuguese",
            "ru" => "russian",
            "ja" => "japanese",
            "ko" => "korean",
            "zh" => "schinese",
            _ => "english",
        };
}
