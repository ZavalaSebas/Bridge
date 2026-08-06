using Bridge.Core.Contracts;
using Bridge.Core.Entities;

namespace Bridge.Storage.Repositories;

public class GameRepository(BridgeDbContext context) : Repository<Game>(context), IGameRepository
{
    public Game? FindByExternalId(string externalId, Guid sourceId) =>
        Set.FirstOrDefault(g => g.ExternalId == externalId && g.SourceId == sourceId);
}
