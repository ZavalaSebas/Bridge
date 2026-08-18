using System.Diagnostics;
using System.IO;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Core.Utilities;
using Bridge.Import.Steam;

namespace Bridge.Services;

/// <summary>
/// Launches play actions and tracks sessions by polling. Steam games without a
/// configured action get a runtime steam:// launch. Not supported yet: Script actions,
/// general Url actions, and emulator tokens beyond {RomPath}/{CorePath}.
/// </summary>
public class GameLauncher(IRepository<Emulator> emulatorRepository)
{
    public event Action<Game>? GameStarted;
    public event Action<Game, ulong>? GameStopped;

    // Tracks the launched PID (tree mode) or install directory (directory mode)
    // per game so Stop() knows what to kill. Guarded by a lock — the tracking
    // tasks run on the UI thread, but Stop() can be called from a command while
    // a task is mid-loop.
    private readonly object _activeLock = new();
    private readonly Dictionary<Guid, ActiveTracking> _active = [];

    private sealed class ActiveTracking
    {
        public int LaunchedPid = -1;
        public string InstallDirectory = string.Empty;
        public CancellationTokenSource Cancellation = new();
    }

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

        if (!TryStartTracking(game, action, process))
        {
            return;
        }

        game.IsRunning = true;
        game.LastActivity = DateTime.Now;
        game.PlayCount++;
        GameStarted?.Invoke(game);
    }

    // Sets up the polling loop for this launch. Returns false when tracking
    // cannot start (e.g. PID unavailable and no install directory to fall back
    // to) so PlayCount/IsRunning are never incremented for a failed launch.
    private bool TryStartTracking(Game game, GameAction action, Process process)
    {
        switch (action.TrackingMode)
        {
            case TrackingMode.Directory:
                ActiveTracking directoryTracking;
                lock (_activeLock)
                {
                    directoryTracking = new ActiveTracking { InstallDirectory = game.InstallDirectory };
                    _active[game.Id] = directoryTracking;
                }
                TrackDirectoryAsync(game, directoryTracking.Cancellation.Token, action.InitialTrackingDelayMs, action.TrackingFrequencyMs)
                    .FireAndForget("GameLauncher.TrackDirectory");
                return true;

            // Default: process tree for File/Emulator, install directory for Url.
            case TrackingMode.Process:
            case TrackingMode.Default when action.Type is GameActionType.File or GameActionType.Emulator:
                try
                {
                    var pid = process.Id;
                    process.Dispose();
                    ActiveTracking treeTracking;
                    lock (_activeLock)
                    {
                        treeTracking = new ActiveTracking { LaunchedPid = pid };
                        _active[game.Id] = treeTracking;
                    }
                    TrackProcessTreeAsync(game, pid, treeTracking.Cancellation.Token, action.TrackingFrequencyMs)
                        .FireAndForget("GameLauncher.TrackProcessTree");
                    return true;
                }
                catch
                {
                    process.Dispose();
                    // PID unavailable — fall back to directory tracking when possible
                    // instead of passing a disposed Process handle to TrackAsync.
                    if (!string.IsNullOrWhiteSpace(game.InstallDirectory))
                    {
                        ActiveTracking fallbackTracking;
                        lock (_activeLock)
                        {
                            fallbackTracking = new ActiveTracking { InstallDirectory = game.InstallDirectory };
                            _active[game.Id] = fallbackTracking;
                        }

                        TrackDirectoryAsync(game, fallbackTracking.Cancellation.Token, action.InitialTrackingDelayMs, action.TrackingFrequencyMs)
                            .FireAndForget("GameLauncher.TrackDirectoryFallback");
                        return true;
                    }

                    return false;
                }

            default:
                ActiveTracking exactTracking;
                lock (_activeLock)
                {
                    exactTracking = new ActiveTracking { LaunchedPid = GetProcessId(process) };
                    _active[game.Id] = exactTracking;
                }
                TrackAsync(game, process, exactTracking.Cancellation.Token, action.TrackingFrequencyMs)
                    .FireAndForget("GameLauncher.TrackExact");
                return true;
        }
    }

    private static int GetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return -1;
        }
    }

    private void UnregisterActive(Game game)
    {
        lock (_activeLock)
        {
            if (_active.Remove(game.Id, out var tracking))
            {
                tracking.Cancellation.Dispose();
            }
        }
    }

    // Kills the processes Bridge launched for this game and forces the tracking
    // loop to finalize the session immediately (GameStopped → IsRunning=false),
    // so the Stop button always reverts to Play even if the process can't be
    // killed (elevated launcher). Directory mode kills everything under the
    // install directory (the launched process is a launcher, not the game);
    // tree/exact mode kills the launched PID's process tree.
    public void Stop(Game game)
    {
        ActiveTracking tracking;
        lock (_activeLock)
        {
            if (!_active.TryGetValue(game.Id, out tracking!))
            {
                return;
            }
        }

        // Cancel first: the tracking loop's Delay throws immediately, the session
        // finalizes in its finally (GameStopped → IsRunning=false) and the button
        // reverts to Play regardless of whether the kill below succeeds.
        tracking.Cancellation.Cancel();

        bool killedAny;
        if (!string.IsNullOrWhiteSpace(tracking.InstallDirectory))
        {
            // Same name fallback the tracker uses: Genshin's launcher (HYP.exe)
            // runs elevated, so MainModule.FileName is unreadable and path-only
            // matching would miss it — the launcher must be killed by name too.
            var names = GetExecutableNames(tracking.InstallDirectory);
            killedAny = KillProcessesInDirectory(tracking.InstallDirectory, names);
        }
        else if (tracking.LaunchedPid > 0)
        {
            killedAny = KillProcessTree(tracking.LaunchedPid);
        }
        else
        {
            killedAny = false;
        }

        // The game's process may not have spawned yet (a launcher that takes a
        // few seconds, or a quick Stop right after Play). Keep watching for a
        // short window and kill it the moment it appears — otherwise the game
        // opens right after Stop and runs untracked.
        if (!killedAny)
        {
            KillWhenAppearsAsync(tracking).FireAndForget("GameLauncher.KillWhenAppears");
        }
    }

    // Watches the tracking target (install directory or launched PID) for a
    // short grace window, killing the game's processes as soon as they appear.
    // Fire-and-forget: it runs after Stop has already reverted the button, so it
    // never touches the UI — pure process management.
    private static async Task KillWhenAppearsAsync(ActiveTracking tracking)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < StopKillGrace)
        {
            bool killed;
            if (!string.IsNullOrWhiteSpace(tracking.InstallDirectory))
            {
                var names = GetExecutableNames(tracking.InstallDirectory);
                killed = KillProcessesInDirectory(tracking.InstallDirectory, names);
            }
            else if (tracking.LaunchedPid > 0)
            {
                killed = KillProcessTree(tracking.LaunchedPid);
            }
            else
            {
                return;
            }

            if (killed)
            {
                return;
            }

            await Task.Delay(500);
        }
    }

    private static readonly TimeSpan StopKillGrace = TimeSpan.FromSeconds(15);

    private static bool KillProcessTree(int pid)
    {
        // The launched PID may already be gone (a launcher that spawned the game
        // and exited), so expand the tree from the snapshot and kill every live
        // member — the game itself is a descendant. Returns true if anything
        // was actually killed.
        var snapshot = ProcessTreeSnapshot.Collect();
        var tree = new HashSet<int> { pid };
        bool changed;
        do
        {
            changed = false;
            foreach (var entry in snapshot)
            {
                if (tree.Contains(entry.ParentPid) && tree.Add(entry.Pid))
                {
                    changed = true;
                }
            }
        } while (changed);

        var alive = new HashSet<int>(snapshot.Select(e => e.Pid));
        bool killedAny = false;
        foreach (var candidate in tree)
        {
            if (!alive.Contains(candidate))
            {
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(candidate);
                process.Kill(entireProcessTree: true);
                killedAny = true;
            }
            catch
            {
                // Process already exited, or access denied (elevated process) —
                // try the next one; the tracking loop finalizes the session either way.
            }
        }

        return killedAny;
    }

    private static bool KillProcessesInDirectory(string installDirectory, IReadOnlySet<string> executableNames)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return false;
        }

        bool killedAny = false;
        try
        {
            foreach (var process in processes)
            {
                bool matches = false;

                string? path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch
                {
                    // Elevated/protected — path unreadable; fall back to name below.
                }

                if (!string.IsNullOrWhiteSpace(path) &&
                    PathContainment.IsPathUnderDirectory(path, installDirectory))
                {
                    matches = true;
                }
                else if (executableNames.Contains(process.ProcessName))
                {
                    // Name fallback for elevated launchers (Genshin's HYP.exe).
                    matches = true;
                }

                if (!matches)
                {
                    continue;
                }

                try
                {
                    process.Kill(entireProcessTree: true);
                    killedAny = true;
                }
                catch
                {
                    // Already exited or access denied — ignore.
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        return killedAny;
    }

    private static GameAction? TryResolveAutomaticAction(Game game)
    {
        // Steam games with no stored action get a runtime steam:// play action.
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

    // Prefer steam.exe -silent "steam://..."; fall back to ShellExecute on the URL.
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

        if (!UrlValidator.IsSafeToOpen(action.Path))
        {
            throw new InvalidOperationException($"Refusing to open unsafe URL: {action.Path}");
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
        if (!File.Exists(romPath))
        {
            throw new InvalidOperationException($"ROM file not found: {romPath}");
        }

        var executable = Path.IsPathRooted(profile.Executable)
            ? profile.Executable
            : Path.Combine(emulator.InstallDirectory, profile.Executable);
        var arguments = profile.Arguments
            .Replace("{RomPath}", $"\"{romPath}\"")
            .Replace("{CorePath}", $"\"{profile.CorePath}\"");
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
    private async Task TrackAsync(Game game, Process process, CancellationToken token, int frequencyMs)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            while (!process.HasExited)
            {
                await Task.Delay(frequencyMs, token);
            }
        }
        catch
        {
            // HasExited can throw when the process was started with UseShellExecute
            // (no usable handle) or if it exited/disposed between checks; the token
            // throws when Stop() cancels. Either way, record whatever elapsed rather
            // than a phantom 0-second session. This is the fire-and-forget task —
            // swallowing is what prevents an unobserved-task exception.
            var sessionSeconds = (ulong)stopwatch.Elapsed.TotalSeconds;
            game.IsRunning = false;
            game.PlaytimeSeconds += sessionSeconds;
            UnregisterActive(game);
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
        UnregisterActive(game);
        GameStopped?.Invoke(game, elapsed);
    }

    // Watch processes under InstallDirectory — steam.exe is not the game itself.
    private async Task TrackDirectoryAsync(Game game, CancellationToken token, int initialDelayMs, int frequencyMs)
    {
        var stopwatch = Stopwatch.StartNew();
        var sessionStart = stopwatch.Elapsed;
        bool launched = false;

        try
        {
            if (initialDelayMs > 0)
            {
                await Task.Delay(initialDelayMs, token);
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

                await Task.Delay(frequencyMs, token);
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
            // the idle timeout in a row. The timeout is longer during the launch
            // window (a gap right after Play is the launcher still spawning the
            // game — slow when the app just started) and shortens after, so the
            // close is still detected quickly once the game has been running.
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
                else if (stopwatch.Elapsed - idleSince >= IdleTimeoutFor(stopwatch.Elapsed - sessionStart))
                {
                    break;
                }

                await Task.Delay(frequencyMs, token);
            }
        }
        catch
        {
            // Swallow — this is a fire-and-forget task; the session is still
            // finalized in `finally`. Nothing here should throw in practice
            // (HasProcessInDirectoryAsync catches internally; a Stop cancellation
            // is exactly the token's TaskCanceledException), this is purely
            // defensive against an unobserved-task exception.
        }
        finally
        {
            stopwatch.Stop();
            game.IsRunning = false;
            UnregisterActive(game);

            if (launched)
            {
                var sessionSeconds = (ulong)(stopwatch.Elapsed - sessionStart).TotalSeconds;
                game.PlaytimeSeconds += sessionSeconds;
                GameStopped?.Invoke(game, sessionSeconds);
            }
        }
    }

    private static readonly TimeSpan DirectoryLaunchTimeout = TimeSpan.FromMinutes(5);

    // How long the install directory / process tree must stay empty before an
    // already-launched session ends. Covers launcher transitions with a gap
    // (Genshin's UAC relaunch, a launcher exiting before the game's process
    // appears). The timeout is longer during the launch window (the gap right
    // after Play is the launcher still spawning the game — slow right after the
    // app starts) and shortens once the session has been running, so the close
    // is still detected quickly.
    private static readonly TimeSpan DirectoryIdleTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DirectoryLaunchIdleTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DirectoryLaunchIdleWindow = TimeSpan.FromSeconds(30);

    private static TimeSpan IdleTimeoutFor(TimeSpan sessionAge) =>
        sessionAge < DirectoryLaunchIdleWindow ? DirectoryLaunchIdleTimeout : DirectoryIdleTimeout;

    // Expand the tree each poll so launcher-spawned games stay tracked after the launcher exits.
    private async Task TrackProcessTreeAsync(Game game, int launchedPid, CancellationToken token, int frequencyMs)
    {
        var stopwatch = Stopwatch.StartNew();
        var tree = new HashSet<int> { launchedPid };
        bool launched = false;
        var sessionStart = stopwatch.Elapsed;

        try
        {
            // Wait for the game's process to appear. The launched PID can take a
            // moment to spawn (or is a launcher that spawns the real game), so this
            // must be a poll loop — a single check would see "not running yet" and
            // end the session before the game ever appears. Gives up after
            // DirectoryLaunchTimeout like the directory tracker.
            while (!await IsProcessTreeRunningAsync(tree))
            {
                if (stopwatch.Elapsed >= DirectoryLaunchTimeout)
                {
                    return;
                }

                await Task.Delay(frequencyMs, token);
            }

            launched = true;
            sessionStart = stopwatch.Elapsed;

            // Same graceful idle as the directory tracker: a gap in the tree right
            // after Play is the launcher still spawning the game (or the snapshot
            // missing a just-spawned process), not a close. Only end once the tree
            // stays empty for the (launch-aware) idle timeout.
            var idleSince = TimeSpan.MaxValue;
            while (true)
            {
                if (await IsProcessTreeRunningAsync(tree))
                {
                    idleSince = TimeSpan.MaxValue;
                }
                else if (idleSince == TimeSpan.MaxValue)
                {
                    idleSince = stopwatch.Elapsed;
                }
                else if (stopwatch.Elapsed - idleSince >= IdleTimeoutFor(stopwatch.Elapsed - sessionStart))
                {
                    break;
                }

                await Task.Delay(frequencyMs, token);
            }
        }
        catch
        {
            // Swallow — this is a fire-and-forget task; the session is still
            // finalized in `finally`. Nothing here should throw in practice
            // (the snapshot/expansion catch internally; a Stop cancellation is
            // exactly the token's TaskCanceledException), this is purely
            // defensive against an unobserved-task exception.
        }
        finally
        {
            stopwatch.Stop();
            game.IsRunning = false;
            UnregisterActive(game);

            if (launched)
            {
                var sessionSeconds = (ulong)(stopwatch.Elapsed - sessionStart).TotalSeconds;
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

                if (!string.IsNullOrWhiteSpace(path) &&
                    PathContainment.IsPathUnderDirectory(path, installDirectory))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    // Path is readable but outside the install folder — do not
                    // fall back to process name (would match unrelated copies of
                    // the same exe name elsewhere on the machine).
                    continue;
                }

                // Path unreadable (elevated launcher like Genshin's HYP.exe) —
                // the process name alone is still readable. Match it against the
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
