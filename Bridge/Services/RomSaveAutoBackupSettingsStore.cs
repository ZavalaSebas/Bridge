using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), Bridge copies a ROM's SRAM and savestates into a
/// dated snapshot under <c>save-backups/</c> after each session.
/// </summary>
public static class RomSaveAutoBackupSettingsStore
{
    private static string SettingsFile => Config.RomSaveAutoBackupFilePath;

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, null, true, bool.TryParse);

    public static void Save(bool enabled) =>
        ScalarSettingStore.Save(SettingsFile, enabled.ToString());
}
