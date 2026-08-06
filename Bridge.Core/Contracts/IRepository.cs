using Bridge.Core.Entities;

namespace Bridge.Core.Contracts;

/// <summary>
/// Generic CRUD shape shared by every reference-entity collection (Genre, Tag,
/// Category, etc.). Trimmed down from Playnite's real ItemCollection&lt;T&gt;
/// (PROJECT_FOUNDATION.md §28.2) to what Bridge's MVP actually needs — no
/// MetadataProperty-based resolution (see Import/GameMetadata.cs for why),
/// no buffered-update batching (that's an optimization for a UI that's
/// listening to change events on every write; add it back if profiling ever
/// shows it's needed, don't build it speculatively).
/// </summary>
public interface IRepository<T> where T : DatabaseObject
{
    T? Get(Guid id);
    IReadOnlyList<T> GetAll();
    void Add(T item);
    void Update(T item);
    bool Remove(Guid id);

    /// <summary>Case-insensitive find-by-name-or-create, matching Playnite's ItemCollection.Add(string) resolution behavior (§28.2) — this is how importers turn a bare name into a real reference-entity id.</summary>
    T GetOrCreateByName(string name);
}
