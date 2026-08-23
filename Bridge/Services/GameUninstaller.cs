using Bridge.Core.Utilities;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Bridge.Core.Entities;
using Microsoft.Win32;

namespace Bridge.Services;

/// <summary>
/// Runs the platform uninstall flow per source (Steam URI, Epic library page, registry entry).
/// Waits until the game is no longer installed before returning.
/// </summary>
public sealed class GameUninstaller
{
    public static string? Resolve(Game game, string? sourceName)
    {
        if (sourceName == "Steam" && uint.TryParse(game.ExternalId, out _))
            return $"steam://uninstall/{game.ExternalId}";

        // Epic: no uninstall action exists — open the library and let the user
        // trigger it from the client (the watcher in RunAsync detects completion).
        if (sourceName == "Epic" && !string.IsNullOrWhiteSpace(game.ExternalId))
            return "com.epicgames.launcher://store/library";

        return ResolveFromRegistry(game);
    }

    // Waits up to ~2 minutes for the game to leave. Epic games are detected via
    // LauncherInstalled.dat (the install folder may linger); everything else via
    // the install folder disappearing. Returns true once it's gone, or false on
    // timeout / failed launch. A game without a tracked folder (or a folder that
    // never existed) reports done immediately — the uninstaller was launched, and
    // there's nothing left to wait on.
    public static async Task<bool> RunAsync(string command, Game game, string? sourceName)
    {
        var started = UrlValidator.IsSafeToOpen(command)
            ? SafeLauncher.TryOpenUrl(command)
            : SafeLauncher.TryRunUninstallCommand(command);
        if (!started)
            return false;

        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(2000);
            if (IsGameGone(game, sourceName))
                return true;
        }

        return false;
    }

    private static bool IsGameGone(Game game, string? sourceName)
    {
        // Epic: the launcher drops the game from LauncherInstalled.dat on
        // uninstall. Prefer that over the folder check — Epic sometimes leaves
        // empty folders behind after uninstalling.
        if (sourceName == "Epic" && !string.IsNullOrWhiteSpace(game.ExternalId))
            return !IsEpicAppInstalled(game.ExternalId);

        return string.IsNullOrWhiteSpace(game.InstallDirectory)
               || !Directory.Exists(game.InstallDirectory);
    }

    private static bool IsEpicAppInstalled(string appName)
    {
        var path = Path.Combine(
            Environment.ExpandEnvironmentVariables("%PROGRAMDATA%"),
            "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");
        if (!File.Exists(path))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("InstallationList", out var list) || list.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var item in list.EnumerateArray())
            {
                if (item.TryGetProperty("AppName", out var name) &&
                    name.ValueKind == JsonValueKind.String &&
                    name.GetString() == appName)
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            // Can't read Epic's installed list (missing/locked/garbled) — treat app as not present.
            return false;
        }
        catch (IOException)
        {
            // Can't read Epic's installed list (missing/locked/garbled) — treat app as not present.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Can't read Epic's installed list (missing/locked/garbled) — treat app as not present.
            return false;
        }
    }

    private static string? ResolveFromRegistry(Game game)
    {
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null)
                continue;

            foreach (var subKeyName in uninstall.GetSubKeyNames())
            {
                using var key = uninstall.OpenSubKey(subKeyName);
                var uninstallString = key?.GetValue("UninstallString") as string;
                if (string.IsNullOrWhiteSpace(uninstallString))
                    continue;

                var displayName = key?.GetValue("DisplayName") as string;
                var nameMatch = !string.IsNullOrWhiteSpace(displayName)
                    && displayName.Trim().Equals(game.Name.Trim(), StringComparison.OrdinalIgnoreCase);

                var installLocation = key?.GetValue("InstallLocation") as string;
                var dirMatch = !string.IsNullOrWhiteSpace(game.InstallDirectory)
                    && !string.IsNullOrWhiteSpace(installLocation)
                    && installLocation.TrimEnd('\\')
                        .Equals(game.InstallDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

                if (nameMatch || dirMatch)
                    return uninstallString;
            }
        }

        return null;
    }
}
