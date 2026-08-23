using System.Globalization;

namespace Bridge.Import.Steam;

/// <summary>
/// Finds Steam Cloud folders under <c>userdata\{account}\remote\{appid}</c>
/// (and the older <c>userdata\{account}\{appid}\remote</c> layout).
/// </summary>
public static class SteamCloudSaveLocator
{
    public static string? TryFind(string? steamInstallPath, string appId)
    {
        if (string.IsNullOrWhiteSpace(steamInstallPath) || string.IsNullOrWhiteSpace(appId))
            return null;

        var userDataDir = Path.Combine(steamInstallPath, "userdata");
        if (!Directory.Exists(userDataDir))
            return null;

        string? best = null;
        var bestWrite = DateTime.MinValue;

        foreach (var accountDir in Directory.EnumerateDirectories(userDataDir))
        {
            if (!IsSteamAccountFolder(Path.GetFileName(accountDir)))
                continue;

            foreach (var candidate in Candidates(accountDir, appId.Trim()))
            {
                if (!Directory.Exists(candidate))
                    continue;

                var write = Directory.GetLastWriteTimeUtc(candidate);
                if (best is null || write >= bestWrite)
                {
                    best = candidate;
                    bestWrite = write;
                }
            }
        }

        return best;
    }

    private static IEnumerable<string> Candidates(string accountDir, string appId)
    {
        yield return Path.Combine(accountDir, "remote", appId);
        yield return Path.Combine(accountDir, appId, "remote");
    }

    private static bool IsSteamAccountFolder(string? name) =>
        !string.IsNullOrEmpty(name) &&
        ulong.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out _);
}
