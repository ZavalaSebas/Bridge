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

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, true, bool.TryParse);

    public static void Save(bool enabled) =>
        ScalarSettingStore.Save(SettingsFile, enabled.ToString());
}
