using System.IO;
using System.Reflection;

namespace Bridge;

public static class Config
{
    public const string AppName = "Bridge";

    // The full releases list (newest first), not /releases/latest: Bridge picks
    // which release to offer based on the update channel (Stable skips
    // prereleases, Beta accepts them) instead of letting GitHub's "latest"
    // endpoint decide — that one excludes prereleases entirely.
    public const string GitHubReleasesUrl = "https://api.github.com/repos/ZavalaSebas/Bridge/releases?per_page=100";

    public const int RequestTimeoutSeconds = 10;

    public const int MetadataRequestTimeoutSeconds = 10;

    public const int DownloadRequestTimeoutSeconds = 900;

    public const string BridgeIgdbMetadataEndpoint = "https://bridge-igdb.sebaszavala120.workers.dev/metadata";

    public const string UpdateAssetName = "Bridge.exe";

    public const string KoFiUrl = "https://ko-fi.com/sebastianzavala82573";

    public const string GitHubSponsorsUrl = "https://github.com/sponsors/ZavalaSebas";

    /// <summary>Default sponsor link for commands and future status-bar affordances.</summary>
    public const string PrimarySponsorUrl = GitHubSponsorsUrl;

    public static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppName);

    public static string ConfigDirectoryPath => Path.Combine(AppDataPath, "config");

    public static string SecretsDirectoryPath => Path.Combine(ConfigDirectoryPath, "secrets");

    public static string DatabasePath => Path.Combine(AppDataPath, "bridge.db");

    // On-disk cache for artwork RemoteImageCache downloads (covers, backgrounds,
    // icons). Keeping the decoded bytes on disk means reopens read locally and
    // render instantly instead of re-downloading every remote image.
    public static string ImageCachePath => Path.Combine(AppDataPath, "image-cache");

    // Bridge-managed RetroArch installation. Kept separate from the game database
    // so deleting an emulator install never risks a user's library data.
    public static string EmulatorInstallPath => Path.Combine(AppDataPath, "emulators", "retroarch");

    public static string EmulatorDownloadPath => Path.Combine(AppDataPath, "emulator-downloads");

    // Records the RetroArch version currently installed (e.g. "1.22.2"). Since
    // stable builds moved off GitHub to the Libretro buildbot, which publishes
    // no SHA-256 digest, the version string is the change signal: a different
    // resolved version means the frontend must be refreshed.
    public static string RetroArchVersionPath => Path.Combine(AppDataPath, "emulators", "retroarch.version");

    // Per-game .cht files RetroArchCheatService fetches and manages.
    public static string CheatsPath => Path.Combine(AppDataPath, "cheats");

    // Apply enabled cheats automatically when launching a ROM through Bridge RetroArch.
    public static string AutoApplyCheatsOnLaunchFilePath =>
        Path.Combine(ConfigDirectoryPath, "auto-apply-cheats-on-launch.txt");

    public static string ThemeFilePath => Path.Combine(ConfigDirectoryPath, "theme.json");

    public static string ViewModeFilePath => Path.Combine(ConfigDirectoryPath, "viewmode.txt");

    public static string ScrollPositionsFilePath => Path.Combine(ConfigDirectoryPath, "scrollpositions.txt");

    // The update channel (Stable/Beta) Bridge offers releases from. Kept in a
    // plain file like viewmode.txt: an app-instance preference, not library data.
    public static string UpdateChannelFilePath => Path.Combine(ConfigDirectoryPath, "update-channel.txt");

    // UI language (English/Spanish). Plain file like update-channel.txt.
    public static string LanguageFilePath => Path.Combine(ConfigDirectoryPath, "language.txt");

    // Launch Bridge at Windows sign-in. Plain file like update-channel.txt.
    public static string StartupFilePath => Path.Combine(ConfigDirectoryPath, "startup.txt");

    // Minimize to the notification area when the main window is closed.
    public static string TrayIconFilePath => Path.Combine(ConfigDirectoryPath, "tray-icon.txt");

    // Keep the same selected game when switching List/Covers/Table views.
    public static string KeepSelectionAcrossViewsFilePath =>
        Path.Combine(ConfigDirectoryPath, "keep-selection-across-views.txt");

    // Dock edge for the List details panel and Covers compact info panel.
    public static string DetailPanelPositionFilePath =>
        Path.Combine(ConfigDirectoryPath, "detail-panel-position.txt");

    // Details column vs Overview/Images tabs within the game details content area.
    public static string DetailSectionPositionFilePath =>
        Path.Combine(ConfigDirectoryPath, "detail-section-position.txt");

    // Semi-transparent sidebar (frosted look over the game background).
    public static string SidebarTranslucentFilePath =>
        Path.Combine(ConfigDirectoryPath, "sidebar-translucent.txt");

    // Blurred game art and semi-transparent library/detail panels.
    public static string TranslucentBackgroundFilePath =>
        Path.Combine(ConfigDirectoryPath, "translucent-background.txt");

    // Last app version for which the What's New dialog was shown.
    public static string WhatsNewSeenFilePath =>
        Path.Combine(ConfigDirectoryPath, "whats-new-seen.txt");

    // Watched ROM folder for Scan ROMs and automatic import.
    public static string RomScanFolderFilePath =>
        Path.Combine(ConfigDirectoryPath, "rom-scan-folder.txt");

    // Watched folder for Scan Automatically (Scan Folder).
    public static string InstalledScanFolderFilePath =>
        Path.Combine(ConfigDirectoryPath, "installed-scan-folder.txt");

    // First-run setup completion flag.
    public static string SetupCompleteFilePath =>
        Path.Combine(ConfigDirectoryPath, "setup-complete.txt");

    // User profile (display name + avatar).
    public static string UserProfileFilePath =>
        Path.Combine(ConfigDirectoryPath, "user-profile.json");

    public static string GameDisplayPreferencesFilePath =>
        Path.Combine(ConfigDirectoryPath, "game-display-preferences.json");

    public static string UserProfileDirectoryPath =>
        Path.Combine(AppDataPath, "profile");

    // Tracks which numbered AppData migration steps have run (see AppDataMigrator).
    public const string AppDataVersionFileName = "appdata-version.txt";

    public static string AppDataVersionFilePath => Path.Combine(AppDataPath, AppDataVersionFileName);

    public static Version AssemblyVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
}
