using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether the normal hero buttons (Play / More / Edit) are shown on the Details hero.
/// Default is visible (true) to preserve existing behavior.
/// </summary>
public static class DetailHeroButtonsSettingsStore
{
    private static string SettingsFile => Config.DetailHeroButtonsFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "detail-hero-buttons.txt");

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
        }

        return true;
    }

    public static void Save(bool enabled)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, enabled.ToString());
        }
        catch
        {
        }
    }

    private static bool TryLoadFromFile(string path, out bool enabled)
    {
        enabled = true;
        return File.Exists(path) &&
            bool.TryParse(File.ReadAllText(path).Trim(), out enabled);
    }
}
