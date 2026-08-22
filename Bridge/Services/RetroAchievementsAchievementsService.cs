using System.Collections.Concurrent;
using System.Net.Http;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Emulation;
using Bridge.Metadata;
using Bridge.Resources;
using Bridge.Storage.Repositories;

namespace Bridge.Services;

public enum RomAchievementsStatus
{
    None,
    NoPlatform,
    NotMatched,
    ApiError,
    NoAchievements,
}

/// <summary>Loads RetroAchievements progress for ROM library games.</summary>
public sealed class RetroAchievementsAchievementsService(
    IRepository<GameSource> sourceRepository,
    RetroAchievementsSettings settings,
    RetroAchievementsClient client,
    RetroAchievementsHashIndex hashIndex)
{
    private readonly Guid _romSourceId = sourceRepository.GetOrCreateByName("ROM").Id;
    private readonly ConcurrentDictionary<(Guid GameId, string Username), GameAchievementsSnapshot?> _cache = new();
    private readonly ConcurrentDictionary<Guid, RomAchievementsStatus> _lastStatus = new();

    public bool IsRomGame(Game game) =>
        game.Roms.Count > 0 || game.SourceId == _romSourceId;

    public bool IsConfigured() => settings.IsConfigured;

    public RomAchievementsStatus GetLastStatus(Game game) =>
        _lastStatus.GetValueOrDefault(game.Id, RomAchievementsStatus.None);

    public string GetEmptyMessage(Game game) =>
        GetLastStatus(game) switch
        {
            RomAchievementsStatus.NoPlatform => Strings.AchievementsRomUnsupportedPlatform,
            RomAchievementsStatus.NotMatched => HasDatMetadata(game) && game.Roms.Count > 0
                ? Strings.AchievementsRomDatNameNotRaVerified
                : Strings.AchievementsRomNotMatched,
            RomAchievementsStatus.ApiError => Strings.AchievementsRomApiError,
            RomAchievementsStatus.NoAchievements => Strings.AchievementsNone,
            _ => Strings.AchievementsNone,
        };

    public bool TryGetCached(Game game, out GameAchievementsSnapshot? snapshot)
    {
        snapshot = null;
        if (!IsRomGame(game) || !settings.IsConfigured)
            return false;

        return _cache.TryGetValue((game.Id, settings.Username), out snapshot);
    }

    public async Task<GameAchievementsSnapshot?> LoadForGameAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        if (!IsRomGame(game) || !settings.IsConfigured || game.Roms.Count == 0)
            return null;

        var key = (game.Id, settings.Username);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var rom = game.Roms[0];
        var md5 = RomMd5.TryComputeFromRomPath(rom.Path);
        if (string.IsNullOrWhiteSpace(md5))
        {
            SetStatus(game.Id, RomAchievementsStatus.NotMatched);
            _cache[key] = null;
            return null;
        }

        var platformName = ResolvePlatformName(rom);
        if (string.IsNullOrWhiteSpace(platformName))
        {
            SetStatus(game.Id, RomAchievementsStatus.NoPlatform);
            _cache[key] = null;
            return null;
        }

        GameAchievementsSnapshot? snapshot;
        try
        {
            var consoleIds = await hashIndex.GetConsoleIdsAsync(settings.WebApiKey, cancellationToken);
            var lookupConsoleIds = RetroAchievementsConsoleCatalog.ResolveConsoleIdsForHashLookup(
                platformName,
                consoleIds);
            if (lookupConsoleIds.Count == 0)
            {
                SetStatus(game.Id, RomAchievementsStatus.NoPlatform);
                _cache[key] = null;
                return null;
            }

            int? gameId = null;
            foreach (var lookupConsoleId in lookupConsoleIds)
            {
                gameId = await hashIndex.TryResolveGameIdAsync(
                    md5,
                    lookupConsoleId,
                    settings.WebApiKey,
                    cancellationToken);
                if (gameId is not null)
                    break;
            }

            if (gameId is null)
            {
                SetStatus(game.Id, RomAchievementsStatus.NotMatched);
                _cache[key] = null;
                return null;
            }

            snapshot = await client.GetGameAchievementsAsync(
                settings.WebApiKey,
                settings.Username,
                gameId.Value,
                cancellationToken);

            SetStatus(game.Id, snapshot is null ? RomAchievementsStatus.NoAchievements : RomAchievementsStatus.None);
        }
        catch (HttpRequestException)
        {
            SetStatus(game.Id, RomAchievementsStatus.ApiError);
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

        _cache[key] = snapshot;
        return snapshot;
    }

    public void ClearCache()
    {
        _cache.Clear();
        _lastStatus.Clear();
        hashIndex.ClearMemoryCache();
    }

    private void SetStatus(Guid gameId, RomAchievementsStatus status)
    {
        if (status == RomAchievementsStatus.None)
            _lastStatus.TryRemove(gameId, out _);
        else
            _lastStatus[gameId] = status;
    }

    private static bool HasDatMetadata(Game game) =>
        game.Roms.Any(rom =>
            !string.IsNullOrWhiteSpace(rom.DatPlatform) ||
            !string.IsNullOrWhiteSpace(rom.DatRegion));

    private static string? ResolvePlatformName(GameRom rom)
    {
        if (!string.IsNullOrWhiteSpace(rom.DatPlatform))
        {
            var fromDat = RomPlatformCatalog.FindByPlatformName(rom.DatPlatform)?.PlatformName ?? rom.DatPlatform;
            if (!string.IsNullOrWhiteSpace(fromDat))
                return fromDat;
        }

        var extension = RomArchivePath.GetRomExtension(rom.Path);
        return RomPlatformCatalog.TryGetByExtension(extension, out var platform)
            ? platform!.PlatformName
            : null;
    }
}
