namespace Bridge.Core.Entities;

/// <summary>
/// Import source label and dedup-key component paired with <see cref="Game.ExternalId"/>.
/// </summary>
public class GameSource : DatabaseObject
{
    /// <summary>Source id for manually added games (<see cref="Game.IsCustomGame"/>).</summary>
    public static readonly Guid ManualId = Guid.Empty;
}
