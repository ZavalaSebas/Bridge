using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), Bridge copies a Steam/Epic/external game's user-chosen
/// save folder into <c>save-backups/</c> after each session.
/// </summary>
public static class PcSaveAutoBackupSettingsStore
{
    private static string SettingsFile => Config.PcSaveAutoBackupFilePath;

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, null, true, bool.TryParse);

    public static void Save(bool enabled) =>
        ScalarSettingStore.Save(SettingsFile, enabled.ToString());
}
