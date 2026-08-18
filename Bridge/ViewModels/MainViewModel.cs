using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Core.Import;
using Bridge;
using Bridge.Converters;
using Bridge.Import.Epic;
using Bridge.Import.Steam;
using Bridge.Metadata;
using Bridge.Resources;
using Bridge.Emulation;
using Bridge.Services;
using Bridge.Statistics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IGameRepository _gameRepository;
    private readonly IRepository<Genre> _genreRepository;
    private readonly IRepository<Company> _companyRepository;
    private readonly IRepository<Platform> _platformRepository;
    private readonly IRepository<GameSource> _sourceRepository;
    private readonly IRepository<CompletionStatus> _completionStatusRepository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly IRepository<Tag> _tagRepository;
    private readonly IRepository<GameFeature> _featureRepository;
    private readonly IRepository<Series> _seriesRepository;
    private readonly IRepository<AgeRating> _ageRatingRepository;
    private readonly IRepository<Region> _regionRepository;
    private readonly GameLauncher _launcher;
    private readonly RomScanner _romScanner;
    private readonly RetroArchService _retroArch;
    private readonly MetadataSyncService _metadataSync;
    private readonly SteamMetadataProvider _steamMetadataProvider;
    private readonly SteamLibraryImporter _steamImporter;
    private readonly EpicLibraryImporter _epicImporter;
    private readonly AppUpdateService _appUpdateService;
    private readonly IDialogService _dialogService;

    private IReadOnlyDictionary<Guid, string>? _companyNames;
    private IReadOnlyDictionary<Guid, string>? _platformNames;
    private IReadOnlyDictionary<Guid, string>? _genreNames;
    private IReadOnlyDictionary<Guid, string>? _categoryNames;
    private IReadOnlyDictionary<Guid, string>? _tagNames;
    private IReadOnlyDictionary<Guid, string>? _featureNames;
    private IReadOnlyDictionary<Guid, string>? _seriesNames;
    private IReadOnlyDictionary<Guid, string>? _ageRatingNames;
    private IReadOnlyDictionary<Guid, string>? _regionNames;
    private IReadOnlyDictionary<Guid, string>? _sourceNames;
    private IReadOnlyDictionary<Guid, string>? _completionStatusNames;

    private Task? _artworkPreloadTask;
    private readonly object _artworkPreloadLock = new();

    public ObservableCollection<Game> Games { get; } = [];

    public ICollectionView GamesView { get; }

    public ObservableCollection<GameDetailRow> DetailedRows { get; } = [];

    [ObservableProperty]
    private Game? _selectedGame;

    [ObservableProperty]
    private string _searchText = string.Empty;

    // RetroArch install in progress — drives the Play button label/symbol only;
    // the status-bar ProgressBar uses ShowStatusProgress/StatusProgress instead.
    [ObservableProperty]
    private bool _isEmulationBusy;

    // True when the selected managed ROM has no installed frontend/core yet, so
    // the Play button reads "Download" and installs on first click instead of
    // pretending it will launch immediately.
    [ObservableProperty]
    private bool _needsEmulatorDownload;

    // Play button label/symbol: "Play" normally, "Stop" while the game runs,
    // "Download" when the selected ROM needs its frontend/core installed, and
    // "Downloading" while that install is in progress.
    [ObservableProperty]
    private string _playButtonText = "Play";

    [ObservableProperty]
    private string _playButtonSymbol = "Play24";

    [ObservableProperty]
    private bool _playButtonIsStop;

    // Shown beside the random-game button when the user skipped an available update.
    [ObservableProperty]
    private bool _hasPendingUpdate;

    [ObservableProperty]
    private string _pendingUpdateToolTip = string.Empty;

    private AppUpdateInfo? _pendingUpdate;

    private void UpdateNeedsEmulatorDownload()
    {
        var game = SelectedGame;
        NeedsEmulatorDownload = game is not null && _retroArch.NeedsInstall(game);
        UpdatePlayButtonState();
    }

    // Per-row Download state for managed ROMs in covers/table views.
    public void RefreshAllEmulatorDownloadStates()
    {
        foreach (var game in Games)
        {
            var needs = _retroArch.IsManagedRom(game) && _retroArch.NeedsInstall(game);
            if (game.NeedsEmulatorDownload != needs)
                game.NeedsEmulatorDownload = needs;
        }

        UpdateNeedsEmulatorDownload();
    }

    partial void OnIsEmulationBusyChanged(bool value) => UpdatePlayButtonState();

    private void UpdatePlayButtonState()
    {
        var running = SelectedGame?.IsRunning == true;
        PlayButtonText = running ? Strings.Stop
            : IsEmulationBusy ? Strings.Downloading
            : NeedsEmulatorDownload ? Strings.Download
            : Strings.Play;
        PlayButtonSymbol = running ? "Stop24"
            : NeedsEmulatorDownload || IsEmulationBusy ? "ArrowDownload24"
            : "Play24";
        PlayButtonIsStop = running;
    }

    partial void OnSearchTextChanged(string value)
    {
        GamesView.Refresh();
    }

    [ObservableProperty]
    private LibraryFilterPreset _filterPreset;

    // When true, games flagged Hidden stay visible in the library so they can
    // be un-hidden from the More menu; when false (default) they're filtered out.
    [ObservableProperty]
    private bool _showHidden;

    partial void OnShowHiddenChanged(bool value)
    {
        GamesView.Refresh();
    }

    // Covers-view zoom (0.6x - 1.6x). Scales the cover cards so the wrapping
    // grid reflows to fit more/fewer columns.
    [ObservableProperty]
    private double _zoom = 1.0;

    // Sidebar visibility (View > Sidebar > Show Sidebar); the window applies it.
    [ObservableProperty]
    private bool _sidebarVisible = true;

    [ObservableProperty]
    private LibraryStatistics? _statistics;

    // True during bulk imports so the per-row collection changes don't each
    // trigger a full RebuildDetailedRows (O(n²) on large libraries); the import
    // calls RebuildDetailedRows once when it finishes.
    private bool _suspendDetailedRows;

    [ObservableProperty]
    private GameSortField _sortField;

    [ObservableProperty]
    private bool _sortDescending;

    [ObservableProperty]
    private GameGroupField _groupField;

    [ObservableProperty]
    private ViewMode _viewMode = ViewModeSettingsStore.Load();

    partial void OnViewModeChanged(ViewMode value)
    {
        ViewModeSettingsStore.Save(value);
    }

    // Fase 1 (UI overhaul): sidebar navigation. This is the single allowed
    // ViewModel change for that phase — an additive property that maps sidebar
    // shortcuts onto the FilterPreset / GroupField state that already exists.
    [ObservableProperty]
    private NavigationSection _navigationSection = NavigationSection.Library;

    partial void OnNavigationSectionChanged(NavigationSection value)
    {
        switch (value)
        {
            case NavigationSection.Library:
                // The sidebar "Library" shortcut means "show the whole library":
                // reset filter/group shortcuts other sidebar entries may have left
                // active (otherwise "Favorites" or "Sources" stays stuck with no
                // way to clear it from the sidebar).
                FilterPreset = LibraryFilterPreset.All;
                GroupField = GameGroupField.None;
                break;
            case NavigationSection.Favorites:
                FilterPreset = LibraryFilterPreset.Favorite;
                GroupField = GameGroupField.None;
                break;
            case NavigationSection.Sources:
                FilterPreset = LibraryFilterPreset.All;
                GroupField = GameGroupField.Library;
                break;
        }
    }

    partial void OnSortFieldChanged(GameSortField value)
    {
        ApplySort();
    }

    partial void OnSortDescendingChanged(bool value)
    {
        ApplySort();
        OnPropertyChanged(nameof(SortDirectionText));
    }

    // Sort menu's direction entry label: shows what toggling will switch to.
    public string SortDirectionText => SortDescending ? Strings.SortAscending : Strings.SortDescending;

    private void ApplySort()
    {
        if (GamesView is not ListCollectionView listView)
            return;

        listView.CustomSort = new GameSortComparer(
            SortField,
            SortDescending,
            BuildNameLookup(_companyRepository),
            BuildNameLookup(_platformRepository),
            BuildNameLookup(_genreRepository),
            BuildNameLookup(_sourceRepository));
        listView.Refresh();
    }

    private void ApplyGrouping()
    {
        if (GamesView is not ListCollectionView listView)
            return;

        listView.GroupDescriptions.Clear();
        if (GroupField != GameGroupField.None)
        {
            listView.GroupDescriptions.Add(new PropertyGroupDescription(
                null,
                new GameGroupConverter
                {
                    Resolver = new GameGroupResolver(
                        BuildNameLookup(_companyRepository),
                        BuildNameLookup(_platformRepository),
                        BuildNameLookup(_genreRepository),
                        BuildNameLookup(_sourceRepository),
                        BuildNameLookup(_completionStatusRepository)),
                    Field = GroupField
                }));
        }

        listView.Refresh();
    }

    private static IReadOnlyDictionary<Guid, string> BuildNameLookup<T>(IRepository<T> repository)
        where T : DatabaseObject
        => repository.GetAll().ToDictionary(item => item.Id, item => item.Name);

    partial void OnFilterPresetChanged(LibraryFilterPreset value)
    {
        ApplyFilterPreset();
        SyncNavigationSectionFromFilters();
    }

    partial void OnGroupFieldChanged(GameGroupField value)
    {
        ApplyGrouping();
        SyncNavigationSectionFromGrouping();
    }

    private void SyncNavigationSectionFromFilters()
    {
        if (NavigationSection is NavigationSection.Statistics or NavigationSection.Settings)
            return;

        if (FilterPreset == LibraryFilterPreset.Favorite)
        {
            if (NavigationSection != NavigationSection.Favorites)
                NavigationSection = NavigationSection.Favorites;
        }
        else if (NavigationSection == NavigationSection.Favorites)
        {
            NavigationSection = NavigationSection.Library;
        }
    }

    private void SyncNavigationSectionFromGrouping()
    {
        if (NavigationSection is NavigationSection.Statistics or NavigationSection.Settings)
            return;

        if (GroupField == GameGroupField.Library)
        {
            if (NavigationSection != NavigationSection.Sources)
                NavigationSection = NavigationSection.Sources;
        }
        else if (NavigationSection == NavigationSection.Sources)
        {
            NavigationSection = NavigationSection.Library;
        }
    }

    private void ApplyFilterPreset()
    {
        // Filter presets are pure predicates (see GameMatchesSearch) — they
        // never mutate the sort. Changing one only re-evaluates which games
        // are visible, leaving the active sort/group untouched.
        GamesView.Refresh();
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private StatusMessageKind _statusMessageKind = StatusMessageKind.Normal;

    private void SetStatus(string message, StatusMessageKind kind = StatusMessageKind.Normal)
    {
        StatusMessageKind = kind;
        StatusMessage = message;
    }

    [ObservableProperty]
    private string _statisticsSummary = string.Empty;

    [ObservableProperty]
    private string _developersText = string.Empty;

    [ObservableProperty]
    private string _publishersText = string.Empty;

    [ObservableProperty]
    private string _platformsText = string.Empty;

    [ObservableProperty]
    private string _genresText = string.Empty;

    [ObservableProperty]
    private string _libraryText = string.Empty;

    [ObservableProperty]
    private string _categoriesText = string.Empty;

    [ObservableProperty]
    private string _tagsText = string.Empty;

    [ObservableProperty]
    private string _featuresText = string.Empty;

    [ObservableProperty]
    private string _seriesText = string.Empty;

    [ObservableProperty]
    private string _ageRatingsText = string.Empty;

    [ObservableProperty]
    private string _regionsText = string.Empty;

    [ObservableProperty]
    private string _completionStatusText = string.Empty;

    [ObservableProperty]
    private string _versionText = string.Empty;

    [ObservableProperty]
    private string _installSizeText = string.Empty;

    [ObservableProperty]
    private string _addedText = string.Empty;

    [ObservableProperty]
    private string _lastPlayedText = string.Empty;

    // Opens a game link (Steam store page, official site, ...) in the default browser.
    [RelayCommand]
    private void OpenLink(Link link)
    {
        if (string.IsNullOrWhiteSpace(link.Url))
            return;

        SafeLauncher.TryOpenUrl(link.Url);
    }

    partial void OnSelectedGameChanged(Game? value)
    {
        if (_selectedGameSubscription is not null)
        {
            _selectedGameSubscription.PropertyChanged -= OnSelectedGamePropertyChanged;
        }
        RefreshReferenceFields(value);
        OnPropertyChanged(nameof(FavoriteMenuText));
        OnPropertyChanged(nameof(HiddenMenuText));
        OnPropertyChanged(nameof(SelectedGameLinks));
        if (value is not null)
        {
            value.PropertyChanged += OnSelectedGamePropertyChanged;
        }
        _selectedGameSubscription = value;
        UpdateNeedsEmulatorDownload();
    }

    // Tracks which Game's PropertyChanged we're subscribed to, so a selection
    // change can detach the previous one (the generated setter has already
    // overwritten _selectedGame by the time the partial runs).
    private Game? _selectedGameSubscription;

    // The Play/Stop button label depends on SelectedGame.IsRunning, which changes
    // when the game launches or closes — keep the computed state in sync.
    private void OnSelectedGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Game.IsRunning))
        {
            UpdatePlayButtonState();
        }
        else if (e.PropertyName == nameof(Game.CompletionStatusId) && sender is Game game)
        {
            if (ReferenceEquals(game, SelectedGame))
                CompletionStatusText = ResolveCompletionStatusName(game);
        }
    }

    private void RefreshReferenceFields(Game? game)
    {
        if (game is null)
        {
            DevelopersText = string.Empty;
            PublishersText = string.Empty;
            PlatformsText = string.Empty;
            GenresText = string.Empty;
            LibraryText = string.Empty;
            CategoriesText = string.Empty;
            TagsText = string.Empty;
            FeaturesText = string.Empty;
            SeriesText = string.Empty;
            AgeRatingsText = string.Empty;
            RegionsText = string.Empty;
            CompletionStatusText = string.Empty;
            VersionText = string.Empty;
            InstallSizeText = string.Empty;
            AddedText = string.Empty;
            LastPlayedText = string.Empty;
            return;
        }

        // Resolve stored ids back to display names from in-memory lookups so
        // RebuildDetailedRows doesn't hit the DB once per id per game.
        EnsureReferenceCaches();
        DevelopersText = JoinNames(game.DeveloperIds, _companyNames!);
        PublishersText = JoinNames(game.PublisherIds, _companyNames!);
        PlatformsText = JoinNames(game.PlatformIds, _platformNames!);
        GenresText = JoinNames(game.GenreIds, _genreNames!);
        CategoriesText = JoinNames(game.CategoryIds, _categoryNames!);
        TagsText = JoinNames(game.TagIds, _tagNames!);
        FeaturesText = JoinNames(game.FeatureIds, _featureNames!);
        SeriesText = JoinNames(game.SeriesIds, _seriesNames!);
        AgeRatingsText = JoinNames(game.AgeRatingIds, _ageRatingNames!);
        RegionsText = JoinNames(game.RegionIds, _regionNames!);
        LibraryText = _sourceNames!.TryGetValue(game.SourceId, out var sourceName) && sourceName.Length > 0
            ? sourceName
            : Strings.Manual;
        CompletionStatusText = ResolveCompletionStatusName(game);
        VersionText = game.Version;
        InstallSizeText = game.InstallSizeBytes is { } bytes ? PlaytimeFormatter.FormatBytes(bytes) : string.Empty;
        AddedText = game.Added is { } added ? added.ToString("d") : string.Empty;
        LastPlayedText = game.LastActivity is { } last ? last.ToString("d") : string.Empty;
    }

    private string ResolveCompletionStatusName(Game game)
    {
        if (game.CompletionStatusId == Guid.Empty)
            return string.Empty;

        EnsureReferenceCaches();
        if (_completionStatusNames!.TryGetValue(game.CompletionStatusId, out var cached))
            return cached;

        // Cache may lag behind a status just created via GetOrCreateByName — fall
        // back to a direct lookup so the hero badge updates immediately.
        return _completionStatusRepository.Get(game.CompletionStatusId)?.Name ?? string.Empty;
    }

    private static string JoinNames(IEnumerable<Guid> ids, IReadOnlyDictionary<Guid, string> names)
        => string.Join(", ", ids
            .Select(id => names.TryGetValue(id, out var name) ? name : null)
            .Where(n => !string.IsNullOrWhiteSpace(n)));

    private void EnsureReferenceCaches()
    {
        if (_companyNames is not null)
            return;

        _companyNames = BuildNameLookup(_companyRepository);
        _platformNames = BuildNameLookup(_platformRepository);
        _genreNames = BuildNameLookup(_genreRepository);
        _categoryNames = BuildNameLookup(_categoryRepository);
        _tagNames = BuildNameLookup(_tagRepository);
        _featureNames = BuildNameLookup(_featureRepository);
        _seriesNames = BuildNameLookup(_seriesRepository);
        _ageRatingNames = BuildNameLookup(_ageRatingRepository);
        _regionNames = BuildNameLookup(_regionRepository);
        _sourceNames = BuildNameLookup(_sourceRepository);
        _completionStatusNames = BuildNameLookup(_completionStatusRepository);
    }

    public void InvalidateReferenceCaches()
    {
        _companyNames = null;
        _platformNames = null;
        _genreNames = null;
        _categoryNames = null;
        _tagNames = null;
        _featureNames = null;
        _seriesNames = null;
        _ageRatingNames = null;
        _regionNames = null;
        _sourceNames = null;
        _completionStatusNames = null;

        if (SelectedGame is not null)
            RefreshReferenceFields(SelectedGame);
    }

    // Rebuilds the detailed-list rows from whatever GamesView currently shows
    // (respects search/filter/sort/group). Called on every view refresh.
    private void RebuildDetailedRows()
    {
        EnsureReferenceCaches();
        DetailedRows.Clear();
        foreach (var item in GamesView)
        {
            if (item is not Game game)
                continue;

            DetailedRows.Add(new GameDetailRow
            {
                Game = game,
                DevelopersText = JoinNames(game.DeveloperIds, _companyNames!),
                PublishersText = JoinNames(game.PublisherIds, _companyNames!),
                PlatformsText = JoinNames(game.PlatformIds, _platformNames!),
                GenresText = JoinNames(game.GenreIds, _genreNames!),
                LibraryText = _sourceNames!.TryGetValue(game.SourceId, out var sourceName) && sourceName.Length > 0
                    ? sourceName
                    : Strings.Manual
            });
        }
    }

    public MainViewModel(
        IGameRepository gameRepository,
        IRepository<Genre> genreRepository,
        IRepository<Company> companyRepository,
        IRepository<Platform> platformRepository,
        IRepository<GameSource> sourceRepository,
        IRepository<CompletionStatus> completionStatusRepository,
        IRepository<Category> categoryRepository,
        IRepository<Tag> tagRepository,
        IRepository<GameFeature> featureRepository,
        IRepository<Series> seriesRepository,
        IRepository<AgeRating> ageRatingRepository,
        IRepository<Region> regionRepository,
        GameLauncher launcher,
        RomScanner romScanner,
        RetroArchService retroArch,
        SteamMetadataProvider steamMetadataProvider,
        SteamLibraryImporter steamImporter,
        EpicLibraryImporter epicImporter,
        AppUpdateService appUpdateService,
        MetadataSyncService metadataSyncService,
        IDialogService dialogService)
    {
        _gameRepository = gameRepository;
        _genreRepository = genreRepository;
        _companyRepository = companyRepository;
        _platformRepository = platformRepository;
        _sourceRepository = sourceRepository;
        _completionStatusRepository = completionStatusRepository;
        _categoryRepository = categoryRepository;
        _tagRepository = tagRepository;
        _featureRepository = featureRepository;
        _seriesRepository = seriesRepository;
        _ageRatingRepository = ageRatingRepository;
        _regionRepository = regionRepository;
        _launcher = launcher;
        _romScanner = romScanner;
        _retroArch = retroArch;
        _metadataSync = metadataSyncService;
        _steamMetadataProvider = steamMetadataProvider;
        _steamImporter = steamImporter;
        _epicImporter = epicImporter;
        _appUpdateService = appUpdateService;
        _dialogService = dialogService;
        _launcher.GameStarted += OnGameStarted;
        _launcher.GameStopped += OnGameStopped;
        LoadGames();
        SelectedGame = SelectInitialGame(Games);
        GamesView = CollectionViewSource.GetDefaultView(Games);
        GamesView.Filter = GameMatchesSearch;
        ((INotifyCollectionChanged)GamesView).CollectionChanged += (_, _) =>
        {
            if (!_suspendDetailedRows)
            {
                RebuildDetailedRows();
            }
        };
        RebuildDetailedRows();
        InitializeAsync().FireAndForget("MainViewModel.Initialize");
    }

    // Startup work that used to run synchronously in the constructor (Steam
    // import + background metadata sync), which blocked the window from showing
    // for seconds on large libraries. Runs after the window is visible; stages
    // stay on the UI thread between awaits but yield regularly so the UI keeps
    // responding.
    private async Task InitializeAsync()
    {
        try
        {
            // Both library imports are pure local file I/O (fast), so run them
            // back-to-back first — Steam and Epic games appear together — then
            // the slow per-game HTTP metadata syncs run after.
            _ = PreloadArtworkAsync();
            var steamSourceId = _sourceRepository.GetOrCreateByName("Steam").Id;
            var epicSourceId = _sourceRepository.GetOrCreateByName("Epic").Id;
            await ImportSteamLibraryCoreAsync(steamSourceId);
            await ImportEpicLibraryCoreAsync(epicSourceId);
            _ = PreloadArtworkAsync();

            // First run: the constructor's SelectInitialGame ran against an empty
            // library, so nothing got selected. Pick the initial game now that the
            // imports have populated Games — only if the user hasn't already chosen
            // something (they may have clicked while the import was running).
            if (SelectedGame is null)
            {
                SelectedGame = SelectInitialGame(Games);
            }

            await DownloadMissingSteamMetadataAsync(steamSourceId);
            _ = PreloadArtworkAsync();
            await DownloadMissingMetadataByNameAsync([epicSourceId]);
            _ = PreloadArtworkAsync();
            await CheckForUpdatesCoreAsync(promptWhenUpToDate: false);
            RefreshAllEmulatorDownloadStates();
        }
        catch (Exception ex)
        {
            SetStatus(Strings.Format(nameof(Strings.StartupImportFailedFormat), ex.Message), StatusMessageKind.Error);
        }
    }

    // Warms the frozen-image cache so the artwork is usually ready by the time
    // the library renders — not just the small icons, but the covers (Grid view)
    // and the selected game's background too. Without the covers preloaded, the
    // Grid opens with many covers blank that pop in as their downloads finish;
    // without the background preloaded, the hero's FadeImage shows it "all at
    // once" instead of from the first frame. Remote images decode in the
    // background; a failed/unreachable one simply never enters the cache. Each
    // Image in the UI picks up its artwork the moment it's cached via
    // CachedImage.SourceUrl.
    private Task PreloadArtworkAsync()
    {
        lock (_artworkPreloadLock)
        {
            if (_artworkPreloadTask is { IsCompleted: false } running)
                return running;

            _artworkPreloadTask = PreloadArtworkCoreAsync();
            return _artworkPreloadTask;
        }
    }

    private async Task PreloadArtworkCoreAsync()
    {
        var urls = CollectStartupPreloadUrls().ToList();
        if (urls.Count == 0)
            return;

        BeginStatusProgress(indeterminate: urls.Count <= 1);
        ReportBatchProgress(0, urls.Count);
        try
        {
            await RemoteImageCache.PreloadAndWaitAsync(
                urls,
                new Progress<(int Completed, int Total)>(p => ReportBatchProgress(p.Completed, p.Total)));
        }
        finally
        {
            EndStatusProgress();
        }
    }


    // Startup preload: selected game hero art + the first grid page, not the
    // entire library — keeps cold start snappy on large collections.
    private IEnumerable<string> CollectStartupPreloadUrls()
    {
        const int gridWarmCount = 32;
        var urls = new List<string>();

        if (SelectedGame is { } selected)
        {
            if (!string.IsNullOrWhiteSpace(selected.Icon))
                urls.Add(selected.Icon);
            if (!string.IsNullOrWhiteSpace(selected.CoverImage))
                urls.Add(selected.CoverImage);
            if (!string.IsNullOrWhiteSpace(selected.BackgroundImage))
                urls.Add(selected.BackgroundImage);
        }

        foreach (var game in Games.Take(gridWarmCount))
        {
            if (!string.IsNullOrWhiteSpace(game.Icon))
                urls.Add(game.Icon);
            if (!string.IsNullOrWhiteSpace(game.CoverImage))
                urls.Add(game.CoverImage);
        }

        return urls;
    }

    // Awaited asynchronously after the window is shown: decode from disk when
    // cached so the hero and first covers appear quickly without blocking the
    // UI thread. A short timeout keeps startup snappy if a download stalls.
    public async Task WaitForStartupArtworkAsync()
    {
        var preload = PreloadArtworkAsync();
        await Task.WhenAny(preload, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(false);
    }

    private void LoadGames()
    {
        foreach (var game in _gameRepository.GetAll())
        {
            ApplySteamLocalArtwork(game);
            AddGameSorted(game);
        }

        // IsRunning is in-memory only (not persisted). Reset on startup so a
        // stale in-process flag can't survive a hot reload during development.
        foreach (var game in Games)
        {
            if (!game.IsRunning)
                continue;
            game.IsRunning = false;
        }

        RefreshStatistics();
        RefreshAllEmulatorDownloadStates();
    }

    // Startup selection: resume where the user left off. On a fresh app start the
    // is selected (LastActivity is persisted when a game is launched). Falls back
    // to the first game when nothing has been played yet. Pure so it can be
    // unit-tested without constructing the whole MainViewModel.
    public static Game? SelectInitialGame(IEnumerable<Game> games)
    {
        var lastPlayed = games
            .Where(g => g.LastActivity.HasValue)
            .OrderByDescending(g => g.LastActivity)
            .FirstOrDefault();
        return lastPlayed ?? games.FirstOrDefault();
    }

    // Inserts into Games keeping the collection ordered by name (ordinal,
    // case-insensitive) — binary-search find the insert position instead of
    // sorting the whole collection after every add. SortingName is never
    // populated by the importers yet, so Name is the sort key.
    private void AddGameSorted(Game game)
    {
        int index = 0;
        int lo = 0, hi = Games.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (string.Compare(Games[mid].Name, game.Name, StringComparison.OrdinalIgnoreCase) <= 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        index = lo;

        Games.Insert(index, game);
    }

    // Search + preset filter: matches the name search and the active preset.
    // Empty/whitespace search shows everything within the preset.
    private bool GameMatchesSearch(object item)
    {
        if (item is not Game game)
            return false;

        if (!ShowHidden && game.Hidden)
            return false;

        if (!string.IsNullOrWhiteSpace(SearchText)
            && !game.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return FilterPreset switch
        {
            LibraryFilterPreset.Favorite => game.Favorite,
            LibraryFilterPreset.Installed => game.IsInstalled,
            LibraryFilterPreset.NotPlayed => game.PlaytimeSeconds == 0 && !game.LastActivity.HasValue,
            LibraryFilterPreset.RecentlyPlayed => game.LastActivity.HasValue,
            _ => true
        };
    }

    // Playnite shows Steam's square 32x32 clienticon (PROJECT_FOUNDATION.md
    // §28.26), which Steam keeps locally in appcache\librarycache\{appid}\ —
    // the web API no longer returns the `clienticon` field, so the local file
    // is the faithful source. Falls back to whatever Icon already holds
    // (e.g. the header.jpg URL from metadata) when Steam isn't installed or
    // that app has no cached icon.
    // Prefers the artwork Steam caches locally (appcache\librarycache\{appid}\)
    // over web URLs so the library shows complete art the moment a game is
    // loaded — no download, no "blank until metadata" state. Resolves all three
    // pieces (square icon, vertical cover, widescreen hero background) and only
    // overrides fields a local file exists for.
    private void ApplySteamLocalArtwork(Game game)
    {
        if (game.SourceId == GameSource.ManualId || !uint.TryParse(game.ExternalId, out _))
            return;

        var icon = SteamLocalIconResolver.TryGetLocalIconPath(game.ExternalId);
        if (!string.IsNullOrWhiteSpace(icon))
            game.Icon = icon;

        var cover = SteamLocalIconResolver.TryGetLocalCoverPath(game.ExternalId);
        if (!string.IsNullOrWhiteSpace(cover))
            game.CoverImage = cover;

        var background = SteamLocalIconResolver.TryGetLocalBackgroundPath(game.ExternalId);
        if (!string.IsNullOrWhiteSpace(background))
            game.BackgroundImage = background;

        // Deterministic store/community links fill in immediately for games
        // imported before this was added (new imports get them in the metadata).
        // Merge only the missing URLs so a metadata download that added
        // Achievements/Workshop isn't clobbered.
        foreach (var link in SteamLibraryImporter.BuildDefaultLinks(game.ExternalId))
        {
            if (!game.Links.Any(l => l.Url.Equals(link.Url, StringComparison.OrdinalIgnoreCase)))
                game.Links.Add(link);
        }
    }

    private void RefreshStatistics()
    {
        var stats = LibraryStatistics.Compute(Games);
        Statistics = stats;
        StatisticsSummary = Strings.Format(
            nameof(Strings.StatisticsSummaryFormat),
            stats.TotalCount,
            stats.InstalledCount,
            stats.FavoriteCount,
            stats.HiddenCount,
            stats.TotalPlaytimeDisplay);
    }

    // Adds an already-persisted game to the in-memory library and selects it.
    // Used after the edit window saves a brand-new manual game.
    public void AddGameToLibrary(Game game)
    {
        AddGameSorted(game);
        SelectedGame = game;
        InvalidateReferenceCaches();
        RefreshAllEmulatorDownloadStates();
        RefreshStatistics();
    }

    // All web links the selected game has (Steam store, official site, IGDB...).
    public IReadOnlyList<Link> SelectedGameLinks => SelectedGame?.Links ?? [];
}
