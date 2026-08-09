namespace Bridge.Import.Steam;

/// <summary>
/// Resolves the artwork Steam keeps on disk for the library —
/// appcache\librarycache\{appid}\ — the same images Playnite shows
/// (PROJECT_FOUNDATION.md §28.26): the square 32x32 clienticon (a
/// 40-hex-character .jpg), the vertical cover (library_600x900.jpg) and the
/// widescreen hero background (library_hero.jpg). Wide header.jpg and
/// logo.png also live alongside them but aren't used. Returns null when Steam
/// isn't installed or that app has no cached file (downloads aren't guaranteed
/// for every app), so callers fall back to web-sourced art.
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

        return Directory.GetFiles(cacheDir, "*.jpg").FirstOrDefault(match);
    }
}
