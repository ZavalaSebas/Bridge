using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), Bridge writes RetroArch per-game override configs so
/// enabled cheats apply automatically on launch.
/// </summary>
public static class AutoApplyCheatsSettingsStore
{
    private static string SettingsFile => Config.AutoApplyCheatsOnLaunchFilePath;

    public static bool Load()
    {
        try
        {
            if (File.Exists(SettingsFile) &&
                bool.TryParse(File.ReadAllText(SettingsFile).Trim(), out var saved))
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
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, autoApplyCheatsOnLaunch.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
