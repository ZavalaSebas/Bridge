using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists the ROM folder used by Scan ROMs and the automatic folder watcher.
/// </summary>
public static class RomScanFolderSettingsStore
{
    private static string SettingsFile => Config.RomScanFolderFilePath;

    public static string? Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return null;
            }

            var path = File.ReadAllText(SettingsFile).Trim();
            return string.IsNullOrWhiteSpace(path) ? null : path;
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
            Directory.CreateDirectory(Config.AppDataPath);
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
}
