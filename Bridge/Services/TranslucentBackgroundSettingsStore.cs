using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether the main window shows blurred game art and semi-transparent
/// Default is translucent (true) — the original Bridge look.
/// </summary>
public static class TranslucentBackgroundSettingsStore
{
    private static string SettingsFile => Config.TranslucentBackgroundFilePath;

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
