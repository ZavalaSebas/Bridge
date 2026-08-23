using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), Bridge copies a ROM's SRAM and savestates into a
/// dated snapshot under <c>save-backups/</c> after each session.
/// </summary>
public static class RomSaveAutoBackupSettingsStore
{
    private static string SettingsFile => Config.RomSaveAutoBackupFilePath;

    public static bool Load()
    {
        try
        {
            if (File.Exists(SettingsFile) &&
                bool.TryParse(File.ReadAllText(SettingsFile).Trim(), out var saved))
            {
                return saved;
            }
        }
        catch
        {
            // Corrupt/missing settings — fall back to the default.
        }

        return true;
    }

    public static void Save(bool enabled)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, enabled.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
