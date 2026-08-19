using System.IO;

namespace Bridge.Services;

/// <summary>
/// Remembers the last Bridge version for which the user saw the What's New
/// dialog, so release notes only appear after an update — not on first install.
/// </summary>
public static class WhatsNewSettingsStore
{
    private static string SettingsFile => Config.WhatsNewSeenFilePath;

    public static Version? Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return null;

            var text = File.ReadAllText(SettingsFile).Trim();
            if (Version.TryParse(text, out var version))
                return Normalize(version);
        }
        catch
        {
            // Corrupt/missing settings — treat as never seen.
        }

        return null;
    }

    public static void Save(Version version)
    {
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, Normalize(version).ToString(3));
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    internal static Version Normalize(Version version) =>
        new(version.Major, version.Minor, version.Build);
}
