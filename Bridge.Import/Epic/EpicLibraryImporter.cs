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
        var games = new List<GameMetadata>();
        if (!File.Exists(_installedAppListPath))
        {
            // No launcher/install data is a normal condition (the import is
            // optional) — return empty rather than throw.
            return games;
        }

        var appList = ReadInstalledAppList(_installedAppListPath);
        var manifests = ReadManifests(_manifestsDirectory);

        foreach (var app in appList)
        {
            var appName = app.AppName;
            if (string.IsNullOrWhiteSpace(appName) || appName.StartsWith("UE_"))
            {
                continue;
            }

            var manifest = manifests.FirstOrDefault(m => m.AppName == appName);
            if (manifest is null)
            {
                continue;
            }

            // Skip non-launchable DLC add-ons.
            if (manifest.AppCategories?.Contains("addons") == true &&
                manifest.AppCategories?.Any(a => a == "addons/launchable") == false)
            {
                continue;
            }

            // Unreal Engine plugins / engine bits.
            if (manifest.AppCategories?.Any(a => a is "plugins" or "plugins/engine") == true ||
                manifest.CompatibleApps?.Any(a => a.StartsWith("UE_")) == true ||
                manifest.TechnicalType?.Contains("plugins/engine") == true)
            {
                continue;
            }

            var gameName = manifest.DisplayName ?? Path.GetFileName(app.InstallLocation?.TrimEnd('\\', '/') ?? string.Empty);
            if (string.IsNullOrWhiteSpace(gameName))
            {
                continue;
            }

            // The app list tends to have the correct location; the manifest can
            // be stale if the install was moved.
            var installLocation = app.InstallLocation;
            if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
            {
                installLocation = manifest.InstallLocation;
            }

            if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
            {
                continue;
            }

            try
            {
                installLocation = Path.GetFullPath(installLocation.TrimEnd('\\', '/'));
            }
            catch
            {
                continue;
            }

            if (!Directory.Exists(installLocation))
            {
                continue;
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

            games.Add(metadata);
        }

        return games;
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
            }
            catch (UnauthorizedAccessException)
            {
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
