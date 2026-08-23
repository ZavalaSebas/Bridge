using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists the ROM folder used by Scan ROMs and the automatic folder watcher.
/// </summary>
public static class RomScanFolderSettingsStore
{
    private static string SettingsFile => Config.RomScanFolderFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "rom-scan-folder.txt");

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
