using Bridge.Core.Entities;

namespace Bridge.Core.Import;

/// <summary>
/// What an importer (Bridge.Import) produces from a source, before it becomes a
/// persisted Game. Mirrors the role of Playnite's GameMetadata (PROJECT_FOUNDATION.md
/// §28.1) but drops the MetadataProperty abstraction — Playnite needs it because
/// third-party plugins can't know a reference entity's real Guid ahead of time, so
/// they hand back either a name or an id and let Playnite's ItemCollection resolve
/// it (§28.2). Bridge's importers are internal code with a real Bridge.Storage
/// repository to call directly, so they can just resolve-or-create by name
/// themselves and hand back real Guids here — one less type, same capability.
/// </summary>
public class GameMetadata
{
    public string Name { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> DescriptionImages { get; set; } = [];
    public string InstallDirectory { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public ulong? InstallSizeBytes { get; set; }
    public ReleaseDate? ReleaseDate { get; set; }
    public string Version { get; set; } = string.Empty;

    public List<GameAction> GameActions { get; set; } = [];
    public List<GameRom> Roms { get; set; } = [];
    public List<Link> Links { get; set; } = [];

    public ulong PlaytimeSeconds { get; set; }
    public ulong PlayCount { get; set; }
    public DateTime? LastActivity { get; set; }

    public int? UserScore { get; set; }
    public int? CriticScore { get; set; }
    public int? CommunityScore { get; set; }

    /// <summary>Local file path or URL — resolved into a stored file by Bridge.Storage the same way for Icon/CoverImage/BackgroundImage.</summary>
    public string? Icon { get; set; }
    public string? CoverImage { get; set; }
    public string? BackgroundImage { get; set; }

    public List<string> Genres { get; set; } = [];
    public List<string> Developers { get; set; } = [];
    public List<string> Publishers { get; set; } = [];
    public List<string> Categories { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<string> Features { get; set; } = [];
    public List<string> Platforms { get; set; } = [];
    public List<string> Series { get; set; } = [];
    public List<string> AgeRatings { get; set; } = [];
    public List<string> Regions { get; set; } = [];
}
