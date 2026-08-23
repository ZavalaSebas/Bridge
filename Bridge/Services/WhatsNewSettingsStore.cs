using System.IO;

namespace Bridge.Services;

/// <summary>
/// Remembers the last Bridge version for which the user saw the What's New
/// dialog, so release notes only appear after an update — not on first install.
/// </summary>
public static class WhatsNewSettingsStore
{
    private static string SettingsFile => Config.WhatsNewSeenFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "whats-new-seen.txt");

    public static Version? Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile) is { } current)
                return current;

            if (TryLoadFromFile(LegacySettingsFile) is { } legacy)
                return legacy;
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
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, Normalize(version).ToString(3));
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static Version? TryLoadFromFile(string path)
    {
        if (!File.Exists(path))
            return null;

        var text = File.ReadAllText(path).Trim();
        if (Version.TryParse(text, out var version))
                return Normalize(version);

        return null;
    }

    internal static Version Normalize(Version version) =>
        new(version.Major, version.Minor, version.Build);
}
