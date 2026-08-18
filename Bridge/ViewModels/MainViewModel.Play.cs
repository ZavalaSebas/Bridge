using Bridge.Core.Entities;
using Bridge.Emulation;
using Bridge.Resources;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task PlayGameAsync(Game? game = null)
    {
        var target = game ?? SelectedGame;
        if (target is null)
        {
            return;
        }

        try
        {
            if (_retroArch.IsManagedRom(target))
            {
                IsEmulationBusy = true;
                try
                {
                    StatusMessage = Strings.Format(nameof(Strings.PreparingRetroArchFormat), target.Name);
                    await _retroArch.EnsureReadyAsync(target, new Progress<EmulatorProgress>(p =>
                    {
                        StatusMessage = p.Message;
                        if (p.Percent is { } percent)
                        {
                            EmulationProgress = percent;
                            IsEmulationIndeterminate = false;
                        }
                        else
                        {
                            IsEmulationIndeterminate = true;
                        }
                    }));
                    _gameRepository.Update(target);
                    // Now that the frontend/core exist, the button goes back to
                    // "Play" (or "Stop" once the game launches) instead of "Download".
                    UpdateNeedsEmulatorDownload();
                }
                finally
                {
                    IsEmulationBusy = false;
                    IsEmulationIndeterminate = false;
                    EmulationProgress = 0;
                }
            }
            _launcher.Launch(target);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Format(nameof(Strings.CouldNotLaunchGameFormat), target.Name, ex.Message);
        }
    }

    // Kills the running game's processes (the Play button turns into Stop while
    // the game runs). The launcher's tracking loop sees the processes die and
    // finalizes playtime/IsRunning through GameStopped — no bookkeeping here.
    [RelayCommand]
    private void StopGame()
    {
        if (SelectedGame is null)
        {
            return;
        }

        _launcher.Stop(SelectedGame);
        StatusMessage = Strings.Format(nameof(Strings.StoppingGameFormat), SelectedGame.Name);
    }

    // See the threading note on GameLauncher.TrackAsync — both handlers below
    // run on the UI thread, so touching the repository/ObservableCollection
    // here directly is safe, no Dispatcher.Invoke needed.
    private void OnGameStarted(Game game)
    {
        // Persist the launch-side bookkeeping immediately — PlayCount/LastActivity/
        // IsRunning only used to reach the DB via OnGameStopped, which never runs
        // if Bridge is closed while the game is still running. Saving here means a
        // close mid-game still records "played once / last played now".
        _gameRepository.Update(game);
        StatusMessage = Strings.Format(nameof(Strings.PlayingGameFormat), game.Name);
    }

    private void OnGameStopped(Game game, ulong sessionSeconds)
    {
        _gameRepository.Update(game);

        // Re-applies the active CustomSort comparer so the game re-positions
        // when the user sorted by Playtime/PlayCount/LastActivity.
        GamesView.Refresh();

        // The table view renders playtime through GameDetailRow wrappers (plain
        // POCOs) — rebuild them so the updated session time shows, without
        // removing the game from Games (that would drop the ListBox selection).
        RebuildDetailedRows();

        // Re-binds the detail panel so the updated playtime/last-played render
        // (Game is a POCO — no INPC). Setting the same SelectedGame reference
        // after the refresh re-asserts the ListBox selection too: the row is
        // never removed/re-inserted here, so the selection survives.
        if (SelectedGame == game)
        {
            SelectedGame = null;
            SelectedGame = game;
        }

        RefreshStatistics();
        StatusMessage = Strings.Format(nameof(Strings.SessionSummaryFormat), game.Name, sessionSeconds, game.PlaytimeSeconds);
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
        // Capture selection BEFORE removing: the RemoveAt below fires
        // CollectionChanged, and WPF's ListBox clears SelectedItem → SelectedGame
        // becomes null via the TwoWay binding. Comparing after the fact (like the
        // old `if (SelectedGame == game)`) would miss it and leave the selection
        // lost. Restore by reference instead.
        var wasSelected = ReferenceEquals(SelectedGame, game);
        var index = Games.IndexOf(game);
        if (index >= 0)
        {
            Games.RemoveAt(index);
            Games.Insert(index, game);
        }

        if (wasSelected)
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
