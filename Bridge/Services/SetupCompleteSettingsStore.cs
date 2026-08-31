using System.IO;

namespace Bridge.Services;

public static class SetupCompleteSettingsStore
{
    private static string SettingsFile => Config.SetupCompleteFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "setup-complete.txt");

    public static bool IsComplete() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, false, bool.TryParse);

    public static void MarkComplete() =>
        ScalarSettingStore.Save(SettingsFile, bool.TrueString);
}
