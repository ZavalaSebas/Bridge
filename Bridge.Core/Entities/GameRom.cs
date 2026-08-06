namespace Bridge.Core.Entities;

/// <summary>Matches Playnite's GameRom exactly — just a name and a path (PROJECT_FOUNDATION.md §28.8).</summary>
public class GameRom
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
