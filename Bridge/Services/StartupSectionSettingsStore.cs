using System.IO;
using Bridge.Core.Enums;

namespace Bridge.Services;

public static class StartupSectionSettingsStore
{
    private static string SettingsFile => Config.StartupSectionFilePath;
    private static string LegacyFile => Path.Combine(Config.AppDataPath, "startup-section.txt");

    public static NavigationSection Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile, out var sec) ||
                TryLoadFromFile(LegacyFile, out sec))
                return sec;
        }
        catch { }
        return NavigationSection.Library;
    }

    public static void Save(NavigationSection section)
    {
        // Only Home/Library/Roms are valid startup sections; fallback to Library for others
        if (section != NavigationSection.Home && section != NavigationSection.Library && section != NavigationSection.Roms)
            section = NavigationSection.Library;

        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, section.ToString());
        }
        catch { }
    }

    private static bool TryLoadFromFile(string path, out NavigationSection section)
    {
        section = NavigationSection.Library;
        if (!File.Exists(path)) return false;
        var raw = File.ReadAllText(path).Trim();
        if (Enum.TryParse<NavigationSection>(raw, true, out var parsed))
        {
            if (parsed == NavigationSection.Home || parsed == NavigationSection.Library || parsed == NavigationSection.Roms)
            {
                section = parsed;
                return true;
            }
        }
        return false;
    }
}
