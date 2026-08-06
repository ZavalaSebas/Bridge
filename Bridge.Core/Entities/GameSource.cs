namespace Bridge.Core.Entities;

/// <summary>
/// In Playnite, "where did this game come from" is split across two things:
/// Game.PluginId (which LibraryPlugin instance imported it — the dedup key) and
/// GameSource (a separate, user-editable label like "Steam"/"Retail" that's
/// mostly cosmetic). See PROJECT_FOUNDATION.md §28.1. Bridge has no plugin
/// instances to identify, so that split has no reason to exist — GameSource
/// alone plays both roles: it's the dedup-key component (paired with
/// Game.ExternalId) AND the label shown in the UI. One less entity, same
/// information. See ADR-6 in ARCHITECTURE.md for the full reasoning.
/// </summary>
public class GameSource : DatabaseObject
{
    /// <summary>Well-known source id for manually-added games — mirrors Playnite's PluginId == Guid.Empty check for Game.IsCustomGame.</summary>
    public static readonly Guid ManualId = Guid.Empty;
}
