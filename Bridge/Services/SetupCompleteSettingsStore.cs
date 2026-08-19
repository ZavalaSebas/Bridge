using System.IO;

namespace Bridge.Services;

public static class SetupCompleteSettingsStore
{
    private static string SettingsFile => Config.SetupCompleteFilePath;

    public static bool IsComplete()
    {
        try
        {
            return File.Exists(SettingsFile)
                && bool.TryParse(File.ReadAllText(SettingsFile).Trim(), out var complete)
                && complete;
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
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, bool.TrueString);
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
