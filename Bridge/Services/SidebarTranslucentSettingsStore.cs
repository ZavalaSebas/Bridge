using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether the main sidebar uses the legacy semi-transparent look.
/// Default is solid (false).
/// </summary>
public static class SidebarTranslucentSettingsStore
{
    private static string SettingsFile => Config.SidebarTranslucentFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "sidebar-translucent.txt");

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, false, bool.TryParse);

    public static void Save(bool translucent) =>
        ScalarSettingStore.Save(SettingsFile, translucent.ToString());
}
