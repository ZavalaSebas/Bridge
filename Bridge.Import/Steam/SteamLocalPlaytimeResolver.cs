namespace Bridge.Import.Steam;

/// <summary>
/// Reads playtime from userdata\{steamid}\config\localconfig.vdf
/// (Playtime in minutes, LastPlayed as unix seconds). Merges multiple accounts
/// on one install; returns null if Steam or localconfig.vdf is missing.
/// </summary>
public static class SteamLocalPlaytimeResolver
{
    /// <summary>Minutes → the seconds Bridge stores.</summary>
    private const int MinutesToSeconds = 60;

    /// <summary>
    /// Maps appid → Steam playtime recorded in the local config. Multiple
    /// userdata accounts on the same install are merged: the largest playtime
    /// and the most recent LastPlayed win per game (the local config is
    /// per-account, and any of them could own the game).
    /// </summary>
    public static Dictionary<string, SteamLocalPlaytime>? GetPlaytimes(string? steamInstallPath = null)
    {
        steamInstallPath ??= SteamPaths.GetInstallationPath();
        if (string.IsNullOrWhiteSpace(steamInstallPath))
            return null;

        var userDataDir = Path.Combine(steamInstallPath, "userdata");
        if (!Directory.Exists(userDataDir))
            return null;

        Dictionary<string, SteamLocalPlaytime>? result = null;

        foreach (var accountDir in Directory.GetDirectories(userDataDir))
        {
            var configPath = Path.Combine(accountDir, "config", "localconfig.vdf");
            if (!File.Exists(configPath))
                continue;

            Dictionary<string, object> root;
            try
            {
                root = VdfParser.Parse(File.ReadAllText(configPath));
            }
            catch
            {
                // A malformed/partial localconfig.vdf (Steam writing it) is
                // skipped rather than aborting the whole import.
                continue;
            }

            if (!TryGetApps(root, out var apps))
                continue;

            foreach (var (appId, appState) in apps)
            {
                if (!appState.TryGetValue("Playtime", out var minutesObj) ||
                    minutesObj is not string minutesStr ||
                    !ulong.TryParse(minutesStr, out var minutes) ||
                    minutes == 0)
                {
                    continue;
                }

                var lastActivity = TryReadLastActivity(appState);
                result ??= new Dictionary<string, SteamLocalPlaytime>(StringComparer.Ordinal);

                if (result.TryGetValue(appId, out var existing))
                {
                    var merged = new SteamLocalPlaytime(
                        Math.Max(existing.PlaytimeSeconds, minutes * MinutesToSeconds),
                        lastActivity > existing.LastActivity ? lastActivity : existing.LastActivity);
                    result[appId] = merged;
                }
                else
                {
                    result[appId] = new SteamLocalPlaytime(minutes * MinutesToSeconds, lastActivity);
                }
            }
        }

        return result;
    }

    // UserLocalConfigStore > Software > Valve > Steam > apps — the per-appid
    // block holding Playtime/LastPlayed. Keys are matched case-insensitively by
    // VdfParser itself.
    private static bool TryGetApps(
        Dictionary<string, object> root,
        out Dictionary<string, Dictionary<string, object>> apps)
    {
        apps = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        if (!root.TryGetValue("UserLocalConfigStore", out var storeObj) ||
            storeObj is not Dictionary<string, object> store ||
            !store.TryGetValue("Software", out var softwareObj) ||
            softwareObj is not Dictionary<string, object> software ||
            !software.TryGetValue("Valve", out var valveObj) ||
            valveObj is not Dictionary<string, object> valve ||
            !valve.TryGetValue("Steam", out var steamObj) ||
            steamObj is not Dictionary<string, object> steam ||
            !steam.TryGetValue("apps", out var appsObj) ||
            appsObj is not Dictionary<string, object> rawApps)
        {
            return false;
        }

        foreach (var (appId, entry) in rawApps)
        {
            if (entry is Dictionary<string, object> appState)
            {
                apps[appId] = appState;
            }
        }

        return apps.Count > 0;
    }

    // "LastPlayed" is a unix timestamp in seconds; 0 or missing means "never
    // played recently enough for Steam to record it".
    private static DateTime? TryReadLastActivity(Dictionary<string, object> appState)
    {
        if (!appState.TryGetValue("LastPlayed", out var playedObj) ||
            playedObj is not string playedStr ||
            !long.TryParse(playedStr, out var unixSeconds) ||
            unixSeconds <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

/// <summary>A game's Steam-recorded playtime, in the units Bridge stores.</summary>
public readonly record struct SteamLocalPlaytime(ulong PlaytimeSeconds, DateTime? LastActivity);
