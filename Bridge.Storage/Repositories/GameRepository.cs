using Bridge.Core.Contracts;
using Bridge.Core.Entities;

namespace Bridge.Storage.Repositories;

public class GameRepository(BridgeDbContext context) : Repository<Game>(context), IGameRepository
{
    public Game? FindByExternalId(string externalId, Guid sourceId)
    {
        var game = Set.FirstOrDefault(g => g.ExternalId == externalId && g.SourceId == sourceId);
        ResetTransientFlags(game);
        return game;
    }

    public override Game? Get(Guid id)
    {
        var game = base.Get(id);
        ResetTransientFlags(game);
        return game;
    }

    public override IReadOnlyList<Game> GetAll()
    {
        var games = base.GetAll();
        foreach (var game in games)
        {
            ResetTransientFlags(game);
        }

        return games;
    }

    // Runtime-only flags (IsInstalling/IsUninstalling/IsLaunching) are transient —
    // Game.cs assigns Bridge.Storage's load path the responsibility of resetting
    // them, mirroring Playnite's crash recovery (see the doc comment on Game.cs).
    // IsRunning is deliberately NOT reset here: it's a live flag set by
    // GameLauncher when the user launches a game, and ResetTransientFlags runs on
    // every GetAll/Get/FindByExternalId — including the background metadata sync
    // that can overlap a just-started game, where resetting IsRunning would flip
    // the hero button back to Play mid-game. The stale-IsRunning crash reset
    // happens once, in MainViewModel.LoadGames on startup.
    private void ResetTransientFlags(Game? game)
    {
        if (game is null ||
            (!game.IsInstalling && !game.IsUninstalling && !game.IsLaunching))
        {
            return;
        }

        game.IsInstalling = false;
        game.IsUninstalling = false;
        game.IsLaunching = false;
        Context.SaveChanges();
    }
}
