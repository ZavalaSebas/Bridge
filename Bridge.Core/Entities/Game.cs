namespace Bridge.Core.Entities;

/// <summary>
/// Bridge's equivalent of Playnite's Game (PROJECT_FOUNDATION.md §28.1), with the
/// plugin-specific fields removed since Bridge has no plugin runtime (ADR-1):
/// PluginId and IncludeLibraryPluginAction are gone. GameId is renamed ExternalId
/// to read correctly now that it's paired with SourceId instead of a PluginId
/// (dedup key becomes (ExternalId, SourceId) — see GameSource.cs and ADR-6 in
/// ARCHITECTURE.md). Runtime-only flags (IsInstalling/IsUninstalling/IsLaunching/
/// IsRunning) must be reset to false on every load, mirroring Playnite's own
/// crash-recovery behavior (§28.10, finding 5) — Bridge.Storage's load path is
/// responsible for that reset, not this class.
/// </summary>
public class Game : DatabaseObject
{
    public string ExternalId { get; set; } = string.Empty;
    public Guid SourceId { get; set; } = GameSource.ManualId;
    public bool IsCustomGame => SourceId == GameSource.ManualId;

    public string SortingName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> DescriptionImages { get; set; } = [];
    public List<DescriptionBlock> DescriptionBlocks { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public ReleaseDate? ReleaseDate { get; set; }

    // Artwork (relative paths under the file cache — see Bridge.Storage's
    // AddFile-equivalent, PROJECT_FOUNDATION.md §28.2)
    public string Icon { get; set; } = string.Empty;
    public string CoverImage { get; set; } = string.Empty;
    public string BackgroundImage { get; set; } = string.Empty;
    public List<string> Screenshots { get; set; } = [];

    // Install / runtime state — the Is* flags are transient, reset on every DB load
    public bool IsInstalled { get; set; }
    public bool IsInstalling { get; set; }
    public bool IsUninstalling { get; set; }
    public bool IsLaunching { get; set; }
    public bool IsRunning { get; set; }
    public bool OverrideInstallState { get; set; }
    public string InstallDirectory { get; set; } = string.Empty;
    public ulong? InstallSizeBytes { get; set; }

    public List<GameAction> GameActions { get; set; } = [];
    public List<GameRom> Roms { get; set; } = [];
    public ulong PlaytimeSeconds { get; set; }
    public ulong PlayCount { get; set; }
    public DateTime? LastActivity { get; set; }
    public DateTime? Added { get; set; }
    public DateTime? Modified { get; set; }

    // Scripts — see PROJECT_FOUNDATION.md §28.9 for exact invocation order
    // (global pre → per-game pre → launch → per-game started → global started →
    // stop → per-game post → global post)
    public string PreScript { get; set; } = string.Empty;
    public string PostScript { get; set; } = string.Empty;
    public string GameStartedScript { get; set; } = string.Empty;
    public bool UseGlobalPreScript { get; set; } = true;
    public bool UseGlobalPostScript { get; set; } = true;
    public bool UseGlobalGameStartedScript { get; set; } = true;

    public bool Hidden { get; set; }
    public bool Favorite { get; set; }

    public int? UserScore { get; set; }
    public int? CriticScore { get; set; }
    public int? CommunityScore { get; set; }

    // Links and reference-entity ids (resolved to Genre/Company/etc. by the
    // caller via Bridge.Storage — Game itself only holds ids, per Playnite's
    // own pattern of keeping the entity flat and letting the read side join)
    public List<Link> Links { get; set; } = [];
    public List<Guid> GenreIds { get; set; } = [];
    public List<Guid> DeveloperIds { get; set; } = [];
    public List<Guid> PublisherIds { get; set; } = [];
    public List<Guid> CategoryIds { get; set; } = [];
    public List<Guid> TagIds { get; set; } = [];
    public List<Guid> FeatureIds { get; set; } = [];
    public List<Guid> PlatformIds { get; set; } = [];
    public List<Guid> SeriesIds { get; set; } = [];
    public List<Guid> AgeRatingIds { get; set; } = [];
    public List<Guid> RegionIds { get; set; } = [];
    public Guid CompletionStatusId { get; set; }
}

/// <summary>Minimal stand-in for a release date — a year is always known, month/day are not always available. Matches the shape Playnite's ReleaseDate? implies.</summary>
public readonly record struct ReleaseDate(int Year, int? Month = null, int? Day = null);
