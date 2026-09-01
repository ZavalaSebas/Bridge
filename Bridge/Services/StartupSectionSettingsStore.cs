using System.IO;
using Bridge.Core.Enums;

namespace Bridge.Services;

public static class StartupSectionSettingsStore
{
    private static string SettingsFile => Config.StartupSectionFilePath;
    private static string LegacyFile => Path.Combine(Config.AppDataPath, "startup-section.txt");

    public static NavigationSection Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacyFile, NavigationSection.Library, TryParseSection);

    public static void Save(NavigationSection section)
    {
        // Only Home/Library/Roms are valid startup sections; fallback to Library for others
        if (section != NavigationSection.Home && section != NavigationSection.Library && section != NavigationSection.Roms)
            section = NavigationSection.Library;

        ScalarSettingStore.Save(SettingsFile, section.ToString());
    }

    private static bool TryParseSection(string raw, out NavigationSection section)
    {
        section = NavigationSection.Library;
        if (Enum.TryParse<NavigationSection>(raw, true, out var parsed) &&
            (parsed == NavigationSection.Home || parsed == NavigationSection.Library || parsed == NavigationSection.Roms))
        {
            section = parsed;
            return true;
        }

        return false;
    }
}
