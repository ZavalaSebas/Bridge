using System.IO;

namespace Bridge.Services;

public enum UpdateChannel
{
    Stable,
    Beta
}

/// <summary>
/// Persists the update channel (Stable/Beta) in a small text file under
/// AppDataPath, same pattern as ViewModeSettingsStore: a plain file, tolerant
/// of corruption, and saving never crashes the app. Stable is the default, so
/// a fresh install never receives prereleases without opting in.
/// </summary>
public static class UpdateChannelSettingsStore
{
    private static string SettingsFile => Config.UpdateChannelFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "update-channel.txt");

    public static UpdateChannel Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile, out var saved) ||
                TryLoadFromFile(LegacySettingsFile, out saved))
            {
                return saved;
            }
        }
        catch
        {
            // Corrupt/missing settings — fall back to the default.
        }

        return UpdateChannel.Stable;
    }

    public static void Save(UpdateChannel channel)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, channel.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static bool TryLoadFromFile(string path, out UpdateChannel channel)
    {
        channel = UpdateChannel.Stable;
        return File.Exists(path) &&
            Enum.TryParse<UpdateChannel>(File.ReadAllText(path).Trim(), out channel);
    }
}