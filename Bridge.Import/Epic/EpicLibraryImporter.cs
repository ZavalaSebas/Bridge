using System.IO;
using System.Text.Json;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Core.Import;
using Bridge.Core.Utilities;

namespace Bridge.Import.Epic;

/// <summary>
/// Detects locally installed Epic games from LauncherInstalled.dat and per-game
/// .item manifests. Skips Unreal Engine, DLC, and engine plugins. Play action
/// uses com.epicgames.launcher:// and directory tracking like Steam.
/// </summary>
public class EpicLibraryImporter
{
    private readonly string _installedAppListPath;
    private readonly string _manifestsDirectory;

    public EpicLibraryImporter() : this(EpicPaths.InstalledAppListPath, EpicPaths.ManifestsDirectory)
    {
    }

    // Internal for tests: point at a temp tree instead of the real
    // %PROGRAMDATA%\Epic folders.
    internal EpicLibraryImporter(string installedAppListPath, string manifestsDirectory)
    {
        _installedAppListPath = installedAppListPath;
        _manifestsDirectory = manifestsDirectory;
    }

    public List<GameMetadata> GetInstalledGames()
    {
        var appList = ReadInstalledAppList(_installedAppListPath);
        var manifests = ReadManifests(_manifestsDirectory);
        var installedByAppName = appList
            .Where(app => !string.IsNullOrWhiteSpace(app.AppName))
            .GroupBy(app => app.AppName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var games = new List<GameMetadata>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in manifests)
        {
            var appName = manifest.AppName;
            if (string.IsNullOrWhiteSpace(appName) || appName.StartsWith("UE_"))
            {
                continue;
            }

            if (ShouldSkipManifest(manifest))
            {
                continue;
            }

            installedByAppName.TryGetValue(appName, out var installedApp);
            var installLocation = ResolveInstallLocation(installedApp, manifest);
            if (installLocation is null || !seen.Add(appName))
            {
                continue;
            }

            games.Add(BuildGameMetadata(appName, manifest, installLocation));
        }

        return games;
    }

    private static bool ShouldSkipManifest(EpicManifest manifest)
    {
        if (manifest.IsIncompleteInstall)
        {
            return true;
        }

        // Skip non-launchable DLC add-ons.
        if (manifest.AppCategories?.Contains("addons") == true &&
            manifest.AppCategories?.Any(a => a == "addons/launchable") == false)
        {
            return true;
        }

        // Unreal Engine plugins / engine bits.
        if (manifest.AppCategories?.Any(a => a is "plugins" or "plugins/engine") == true ||
            manifest.CompatibleApps?.Any(a => a.StartsWith("UE_")) == true ||
            manifest.TechnicalType?.Contains("plugins/engine") == true)
        {
            return true;
        }

        return false;
    }

    private static string? ResolveInstallLocation(InstalledApp? installedApp, EpicManifest manifest)
    {
        foreach (var candidate in new[] { installedApp?.InstallLocation, manifest.InstallLocation })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                var normalized = Path.GetFullPath(candidate.TrimEnd('\\', '/'));
                if (Directory.Exists(normalized))
                {
                    return normalized;
                }
            }
            catch (Exception)
            {
                // Malformed/too-long/invalid path — skip this candidate.
            }
        }

        return null;
    }

    private static GameMetadata BuildGameMetadata(string appName, EpicManifest manifest, string installLocation)
    {
        var gameName = manifest.DisplayName ?? Path.GetFileName(installLocation);
        if (string.IsNullOrWhiteSpace(gameName))
        {
            gameName = appName;
        }

        var metadata = new GameMetadata
        {
            ExternalId = appName,
            Name = RemoveTrademarks(gameName),
            InstallDirectory = installLocation,
            IsInstalled = true
        };

        // Epic has no server icon — use the launch exe path; ExeIconLoader renders it.
        if (!string.IsNullOrWhiteSpace(manifest.LaunchExecutable))
        {
            var exePath = PathContainment.TryResolveUnderRoot(installLocation, manifest.LaunchExecutable);
            if (exePath is not null && File.Exists(exePath))
            {
                metadata.Icon = exePath;
            }
        }

        // Track by directory like the Steam action: the launched process is
        // the Epic client, not the game, so watch for processes under the
        // game's install directory.
        metadata.GameActions.Add(new GameAction
        {
            Name = "Play via Epic",
            Type = GameActionType.Url,
            IsPlayAction = true,
            Path = $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true",
            TrackingMode = TrackingMode.Directory
        });

        return metadata;
    }

    private static List<InstalledApp> ReadInstalledAppList(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var data = JsonSerializer.Deserialize<LauncherInstalledData>(File.ReadAllText(path));
            return data?.InstallationList ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<EpicManifest> ReadManifests(string directory)
    {
        var manifests = new List<EpicManifest>();
        if (!Directory.Exists(directory))
        {
            return manifests;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.item"))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<EpicManifest>(File.ReadAllText(file));
                if (manifest is not null)
                {
                    manifests.Add(manifest);
                }
            }
            catch (JsonException)
            {
                // Epic can leave a manifest half-written (or the user hand-edited
                // it while moving a game between drives) — skip that one file.
            }
            catch (IOException)
            {
                // Locked/half-written/removed manifest — skip this file.
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to read this manifest — skip this file.
            }
        }

        return manifests;
    }

    private static string RemoveTrademarks(string name)
        => name.Replace("\u2122", string.Empty) // ™
            .Replace("\u00A9", string.Empty)    // ©
            .Replace("\u00AE", string.Empty)    // ®
            .Trim();
}
