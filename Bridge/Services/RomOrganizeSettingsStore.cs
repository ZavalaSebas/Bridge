using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), newly imported ROMs are moved into a per-platform
/// folder and renamed to the official DAT name.
/// </summary>
public static class RomOrganizeSettingsStore
{
    private static string SettingsFile => Config.RomOrganizeOnImportFilePath;

    public static bool Load() =>
        ScalarSettingStore.Load(SettingsFile, null, true, bool.TryParse);

    public static void Save(bool enabled) =>
        ScalarSettingStore.Save(SettingsFile, enabled.ToString());
}
