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
///   WatchDirectoryProcesses, §28.9). File/Emulator actions with the default
///   tracking use process-tree walking (Playnite's MonitorProcessTree, §28.10)
///   so launcher-based games (Genshin's launcher.exe, Epic/GOG frontends) keep
///   tracking after the launcher spawns the real game and exits. Other modes
///   track the exact launched process only (Playnite's OriginalProcess).
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
        if (game.IsRunning)
        {
            // Double-click / repeated Play while the game is already launching or
            // running: launching a second copy duplicates process tracking (which
            // would double-count playtime) and corrupts the running state.
            return;
        }

        var action = game.GameActions.FirstOrDefault(a => a.IsPlayAction)
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

        switch (action.TrackingMode)
        {
            case TrackingMode.Directory:
                _ = TrackDirectoryAsync(game, action.InitialTrackingDelayMs, action.TrackingFrequencyMs);
                break;

            // Process-tree tracking: the launched process AND every descendant it
            // spawns (the launcher-spawns-game-and-exits case — Genshin's
            // launcher.exe, Epic/GOG frontends). Default auto-chooses the tree
            // for File/Emulator actions, matching Playnite's automatic choice.
            case TrackingMode.Process:
            case TrackingMode.Default when action.Type is GameActionType.File or GameActionType.Emulator:
                try
                {
                    var pid = process.Id;
                    process.Dispose();
                    _ = TrackProcessTreeAsync(game, pid, action.TrackingFrequencyMs);
                }
                catch
                {
                    process.Dispose();
                    // No usable handle/Id for the started process — fall back to
                    // exact-process tracking rather than losing the session.
                    _ = TrackAsync(game, process, action.TrackingFrequencyMs);
                }
                break;

            default:
                _ = TrackAsync(game, process, action.TrackingFrequencyMs);
                break;
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
    // ShellExecute on the URL itself (ProcessStarter.StartUrl equivalent). Only
    // steam:// URLs go through steam.exe — anything else (http/https, mailto, ...)
    // is ShellExecute'd directly.
    private static Process StartUrlAction(GameAction action)
    {
        if (action.Path.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
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
        catch
        {
            // HasExited can throw when the process was started with UseShellExecute
            // (no usable handle) or if it exited/disposed between checks. For a
            // shell-executed process there's no handle to poll, so record whatever
            // elapsed rather than a phantom 0-second session. This is the
            // fire-and-forget task — swallowing is what prevents an unobserved-task
            // exception.
            var sessionSeconds = (ulong)stopwatch.Elapsed.TotalSeconds;
            game.IsRunning = false;
            game.PlaytimeSeconds += sessionSeconds;
            GameStopped?.Invoke(game, sessionSeconds);
            return;
        }
        finally
        {
            process.Dispose();
        }

        stopwatch.Stop();
        var elapsed = (ulong)stopwatch.Elapsed.TotalSeconds;
        game.IsRunning = false;
        game.PlaytimeSeconds += elapsed;
        GameStopped?.Invoke(game, elapsed);
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

            // Executables under the install directory, matched by process name as a
            // fallback: some launchers (Genshin's HYP.exe/HYPHelper.exe) run elevated
            // or otherwise protected, so MainModule.FileName is unreadable even though
            // the process is running from that directory. Build the name set once per
            // session — the directory doesn't change mid-session.
            var executableNames = GetExecutableNames(game.InstallDirectory);

            // Wait for the game's process to appear. If it never does (Steam fails
            // to start it, a bad InstallDirectory, offline mode, ...), don't hang
            // forever: give up after DirectoryLaunchTimeout so IsRunning clears and
            // no phantom playtime gets recorded.
            while (!await HasProcessInDirectoryAsync(game.InstallDirectory, executableNames))
            {
                if (stopwatch.Elapsed >= DirectoryLaunchTimeout)
                {
                    return;
                }

                await Task.Delay(frequencyMs);
            }

            launched = true;
            sessionStart = stopwatch.Elapsed;

            // Grace period for launcher transitions: some games (Genshin) launch a
            // non-elevated launcher that asks for admin rights (UAC) and then
            // relaunches elevated, or exit after spawning the real game — leaving a
            // gap of a few seconds with no process under the install directory. If
            // we ended the session the moment the processes vanished, the session
            // would last only as long as the user took to approve the UAC prompt.
            // Instead, keep polling: only end once the directory stays empty for
            // DirectoryIdleTimeout in a row.
            var idleSince = TimeSpan.MaxValue;

            while (true)
            {
                if (await HasProcessInDirectoryAsync(game.InstallDirectory, executableNames))
                {
                    idleSince = TimeSpan.MaxValue;
                }
                else if (idleSince == TimeSpan.MaxValue)
                {
                    idleSince = stopwatch.Elapsed;
                }
                else if (stopwatch.Elapsed - idleSince >= DirectoryIdleTimeout)
                {
                    break;
                }

                await Task.Delay(frequencyMs);
            }
        }
        catch
        {
            // Swallow — this is a fire-and-forget task; the session is still
            // finalized in `finally`. Nothing here should throw in practice
            // (HasProcessInDirectoryAsync catches internally), this is purely
            // defensive against an unobserved-task exception.
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

    // How long the install directory must stay empty before an already-launched
    // session ends. Covers launcher transitions with a gap (Genshin's UAC
    // relaunch, a launcher exiting before the game's process appears).
    private static readonly TimeSpan DirectoryIdleTimeout = TimeSpan.FromSeconds(10);

    // Process-tree tracking for the launcher-spawns-child-and-exits case (Genshin's
    // launcher.exe, Epic/GOG frontends). Mirrors Playnite's MonitorProcessTree
    // (§28.10): start with the launched PID, then every poll expand the tree to
    // include any process whose parent is already in it, and prune to the ones
    // still alive. The launcher may exit after spawning the game — the game stays
    // in the tree as a descendant, so the session survives until the game itself
    // closes. Gives up after DirectoryLaunchTimeout if nothing ever appears.
    private async Task TrackProcessTreeAsync(Game game, int launchedPid, int frequencyMs)
    {
        var stopwatch = Stopwatch.StartNew();
        var tree = new HashSet<int> { launchedPid };
        bool launched = false;

        try
        {
            if (!await IsProcessTreeRunningAsync(tree))
            {
                if (stopwatch.Elapsed >= DirectoryLaunchTimeout)
                {
                    return;
                }
            }

            while (await IsProcessTreeRunningAsync(tree))
            {
                // Wait for at least one poll where the tree is alive, then keep
                // tracking until it's gone.
                if (!launched)
                {
                    launched = true;
                    stopwatch.Restart();
                }

                await Task.Delay(frequencyMs);
            }
        }
        catch
        {
            // Swallow — this is a fire-and-forget task; the session is still
            // finalized in `finally`. Nothing here should throw in practice
            // (the snapshot/expansion catch internally), this is purely
            // defensive against an unobserved-task exception.
        }
        finally
        {
            stopwatch.Stop();
            game.IsRunning = false;

            if (launched)
            {
                var sessionSeconds = (ulong)stopwatch.Elapsed.TotalSeconds;
                game.PlaytimeSeconds += sessionSeconds;
                GameStopped?.Invoke(game, sessionSeconds);
            }
        }
    }

    private static async Task<bool> IsProcessTreeRunningAsync(HashSet<int> tree)
    {
        // Process enumeration takes 100-300ms — offload to a pool thread like
        // HasProcessInDirectoryAsync; the continuation stays on the UI thread.
        var snapshot = await Task.Run(() => ProcessTreeSnapshot.Collect());
        var next = ProcessTreeExpander.ExpandAndPrune(tree, snapshot);
        tree.Clear();
        tree.UnionWith(next);
        return tree.Count > 0;
    }

    // Enumerating every running process can take 100-300ms on a loaded system.
    // The tracking loops run on the UI thread (see the note above TrackAsync),
    // so the enumeration itself must not — offload it to a pool thread and keep
    // only the (cheap) continuation on the UI thread.
    private static Task<bool> HasProcessInDirectoryAsync(string installDirectory, IReadOnlySet<string> executableNames) =>
        Task.Run(() => HasProcessInDirectory(installDirectory, executableNames));

    // Executable file names (without .exe) under the install directory, used to
    // match running processes whose MainModule is unreadable (elevated/protected
    // launchers like Genshin's HYP.exe). Recursive so the real game's exe in a
    // nested games\ folder is included too.
    private static HashSet<string> GetExecutableNames(string installDirectory)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            return names;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(installDirectory, "*.exe", SearchOption.AllDirectories))
            {
                names.Add(Path.GetFileNameWithoutExtension(file));
            }
        }
        catch
        {
            // Unreadable subfolder (permissions) — the path check alone still works
            // for the processes whose MainModule is readable.
        }

        return names;
    }

    private static bool HasProcessInDirectory(string installDirectory, IReadOnlySet<string> executableNames)
    {
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            return false;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            // Process enumeration itself failed (rare) — treat as "not running".
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                // MainModule.FileName THROWS (Win32 access denied) for elevated or
                // protected processes (Genshin's HYP.exe) instead of returning null,
                // so it must be read in its own try/catch — otherwise the catch below
                // swallows it and the name fallback never runs.
                string? path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch
                {
                    // Elevated/protected process — path unreadable, fall back to name.
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    if (path.StartsWith(installDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        // Path-prefix boundary check: "C:\Games\Steam2\game.exe" must not
                        // match an install dir of "C:\Games\Steam".
                        if (path.Length == installDirectory.Length ||
                            path[installDirectory.Length] is ('\\' or '/'))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    // Path unreadable (elevated launcher like Genshin's HYP.exe) — the
                    // process name alone is still readable. Match it against the
                    // executables under the install directory.
                    try
                    {
                        if (executableNames.Contains(process.ProcessName))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // Process exited between enumeration and name read — skip it.
                    }
                }
            }

            return false;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
