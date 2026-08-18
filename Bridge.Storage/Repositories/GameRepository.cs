using Bridge.Core.Contracts;
using Bridge.Core.Entities;
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
}
