using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bridge.Metadata;
using Bridge.Services;

namespace Bridge.Settings;

/// <summary>RetroAchievements username/API key in a protected JSON file under AppData.</summary>
public static class RetroAchievementsSettingsStore
{
    private static readonly byte[] ProtectionEntropy = "Bridge.RetroAchievementsSettings.v1"u8.ToArray();
    private const byte ProtectedFormatVersion = 1;

    private static string FilePath => Path.Combine(Config.AppDataPath, "retroachievements-settings.json");

    public static RetroAchievementsSettings Load()
    {
        if (!File.Exists(FilePath))
            return new RetroAchievementsSettings();

        try
        {
            var bytes = File.ReadAllBytes(FilePath);
            if (bytes.Length > 0 && bytes[0] == ProtectedFormatVersion)
            {
                var protectedPayload = bytes[1..];
                var json = Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(protectedPayload, ProtectionEntropy, DataProtectionScope.CurrentUser));
                return JsonSerializer.Deserialize<RetroAchievementsSettings>(json) ?? new RetroAchievementsSettings();
            }

            var legacyJson = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<RetroAchievementsSettings>(legacyJson) ?? new RetroAchievementsSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new RetroAchievementsSettings();
        }
    }

    public static void Save(RetroAchievementsSettings settings)
    {
        Directory.CreateDirectory(Config.AppDataPath);
        var json = JsonSerializer.Serialize(settings);
        var protectedPayload = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json),
            ProtectionEntropy,
            DataProtectionScope.CurrentUser);
        var fileBytes = new byte[protectedPayload.Length + 1];
        fileBytes[0] = ProtectedFormatVersion;
        Buffer.BlockCopy(protectedPayload, 0, fileBytes, 1, protectedPayload.Length);
        File.WriteAllBytes(FilePath, fileBytes);
    }
}
