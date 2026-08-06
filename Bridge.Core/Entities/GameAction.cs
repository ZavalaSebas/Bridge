using Bridge.Core.Enums;

namespace Bridge.Core.Entities;

/// <summary>
/// Field shape matches Playnite's GameAction 1:1 (PROJECT_FOUNDATION.md §28.8) —
/// field usage is type-dependent: File uses Path/Arguments/WorkingDir; Url uses
/// only Path; Emulator uses EmulatorId/EmulatorProfileId/Arguments(if
/// OverrideDefaultArgs)/AdditionalArguments, never Path/WorkingDir; Script uses
/// only Script. See §28.9 for the exact per-type launch algorithm this feeds.
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
