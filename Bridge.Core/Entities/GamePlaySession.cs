namespace Bridge.Core.Entities;

/// <summary>One completed play session for a game.</summary>
public sealed class GamePlaySession
{
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public ulong DurationSeconds { get; set; }
}
