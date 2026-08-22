using Bridge.Core.Entities;

namespace Bridge.Services;

/// <summary>Unified achievements loader for Steam, Epic, and ROM games.</summary>
public sealed class GameAchievementsService(
    SteamAchievementsService steamAchievementsService,
    EpicAchievementsService epicAchievementsService,
    RetroAchievementsAchievementsService retroAchievementsService)
{
    public bool SupportsAchievements(Game game) =>
        steamAchievementsService.SupportsAchievements(game) ||
        epicAchievementsService.IsEpicGame(game) ||
        retroAchievementsService.IsRomGame(game);

    public bool IsDefinitionsOnly(Game game) => steamAchievementsService.IsDefinitionsOnlyGame(game);

    public bool IsRomGame(Game game) => retroAchievementsService.IsRomGame(game);

    public bool IsEpicGame(Game game) => epicAchievementsService.IsEpicGame(game);

    public bool IsEpicLauncherSessionAvailable() => epicAchievementsService.IsLauncherSessionAvailable();

    public bool IsRetroAchievementsConfigured() => retroAchievementsService.IsConfigured();

    public string GetRomEmptyMessage(Game game) => retroAchievementsService.GetEmptyMessage(game);

    public void ClearRetroAchievementsCache() => retroAchievementsService.ClearCache();

    public event Action<Game>? RomSessionEnded;

    public void NotifyRomSessionEnded(Game game)
    {
        ClearRetroAchievementsCache();
        RomSessionEnded?.Invoke(game);
    }

    public bool TryGetCached(Game game, out GameAchievementsSnapshot? snapshot)
    {
        if (steamAchievementsService.SupportsAchievements(game))
            return steamAchievementsService.TryGetCached(game, out snapshot);

        if (epicAchievementsService.IsEpicGame(game))
            return epicAchievementsService.TryGetCached(game, out snapshot);

        if (retroAchievementsService.IsRomGame(game))
            return retroAchievementsService.TryGetCached(game, out snapshot);

        snapshot = null;
        return false;
    }

    public Task<GameAchievementsSnapshot?> LoadForGameAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        if (steamAchievementsService.SupportsAchievements(game))
            return steamAchievementsService.LoadForGameAsync(game, cancellationToken);

        if (epicAchievementsService.IsEpicGame(game))
            return epicAchievementsService.LoadForGameAsync(game, cancellationToken);

        if (retroAchievementsService.IsRomGame(game))
            return retroAchievementsService.LoadForGameAsync(game, cancellationToken);

        return Task.FromResult<GameAchievementsSnapshot?>(null);
    }
}
