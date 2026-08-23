using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether Bridge should launch at Windows sign-in. The actual registry
/// entry is managed by <see cref="WindowsStartupRegistration"/>.
/// </summary>
public static class StartupSettingsStore
{
    private static string SettingsFile => Config.StartupFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "startup.txt");

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

    public static void Save(bool launchAtStartup)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, launchAtStartup.ToString());
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
