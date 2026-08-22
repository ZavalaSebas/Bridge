namespace Bridge.Core.Entities;

/// <summary>
/// Import source label and dedup-key component paired with <see cref="Game.ExternalId"/>.
/// </summary>
public class GameSource : DatabaseObject
{
    /// <summary>Source id for manually added games (<see cref="Game.IsCustomGame"/>).</summary>
    public static readonly Guid ManualId = Guid.Empty;

    /// <summary>Source id for games imported from an external folder via Bridge scan.</summary>
    public static readonly Guid BridgeId = new("7c8f9a2b-3d4e-5f6a-8b9c-0d1e2f3a4b5c");

    public static bool IsUserManaged(Guid sourceId) =>
        sourceId == ManualId || sourceId == BridgeId;
}
