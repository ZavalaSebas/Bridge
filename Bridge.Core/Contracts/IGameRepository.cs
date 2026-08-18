using Bridge.Core.Entities;

namespace Bridge.Core.Contracts;

public interface IGameRepository : IRepository<Game>
{
    /// <summary>Find an existing game by external id and source — import dedup key.</summary>
    Game? FindByExternalId(string externalId, Guid sourceId);
}
