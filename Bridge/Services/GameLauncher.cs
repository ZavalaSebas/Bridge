using System.Diagnostics;
using System.IO;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Import.Steam;

namespace Bridge.Services;

/// <summary>
/// Launches a game's play GameAction and tracks it by polling — matching
/// Playnite's real mechanism exactly (PROJECT_FOUNDATION.md §28.9-28.10):
/// no Process.Exited event, a loop checking !process.HasExited every
/// TrackingFrequencyMs, session length accumulated from elapsed wall time.
///
/// MVP scope, deliberately narrower than Playnite's real behavior — see
/// PLAN.md's Fase 3/6 entries for what's still missing, don't treat these
/// gaps as bugs:
/// - GameActionType.Url is supported but only for the auto-resolved Steam
///   case below, not as a general user-configured action.
/// - GameActionType.Script isn't supported.
/// - Emulator argument substitution is a single literal "{RomPath}" token
///   replace, not Playnite's full ExpandVariables system (§28.9) — no
///   {InstallDir}/{PlayniteDir}/etc. tokens yet.
/// - Steam tracking uses TrackingMode.Directory (watch processes whose
///   binary lives under the game's InstallDirectory — Playnite's
///   WatchDirectoryProcesses, §28.9). Everything else tracks the exact
///   launched process only (behaves like Playnite's OriginalProcess for every
///   TrackingMode value) — no process-tree walking yet.
///
/// Automatic Steam play action: mirrors Playnite's SteamPlayController
/// (SteamGameController.cs:160-204, verified against the real extension and
/// core in PROJECT_FOUNDATION.md §28.26). When a Steam-imported game (appid in
/// ExternalId) has no user-configured GameAction, Launch() resolves one at
/// runtime — steam://rungameid/{appid} passed to steam.exe as
/// "-silent \"steam://rungameid/{appid}\"". The local .exe in InstallDirectory
/// is deliberately NOT used to launch (Steamworks DRM — running the exe
/// directly without the Steam client fails), which is why Playnite never does
/// either. The resolved action is not persisted.
/// </summary>
public class GameLauncher(IRepository<Emulator> emulatorRepository)
{
    public event Action<Game>? GameStarted;
    public event Action<Game, ulong>? GameStopped;

    public void Launch(Game game)
    {
        var action = game.GameActions.FirstOrDefault(a => a.IsPlayAction)
            ?? game.GameActions.FirstOrDefault()
            ?? TryResolveAutomaticAction(game);

        if (action is null)
        {
            throw new InvalidOperationException($"'{game.Name}' has no play action configured.");
        }

        var process = action.Type switch
        {
            GameActionType.File => StartFileAction(action),
            GameActionType.Url => StartUrlAction(action),
            GameActionType.Emulator => StartEmulatorAction(game, action),
            _ => throw new NotSupportedException($"Action type {action.Type} isn't supported yet.")
        };

        game.IsRunning = true;
        game.LastActivity = DateTime.Now;
        game.PlayCount++;
        GameStarted?.Invoke(game);

        if (action.TrackingMode == TrackingMode.Directory)
        {
            _ = TrackDirectoryAsync(game, action.InitialTrackingDelayMs, action.TrackingFrequencyMs);
        }
        else
        {
            _ = TrackAsync(game, process, action.TrackingFrequencyMs);
        }
    }

    private static GameAction? TryResolveAutomaticAction(Game game)
    {
        // Mirrors Playnite's SteamPlayController, which is created for every Steam game
        // with no stored GameAction (SteamLibrary.cs:101-109 + SteamGameController.cs:139-153).
        // The action build is pure logic in SteamPlayActions (unit-tested without needing
        // Steam installed); only the "is Steam actually installed" check needs the registry.
        var action = SteamPlayActions.CreatePlayAction(game);
        if (action is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(SteamPaths.GetInstallationPath()) ? null : action;
    }

    private static Process StartFileAction(GameAction action) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = action.Path,
            Arguments = action.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(action.WorkingDirectory) ? null : action.WorkingDirectory,
            UseShellExecute = true
        }) ?? throw new InvalidOperationException($"Failed to start process: {action.Path}");

    // Mirrors Playnite's SteamPlayController.Play (SteamGameController.cs:160-204):
    // prefer explicit steam.exe -silent "steam://..." (avoids the client window and
    // is more reliable than relying on the steam:// URL association), fall back to
    // ShellExecute on the URL itself (ProcessStarter.StartUrl equivalent).
    private static Process StartUrlAction(GameAction action)
    {
        var steamPath = SteamPaths.GetInstallationPath();
        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            var steamExe = Path.Combine(steamPath, "steam.exe");
            if (File.Exists(steamExe))
            {
                return Process.Start(new ProcessStartInfo
                {
                    FileName = steamExe,
                    Arguments = $"-silent \"{action.Path}\"",
                    UseShellExecute = true
                }) ?? throw new InvalidOperationException($"Failed to start Steam: {steamExe}");
            }
        }

        return Process.Start(new ProcessStartInfo
        {
            FileName = action.Path,
            UseShellExecute = true
        }) ?? throw new InvalidOperationException($"Failed to open URL: {action.Path}");
    }

    private Process StartEmulatorAction(Game game, GameAction action)
    {
        var emulator = emulatorRepository.Get(action.EmulatorId)
            ?? throw new InvalidOperationException($"Emulator {action.EmulatorId} not found.");
        var profile = emulator.GetProfile(action.EmulatorProfileId)
            ?? throw new InvalidOperationException($"Emulator profile {action.EmulatorProfileId} not found on '{emulator.Name}'.");
        var romPath = game.Roms.FirstOrDefault()?.Path
            ?? throw new InvalidOperationException($"'{game.Name}' has no ROM to launch.");

        var executable = Path.IsPathRooted(profile.Executable)
            ? profile.Executable
            : Path.Combine(emulator.InstallDirectory, profile.Executable);
        var arguments = profile.Arguments.Replace("{RomPath}", $"\"{romPath}\"");
        var workingDirectory = string.IsNullOrWhiteSpace(profile.WorkingDirectory)
            ? emulator.InstallDirectory
            : profile.WorkingDirectory;

        return Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory,
            UseShellExecute = true
        }) ?? throw new InvalidOperationException($"Failed to start emulator: {executable}");
    }

    // Deliberately no ConfigureAwait(false) anywhere in this method: Launch()
    // is always called from the UI thread, so the implicit WPF
    // DispatcherSynchronizationContext captured by the first `await` keeps
    // every line below — including the game.* mutations in `finally` — on
    // the UI thread too. That's what makes it safe to touch the (singleton,
    // not thread-safe) BridgeDbContext-backed repository from the GameStopped
    // handler without any extra marshaling. If this method is ever called
    // from a background thread, that assumption breaks silently.
    private async Task TrackAsync(Game game, Process process, int frequencyMs)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            while (!process.HasExited)
            {
                await Task.Delay(frequencyMs);
            }
        }
        finally
        {
            stopwatch.Stop();
            var sessionSeconds = (ulong)stopwatch.Elapsed.TotalSeconds;
            game.IsRunning = false;
            game.PlaytimeSeconds += sessionSeconds;
            GameStopped?.Invoke(game, sessionSeconds);
        }
    }

    // Directory-based tracking for the auto-resolved Steam case. Mirrors Playnite's
    // WatchDirectoryProcesses (§28.9): the launched process is steam.exe, which is
    // NOT the game, so we watch for any process whose executable lives under the
    // game's InstallDirectory instead of tracking a PID. Waits for at least one such
    // process to appear (InitialTrackingDelayMs grace period for Steam to spin up),
    // then for all of them to exit.
    private async Task TrackDirectoryAsync(Game game, int initialDelayMs, int frequencyMs)
    {
        var stopwatch = Stopwatch.StartNew();
        var sessionStart = stopwatch.Elapsed;
        bool launched = false;

        try
        {
            if (initialDelayMs > 0)
            {
                await Task.Delay(initialDelayMs);
            }

            // Wait for the game's process to appear. If it never does (Steam fails
            // to start it, a bad InstallDirectory, offline mode, ...), don't hang
            // forever: give up after DirectoryLaunchTimeout so IsRunning clears and
            // no phantom playtime gets recorded.
            while (!HasProcessInDirectory(game.InstallDirectory))
            {
                if (stopwatch.Elapsed >= DirectoryLaunchTimeout)
                {
                    return;
                }

                await Task.Delay(frequencyMs);
            }

            launched = true;
            sessionStart = stopwatch.Elapsed;

            while (HasProcessInDirectory(game.InstallDirectory))
            {
                await Task.Delay(frequencyMs);
            }
        }
        finally
        {
            stopwatch.Stop();
            game.IsRunning = false;

            if (launched)
            {
                var sessionSeconds = (ulong)(stopwatch.Elapsed - sessionStart).TotalSeconds;
                game.PlaytimeSeconds += sessionSeconds;
                GameStopped?.Invoke(game, sessionSeconds);
            }
        }
    }

    private static readonly TimeSpan DirectoryLaunchTimeout = TimeSpan.FromMinutes(5);

    private static bool HasProcessInDirectory(string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            return false;
        }

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(path) ||
                    !path.StartsWith(installDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Path-prefix boundary check: "C:\Games\Steam2\game.exe" must not
                // match an install dir of "C:\Games\Steam".
                if (path.Length > installDirectory.Length &&
                    path[installDirectory.Length] is not ('\\' or '/'))
                {
                    continue;
                }

                return true;
            }
            catch
            {
                // Access denied or the process exited mid-iteration — skip it.
            }
        }

        return false;
    }
}
