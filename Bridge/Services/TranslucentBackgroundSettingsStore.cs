using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether the main window shows blurred game art and semi-transparent
/// Default is translucent (true) — the original Bridge look.
/// </summary>
public static class TranslucentBackgroundSettingsStore
{
    private static string SettingsFile => Config.TranslucentBackgroundFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "translucent-background.txt");

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, true, bool.TryParse);

    public static void Save(bool translucent) =>
        ScalarSettingStore.Save(SettingsFile, translucent.ToString());
}
