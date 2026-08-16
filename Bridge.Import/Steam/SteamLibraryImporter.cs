using Bridge.Core.Import;

namespace Bridge.Import.Steam;

/// <summary>
/// Detects locally-installed Steam games — no network call, no Steam Web
/// API key, purely local files. Mirrors Playnite's real SteamLocalService
/// flow exactly (PROJECT_FOUNDATION.md §28.26, verified against Playnite's
/// actual extension source and against real files on this machine, not just
/// the docs): registry → libraryfolders.vdf → appmanifest*.acf per library
/// → filter by the FullyInstalled state flag.
/// </summary>
public class SteamLibraryImporter
{
    // Bit value from Playnite's real AppStateFlags enum (§28.26) — 4 = FullyInstalled.
    private const int FullyInstalledFlag = 4;

    // Steamworks Common Redistributables — not a real game. Playnite's real importer skips this exact AppID too.
    private const string RedistributablesAppId = "228980";

    public List<GameMetadata> GetInstalledGames()
    {
        var installPath = SteamPaths.GetInstallationPath();
        if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
        {
            throw new InvalidOperationException("Steam installation not found (no HKCU\\Software\\Valve\\Steam registry key, or the path it points to doesn't exist).");
        }

        // Steam's locally-recorded playtime (userdata\*\config\localconfig.vdf)
        // — resolved once so every game below gets the same snapshot.
        var playtimes = SteamLocalPlaytimeResolver.GetPlaytimes(installPath);

        var games = new List<GameMetadata>();
        var seenAppIds = new HashSet<string>();

        foreach (var libraryFolder in GetLibraryFolders(installPath))
        {
            var steamAppsDir = Path.Combine(libraryFolder, "steamapps");
            if (!Directory.Exists(steamAppsDir))
            {
                continue;
            }

            foreach (var manifestFile in Directory.GetFiles(steamAppsDir, "appmanifest*.acf"))
            {
                var game = ParseManifest(manifestFile, steamAppsDir);
                if (game is not null && seenAppIds.Add(game.ExternalId))
                {
                    ApplyPlaytime(game, playtimes);
                    games.Add(game);
                }
            }
        }

        return games;
    }

    private static void ApplyPlaytime(GameMetadata game, Dictionary<string, SteamLocalPlaytime>? playtimes)
    {
        if (playtimes is null || !playtimes.TryGetValue(game.ExternalId, out var playtime))
        {
            return;
        }

        game.PlaytimeSeconds = playtime.PlaytimeSeconds;
        game.LastActivity = playtime.LastActivity;
    }

    internal static List<string> GetLibraryFolders(string steamInstallPath)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamInstallPath };

        var vdfPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath))
        {
            return folders.ToList();
        }

        var root = VdfParser.Parse(File.ReadAllText(vdfPath));
        if (root.TryGetValue("libraryfolders", out var lfObj) && lfObj is Dictionary<string, object> libraryFolders)
        {
            foreach (var entry in libraryFolders.Values)
            {
                if (entry is Dictionary<string, object> entryDict &&
                    entryDict.TryGetValue("path", out var pathObj) &&
                    pathObj is string path &&
                    Directory.Exists(path))
                {
                    folders.Add(path);
                }
            }
        }

        return folders.ToList();
    }

    internal static GameMetadata? ParseManifest(string manifestPath, string steamAppsDir)
    {        Dictionary<string, object> appState;
        try
        {
            var root = VdfParser.Parse(File.ReadAllText(manifestPath));
            if (!root.TryGetValue("AppState", out var appStateObj) || appStateObj is not Dictionary<string, object> parsed)
            {
                return null;
            }

            appState = parsed;
        }
        catch
        {
            // Steam can write a malformed .acf mid-update (Playnite's own comment on
            // this exact case, §28.26) — skip this one file, don't abort the whole scan.
            return null;
        }

        if (!appState.TryGetValue("StateFlags", out var stateFlagsObj) ||
            stateFlagsObj is not string stateFlagsStr ||
            !int.TryParse(stateFlagsStr, out var stateFlags) ||
            (stateFlags & FullyInstalledFlag) == 0)
        {
            return null;
        }

        if (!appState.TryGetValue("appid", out var appIdObj) || appIdObj is not string appId)
        {
            return null;
        }

        if (appId == RedistributablesAppId)
        {
            return null;
        }

        var name = appState.TryGetValue("name", out var nameObj) && nameObj is string n
            ? n.Trim()
            : $"Steam App {appId}";

        var installDirName = appState.TryGetValue("installdir", out var dirObj) && dirObj is string d ? d : string.Empty;
        var installDirectory = string.IsNullOrEmpty(installDirName)
            ? string.Empty
            : Path.Combine(steamAppsDir, "common", installDirName);

        // SizeOnDisk is the on-disk size in bytes that Steam writes to the
        // .acf — the same field Playnite's Steam plugin reads for Install Size.
        ulong? sizeOnDisk = null;
        if (appState.TryGetValue("SizeOnDisk", out var sizeObj) &&
            sizeObj is string sizeStr &&
            ulong.TryParse(sizeStr, out var parsedSize))
        {
            sizeOnDisk = parsedSize;
        }

        return new GameMetadata
        {
            ExternalId = appId,
            Name = name,
            InstallDirectory = Directory.Exists(installDirectory) ? installDirectory : string.Empty,
            InstallSizeBytes = sizeOnDisk,
            IsInstalled = true,
            // Deterministic store/community links — built from the appid with no
            // network call, so a fresh import shows the Links section filled in
            // immediately. The metadata provider adds the same links (plus
            // Achievements/Workshop when present) later; ApplyMetadata merges
            // them by URL, so this is just a head start, not a duplicate.
            Links = BuildDefaultLinks(appId)
        };
    }

    // The Steam links that depend only on the appid, matching the names the
    // SteamMetadataProvider uses (ApplyMetadata dedupes by URL). Public so the
    // app can seed existing games at load.
    public static List<Bridge.Core.Entities.Link> BuildDefaultLinks(string appId) =>
    [
        new() { Name = "Community Hub", Url = $"https://steamcommunity.com/app/{appId}" },
        new() { Name = "Discussions", Url = $"https://steamcommunity.com/app/{appId}/discussions/" },
        new() { Name = "Guides", Url = $"https://steamcommunity.com/app/{appId}/guides/" },
        new() { Name = "News", Url = $"https://store.steampowered.com/news/?appids={appId}" },
        new() { Name = "Steam Store", Url = $"https://store.steampowered.com/app/{appId}" },
        new() { Name = "PCGamingWiki", Url = $"https://pcgamingwiki.com/api/appid.php?appid={appId}" }
    ];
}
