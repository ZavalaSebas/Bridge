using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether the main sidebar uses the legacy semi-transparent look.
/// Default is solid (false).
/// </summary>
public static class SidebarTranslucentSettingsStore
{
    private static string SettingsFile => Config.SidebarTranslucentFilePath;

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

        return false;
    }

    public static void Save(bool translucent)
    {
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, translucent.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
