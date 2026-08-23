using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bridge.Metadata;
using Bridge.Services;

namespace Bridge.Settings;

/// <summary>SteamGridDB API key in a protected JSON file under AppData.</summary>
public static class SteamGridDbSettingsStore
{
    private static readonly byte[] ProtectionEntropy = "Bridge.SteamGridDbSettings.v1"u8.ToArray();
    private const byte ProtectedFormatVersion = 1;

    private static string FilePath => Path.Combine(Config.SecretsDirectoryPath, "steamgriddb-settings.json");
    private static string LegacyFilePath => Path.Combine(Config.AppDataPath, "steamgriddb-settings.json");

    public static SteamGridDbSettings Load()
    {
        try
        {
            return TryLoadFromPath(FilePath)
                ?? TryLoadFromPath(LegacyFilePath)
                ?? new SteamGridDbSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new SteamGridDbSettings();
        }
    }

    public static void Save(SteamGridDbSettings settings)
    {
        Directory.CreateDirectory(Config.SecretsDirectoryPath);
        WriteProtected(FilePath, settings);
    }

    internal static void MigratePlainTextToProtectedFormat(AppDataMigrationContext ctx)
    {
        foreach (var path in new[] { ctx.Combine("steamgriddb-settings.json"), ctx.Combine("config", "secrets", "steamgriddb-settings.json") })
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length > 0 && bytes[0] == ProtectedFormatVersion)
                    continue;

                var legacyJson = Encoding.UTF8.GetString(bytes);
                var settings = JsonSerializer.Deserialize<SteamGridDbSettings>(legacyJson) ?? new SteamGridDbSettings();
                WriteProtected(path, settings);
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or CryptographicException)
            {
            }
        }
    }

    private static void WriteProtected(string path, SteamGridDbSettings settings)
    {
        var json = JsonSerializer.Serialize(settings);
        var protectedPayload = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json),
            ProtectionEntropy,
            DataProtectionScope.CurrentUser);
        var fileBytes = new byte[protectedPayload.Length + 1];
        fileBytes[0] = ProtectedFormatVersion;
        Buffer.BlockCopy(protectedPayload, 0, fileBytes, 1, protectedPayload.Length);
        File.WriteAllBytes(path, fileBytes);
    }

    private static SteamGridDbSettings? TryLoadFromPath(string path)
    {
        if (!File.Exists(path))
            return null;

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 0 && bytes[0] == ProtectedFormatVersion)
        {
            var protectedPayload = bytes[1..];
            var json = Encoding.UTF8.GetString(
                ProtectedData.Unprotect(protectedPayload, ProtectionEntropy, DataProtectionScope.CurrentUser));
            return JsonSerializer.Deserialize<SteamGridDbSettings>(json);
        }

        var legacyJson = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<SteamGridDbSettings>(legacyJson);
    }
}
