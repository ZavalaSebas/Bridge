using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether Bridge stays in the notification area when the main window
/// is closed.
/// </summary>
public static class TrayIconSettingsStore
{
    private static string SettingsFile => Config.TrayIconFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "tray-icon.txt");

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, true, bool.TryParse);

    public static void Save(bool minimizeToTray) =>
        ScalarSettingStore.Save(SettingsFile, minimizeToTray.ToString());
}
