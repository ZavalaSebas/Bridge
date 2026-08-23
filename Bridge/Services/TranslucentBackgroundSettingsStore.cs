using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether the main window shows blurred game art and semi-transparent
/// Default is translucent (true) — the original Bridge look.
/// </summary>
public static class TranslucentBackgroundSettingsStore
{
    private static string SettingsFile => Config.TranslucentBackgroundFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "translucent-background.txt");

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
        enabled = true;
        return File.Exists(path) &&
            bool.TryParse(File.ReadAllText(path).Trim(), out enabled);
    }
}
