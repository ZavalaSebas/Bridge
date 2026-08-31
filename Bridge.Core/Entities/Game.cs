namespace Bridge.Core.Entities;

/// <summary>
/// Library game. Dedup key is (ExternalId, SourceId). Install/launch/running flags
/// marked Is* are runtime-only and are not persisted.
/// </summary>
public class Game : DatabaseObject, System.ComponentModel.INotifyPropertyChanged
{
    public string ExternalId { get; set; } = string.Empty;
    public Guid SourceId { get; set; } = GameSource.ManualId;
    public bool IsCustomGame => GameSource.IsUserManaged(SourceId);

    public string SortingName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> DescriptionImages { get; set; } = [];
    public List<DescriptionBlock> DescriptionBlocks { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public ReleaseDate? ReleaseDate { get; set; }

    // Paths or URLs — persisted artwork is resolved by Bridge.Storage when saved.
    public string Icon { get; set; } = string.Empty;
    public string CoverImage { get; set; } = string.Empty;
    public string BackgroundImage { get; set; } = string.Empty;
    public string LogoImage { get; set; } = string.Empty;
    public List<string> Screenshots { get; set; } = [];

    // Install / runtime state — the Is* flags are transient, reset on every DB load
    public bool IsInstalled { get; set; }
    public bool IsInstalling { get; set; }
    public bool IsUninstalling { get; set; }
    public bool IsLaunching { get; set; }

    // Lets the Play button switch to Stop while the game runs.
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning == value)
                return;
            _isRunning = value;
            System.ComponentModel.PropertyChangedEventHandler? handler = PropertyChanged;
            handler?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsRunning)));
        }
    }

    private bool _isRunning;

    // True when a managed ROM still needs RetroArch/core installed.
    public bool NeedsEmulatorDownload
    {
        get => _needsEmulatorDownload;
        set
        {
            if (_needsEmulatorDownload == value)
                return;
            _needsEmulatorDownload = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NeedsEmulatorDownload)));
        }
    }

    private bool _needsEmulatorDownload;

    public bool OverrideInstallState { get; set; }
    public string InstallDirectory { get; set; } = string.Empty;
    public ulong? InstallSizeBytes { get; set; }

    public List<GameAction> GameActions { get; set; } = [];
    public List<GameRom> Roms { get; set; } = [];
    public ulong PlaytimeSeconds { get; set; }
    /// <summary>HLTB main story — without notable extras.</summary>
    public ulong? TimeToBeatMainSeconds { get; set; }
    /// <summary>HLTB main + extras.</summary>
    public ulong? TimeToBeatExtraSeconds { get; set; }
    /// <summary>HLTB completionist — 100%.</summary>
    public ulong? TimeToBeatCompleteSeconds { get; set; }
    public ulong PlayCount { get; set; }
    public DateTime? LastActivity { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? Added { get; set; }
    public DateTime? Modified { get; set; }

    /// <summary>Last time metadata (text, images, refs) was synced, successful or not.</summary>
    public DateTime? MetadataSyncedAt { get; set; }
    /// <summary>Last time social links were synced, successful or not.</summary>
    public DateTime? LinksSyncedAt { get; set; }
    /// <summary>Last time HowLongToBeat data was synced, successful or not.</summary>
    public DateTime? TimeToBeatSyncedAt { get; set; }

    /// <summary>Recorded play sessions used by the Statistics timeline.</summary>
    public List<GamePlaySession> PlaySessions { get; set; } = [];

    // Global scripts run before/after launch; per-game scripts can opt out of globals.
    public string PreScript { get; set; } = string.Empty;
    public string PostScript { get; set; } = string.Empty;
    public string GameStartedScript { get; set; } = string.Empty;
    public bool UseGlobalPreScript { get; set; } = true;
    public bool UseGlobalPostScript { get; set; } = true;
    public bool UseGlobalGameStartedScript { get; set; } = true;

    public bool Hidden { get; set; }

    public bool Favorite
    {
        get => _favorite;
        set
        {
            if (_favorite == value)
                return;
            _favorite = value;
            System.ComponentModel.PropertyChangedEventHandler? handler = PropertyChanged;
            handler?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Favorite)));
        }
    }

    private bool _favorite;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public int? UserScore { get; set; }
    public int? CriticScore { get; set; }
    public int? CommunityScore { get; set; }

    // Flat id lists — Bridge.Storage resolves names when reading or writing.
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

    public Guid CompletionStatusId
    {
        get => _completionStatusId;
        set
        {
            if (_completionStatusId == value)
                return;
            _completionStatusId = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CompletionStatusId)));
        }
    }

    private Guid _completionStatusId;
}

/// <summary>Partial release date — year required, month/day optional.</summary>
public readonly record struct ReleaseDate(int Year, int? Month = null, int? Day = null);
