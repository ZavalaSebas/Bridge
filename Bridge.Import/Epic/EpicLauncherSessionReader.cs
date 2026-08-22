using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bridge.Import.Epic;

/// Reads the Epic Games Launcher RememberMe session from local config (no Bridge login).
public static class EpicLauncherSessionReader
{
    public static EpicLauncherSession? TryReadSession(string? configDirectory = null)
    {
        if (configDirectory is not null)
            return TryReadSessionFromConfigDirectory(configDirectory);

        foreach (var directory in EpicPaths.EnumerateLauncherConfigDirectories())
        {
            var session = TryReadSessionFromConfigDirectory(directory);
            if (session is not null)
                return session;
        }

        return null;
    }

    private static EpicLauncherSession? TryReadSessionFromConfigDirectory(string configDirectory)
    {
        var configPath = Path.Combine(configDirectory, "GameUserSettings.ini");
        if (!File.Exists(configPath))
            return null;

        var encoded = IniReader.TryGetValue(configPath, "RememberMe", "Data");
        if (string.IsNullOrWhiteSpace(encoded))
            return null;

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(encoded.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        string json;
        try
        {
            json = raw.Length > 0 && (raw[0] == '{' || raw[0] == '[')
                ? Encoding.UTF8.GetString(raw)
                : EpicLauncherCrypt.DecryptToJson(EpicLauncherCrypt.DefaultDataKey, raw);
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidDataException)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array ||
                document.RootElement.GetArrayLength() == 0)
            {
                return null;
            }

            var entry = document.RootElement[0];
            if (!entry.TryGetProperty("Token", out var tokenElement))
                return null;

            var refreshToken = tokenElement.GetString();
            return string.IsNullOrWhiteSpace(refreshToken)
                ? null
                : new EpicLauncherSession { RefreshToken = refreshToken };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static class IniReader
    {
        internal static string? TryGetValue(string path, string section, string key)
        {
            var inSection = false;
            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                    continue;

                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    inSection = trimmed[1..^1].Equals(section, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inSection)
                    continue;

                var separator = trimmed.IndexOf('=');
                if (separator <= 0)
                    continue;

                var currentKey = trimmed[..separator].Trim();
                if (!currentKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;

                return trimmed[(separator + 1)..].Trim().Trim('"');
            }

            return null;
        }
    }
}
