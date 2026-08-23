using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether Bridge stays in the notification area when the main window
/// is closed.
/// </summary>
public static class TrayIconSettingsStore
{
    private static string SettingsFile => Config.TrayIconFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "tray-icon.txt");

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

    public static void Save(bool minimizeToTray)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, minimizeToTray.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static bool TryLoadFromFile(string path, out bool enabled)
    {
        enabled = true;
        return File.Exists(path) &&
            bool.TryParse(File.ReadAllText(path).Trim(), out enabled);
    }
}
