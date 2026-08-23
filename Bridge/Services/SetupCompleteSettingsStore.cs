using System.IO;

namespace Bridge.Services;

public static class SetupCompleteSettingsStore
{
    private static string SettingsFile => Config.SetupCompleteFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "setup-complete.txt");

    public static bool IsComplete()
    {
        try
        {
            if (TryReadBool(SettingsFile, out var complete))
                return complete;

            return TryReadBool(LegacySettingsFile, out complete) && complete;
        }
        catch
        {
            return false;
        }
    }

    public static void MarkComplete()
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, bool.TrueString);
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static bool TryReadBool(string path, out bool value)
    {
        value = false;
        return File.Exists(path) &&
            bool.TryParse(File.ReadAllText(path).Trim(), out value);
    }
}
