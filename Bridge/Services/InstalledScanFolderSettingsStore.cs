using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists the folder used by Scan Automatically (Scan Folder) and its watcher.
/// </summary>
public static class InstalledScanFolderSettingsStore
{
    private static string SettingsFile => Config.InstalledScanFolderFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "installed-scan-folder.txt");

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
