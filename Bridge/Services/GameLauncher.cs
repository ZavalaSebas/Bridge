using System.Diagnostics;
using System.IO;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;

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
/// - Only GameActionType.File and GameActionType.Emulator are supported
///   (no Url/Script yet).
/// - Emulator argument substitution is a single literal "{RomPath}" token
///   replace, not Playnite's full ExpandVariables system (§28.9) — no
///   {InstallDir}/{PlayniteDir}/etc. tokens yet.
/// - Only the exact launched process is tracked (behaves like Playnite's
///   TrackingMode.OriginalProcess for every TrackingMode value). Process-tree
///   walking (Process mode) and Directory/ProcessName tracking are NOT
///   implemented — the "launcher spawns the real game and exits" case
///   (Steam/Epic-style launchers) will incorrectly report the game as
///   stopped as soon as the launcher process exits.
/// </summary>
public class GameLauncher(IRepository<Emulator> emulatorRepository)
{
    public event Action<Game>? GameStarted;
    public event Action<Game, ulong>? GameStopped;

    public void Launch(Game game)
    {
        var action = game.GameActions.FirstOrDefault(a => a.IsPlayAction)
            ?? game.GameActions.FirstOrDefault();

        if (action is null)
        {
            throw new InvalidOperationException($"'{game.Name}' has no play action configured.");
        }

        var process = action.Type switch
        {
            GameActionType.File => StartFileAction(action),
            GameActionType.Emulator => StartEmulatorAction(game, action),
            _ => throw new NotSupportedException($"Action type {action.Type} isn't supported yet.")
        };

        game.IsRunning = true;
        game.LastActivity = DateTime.Now;
        game.PlayCount++;
        GameStarted?.Invoke(game);

        _ = TrackAsync(game, process, action.TrackingFrequencyMs);
    }

    private static Process StartFileAction(GameAction action) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = action.Path,
            Arguments = action.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(action.WorkingDirectory) ? null : action.WorkingDirectory,
            UseShellExecute = true
        }) ?? throw new InvalidOperationException($"Failed to start process: {action.Path}");

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
}
