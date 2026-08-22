using System.Text.RegularExpressions;

namespace Bridge.Emulation;

/// <summary>Writes RetroAchievements (rcheevos) credentials into RetroArch config before launch.</summary>
public sealed class RetroArchCheevosService
{
    public Task ApplyLaunchConfigAsync(
        string retroArchExecutablePath,
        RetroArchCheevosCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        if (!credentials.IsConfigured)
            return Task.CompletedTask;

        var configPath = RetroArchConfigPaths.GetMainConfigPath(retroArchExecutablePath);
        var existing = File.Exists(configPath) ? File.ReadAllText(configPath) : string.Empty;

        var updated = existing;
        updated = SetLine(updated, "cheevos_enable", "true");
        updated = SetLine(updated, "cheevos_username", credentials.Username);
        updated = SetLine(updated, "saveconfig_on_exit", "true");
        updated = SetLine(updated, "cheevos_hardcore_mode_enable", credentials.HardcoreMode ? "true" : "false");

        if (!string.IsNullOrWhiteSpace(credentials.Token))
        {
            updated = SetLine(updated, "cheevos_token", credentials.Token);
            updated = RemoveLine(updated, "cheevos_password");
        }
        else
        {
            updated = SetLine(updated, "cheevos_password", credentials.Password);
            updated = RemoveLine(updated, "cheevos_token");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        return File.WriteAllTextAsync(configPath, updated, cancellationToken);
    }

    public bool TryReadBackToken(string retroArchExecutablePath, out string? token)
    {
        token = null;
        var configPath = RetroArchConfigPaths.GetMainConfigPath(retroArchExecutablePath);
        if (!File.Exists(configPath))
            return false;

        var match = CreateLinePattern("cheevos_token").Match(File.ReadAllText(configPath));
        if (!match.Success)
            return false;

        token = ReadConfigValue(match.Value);
        return !string.IsNullOrWhiteSpace(token);
    }

    private static string SetLine(string content, string key, string value)
    {
        var line = $"{key} = \"{EscapeValue(value)}\"\n";
        var pattern = CreateLinePattern(key);
        return pattern.IsMatch(content)
            ? pattern.Replace(content, line)
            : content + (content.Length > 0 && !content.EndsWith('\n') ? "\n" : "") + line;
    }

    private static string RemoveLine(string content, string key) =>
        CreateLinePattern(key).Replace(content, string.Empty);

    private static Regex CreateLinePattern(string key) =>
        new($@"^[ \t]*{Regex.Escape(key)}[ \t]*=.*\r?\n?", RegexOptions.Multiline);

    private static string ReadConfigValue(string line)
    {
        var equalsIndex = line.IndexOf('=');
        if (equalsIndex < 0)
            return string.Empty;

        return line[(equalsIndex + 1)..]
            .Trim()
            .Trim('"')
            .Trim();
    }

    private static string EscapeValue(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}

public sealed record RetroArchCheevosCredentials(
    string Username,
    string Password,
    string Token,
    bool HardcoreMode)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Username) &&
        (!string.IsNullOrWhiteSpace(Token) || !string.IsNullOrWhiteSpace(Password));
}
