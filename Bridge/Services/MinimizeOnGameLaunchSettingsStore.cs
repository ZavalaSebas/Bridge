using System.IO;

namespace Bridge.Services;

public static class MinimizeOnGameLaunchSettingsStore
{
    private static string SettingsFile => Config.MinimizeOnGameLaunchFilePath;
    private static string LegacyFile => Path.Combine(Config.AppDataPath, "minimize-on-game-launch.txt");

    public static bool Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile, out var v) ||
                TryLoadFromFile(LegacyFile, out v))
                return v;
        }
        catch { }
        return true; // active by default
    }

    public static void Save(bool enabled)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, enabled.ToString());
        }
        catch { }
    }

    private static bool TryLoadFromFile(string path, out bool value)
    {
        value = true;
        if (!File.Exists(path)) return false;
        return bool.TryParse(File.ReadAllText(path).Trim(), out value);
    }
}
