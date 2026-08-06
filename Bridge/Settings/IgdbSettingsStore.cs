using System.IO;
using System.Text.Json;
using Bridge.Metadata;

namespace Bridge.Settings;

/// <summary>
/// IGDB Client ID/Secret live in a plain JSON file under AppDataPath, not in
/// bridge.db — same separation Playnite's real PlayniteSettings uses (config
/// vs. library data, PROJECT_FOUNDATION.md §28.12), and a sensible one: these
/// are app-instance settings, not library content.
/// </summary>
public static class IgdbSettingsStore
{
    private static string FilePath => Path.Combine(Config.AppDataPath, "igdb-settings.json");

    public static IgdbSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new IgdbSettings();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<IgdbSettings>(json) ?? new IgdbSettings();
        }
        catch (JsonException)
        {
            return new IgdbSettings();
        }
    }

    public static void Save(IgdbSettings settings)
    {
        Directory.CreateDirectory(Config.AppDataPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
    }
}
