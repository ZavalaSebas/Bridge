using System.IO;
using System.Text.Json;

namespace Bridge.Services;

public static class UserProfileSettingsStore
{
    private static string SettingsFile => Config.UserProfileFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "user-profile.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static UserProfile Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile) is { } current)
                return current;

            return TryLoadFromFile(LegacySettingsFile) ?? UserProfile.CreateDefault();
        }
        catch
        {
            return UserProfile.CreateDefault();
        }
    }

    public static void Save(UserProfile profile)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static UserProfile? TryLoadFromFile(string path)
    {
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<UserProfile>(json, JsonOptions);
    }
}
