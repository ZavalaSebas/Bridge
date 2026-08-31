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

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, false, bool.TryParse);

    public static void Save(bool enabled) =>
        ScalarSettingStore.Save(SettingsFile, enabled.ToString());
}
