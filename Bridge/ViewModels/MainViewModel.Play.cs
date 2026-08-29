using Bridge.Core.Entities;
using Bridge;
using Bridge.Core.Utilities;
using Bridge.Emulation;
using Bridge.Resources;
using Bridge.Services;
using Bridge.Settings;
using CommunityToolkit.Mvvm.Input;
using System.IO;

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

        // Not-installed handling: external/manual/ROM files just show "Not installed".
        // Store games (Steam/Epic) trigger the store's install flow but must NOT
        // enter the playing/tracking state (IsRunning → Stop) — that's the bug
        // where "Install" immediately flips to "Stop" as if the game were running.
        if (!target.IsInstalled)
        {
            if (_retroArch.IsManagedRom(target))
            {
                // ROM file missing — handled below via emulator launch path (will
                // throw "ROM file not found" and still show NotInstalled via the
                // play button). Just block the tracking path.
                SetStatus($"{target.Name} — {Strings.NotInstalled}", StatusMessageKind.Warning);
                return;
            }

            if (IsStoreGame(target))
            {
                var installUrl = TryGetStoreInstallUrl(target);
                if (!string.IsNullOrWhiteSpace(installUrl))
                {
                    try
                    {
                        // Mirror GameLauncher.StartUrlAction's steam.exe -silent path
                        // but WITHOUT marking IsRunning/PlayCount or starting the
                        // directory tracker — an install is not a play session.
                        if (installUrl.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
                        {
                            var steamPath = Bridge.Import.Steam.SteamPaths.GetInstallationPath();
                            if (!string.IsNullOrWhiteSpace(steamPath))
                            {
                                var steamExe = Path.Combine(steamPath, "steam.exe");
                                if (File.Exists(steamExe))
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = steamExe,
                                        Arguments = $"-silent \"{installUrl}\"",
                                        UseShellExecute = true
                                    });
                                    SetStatus(Strings.Format(nameof(Strings.InstallingGameFormat), target.Name), StatusMessageKind.Normal);
                                    return;
                                }
                            }
                        }

                        if (Bridge.Services.SafeLauncher.TryOpenUrl(installUrl))
                        {
                            SetStatus(Strings.Format(nameof(Strings.InstallingGameFormat), target.Name), StatusMessageKind.Normal);
                        }
                        else
                        {
                            SetStatus(Strings.Format(nameof(Strings.CouldNotLaunchGameFormat), target.Name, Strings.CouldNotOpenStoreLink), StatusMessageKind.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        SetStatus(Strings.Format(nameof(Strings.CouldNotLaunchGameFormat), target.Name, ex.Message), StatusMessageKind.Error);
                    }

                    return;
                }

                SetStatus($"{target.Name} — {Strings.NotInstalled}", StatusMessageKind.Warning);
                return;
            }

            // External / manual — no store to install from
            SetStatus($"{target.Name} — {Strings.NotInstalled}", StatusMessageKind.Warning);
            return;
        }

        try
        {
            if (_retroArch.IsManagedRom(target))
            {
                IsEmulationBusy = true;
                BeginStatusProgress(indeterminate: true);
                try
                {
                    StatusMessage = Strings.Format(nameof(Strings.PreparingRetroArchFormat), target.Name);
                    await _retroArch.EnsureReadyAsync(target, new Progress<EmulatorProgress>(p =>
                    {
                        StatusMessage = p.Message;
                        if (p.Percent is { } percent)
                            ReportStatusProgress(percent, indeterminate: false);
                        else
                            ReportStatusProgress(StatusProgress, indeterminate: true);
                    }));
                    _gameRepository.Update(target);
                    // Now that the frontend/core exist, the button goes back to
                    // "Play" (or "Stop" once the game launches) instead of "Download".
                    RefreshAllEmulatorDownloadStates();
                    await ApplyCheatLaunchOverridesIfNeededAsync(target);
                    await ApplyCheevosLaunchConfigIfNeededAsync(target);
                }
                finally
                {
                    IsEmulationBusy = false;
                    EndStatusProgress();
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
    private void StopGame(Game? game = null)
    {
        var target = game ?? SelectedGame;
        if (target is null)
        {
            return;
        }

        _launcher.Stop(target);
        StatusMessage = Strings.Format(nameof(Strings.StoppingGameFormat), target.Name);
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
        if (MinimizeOnGameLaunchSettingsStore.Load())
            MinimizeWindowRequested?.Invoke();
    }

    private void OnGameStopped(Game game, ulong sessionSeconds)
    {
        var startedAt = game.LastActivity ?? DateTime.Now.AddSeconds(-Math.Max(1, (double)sessionSeconds));
        var endedAt = DateTime.Now;
        game.PlaySessions ??= new List<GamePlaySession>();
        game.PlaySessions.Add(new GamePlaySession
        {
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationSeconds = sessionSeconds
        });
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
        HandleRetroArchSessionEnded(game);
        TryAutoBackupRomSaves(game);
        if (MinimizeOnGameLaunchSettingsStore.Load())
            RestoreWindowRequested?.Invoke();
    }

    private void TryAutoBackupRomSaves(Game game)
    {
        try
        {
            RomSaveBackupResult? result = null;
            if (RomSaveBackupService.IsRomGame(game))
            {
                if (!RomSaveAutoBackupSettingsStore.Load())
                    return;

                result = RomSaveBackupService.Create(game, RomSaveBackupKind.Automatic);
            }
            else
            {
                if (!PcSaveAutoBackupSettingsStore.Load())
                    return;

                var folder = GameSaveFolderStore.Get(game.Id);
                if (string.IsNullOrWhiteSpace(folder))
                    return;

                result = RomSaveBackupService.Create(
                    game,
                    RomSaveBackupKind.Automatic,
                    customSaveFolder: folder);
            }

            if (ReferenceEquals(SelectedGame, game))
                RefreshSelectedGameSaveBackups();

            if (result is null || !result.Success || result.Unchanged)
                return;

            var when = (result.CreatedUtc ?? DateTime.UtcNow).ToLocalTime().ToString("g");
            StatusMessage = Strings.Format(
                nameof(Strings.RomSaveAutoBackupStatusFormat),
                game.Name,
                when,
                result.FileCount);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }

    private void HandleRetroArchSessionEnded(Game game)
    {
        if (!_retroArch.IsManagedRom(game) || !_retroAchievementsSettings.IsEmulatorConfigured)
            return;

        var executablePath = Path.Combine(Config.EmulatorInstallPath, "retroarch.exe");
        if (File.Exists(executablePath) &&
            _cheevosService.TryReadBackToken(executablePath, out var token) &&
            !string.IsNullOrWhiteSpace(token) &&
            !string.Equals(_retroAchievementsSettings.ConnectToken, token, StringComparison.Ordinal))
        {
            _retroAchievementsSettings.ConnectToken = token;
            _retroAchievementsSettings.Password = string.Empty;
            RetroAchievementsSettingsStore.Save(_retroAchievementsSettings);
        }

        if (_gameAchievementsService.IsRomGame(game))
            _gameAchievementsService.NotifyRomSessionEnded(game);
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
        InvalidateReferenceCaches();
        RefreshListDisplay(game);
        RefreshStatistics();
    }

    private async Task ApplyCheatLaunchOverridesIfNeededAsync(Game game)
    {
        var platformDefinition = RomPlatformResolver.Resolve(game, _platformRepository);
        if (platformDefinition is null || !platformDefinition.SupportsCheats)
        {
            return;
        }

        var cheatDirectory = _cheatService.GetCheatDirectoryIfExists(game, platformDefinition);
        if (cheatDirectory is null)
        {
            return;
        }

        var executablePath = Path.Combine(Config.EmulatorInstallPath, "retroarch.exe");
        if (!File.Exists(executablePath))
        {
            return;
        }

        await _cheatService.ApplyCheatLaunchOverridesAsync(
            game,
            platformDefinition,
            executablePath,
            cheatDirectory,
            AutoApplyCheatsSettingsStore.Load());
    }

    private async Task ApplyCheevosLaunchConfigIfNeededAsync(Game game)
    {
        if (!_retroAchievementsSettings.IsEmulatorConfigured)
            return;

        var executablePath = Path.Combine(Config.EmulatorInstallPath, "retroarch.exe");
        if (!File.Exists(executablePath))
            return;

        var credentials = new RetroArchCheevosCredentials(
            _retroAchievementsSettings.Username.Trim(),
            _retroAchievementsSettings.Password.Trim(),
            _retroAchievementsSettings.ConnectToken.Trim(),
            false);

        await _cheevosService.ApplyLaunchConfigAsync(executablePath, credentials);
    }

    private string? TryGetStoreInstallUrl(Game game)
    {
        // Epic already stores its launch URL as a GameAction; Steam creates it
        // runtime via SteamPlayActions. Prefer the stored Url action first.
        var storedUrl = game.GameActions.FirstOrDefault(a => a.IsPlayAction && a.Type == Bridge.Core.Enums.GameActionType.Url)?.Path;
        if (!string.IsNullOrWhiteSpace(storedUrl) && Bridge.Core.Utilities.UrlValidator.IsSafeToOpen(storedUrl))
            return storedUrl;

        var steamAction = Bridge.Import.Steam.SteamPlayActions.CreatePlayAction(game);
        return steamAction?.Path;
    }
}
