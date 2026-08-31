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

    public static Version? Load() =>
        ScalarSettingStore.Load<Version?>(SettingsFile, LegacySettingsFile, null, TryParseVersion);

    public static void Save(Version version) =>
        ScalarSettingStore.Save(SettingsFile, Normalize(version).ToString(3));

    private static bool TryParseVersion(string raw, out Version? value)
    {
        if (Version.TryParse(raw, out var version))
        {
            value = Normalize(version);
            return true;
        }

        value = null;
        return false;
    }

    internal static Version Normalize(Version version) =>
        new(version.Major, version.Minor, version.Build);
}
