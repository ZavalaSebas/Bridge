using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Bridge.Storage.Repositories;

public class GameRepository(IDbContextFactory<BridgeDbContext> factory)
    : Repository<Game>(factory), IGameRepository
{
    public Game? FindByExternalId(string externalId, Guid sourceId)
    {
        using var context = factory.CreateDbContext();
        return context.Games
            .AsNoTracking()
            .FirstOrDefault(g => g.ExternalId == externalId && g.SourceId == sourceId);
    }

    /// <summary>
    /// Inserts several games in a single transaction (one DbContext, one
    /// SaveChanges) instead of one round-trip per game — used by the ROM scan and
    /// library imports to avoid SQLite write contention on large batches.
    /// </summary>
    public void AddMany(IReadOnlyList<Game> games)
    {
        if (games.Count == 0)
            return;

        using var context = factory.CreateDbContext();
        context.Games.AddRange(games);
        context.SaveChanges();
    }

    /// <summary>
    /// Batch update of metadata sync markers (seal multiple games in one transaction).
    /// Used to mark games as "attempted" (success or fail) to respect TTL and avoid
    /// perpetual re-downloads. Operates in its own DbContext (isolated from UI).
    /// </summary>
    public void UpdateManyMetadataSyncMarkers(IReadOnlyList<Game> games, MetadataSyncMarker marker)
    {
        if (games.Count == 0)
            return;

        var now = DateTime.Now;  // Local time, consistent with existing code (Added, Modified)
        
        using var context = factory.CreateDbContext();
        
        // Attach and seal games in this isolated context — does NOT modify UI's Games collection
        foreach (var game in games)
        {
            var live = context.Games.Find(game.Id);
            if (live is null)
                continue;
            
            switch (marker)
            {
                case MetadataSyncMarker.Metadata:
                    live.MetadataSyncedAt = now;
                    break;
                case MetadataSyncMarker.Links:
                    live.LinksSyncedAt = now;
                    break;
                case MetadataSyncMarker.TimeToBeat:
                    live.TimeToBeatSyncedAt = now;
                    break;
            }
            
            context.Update(live);
        }
        
        // Single batch transaction for all seals
        context.SaveChanges();
    }
}
