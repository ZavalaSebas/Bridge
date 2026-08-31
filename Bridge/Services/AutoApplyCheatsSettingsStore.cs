using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), Bridge writes RetroArch per-game override configs so
/// enabled cheats apply automatically on launch.
/// </summary>
public static class AutoApplyCheatsSettingsStore
{
    private static string SettingsFile => Config.AutoApplyCheatsOnLaunchFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "auto-apply-cheats-on-launch.txt");

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, true, bool.TryParse);

    public static void Save(bool autoApplyCheatsOnLaunch) =>
        ScalarSettingStore.Save(SettingsFile, autoApplyCheatsOnLaunch.ToString());
}
