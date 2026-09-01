using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists the ROM folder used by Scan ROMs and the automatic folder watcher.
/// </summary>
public static class RomScanFolderSettingsStore
{
    private static string SettingsFile => Config.RomScanFolderFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "rom-scan-folder.txt");

    public static string? Load() =>
        ScalarSettingStore.Load<string?>(SettingsFile, LegacySettingsFile, null, TryParseFolder);

    public static void Save(string? folderPath)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                if (File.Exists(SettingsFile))
                {
                    File.Delete(SettingsFile);
                }

                return;
            }

            File.WriteAllText(SettingsFile, folderPath.Trim());
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Failed to save setting to '{SettingsFile}'.", ex);
        }
    }

    private static bool TryParseFolder(string raw, out string? value)
    {
        value = string.IsNullOrWhiteSpace(raw) ? null : raw;
        return value is not null;
    }
}
