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
}
