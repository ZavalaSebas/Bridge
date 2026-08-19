using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether Bridge stays in the notification area when the main window
/// is closed.
/// </summary>
public static class TrayIconSettingsStore
{
    private static string SettingsFile => Config.TrayIconFilePath;

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

    public static void Save(bool minimizeToTray)
    {
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, minimizeToTray.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
