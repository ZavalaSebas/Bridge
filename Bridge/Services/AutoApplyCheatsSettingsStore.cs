using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), Bridge writes RetroArch per-game override configs so
/// enabled cheats apply automatically on launch.
/// </summary>
public static class AutoApplyCheatsSettingsStore
{
    private static string SettingsFile => Config.AutoApplyCheatsOnLaunchFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "auto-apply-cheats-on-launch.txt");

    public static bool Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile, out var saved) ||
                TryLoadFromFile(LegacySettingsFile, out saved))
            {
                return saved;
            }
        }
        catch
        {
            // Corrupt/missing settings — fall back to the default.
        }

        return true;
    }

    public static void Save(bool autoApplyCheatsOnLaunch)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, autoApplyCheatsOnLaunch.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }

        private static bool TryLoadFromFile(string path, out bool enabled)
        {
            enabled = true;
            return File.Exists(path) &&
                bool.TryParse(File.ReadAllText(path).Trim(), out enabled);
        }
    }
}
