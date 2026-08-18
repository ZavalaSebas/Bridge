using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bridge.Metadata;
using Bridge.Services;

namespace Bridge.Settings;

/// <summary>IGDB Client ID/Secret in a protected JSON file under AppData, not in bridge.db.</summary>
public static class IgdbSettingsStore
{
    private static readonly byte[] ProtectionEntropy = "Bridge.IgdbSettings.v1"u8.ToArray();
    private const byte ProtectedFormatVersion = 1;

    private static string FilePath => Path.Combine(Config.AppDataPath, "igdb-settings.json");

    public static IgdbSettings Load()
    {
        if (!File.Exists(FilePath))
            return new IgdbSettings();

        try
        {
            var bytes = File.ReadAllBytes(FilePath);
            if (bytes.Length > 0 && bytes[0] == ProtectedFormatVersion)
            {
                var protectedPayload = bytes[1..];
                var json = Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(protectedPayload, ProtectionEntropy, DataProtectionScope.CurrentUser));
                return JsonSerializer.Deserialize<IgdbSettings>(json) ?? new IgdbSettings();
            }

            var legacyJson = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<IgdbSettings>(legacyJson) ?? new IgdbSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new IgdbSettings();
        }
    }

    public static void Save(IgdbSettings settings)
    {
        Directory.CreateDirectory(Config.AppDataPath);
        WriteProtected(FilePath, settings);
    }

    /// <summary>
    /// Rewrites plain-text legacy JSON as DPAPI-protected bytes. Called from
    /// <see cref="AppDataMigrator"/>; load still accepts plain text as fallback.
    /// </summary>
    internal static void MigratePlainTextToProtectedFormat(AppDataMigrationContext ctx)
    {
        var path = ctx.Combine("igdb-settings.json");
        if (!File.Exists(path))
            return;

        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > 0 && bytes[0] == ProtectedFormatVersion)
                return;

            var legacyJson = Encoding.UTF8.GetString(bytes);
            var settings = JsonSerializer.Deserialize<IgdbSettings>(legacyJson) ?? new IgdbSettings();
            WriteProtected(path, settings);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            // Leave the file untouched; Load() will fall back to empty settings.
        }
    }

    private static void WriteProtected(string path, IgdbSettings settings)
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
}
