using System.IO;

namespace Bridge.Services;

public static class MinimizeOnGameLaunchSettingsStore
{
    private static string SettingsFile => Config.MinimizeOnGameLaunchFilePath;
    private static string LegacyFile => Path.Combine(Config.AppDataPath, "minimize-on-game-launch.txt");

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacyFile, true, bool.TryParse);

    public static void Save(bool enabled) =>
        ScalarSettingStore.Save(SettingsFile, enabled.ToString());
}
