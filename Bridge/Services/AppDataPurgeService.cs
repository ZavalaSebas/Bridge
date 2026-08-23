using System.IO;
using Microsoft.Data.Sqlite;

namespace Bridge.Services;

public static class AppDataPurgeService
{
    public static (bool Success, string? Message) PurgeAllData()
    {
        try
        {
            // Clear SQLite pools so bridge.db can be deleted
            try { SqliteConnection.ClearAllPools(); } catch { }

            var appData = Config.AppDataPath;

            // Give a moment for any pending writes to flush
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (Directory.Exists(appData))
            {
                // Delete everything under AppData/Bridge
                foreach (var dir in Directory.GetDirectories(appData))
                {
                    try { Directory.Delete(dir, true); } catch { /* try file by file below */ }
                }
                foreach (var file in Directory.GetFiles(appData))
                {
                    try { File.Delete(file); } catch { }
                }

                // Second pass: ensure empty subdirs are gone, retry with file-by-file for stubborn locks
                foreach (var dir in Directory.GetDirectories(appData))
                {
                    TryDeleteDirectoryRecursive(dir);
                }
                foreach (var file in Directory.GetFiles(appData, "*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
                // Remove any remaining empty dirs
                foreach (var dir in Directory.GetDirectories(appData))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }

            // Recreate essential dirs so next launch doesn't fail before migration
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            Directory.CreateDirectory(Config.SecretsDirectoryPath);
            Directory.CreateDirectory(Config.ImageCachePath);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static void TryDeleteDirectoryRecursive(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { }
            }
            Directory.Delete(path, true);
        }
        catch { }
    }
}
