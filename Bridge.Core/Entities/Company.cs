namespace Bridge.Core.Entities;

/// <summary>
/// One entity for developers and publishers, not two. Playnite's own Developer/
/// Publisher subclasses add zero fields and share one storage collection anyway
/// (PROJECT_FOUNDATION.md §28.1, §28.6 finding 5) — the split exists there only
/// for plugin-facing type hints. Bridge has no plugins to hint to, so Game just
/// keeps two separate id lists (DeveloperIds/PublisherIds) both pointing at this
/// one Company table.
/// </summary>
public class Company : DatabaseObject
{
}
