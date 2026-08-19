using System.IO;
using System.Text.Json;

namespace Bridge.Services;

public static class UserProfileSettingsStore
{
    private static string SettingsFile => Config.UserProfileFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static UserProfile Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return UserProfile.CreateDefault();
            }

            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<UserProfile>(json, JsonOptions) ?? UserProfile.CreateDefault();
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
            Directory.CreateDirectory(Config.AppDataPath);
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
