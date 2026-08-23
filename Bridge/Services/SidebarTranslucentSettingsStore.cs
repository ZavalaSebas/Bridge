using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether the main sidebar uses the legacy semi-transparent look.
/// Default is solid (false).
/// </summary>
public static class SidebarTranslucentSettingsStore
{
    private static string SettingsFile => Config.SidebarTranslucentFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "sidebar-translucent.txt");

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

        return false;
    }

    public static void Save(bool translucent)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, translucent.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static bool TryLoadFromFile(string path, out bool enabled)
    {
        enabled = false;
        return File.Exists(path) &&
            bool.TryParse(File.ReadAllText(path).Trim(), out enabled);
    }
}
