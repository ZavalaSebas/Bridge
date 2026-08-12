using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Core.Import;
using Bridge.Converters;
using Bridge.Import.Steam;
using Bridge.Metadata;
using Bridge.Services;
using Bridge.Statistics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IGameRepository _gameRepository;
    private readonly IRepository<Emulator> _emulatorRepository;
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
    private readonly IEnumerable<IGameMetadataProvider> _metadataProviders;
    private readonly SteamMetadataProvider _steamMetadataProvider;
    private readonly IgdbMetadataProvider _igdbMetadataProvider;
    private readonly SteamLibraryImporter _steamImporter;

    public ObservableCollection<Game> Games { get; } = [];

    public ICollectionView GamesView { get; }

    public ObservableCollection<GameDetailRow> DetailedRows { get; } = [];

    [ObservableProperty]
    private Game? _selectedGame;

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        GamesView.Refresh();
    }

    [ObservableProperty]
    private LibraryFilterPreset _filterPreset;

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
    private ViewMode _viewMode;

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
                // reset the filter preset that a filter menu entry may have left
                // active (otherwise "Favorites" stays stuck on with no way to
                // clear it from the sidebar).
                FilterPreset = LibraryFilterPreset.All;
                break;
            case NavigationSection.Favorites:
                FilterPreset = LibraryFilterPreset.Favorite;
                break;
            case NavigationSection.Sources:
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
    }

    partial void OnGroupFieldChanged(GameGroupField value)
    {
        ApplyGrouping();
    }

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
    }

    private void ApplyFilterPreset()
    {
        switch (FilterPreset)
        {
            case LibraryFilterPreset.MostPlayed:
                SortField = GameSortField.PlaytimeSeconds;
                SortDescending = true;
                break;
            case LibraryFilterPreset.RecentlyPlayed:
                SortField = GameSortField.RecentActivity;
                SortDescending = true;
                break;
        }

        GamesView.Refresh();
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _executablePathInput = string.Empty;

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

    [ObservableProperty]
    private string _userScoreText = string.Empty;

    // Opens a game link (Steam store page, official site, ...) in the default browser.
    [RelayCommand]
    private static void OpenLink(Link link)
    {
        if (string.IsNullOrWhiteSpace(link.Url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(link.Url) { UseShellExecute = true });
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // User's machine has no default handler for the URL scheme — nothing to open.
        }
    }

    partial void OnSelectedGameChanged(Game? value)
    {
        var playAction = value?.GameActions.FirstOrDefault(a => a.IsPlayAction);
        ExecutablePathInput = playAction?.Path ?? string.Empty;
        RefreshReferenceFields(value);
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
            UserScoreText = string.Empty;
            return;
        }

        // Resolve stored ids back to display names. Straight lookup per id —
        // fine for the MVP's small reference collections; don't build a
        // caching dictionary until profiling shows these resolve calls matter.
        DevelopersText = JoinNames(game.DeveloperIds, _companyRepository);
        PublishersText = JoinNames(game.PublisherIds, _companyRepository);
        PlatformsText = JoinNames(game.PlatformIds, _platformRepository);
        GenresText = JoinNames(game.GenreIds, _genreRepository);
        CategoriesText = JoinNames(game.CategoryIds, _categoryRepository);
        TagsText = JoinNames(game.TagIds, _tagRepository);
        FeaturesText = JoinNames(game.FeatureIds, _featureRepository);
        SeriesText = JoinNames(game.SeriesIds, _seriesRepository);
        AgeRatingsText = JoinNames(game.AgeRatingIds, _ageRatingRepository);
        RegionsText = JoinNames(game.RegionIds, _regionRepository);
        LibraryText = _sourceRepository.Get(game.SourceId)?.Name ?? "Manual";
        CompletionStatusText = game.CompletionStatusId != Guid.Empty
            ? _completionStatusRepository.Get(game.CompletionStatusId)?.Name ?? string.Empty
            : string.Empty;
        VersionText = game.Version;
        InstallSizeText = game.InstallSizeBytes is { } bytes ? FormatBytes(bytes) : string.Empty;
        AddedText = game.Added is { } added ? added.ToString("d") : string.Empty;
        LastPlayedText = game.LastActivity is { } last ? last.ToString("d") : string.Empty;
        UserScoreText = game.UserScore is { } user ? user.ToString() : string.Empty;
    }

    private static string FormatBytes(ulong bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):0.#} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.#} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.#} MB",
        _ => $"{bytes / 1024.0:0} KB"
    };

    private static string JoinNames<T>(IEnumerable<Guid> ids, IRepository<T> repo)
        where T : DatabaseObject
        => string.Join(", ", ids
            .Select(id => repo.Get(id)?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n)));

    // Rebuilds the detailed-list rows from whatever GamesView currently shows
    // (respects search/filter/sort/group). Called on every view refresh.
    private void RebuildDetailedRows()
    {
        DetailedRows.Clear();
        foreach (var item in GamesView)
        {
            if (item is not Game game)
                continue;

            DetailedRows.Add(new GameDetailRow
            {
                Game = game,
                DevelopersText = JoinNames(game.DeveloperIds, _companyRepository),
                PublishersText = JoinNames(game.PublisherIds, _companyRepository),
                PlatformsText = JoinNames(game.PlatformIds, _platformRepository),
                GenresText = JoinNames(game.GenreIds, _genreRepository),
                LibraryText = _sourceRepository.Get(game.SourceId)?.Name ?? "Manual"
            });
        }
    }

    public MainViewModel(
        IGameRepository gameRepository,
        IRepository<Emulator> emulatorRepository,
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
        IEnumerable<IGameMetadataProvider> metadataProviders,
        SteamMetadataProvider steamMetadataProvider,
        IgdbMetadataProvider igdbMetadataProvider,
        SteamLibraryImporter steamImporter)
    {
        _gameRepository = gameRepository;
        _emulatorRepository = emulatorRepository;
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
        _metadataProviders = metadataProviders;
        _steamMetadataProvider = steamMetadataProvider;
        _igdbMetadataProvider = igdbMetadataProvider;
        _steamImporter = steamImporter;
        _launcher.GameStarted += OnGameStarted;
        _launcher.GameStopped += OnGameStopped;
        LoadGames();
        SelectedGame = Games.FirstOrDefault();
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
        _ = InitializeAsync();
    }

    // Startup work that used to run synchronously in the constructor (Steam
    // import + background metadata sync), which blocked the window from showing
    // for seconds on large libraries. Runs after the window is visible; both
    // stages stay on the UI thread between awaits (the singleton BridgeDbContext
    // isn't thread-safe) but yield regularly so the UI keeps responding.
    private async Task InitializeAsync()
    {
        try
        {
            PreloadIcons();
            var steamSourceId = _sourceRepository.GetOrCreateByName("Steam").Id;
            await ImportSteamLibraryCoreAsync(steamSourceId);
            PreloadIcons();
            await DownloadMissingSteamMetadataAsync(steamSourceId);
            PreloadIcons();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Startup import failed: {ex.Message}";
        }
    }

    // Warms the frozen-image cache so the small icons are usually ready by the
    // time the library renders. Remote images also decode in the background;
    // a failed/unreachable one simply never enters the cache. Each Image in the
    // UI picks up its artwork the moment it's cached via CachedImage.SourceUrl.
    private void PreloadIcons()
    {
        RemoteImageCache.Preload(Games.Select(g => g.Icon));
    }

    private void LoadGames()
    {
        foreach (var game in _gameRepository.GetAll())
        {
            ApplySteamLocalArtwork(game);
            AddGameSorted(game);
        }

        RefreshStatistics();
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

        if (!string.IsNullOrWhiteSpace(SearchText)
            && !game.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return FilterPreset switch
        {
            LibraryFilterPreset.Favorite => game.Favorite,
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
    }

    private void RefreshStatistics()
    {
        var stats = LibraryStatistics.Compute(Games);
        Statistics = stats;
        var hours = stats.TotalPlaytimeSeconds / 3600.0;
        StatisticsSummary =
            $"Total: {stats.TotalCount} | Installed: {stats.InstalledCount} | " +
            $"Favorites: {stats.FavoriteCount} | Hidden: {stats.HiddenCount} | " +
            $"Total playtime: {hours:0.0}h";
    }

    [RelayCommand]
    private void AddGame(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var game = new Game { Name = name.Trim() };
        _gameRepository.Add(game);
        AddGameToLibrary(game);
    }

    // Adds an already-persisted game to the in-memory library and selects it.
    // Used after the edit window saves a brand-new manual game (AddGame would
    // create a second one).
    public void AddGameToLibrary(Game game)
    {
        AddGameSorted(game);
        SelectedGame = game;
        RefreshStatistics();
    }

    [RelayCommand]
    private void DeleteGame()
    {
        if (SelectedGame is null)
        {
            return;
        }

        _gameRepository.Remove(SelectedGame.Id);
        Games.Remove(SelectedGame);
        SelectedGame = null;
        RefreshStatistics();
    }

    [RelayCommand]
    private void SaveGame()
    {
        if (SelectedGame is null)
        {
            return;
        }

        _gameRepository.Update(SelectedGame);
        RefreshListDisplay(SelectedGame);
        RefreshStatistics();
    }

    [RelayCommand]
    private void SetPlayAction()
    {
        if (SelectedGame is null || string.IsNullOrWhiteSpace(ExecutablePathInput))
        {
            return;
        }

        var existing = SelectedGame.GameActions.FirstOrDefault(a => a.IsPlayAction);
        if (existing is not null)
        {
            existing.Type = GameActionType.File;
            existing.Path = ExecutablePathInput.Trim();
        }
        else
        {
            SelectedGame.GameActions.Add(new GameAction
            {
                Name = "Play",
                Type = GameActionType.File,
                IsPlayAction = true,
                Path = ExecutablePathInput.Trim()
            });
        }

        _gameRepository.Update(SelectedGame);
        StatusMessage = $"Play action set for {SelectedGame.Name}.";
    }

    public void ScanRomFolder(string? romFolder, Guid? emulatorId = null, string? profileId = null)
    {
        if (string.IsNullOrWhiteSpace(romFolder))
        {
            return;
        }

        // Resolve the chosen emulator/profile, falling back to the first ones
        // configured (older callers that don't pass ids).
        var emulator = emulatorId is { } eid
            ? _emulatorRepository.Get(eid)
            : _emulatorRepository.GetAll().FirstOrDefault();
        var profile = profileId is { Length: > 0 } pid
            ? emulator?.GetProfile(pid)
            : emulator?.Profiles.FirstOrDefault();
        if (emulator is null || profile is null)
        {
            StatusMessage = "No emulator configured yet — nothing to scan against.";
            return;
        }

        try
        {
            var found = _romScanner.Scan(romFolder.Trim(), emulator.Id, profile, Games);
            foreach (var game in found)
            {
                _gameRepository.Add(game);
                AddGameSorted(game);
            }

            RefreshStatistics();
            StatusMessage = $"Scan complete: {found.Count} new ROM(s) imported from '{romFolder}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportSteamLibrary()
    {
        var steamSource = _sourceRepository.GetOrCreateByName("Steam");
        await ImportSteamLibraryCoreAsync(steamSource.Id);
    }

    private async Task ImportSteamLibraryCoreAsync(Guid steamSourceId)
    {
        // Bulk-importing one game at a time would trigger a full RebuildDetailedRows
        // per insert (each doing per-row repo lookups) — that's O(n²). Suspend the
        // per-change rebuild and do a single one at the end.
        _suspendDetailedRows = true;
        try
        {
            // Manifest enumeration is pure file I/O — run it on a pool thread.
            // The DB writes below stay on the UI thread (singleton DbContext).
            var found = await Task.Run(_steamImporter.GetInstalledGames);
            int added = 0, updated = 0;

            foreach (var metadata in found)
            {
                // Yield periodically so a large library doesn't freeze the UI
                // while the window is already interactive.
                if ((added + updated) > 0 && (added + updated) % 25 == 0)
                {
                    await Task.Yield();
                }

                var existing = _gameRepository.FindByExternalId(metadata.ExternalId, steamSourceId);
                if (existing is null)
                {
                    var game = new Game
                    {
                        Name = metadata.Name,
                        ExternalId = metadata.ExternalId,
                        SourceId = steamSourceId,
                        InstallDirectory = metadata.InstallDirectory,
                        InstallSizeBytes = metadata.InstallSizeBytes,
                        IsInstalled = metadata.IsInstalled,
                        Added = DateTime.Now
                    };
                    // Resolve the locally-cached Steam artwork (icon, cover,
                    // hero background) BEFORE the row binds so the library shows
                    // complete art for every installed Steam game the moment it's
                    // added — no waiting for the (slow) web metadata.
                    ApplySteamLocalArtwork(game);
                    _gameRepository.Add(game);
                    AddGameSorted(game);
                    added++;
                }
                else
                {
                    // Mirrors Playnite's real re-scan behavior (PROJECT_FOUNDATION.md
                    // §28.2): a re-import only syncs install state, it never touches
                    // fields the user (or a metadata download) may have already set —
                    // Name, Description, etc. are left alone on existing games.
                    existing.IsInstalled = metadata.IsInstalled;
                    existing.InstallDirectory = metadata.InstallDirectory;
                    existing.InstallSizeBytes = metadata.InstallSizeBytes;
                    // Match the new-game path: refresh locally-cached artwork too,
                    // so a re-import picks up icons/covers/heroes that have since
                    // been cached. Missing or unchanged artwork is a no-op.
                    ApplySteamLocalArtwork(existing);
                    _gameRepository.Update(existing);
                    RefreshListDisplay(existing);
                    updated++;
                }
            }

            RebuildDetailedRows();
            RefreshStatistics();
            StatusMessage = $"Steam import: {added} new, {updated} updated.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Steam import failed: {ex.Message}";
        }
        finally
        {
            _suspendDetailedRows = false;
        }
    }

    [RelayCommand]
    private async Task DownloadMetadataAsync()
    {
        if (SelectedGame is null)
            return;

        var game = SelectedGame;
        var gameName = game.Name;

        StatusMessage = $"Downloading metadata for '{gameName}'...";

        GameMetadata? metadata = null;
        string? providerName = null;

        // Steam-imported games: use appid directly for a guaranteed lookup
        if (game.SourceId != GameSource.ManualId && uint.TryParse(game.ExternalId, out _))
        {
            try
            {
                metadata = await _steamMetadataProvider.GetByAppIdAsync(game.ExternalId);
                providerName = _steamMetadataProvider.Name;
            }
            catch
            {
                // Steam API failed — fall through to the provider chain
            }
        }

        // Fallback chain: try each provider by name search
        if (metadata is null)
        {
            foreach (var provider in _metadataProviders)
            {
                try
                {
                    metadata = await provider.SearchAsync(gameName);
                    if (metadata is not null)
                    {
                        providerName = provider.Name;
                        break;
                    }
                }
                catch
                {
                    // Try next provider
                }
            }
        }

        if (metadata is null)
        {
            StatusMessage = $"No metadata found for '{gameName}'.";
            return;
        }

        // Playnite merges metadata from multiple sources: the library plugin
        // (Steam) provides store/community links while the metadata provider
        // (IGDB) adds the social links (YouTube, Reddit, Twitter, ...). When
        // Steam was the main source, enrich the links with IGDB's if available.
        if (providerName == _steamMetadataProvider.Name)
        {
            try
            {
                if (await _igdbMetadataProvider.SearchAsync(gameName) is { } igdbMetadata)
                    metadata.Links.AddRange(igdbMetadata.Links);
            }
            catch
            {
                // IGDB optional (may be unconfigured) — Steam links alone are fine
            }
        }

        ApplyMetadata(game, metadata);
        ApplyMetadataReferences(game, metadata);
        ApplySteamLocalArtwork(game);

        _gameRepository.Update(game);
        RefreshListDisplay(game);
        StatusMessage = $"Metadata applied to '{game.Name}' (source: {providerName}).";
    }

    private static void ApplyMetadata(Game game, GameMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.Description))
            game.Description = metadata.Description;

        if (metadata.DescriptionImages.Count > 0)
            game.DescriptionImages = metadata.DescriptionImages;

        if (metadata.DescriptionBlocks.Count > 0)
            game.DescriptionBlocks = metadata.DescriptionBlocks;

        if (metadata.ReleaseDate is { } releaseDate)
            game.ReleaseDate = releaseDate;

        if (!string.IsNullOrWhiteSpace(metadata.CoverImage))
            game.CoverImage = metadata.CoverImage;

        if (!string.IsNullOrWhiteSpace(metadata.Icon))
            game.Icon = metadata.Icon;

        if (!string.IsNullOrWhiteSpace(metadata.BackgroundImage))
            game.BackgroundImage = metadata.BackgroundImage;

        if (metadata.CriticScore.HasValue)
            game.CriticScore = metadata.CriticScore;

        if (metadata.CommunityScore.HasValue)
            game.CommunityScore = metadata.CommunityScore;

        if (metadata.UserScore.HasValue)
            game.UserScore = metadata.UserScore;

        if (!string.IsNullOrWhiteSpace(metadata.Version))
            game.Version = metadata.Version;

        // Merge links instead of replacing: Playnite shows the library links
        // (Steam store, community, ...) together with the social ones a
        // metadata provider adds (YouTube, Reddit, ...). Dedupe by URL.
        if (metadata.Links is { Count: > 0 })
        {
            var known = new HashSet<string>(game.Links.Select(l => l.Url), StringComparer.OrdinalIgnoreCase);
            foreach (var link in metadata.Links.Where(l => !string.IsNullOrWhiteSpace(l.Url)))
            {
                if (known.Add(link.Url))
                    game.Links.Add(link);
            }
        }
    }

    // Resolve metadata names into real reference-entity ids (Genre/Company/
    // Platform) via GetOrCreateByName — the same mechanism Bridge.Import uses
    // for Steam data (see ADR-7 for why Developer/Publisher share one Company
    // table). Appends to the existing id lists without duplicating ids.
    private void ApplyMetadataReferences(Game game, GameMetadata metadata)
    {
        if (metadata.Genres is { Count: > 0 })
        {
            foreach (var genreName in metadata.Genres)
            {
                var genre = _genreRepository.GetOrCreateByName(genreName);
                if (!game.GenreIds.Contains(genre.Id))
                    game.GenreIds.Add(genre.Id);
            }
        }

        if (metadata.Developers is { Count: > 0 })
        {
            foreach (var name in metadata.Developers)
            {
                var company = _companyRepository.GetOrCreateByName(name);
                if (!game.DeveloperIds.Contains(company.Id))
                    game.DeveloperIds.Add(company.Id);
            }
        }

        if (metadata.Publishers is { Count: > 0 })
        {
            foreach (var name in metadata.Publishers)
            {
                var company = _companyRepository.GetOrCreateByName(name);
                if (!game.PublisherIds.Contains(company.Id))
                    game.PublisherIds.Add(company.Id);
            }
        }

        if (metadata.Platforms is { Count: > 0 })
        {
            foreach (var name in metadata.Platforms)
            {
                var platform = _platformRepository.GetOrCreateByName(name);
                if (!game.PlatformIds.Contains(platform.Id))
                    game.PlatformIds.Add(platform.Id);
            }
        }

        if (metadata.Categories is { Count: > 0 })
        {
            foreach (var name in metadata.Categories)
            {
                var category = _categoryRepository.GetOrCreateByName(name);
                if (!game.CategoryIds.Contains(category.Id))
                    game.CategoryIds.Add(category.Id);
            }
        }

        if (metadata.Tags is { Count: > 0 })
        {
            foreach (var name in metadata.Tags)
            {
                var tag = _tagRepository.GetOrCreateByName(name);
                if (!game.TagIds.Contains(tag.Id))
                    game.TagIds.Add(tag.Id);
            }
        }

        if (metadata.Features is { Count: > 0 })
        {
            foreach (var name in metadata.Features)
            {
                var feature = _featureRepository.GetOrCreateByName(name);
                if (!game.FeatureIds.Contains(feature.Id))
                    game.FeatureIds.Add(feature.Id);
            }
        }

        if (metadata.Series is { Count: > 0 })
        {
            foreach (var name in metadata.Series)
            {
                var series = _seriesRepository.GetOrCreateByName(name);
                if (!game.SeriesIds.Contains(series.Id))
                    game.SeriesIds.Add(series.Id);
            }
        }

        if (metadata.AgeRatings is { Count: > 0 })
        {
            foreach (var name in metadata.AgeRatings)
            {
                var ageRating = _ageRatingRepository.GetOrCreateByName(name);
                if (!game.AgeRatingIds.Contains(ageRating.Id))
                    game.AgeRatingIds.Add(ageRating.Id);
            }
        }

        if (metadata.Regions is { Count: > 0 })
        {
            foreach (var name in metadata.Regions)
            {
                var region = _regionRepository.GetOrCreateByName(name);
                if (!game.RegionIds.Contains(region.Id))
                    game.RegionIds.Add(region.Id);
            }
        }
    }

    private async Task DownloadMissingSteamMetadataAsync(Guid steamSourceId)
    {
        var candidates = _gameRepository.GetAll()
            .Where(g => g.SourceId == steamSourceId && string.IsNullOrWhiteSpace(g.Description))
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        StatusMessage = $"Downloading metadata for {candidates.Count} game(s)...";

        // Fetch the HTTP payloads with bounded parallelism (4 at a time): the
        // requests are the slow part, and firing all of them at once would trip
        // Steam's store throttling (429s → "partial" metadata). Task.Run puts
        // the work on pool threads so the HTTP continuations don't come back to
        // the UI thread; only reads (game.ExternalId) happen off the UI thread
        // here — entity mutation and the DbContext saves stay on the UI thread
        // in the loop below.
        using var throttle = new SemaphoreSlim(4);
        var results = await Task.WhenAll(candidates.Select(game =>
            Task.Run(async () =>
            {
                await throttle.WaitAsync();
                try
                {
                    return (game, metadata: await _steamMetadataProvider.GetByAppIdAsync(game.ExternalId));
                }
                finally
                {
                    throttle.Release();
                }
            })));

        int applied = 0;
        foreach (var (game, metadata) in results)
        {
            if (metadata is null)
                continue;

            try
            {
                // This sync runs after the window is interactive — the game may
                // have been deleted (or had its actions edited) while the awaits
                // above were in flight. Only mutate/save what's still live.
                if (!Games.Contains(game))
                    continue;

                ApplyMetadata(game, metadata);
                ApplyMetadataReferences(game, metadata);
                ApplySteamLocalArtwork(game);

                _gameRepository.Update(game);
                RefreshListDisplay(game);
                applied++;
            }
            catch (Exception ex)
            {
                // One bad game shouldn't abort the whole sync — log and continue.
                App.LogException(ex);
            }
        }

        StatusMessage = applied > 0
            ? $"Metadata sync complete: {applied}/{candidates.Count} game(s) updated."
            : $"Metadata sync complete: no updates ({candidates.Count} game(s) checked).";
    }

    [RelayCommand]
    private void PlayGame(Game? game = null)
    {
        var target = game ?? SelectedGame;
        if (target is null)
        {
            return;
        }

        try
        {
            _launcher.Launch(target);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't launch '{target.Name}': {ex.Message}";
        }
    }

    // See the threading note on GameLauncher.TrackAsync — both handlers below
    // run on the UI thread, so touching the repository/ObservableCollection
    // here directly is safe, no Dispatcher.Invoke needed.
    private void OnGameStarted(Game game)
    {
        StatusMessage = $"Playing {game.Name}...";
    }

    private void OnGameStopped(Game game, ulong sessionSeconds)
    {
        _gameRepository.Update(game);
        RefreshListDisplay(game);

        // Re-applies the active CustomSort comparer so the game re-positions
        // when the user sorted by Playtime/PlayCount/LastActivity — the
        // CollectionChanged(Replace) from RefreshListDisplay doesn't do that.
        GamesView.Refresh();

        RefreshStatistics();
        StatusMessage = $"{game.Name} — session: {sessionSeconds}s, total: {game.PlaytimeSeconds}s";
    }

    // Game is a plain POCO (no INotifyPropertyChanged — Bridge.Core entities
    // stay UI-agnostic on purpose), so the ListBox/detail panel won't pick up
    // in-place field changes on their own. A same-reference CollectionChanged
    // (Replace) does NOT make WPF re-read bound properties — virtualized
    // containers keep their old DataContext and never re-bind. Removing and
    // re-inserting at the same index forces the generator to prepare a fresh
    // container, which re-evaluates every binding (icons, covers, etc.) without
    // adding change notification to the entity itself.
    private void RefreshListDisplay(Game game)
    {
        var index = Games.IndexOf(game);
        if (index >= 0)
        {
            Games.RemoveAt(index);
            Games.Insert(index, game);
        }

        if (SelectedGame == game)
        {
            SelectedGame = null;
            SelectedGame = game;
        }
    }

    // Public hook for the edit window: after a game's fields are saved, re-render
    // its row and the detail panel and refresh the statistics.
    public void RefreshGameDisplay(Game game)
    {
        RefreshListDisplay(game);
        RefreshStatistics();
    }
}
