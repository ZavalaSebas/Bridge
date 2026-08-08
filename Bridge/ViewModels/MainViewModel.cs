using System.Collections.ObjectModel;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Core.Import;
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
    private readonly GameLauncher _launcher;
    private readonly RomScanner _romScanner;
    private readonly IEnumerable<IGameMetadataProvider> _metadataProviders;
    private readonly SteamMetadataProvider _steamMetadataProvider;
    private readonly SteamLibraryImporter _steamImporter;

    public ObservableCollection<Game> Games { get; } = [];

    [ObservableProperty]
    private Game? _selectedGame;

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
            return;
        }

        // Resolve stored ids back to display names. Straight lookup per id —
        // fine for the MVP's small reference collections; don't build a
        // caching dictionary until profiling shows these resolve calls matter.
        DevelopersText = FormatField("Developers", game.DeveloperIds, _companyRepository);
        PublishersText = FormatField("Publishers", game.PublisherIds, _companyRepository);
        PlatformsText = FormatField("Platforms", game.PlatformIds, _platformRepository);
    }

    // "Label: value1, value2" — or empty string when there's nothing to show
    // (the XAML collapses empty lines via a DataTrigger).
    private static string FormatField<T>(string label, IEnumerable<Guid> ids, IRepository<T> repo)
        where T : DatabaseObject
    {
        var names = ids
            .Select(id => repo.Get(id)?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        return names.Count == 0 ? string.Empty : $"{label}: {string.Join(", ", names)}";
    }

    public MainViewModel(
        IGameRepository gameRepository,
        IRepository<Emulator> emulatorRepository,
        IRepository<Genre> genreRepository,
        IRepository<Company> companyRepository,
        IRepository<Platform> platformRepository,
        IRepository<GameSource> sourceRepository,
        GameLauncher launcher,
        RomScanner romScanner,
        IEnumerable<IGameMetadataProvider> metadataProviders,
        SteamMetadataProvider steamMetadataProvider,
        SteamLibraryImporter steamImporter)
    {
        _gameRepository = gameRepository;
        _emulatorRepository = emulatorRepository;
        _genreRepository = genreRepository;
        _companyRepository = companyRepository;
        _platformRepository = platformRepository;
        _sourceRepository = sourceRepository;
        _launcher = launcher;
        _romScanner = romScanner;
        _metadataProviders = metadataProviders;
        _steamMetadataProvider = steamMetadataProvider;
        _steamImporter = steamImporter;
        _launcher.GameStarted += OnGameStarted;
        _launcher.GameStopped += OnGameStopped;
        LoadGames();
        ImportSteamLibrary();
        var steamSourceId = _sourceRepository.GetOrCreateByName("Steam").Id;
        _ = DownloadMissingSteamMetadataAsync(steamSourceId);
    }

    private void LoadGames()
    {
        foreach (var game in _gameRepository.GetAll())
        {
            ApplySteamLocalIcon(game);
            Games.Add(game);
        }

        RefreshStatistics();
    }

    // Playnite shows Steam's square 32x32 clienticon (PROJECT_FOUNDATION.md
    // §28.26), which Steam keeps locally in appcache\librarycache\{appid}\ —
    // the web API no longer returns the `clienticon` field, so the local file
    // is the faithful source. Falls back to whatever Icon already holds
    // (e.g. the header.jpg URL from metadata) when Steam isn't installed or
    // that app has no cached icon.
    private void ApplySteamLocalIcon(Game game)
    {
        if (game.SourceId == GameSource.ManualId || !uint.TryParse(game.ExternalId, out _))
            return;

        var localIcon = SteamLocalIconResolver.TryGetLocalIconPath(game.ExternalId);
        if (!string.IsNullOrWhiteSpace(localIcon))
            game.Icon = localIcon;
    }

    private void RefreshStatistics()
    {
        var stats = LibraryStatistics.Compute(Games);
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
        Games.Add(game);
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

    [RelayCommand]
    private void ScanRomFolder(string? romFolder)
    {
        if (string.IsNullOrWhiteSpace(romFolder))
        {
            return;
        }

        // MVP simplification (PLAN.md Fase 6 scope: "single emulator"): scans
        // against whichever Emulator+first Profile happens to exist first.
        // No emulator picker in the UI yet — configure one via IRepository<Emulator>
        // before this does anything useful.
        var emulator = _emulatorRepository.GetAll().FirstOrDefault();
        var profile = emulator?.Profiles.FirstOrDefault();
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
                Games.Add(game);
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
    private void ImportSteamLibrary()
    {
        try
        {
            var steamSource = _sourceRepository.GetOrCreateByName("Steam");
            var found = _steamImporter.GetInstalledGames();
            int added = 0, updated = 0;

            foreach (var metadata in found)
            {
                var existing = _gameRepository.FindByExternalId(metadata.ExternalId, steamSource.Id);
                if (existing is null)
                {
                    var game = new Game
                    {
                        Name = metadata.Name,
                        ExternalId = metadata.ExternalId,
                        SourceId = steamSource.Id,
                        InstallDirectory = metadata.InstallDirectory,
                        IsInstalled = metadata.IsInstalled
                    };
                    _gameRepository.Add(game);
                    Games.Add(game);
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
                    _gameRepository.Update(existing);
                    updated++;
                }
            }

            RefreshStatistics();
            StatusMessage = $"Steam import: {added} new, {updated} updated.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Steam import failed: {ex.Message}";
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

        ApplyMetadata(game, metadata);
        ApplyMetadataReferences(game, metadata);
        ApplySteamLocalIcon(game);

        _gameRepository.Update(game);
        RefreshListDisplay(game);
        StatusMessage = $"Metadata applied to '{game.Name}' (source: {providerName}).";
    }

    private static void ApplyMetadata(Game game, GameMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.Description))
            game.Description = metadata.Description;

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
    }

    private async Task DownloadMissingSteamMetadataAsync(Guid steamSourceId)
    {
        var candidates = _gameRepository.GetAll()
            .Where(g => g.SourceId == steamSourceId && string.IsNullOrWhiteSpace(g.Description))
            .ToList();

        foreach (var game in candidates)
        {
            try
            {
                var metadata = await _steamMetadataProvider.GetByAppIdAsync(game.ExternalId);
                if (metadata is null)
                    continue;

        ApplyMetadata(game, metadata);
        ApplyMetadataReferences(game, metadata);
        ApplySteamLocalIcon(game);

        _gameRepository.Update(game);
        RefreshListDisplay(game);
        StatusMessage = $"Metadata applied to '{game.Name}' (source: {_steamMetadataProvider.Name}).";
            }
            catch
            {
                // Skip this game, try the next one
            }
        }
    }

    [RelayCommand]
    private void PlayGame()
    {
        if (SelectedGame is null)
        {
            return;
        }

        try
        {
            _launcher.Launch(SelectedGame);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't launch '{SelectedGame.Name}': {ex.Message}";
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
        RefreshStatistics();
        StatusMessage = $"{game.Name} — session: {sessionSeconds}s, total: {game.PlaytimeSeconds}s";
    }

    // Game is a plain POCO (no INotifyPropertyChanged — Bridge.Core entities
    // stay UI-agnostic on purpose), so the ListBox/detail panel won't pick up
    // in-place field changes on their own. Re-setting the same reference at
    // its index forces a CollectionChanged(Replace), which is enough to make
    // WPF re-read bound properties without adding change notification to the
    // entity itself.
    private void RefreshListDisplay(Game game)
    {
        var index = Games.IndexOf(game);
        if (index >= 0)
        {
            Games[index] = game;
        }

        if (SelectedGame == game)
        {
            SelectedGame = null;
            SelectedGame = game;
        }
    }
}
