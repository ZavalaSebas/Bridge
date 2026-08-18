using System.Diagnostics;
using Bridge;
using Bridge.Core.Entities;
using Bridge.Resources;
using Bridge.Services;
using CommunityToolkit.Mvvm.Input;
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

        if (!MessageDialogWindow.ShowConfirm(
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

        var sourceName = _sourceRepository.Get(game.SourceId)?.Name ?? "Manual";
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

    // The built-in completion status set (Playnite's defaults). Statuses are
    // reference rows created on first use via GetOrCreateByName; the menu just
    // applies whichever one the user picks.
    public IReadOnlyList<string> CompletionStatuses { get; } =
    [
        "Abandoned", "Beaten", "Completed", "Not Played",
        "On Hold", "Plan to Play", "Played", "Playing"
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
        _gameRepository.Update(SelectedGame);
        RefreshReferenceFields(SelectedGame);
        RefreshStatistics();
    }

    // Opens the selected game's install folder in Explorer.
    [RelayCommand]
    private void OpenGameLocation()
    {
        if (SelectedGame is null)
        {
            return;
        }

        var dir = SelectedGame.InstallDirectory;
        if (string.IsNullOrWhiteSpace(dir))
        {
            StatusMessage = Strings.Format(nameof(Strings.NoInstallDirectoryFormat), SelectedGame.Name);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            StatusMessage = Strings.Format(nameof(Strings.CouldNotOpenDirectoryFormat), dir);
        }
    }
}
