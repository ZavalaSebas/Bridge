using Bridge.Core.Entities;

namespace Bridge.Core.Contracts;

public interface IGameRepository : IRepository<Game>
{
    /// <summary>Dedup lookup for import — matches Playnite's exact (GameId, PluginId) key, adapted to (ExternalId, SourceId) since Bridge has no PluginId (PROJECT_FOUNDATION.md §28.2, ADR-6).</summary>
    Game? FindByExternalId(string externalId, Guid sourceId);

    /// <summary>Clears persisted install/uninstall/launch flags after a crash.</summary>
    void ResetPersistedTransientFlags();
}
