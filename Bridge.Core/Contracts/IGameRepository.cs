using Bridge.Core.Entities;
using Bridge.Core.Utilities;

namespace Bridge.Core.Contracts;

public interface IGameRepository : IRepository<Game>
{
    /// <summary>Find an existing game by external id and source — import dedup key.</summary>
    Game? FindByExternalId(string externalId, Guid sourceId);

    /// <summary>Batch update of metadata sync markers (seal multiple games in one transaction).</summary>
    void UpdateManyMetadataSyncMarkers(IReadOnlyList<Game> games, MetadataSyncMarker marker);
}
