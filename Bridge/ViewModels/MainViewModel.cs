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
    private readonly IRepository<GameSource> _sourceRepository;
    private readonly GameLauncher _launcher;
    private readonly RomScanner _romScanner;
    private readonly IgdbMetadataProvider _metadataProvider;
    private readonly SteamLibraryImporter _steamImporter;

    public ObservableCollection<Game> Games { get; } = [];

    [ObservableProperty]
    private Game? _selectedGame;

    [ObservableProperty]
    private string _newGameName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _executablePathInput = string.Empty;

    [ObservableProperty]
    private string _statisticsSummary = string.Empty;

    [ObservableProperty]
    private string _romFolderInput = string.Empty;

    partial void OnSelectedGameChanged(Game? value)
    {
        var playAction = value?.GameActions.FirstOrDefault(a => a.IsPlayAction);
        ExecutablePathInput = playAction?.Path ?? string.Empty;
    }

    public MainViewModel(
        IGameRepository gameRepository,
        IRepository<Emulator> emulatorRepository,
        IRepository<Genre> genreRepository,
        IRepository<GameSource> sourceRepository,
        GameLauncher launcher,
        RomScanner romScanner,
        IgdbMetadataProvider metadataProvider,
        SteamLibraryImporter steamImporter)
    {
        _gameRepository = gameRepository;
        _emulatorRepository = emulatorRepository;
        _genreRepository = genreRepository;
        _sourceRepository = sourceRepository;
        _launcher = launcher;
        _romScanner = romScanner;
        _metadataProvider = metadataProvider;
        _steamImporter = steamImporter;
        _launcher.GameStarted += OnGameStarted;
        _launcher.GameStopped += OnGameStopped;
        LoadGames();
    }

    private void LoadGames()
    {
        foreach (var game in _gameRepository.GetAll())
        {
            Games.Add(game);
        }

        RefreshStatistics();
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
    private void AddGame()
    {
        if (string.IsNullOrWhiteSpace(NewGameName))
        {
            return;
        }

        var game = new Game { Name = NewGameName.Trim() };
        _gameRepository.Add(game);
        Games.Add(game);
        SelectedGame = game;
        NewGameName = string.Empty;
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
    private void ScanRomFolder()
    {
        if (string.IsNullOrWhiteSpace(RomFolderInput))
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
            var found = _romScanner.Scan(RomFolderInput.Trim(), emulator.Id, profile, Games);
            foreach (var game in found)
            {
                _gameRepository.Add(game);
                Games.Add(game);
            }

            RefreshStatistics();
            StatusMessage = $"Scan complete: {found.Count} new ROM(s) imported from '{RomFolderInput}'.";
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
        {
            return;
        }

        StatusMessage = $"Downloading metadata for '{SelectedGame.Name}'...";
        try
        {
            var metadata = await _metadataProvider.SearchAsync(SelectedGame.Name);
            if (metadata is null)
            {
                StatusMessage = $"No IGDB match found for '{SelectedGame.Name}'.";
                return;
            }

            // No SkipExistingValues semantics yet (PROJECT_FOUNDATION.md §28.3
            // has the real algorithm for that) — this unconditionally
            // overwrites whatever the IGDB response actually provided.
            if (!string.IsNullOrWhiteSpace(metadata.Description))
            {
                SelectedGame.Description = metadata.Description;
            }

            if (metadata.ReleaseDate is { } releaseDate)
            {
                SelectedGame.ReleaseDate = releaseDate;
            }

            if (!string.IsNullOrWhiteSpace(metadata.CoverImage))
            {
                // Stored as a raw URL for now, not downloaded/cached locally —
                // Bridge.Storage has no file-cache equivalent to Playnite's
                // AddFile yet (§28.2). Mirrors Playnite's own lazy-URL
                // background-image behavior (§28.3) rather than inventing
                // something new.
                SelectedGame.CoverImage = metadata.CoverImage;
            }

            foreach (var genreName in metadata.Genres)
            {
                var genre = _genreRepository.GetOrCreateByName(genreName);
                if (!SelectedGame.GenreIds.Contains(genre.Id))
                {
                    SelectedGame.GenreIds.Add(genre.Id);
                }
            }

            _gameRepository.Update(SelectedGame);
            RefreshListDisplay(SelectedGame);
            StatusMessage = $"Metadata applied to '{SelectedGame.Name}' (matched IGDB: '{metadata.Name}').";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Metadata download failed: {ex.Message}";
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
