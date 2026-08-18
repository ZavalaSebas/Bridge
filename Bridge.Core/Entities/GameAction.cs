using Bridge.Core.Enums;

namespace Bridge.Core.Entities;

/// <summary>
/// Launch action for a game. Which fields matter depends on <see cref="Type"/>:
/// File uses Path/Arguments/WorkingDir; Url uses Path; Emulator uses emulator ids
/// and argument overrides; Script uses Script only.
/// </summary>
public class GameAction
{
    public GameActionType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPlayAction { get; set; }

    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;

    public Guid EmulatorId { get; set; }
    public string EmulatorProfileId { get; set; } = string.Empty;
    public bool OverrideDefaultArgs { get; set; }
    public string AdditionalArguments { get; set; } = string.Empty;

    public string Script { get; set; } = string.Empty;

    public TrackingMode TrackingMode { get; set; } = TrackingMode.Default;
    public string TrackingPath { get; set; } = string.Empty;
    public int InitialTrackingDelayMs { get; set; } = 0;
    public int TrackingFrequencyMs { get; set; } = 2000;
}
