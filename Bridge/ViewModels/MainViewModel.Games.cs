using Bridge;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using System.IO;
using Bridge.Core.Utilities;
using Bridge.Import.Steam;
using Bridge.Resources;
using Bridge.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void DeleteGame()
    {
        if (SelectedGame is null)
        {
            return;
        }

        if (SelectedGame.IsRunning)
        {
            SetStatus(Strings.Format(nameof(Strings.GameRunningCloseBeforeDeleteFormat), SelectedGame.Name), StatusMessageKind.Error);
            return;
        }

        if (!_dialogService.ShowConfirm(
                Strings.Format(nameof(Strings.RemoveGameConfirmFormat), SelectedGame.Name),
                Strings.RemoveGameTitle,
                SymbolRegular.Delete24,
                confirmText: Strings.Remove,
                cancelText: Strings.Cancel))
        {
            return;
        }

        _gameRepository.Remove(SelectedGame.Id);
        Games.Remove(SelectedGame);
        SelectedGame = null;
        RefreshStatistics();
        SetStatus(Strings.GameRemovedFromLibrary);
    }

    // Runs the game's real uninstaller (Steam/Epic launcher or the Windows
    // registry entry) and, once the game provably left (install folder gone for
    // Steam/manual, gone from LauncherInstalled.dat for Epic), marks it as not
    // installed. Refuses while the game is running — same reason DeleteGame does.
    [RelayCommand]
    private async Task UninstallGameAsync()
    {
        if (SelectedGame is null)
        {
            return;
        }

        var game = SelectedGame;
        if (game.IsRunning)
        {
            SetStatus(Strings.Format(nameof(Strings.GameRunningCloseBeforeUninstallFormat), game.Name), StatusMessageKind.Error);
            return;
        }

        var sourceName = _sourceRepository.Get(game.SourceId)?.Name ?? Strings.Manual;
        var command = GameUninstaller.Resolve(game, sourceName);
        if (string.IsNullOrWhiteSpace(command))
        {
            SetStatus(Strings.Format(nameof(Strings.NoUninstallerFoundFormat), game.Name), StatusMessageKind.Warning);
            return;
        }

        StatusMessage = Strings.Format(nameof(Strings.LaunchingUninstallerFormat), game.Name);
        var completed = await GameUninstaller.RunAsync(command, game, sourceName);

        // The folder is gone (or was never tracked) — mark not installed. Keep
        // the override off: the source import may re-detect it later.
        game.IsInstalled = false;
        _gameRepository.Update(game);
        RefreshListDisplay(game);
        RefreshStatistics();
        StatusMessage = completed
            ? Strings.Format(nameof(Strings.GameUninstalledFormat), game.Name)
            : Strings.Format(nameof(Strings.UninstallerFinishedStillInstalledFormat), game.Name);
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

    // Flips the favorite flag and persists it immediately (the hero star and
    // the More menu share this state). The label shows the action the menu
    // item would take, not the current state. No list refresh here: the row
    // doesn't render the flag, and re-inserting the item would clear the
    // selection mid-menu.
    [RelayCommand]
    private void ToggleFavorite()
    {
        if (SelectedGame is null)
        {
            return;
        }

        SelectedGame.Favorite = !SelectedGame.Favorite;
        _gameRepository.Update(SelectedGame);
        RefreshStatistics();
        OnPropertyChanged(nameof(FavoriteMenuText));
    }

    public string FavoriteMenuText => SelectedGame?.Favorite == true
        ? Strings.RemoveFromFavorites
        : Strings.AddToFavorites;

    // Persists the hero star's favorite state. The star binds IsChecked TwoWay
    // straight to Game.Favorite (changing the object in memory), so on its own
    // a click never reaches the DB — the More menu path goes through
    // ToggleFavorite, this keeps the star path equivalent. No list refresh: the
    // row doesn't render the flag, and re-inserting would clear the selection.
    public void PersistFavorite()
    {
        if (SelectedGame is null)
            return;

        _gameRepository.Update(SelectedGame);
        RefreshStatistics();
        OnPropertyChanged(nameof(FavoriteMenuText));
    }

    // Flips the Hidden flag and persists it immediately. Hidden games vanish
    // from the library (the filter drops them) unless ShowHidden is active, so
    // no re-insert here — the item just leaves the view.
    [RelayCommand]
    private void ToggleHidden()
    {
        if (SelectedGame is null)
        {
            return;
        }

        SelectedGame.Hidden = !SelectedGame.Hidden;
        _gameRepository.Update(SelectedGame);
        GamesView.Refresh();
        RefreshStatistics();
        OnPropertyChanged(nameof(HiddenMenuText));
    }

    public string HiddenMenuText => SelectedGame?.Hidden == true
        ? Strings.ShowGame
        : Strings.HideGame;

    // Built-in completion statuses seeded on first run via GetOrCreateByName.
    public IReadOnlyList<string> CompletionStatuses { get; } =
    [
        Strings.CompletionStatusAbandoned,
        Strings.CompletionStatusBeaten,
        Strings.CompletionStatusCompleted,
        Strings.CompletionStatusNotPlayed,
        Strings.CompletionStatusOnHold,
        Strings.CompletionStatusPlanToPlay,
        Strings.Played,
        Strings.CompletionStatusPlaying
    ];

    [RelayCommand]
    private void SetCompletionStatus(string? statusName)
    {
        if (SelectedGame is null || string.IsNullOrWhiteSpace(statusName))
        {
            return;
        }

        var status = _completionStatusRepository.GetOrCreateByName(statusName);
        SelectedGame.CompletionStatusId = status.Id;
        if (IsCompletedStatus(status))
            SelectedGame.CompletedAt = DateTime.Now;
        _gameRepository.Update(SelectedGame);
        InvalidateReferenceCaches();
        CompletionStatusText = status.Name;
        GamesView.Refresh();
        RefreshStatistics();
    }

    [RelayCommand]
    private void ChangeArt()
    {
        if (SelectedGame is not { } game)
            return;

        if (_gameEditWindowOpener.Show(game, selectMediaTab: true))
            RefreshGameDisplay(game);
    }

    [RelayCommand]
    private void OpenCheats()
    {
        if (SelectedGame is null || !_retroArch.IsManagedRom(SelectedGame))
        {
            return;
        }

        _cheatsWindowOpener.Show(SelectedGame);
    }

    private static bool IsCompletedStatus(CompletionStatus status)
    {
        return status.Kind == CompletionStatusKind.Played
            || string.Equals(status.Name, Strings.CompletionStatusCompleted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status.Name, Strings.CompletionStatusBeaten, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status.Name, Strings.Played, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Opens the selected game's install folder in Explorer.</summary>
    [RelayCommand]
    private void OpenGameLocation()
    {
        if (SelectedGame is null)
        {
            return;
        }

        var dir = SelectedGame.InstallDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            StatusMessage = string.IsNullOrWhiteSpace(dir)
                ? Strings.Format(nameof(Strings.NoInstallDirectoryFormat), SelectedGame.Name)
                : Strings.Format(nameof(Strings.CouldNotOpenDirectoryFormat), dir);
            return;
        }

        if (!SafeLauncher.TryOpenDirectory(dir))
            StatusMessage = Strings.Format(nameof(Strings.CouldNotOpenDirectoryFormat), dir);
    }

    /// <summary>Opens the ROM save folder, or the user-chosen folder for Steam/Epic/external games.</summary>
    [RelayCommand]
    private void OpenSaveLocation()
    {
        if (SelectedGame is null)
            return;

        string? dir;
        if (RomSaveBackupService.IsRomGame(SelectedGame))
        {
            dir = GameSaveLocationResolver.TryResolve(SelectedGame, new GameSaveLocationOptions
            {
                SteamInstallPath = SteamPaths.GetInstallationPath(),
                RetroArchInstallPath = Config.EmulatorInstallPath,
                IsManagedRom = _retroArch.IsManagedRom(SelectedGame)
            });
        }
        else
        {
            dir = GameSaveFolderStore.Get(SelectedGame.Id);
        }

        if (string.IsNullOrWhiteSpace(dir))
        {
            StatusMessage = Strings.Format(nameof(Strings.NoSaveLocationFormat), SelectedGame.Name);
            return;
        }

        if (!Directory.Exists(dir))
        {
            if (RomSaveBackupService.IsRomGame(SelectedGame) &&
                PathContainment.IsUnderRoot(dir, Config.EmulatorInstallPath))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    StatusMessage = Strings.Format(nameof(Strings.CouldNotOpenDirectoryFormat), dir);
                    return;
                }
            }
            else
            {
                StatusMessage = Strings.Format(nameof(Strings.NoSaveLocationFormat), SelectedGame.Name);
                return;
            }
        }

        if (!SafeLauncher.TryOpenDirectory(dir))
            StatusMessage = Strings.Format(nameof(Strings.CouldNotOpenDirectoryFormat), dir);
    }

    private bool CanSetSaveLocation() => SelectedGameNeedsSaveFolder;

    [RelayCommand(CanExecute = nameof(CanSetSaveLocation))]
    private void SetSaveLocation()
    {
        if (SelectedGame is null || !SelectedGameNeedsSaveFolder)
            return;

        var current = GameSaveFolderStore.Get(SelectedGame.Id);
        var dialog = new OpenFolderDialog
        {
            Title = Strings.SetSaveLocationDialogTitle
        };
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            dialog.InitialDirectory = current;
        }
        else
        {
            var suggested = GameSaveLocationResolver.TryResolve(SelectedGame, new GameSaveLocationOptions
            {
                SteamInstallPath = SteamPaths.GetInstallationPath(),
                RetroArchInstallPath = Config.EmulatorInstallPath,
                IsManagedRom = _retroArch.IsManagedRom(SelectedGame)
            });
            if (!string.IsNullOrWhiteSpace(suggested) && Directory.Exists(suggested))
                dialog.InitialDirectory = suggested;
            else if (!string.IsNullOrWhiteSpace(SelectedGame.InstallDirectory) &&
                     Directory.Exists(SelectedGame.InstallDirectory))
                dialog.InitialDirectory = SelectedGame.InstallDirectory;
        }

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            return;

        GameSaveFolderStore.Set(SelectedGame.Id, dialog.FolderName);
        NotifySaveFolderBindings();
        StatusMessage = Strings.Format(
            nameof(Strings.SaveLocationSetFormat),
            SelectedGame.Name,
            dialog.FolderName);
    }

    private bool CanBackupRomSaves() => SelectedGameCanBackupSaves;

    [RelayCommand(CanExecute = nameof(CanBackupRomSaves))]
    private void BackupRomSaves()
    {
        if (SelectedGame is null || !SelectedGameCanBackupSaves)
            return;

        var result = RomSaveBackupService.Create(
            SelectedGame,
            RomSaveBackupKind.Manual,
            customSaveFolder: GameSaveFolderStore.Get(SelectedGame.Id));
        RefreshSelectedGameSaveBackups();
        if (!result.Success)
        {
            StatusMessage = Strings.Format(
                nameof(Strings.RomSaveBackupFailedFormat),
                SelectedGame.Name,
                result.Message ?? Strings.Unknown);
            return;
        }

        var when = (result.CreatedUtc ?? DateTime.UtcNow).ToLocalTime().ToString("g");
        StatusMessage = Strings.Format(
            nameof(Strings.RomSaveBackupCreatedFormat),
            SelectedGame.Name,
            when,
            result.FileCount);
    }

    [RelayCommand]
    private void RestoreRomSave(RomSaveBackupListItem? item)
    {
        if (SelectedGame is null || item is null)
            return;

        var when = item.CreatedUtc.ToLocalTime().ToString("g");
        var kind = item.Kind == RomSaveBackupKind.Manual
            ? Strings.RomSaveBackupManual
            : Strings.RomSaveBackupAutomatic;
        if (!_dialogService.ShowConfirm(
                Strings.Format(
                    nameof(Strings.RomSaveRestoreConfirmFormat),
                    item.FileCount,
                    when,
                    kind,
                    SelectedGame.Name),
                Strings.RestoreRomSaves,
                SymbolRegular.ArrowReset24,
                Strings.RestoreRomSaves,
                Strings.Cancel))
        {
            return;
        }

        var result = RomSaveBackupService.Restore(
            item.DirectoryPath,
            RomSaveBackupService.GetPrimaryRomPath(SelectedGame),
            customSaveFolder: GameSaveFolderStore.Get(SelectedGame.Id));
        if (!result.Success)
        {
            StatusMessage = Strings.Format(
                nameof(Strings.RomSaveRestoreFailedFormat),
                SelectedGame.Name,
                result.Message ?? Strings.Unknown);
            return;
        }

        StatusMessage = Strings.Format(
            nameof(Strings.RomSaveRestoredFormat),
            SelectedGame.Name,
            when,
            result.FileCount);
    }

    internal void NotifySaveFolderBindings()
    {
        OnPropertyChanged(nameof(SelectedGameHasCustomSaveFolder));
        OnPropertyChanged(nameof(SelectedGameCanBackupSaves));
        BackupRomSavesCommand.NotifyCanExecuteChanged();
        RefreshSelectedGameSaveBackups();
    }

    internal void RefreshSelectedGameSaveBackups()
    {
        SelectedGameSaveBackups.Clear();
        if (SelectedGame is null || !SelectedGameCanBackupSaves)
        {
            OnPropertyChanged(nameof(SelectedGameHasRomSaveBackups));
            return;
        }

        foreach (var snapshot in RomSaveBackupService.List(SelectedGame.Id))
        {
            var kind = snapshot.Kind == RomSaveBackupKind.Manual
                ? Strings.RomSaveBackupManual
                : Strings.RomSaveBackupAutomatic;
            SelectedGameSaveBackups.Add(new RomSaveBackupListItem
            {
                DirectoryPath = snapshot.DirectoryPath,
                CreatedUtc = snapshot.CreatedUtc,
                Kind = snapshot.Kind,
                FileCount = snapshot.FileCount,
                Header = Strings.Format(
                    nameof(Strings.RomSaveBackupItemFormat),
                    kind,
                    snapshot.CreatedUtc.ToLocalTime().ToString("g"),
                    snapshot.FileCount)
            });
        }

        OnPropertyChanged(nameof(SelectedGameHasRomSaveBackups));
    }
}
