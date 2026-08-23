using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), the same game stays selected when switching between
/// List, Covers, and Table. When disabled, switching views clears the selection.
/// </summary>
public static class KeepSelectionAcrossViewsSettingsStore
{
    private static string SettingsFile => Config.KeepSelectionAcrossViewsFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "keep-selection-across-views.txt");

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

    public static void Save(bool keepSelectionAcrossViews)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, keepSelectionAcrossViews.ToString());
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
