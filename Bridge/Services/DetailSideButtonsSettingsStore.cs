using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether floating side buttons (Play / More / Edit) protruding from the list edge over the hero are shown.
/// Default is hidden (false).
/// </summary>
public static class DetailSideButtonsSettingsStore
{
    private static string SettingsFile => Config.DetailSideButtonsFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "detail-side-buttons.txt");

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

        return false;
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
        enabled = false;
        return File.Exists(path) &&
            bool.TryParse(File.ReadAllText(path).Trim(), out enabled);
    }
}
