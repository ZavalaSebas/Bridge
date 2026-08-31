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

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, true, bool.TryParse);

    public static void Save(bool keepSelectionAcrossViews) =>
        ScalarSettingStore.Save(SettingsFile, keepSelectionAcrossViews.ToString());
}
