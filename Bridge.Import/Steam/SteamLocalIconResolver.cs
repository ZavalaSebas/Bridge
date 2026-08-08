namespace Bridge.Import.Steam;

/// <summary>
/// Resolves the square 32x32 clienticon Steam keeps on disk for the library —
/// the same artwork Playnite shows (PROJECT_FOUNDATION.md §28.26). Steam stores
/// it as a 40-hex-character file inside appcache\librarycache\{appid}\ next to
/// the wide header.jpg / library_*.jpg art. Returns null when Steam isn't
/// installed or that app has no cached icon (downloads aren't guaranteed for
/// every app), so callers fall back to a web-sourced icon.
/// </summary>
public static class SteamLocalIconResolver
{
    private const int ClientIconHashLength = 40;

    public static string? TryGetLocalIconPath(string appId, string? steamInstallPath = null)
    {
        if (!uint.TryParse(appId, out _))
            return null;

        steamInstallPath ??= SteamPaths.GetInstallationPath();
        if (string.IsNullOrWhiteSpace(steamInstallPath))
            return null;

        var cacheDir = Path.Combine(steamInstallPath, "appcache", "librarycache", appId);
        if (!Directory.Exists(cacheDir))
            return null;

        // The 40-hex-char .jpg is the clienticon (32x32). header.jpg,
        // library_*.jpg, and logo.png live alongside it — skip them.
        return Directory.GetFiles(cacheDir, "*.jpg")
            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Length == ClientIconHashLength);
    }
}
