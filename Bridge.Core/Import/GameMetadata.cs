using Bridge.Core.Entities;

namespace Bridge.Core.Import;

/// <summary>
/// Metadata fetched from a provider before it is merged into a persisted <see cref="Game"/>.
/// </summary>
public class GameMetadata
{
    public string Name { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> DescriptionImages { get; set; } = [];
    public List<DescriptionBlock> DescriptionBlocks { get; set; } = [];
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

    /// <summary>Full-resolution screenshots shown as a gallery in the details view (Steam path_full).</summary>
    public List<string> Screenshots { get; set; } = [];

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
