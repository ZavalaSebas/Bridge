using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether Bridge should launch at Windows sign-in. The actual registry
/// entry is managed by <see cref="WindowsStartupRegistration"/>.
/// </summary>
public static class StartupSettingsStore
{
    private static string SettingsFile => Config.StartupFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "startup.txt");

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, false, bool.TryParse);

    public static void Save(bool launchAtStartup) =>
        ScalarSettingStore.Save(SettingsFile, launchAtStartup.ToString());
}
