using System.IO;
using Bridge.Core.Enums;

namespace Bridge.Services;

/// <summary>
/// Persists the last-selected library view (List/Grid/Table) in a small text
/// file under AppDataPath, so reopening Bridge restores the view you were using.
/// Same pattern as ThemeManager: a plain file, tolerant of corruption, and
/// saving never crashes the app.
/// </summary>
public static class ViewModeSettingsStore
{
    private static string SettingsFile => Path.Combine(Config.AppDataPath, "viewmode.txt");

    public static ViewMode Load()
    {
        try
        {
            if (File.Exists(SettingsFile) &&
                Enum.TryParse<ViewMode>(File.ReadAllText(SettingsFile).Trim(), out var saved))
            {
                return saved;
            }
        }
        catch
        {
            // Corrupt/missing settings — fall back to the default.
        }

        return ViewMode.List;
    }

    public static void Save(ViewMode mode)
    {
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, mode.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
