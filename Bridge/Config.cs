using System.IO;

namespace Bridge;

public static class Config
{
    public const string AppName = "Bridge";

    public static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppName);

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
}
