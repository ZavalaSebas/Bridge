using Bridge.Core.Enums;

namespace Bridge.Core.Entities;

/// <summary>
/// User-defined completion label. <see cref="Kind"/> marks the default status for
/// new games and the one applied on first launch.
/// </summary>
public class CompletionStatus : DatabaseObject
{
    public CompletionStatusKind Kind { get; set; } = CompletionStatusKind.None;
}
