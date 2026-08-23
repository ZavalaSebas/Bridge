using System.IO;

namespace Bridge.Services;

/// <summary>
/// When enabled (default), newly imported ROMs are moved into a per-platform
/// folder and renamed to the official DAT name.
/// </summary>
public static class RomOrganizeSettingsStore
{
    private static string SettingsFile => Config.RomOrganizeOnImportFilePath;

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
