namespace Bridge.Import.Steam;

/// <summary>
/// Local Steam library art under appcache\librarycache\{appid}\: 32×32
/// clienticon (40-hex .jpg), cover (library_600x900.jpg), hero
/// (library_hero.jpg). Returns null if Steam is missing or the app has no cache.
/// </summary>
public static class SteamLocalIconResolver
{
    private const int ClientIconHashLength = 40;

    public static string? TryGetLocalIconPath(string appId, string? steamInstallPath = null)
        => TryGetArtworkPath(appId, steamInstallPath,
            f => Path.GetFileNameWithoutExtension(f).Length == ClientIconHashLength);

    public static string? TryGetLocalCoverPath(string appId, string? steamInstallPath = null)
        => TryGetArtworkPath(appId, steamInstallPath,
            f => Path.GetFileName(f).Equals("library_600x900.jpg", StringComparison.OrdinalIgnoreCase));

    public static string? TryGetLocalBackgroundPath(string appId, string? steamInstallPath = null)
        => TryGetArtworkPath(appId, steamInstallPath,
            f => Path.GetFileName(f).Equals("library_hero.jpg", StringComparison.OrdinalIgnoreCase));

    private static string? TryGetArtworkPath(string appId, string? steamInstallPath, Func<string, bool> match)
    {
        if (!uint.TryParse(appId, out _))
            return null;

        steamInstallPath ??= SteamPaths.GetInstallationPath();
        if (string.IsNullOrWhiteSpace(steamInstallPath))
            return null;

        var cacheDir = Path.Combine(steamInstallPath, "appcache", "librarycache", appId);
        if (!Directory.Exists(cacheDir))
            return null;

        try
        {
            return Directory.GetFiles(cacheDir, "*.jpg").FirstOrDefault(match);
        }
        catch (IOException)
        {
            // Unreadable/blocked cache folder (permissions, antivirus, mid-update
            // writes) — fall back to web-sourced art rather than crash the load.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
