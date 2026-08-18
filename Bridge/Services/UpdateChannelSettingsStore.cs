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

    public static UpdateChannel Load()
    {
        try
        {
            if (File.Exists(SettingsFile) &&
                Enum.TryParse<UpdateChannel>(File.ReadAllText(SettingsFile).Trim(), out var saved))
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
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, channel.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}