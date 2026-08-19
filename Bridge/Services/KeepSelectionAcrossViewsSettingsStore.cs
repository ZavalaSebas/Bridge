using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), the same game stays selected when switching between
/// List, Covers, and Table. When disabled, switching views clears the selection.
/// </summary>
public static class KeepSelectionAcrossViewsSettingsStore
{
    private static string SettingsFile => Config.KeepSelectionAcrossViewsFilePath;

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

    public static void Save(bool keepSelectionAcrossViews)
    {
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, keepSelectionAcrossViews.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
