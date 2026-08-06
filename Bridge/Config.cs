using System.IO;

namespace Bridge;

public static class Config
{
    public const string AppName = "Bridge";

    public static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppName);

    public static string DatabasePath => Path.Combine(AppDataPath, "bridge.db");
}
