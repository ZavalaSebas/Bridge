namespace Bridge.Core.Entities;

/// <summary>ROM file attached to a game — display name and path on disk.</summary>
public class GameRom
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Crc { get; set; }
    public string? DatRegion { get; set; }
    public string? DatPlatform { get; set; }
}
