using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists the folder used by Scan Automatically (Scan Folder) and its watcher.
/// </summary>
public static class InstalledScanFolderSettingsStore
{
    private static string SettingsFile => Config.InstalledScanFolderFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "installed-scan-folder.txt");

    public static string? Load()
    {
        try
        {
            return TryLoadFromFile(SettingsFile) ?? TryLoadFromFile(LegacySettingsFile);
        }
        catch
        {
            return null;
        }
    }

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
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static string? TryLoadFromFile(string path)
    {
        if (!File.Exists(path))
            return null;

        var value = File.ReadAllText(path).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
