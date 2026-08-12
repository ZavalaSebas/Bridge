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

    // Runtime-only flags (IsInstalling/IsUninstalling/IsLaunching/IsRunning) are
    // transient — Game.cs assigns Bridge.Storage's load path the responsibility
    // of resetting them to false, mirroring Playnite's crash recovery (see the
    // doc comment on Game.cs). Without this, a crash or a forced close mid-game
    // leaves IsRunning=true persisted, and the next session starts showing the
    // game as "running" forever.
    private void ResetTransientFlags(Game? game)
    {
        if (game is null ||
            (!game.IsInstalling && !game.IsUninstalling && !game.IsLaunching && !game.IsRunning))
        {
            return;
        }

        game.IsInstalling = false;
        game.IsUninstalling = false;
        game.IsLaunching = false;
        game.IsRunning = false;
        Context.SaveChanges();
    }
}
