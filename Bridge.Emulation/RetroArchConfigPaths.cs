using System.Text.RegularExpressions;

namespace Bridge.Emulation;

internal static partial class RetroArchConfigPaths
{
    private static readonly Regex RguiConfigDirectoryPattern =
        new(@"^[ \t]*rgui_config_directory[ \t]*=[ \t]*""?([^""\r\n]*)""?[ \t]*$", RegexOptions.Multiline);

    internal static string GetMainConfigPath(string retroArchExecutablePath)
    {
        var executableDirectory = Path.GetDirectoryName(retroArchExecutablePath) ?? string.Empty;
        return Path.Combine(executableDirectory, "retroarch.cfg");
    }

    internal static string ResolveConfigDirectory(string retroArchExecutablePath)
    {
        var mainConfigPath = GetMainConfigPath(retroArchExecutablePath);
        if (!File.Exists(mainConfigPath))
        {
            return Path.GetDirectoryName(retroArchExecutablePath) ?? string.Empty;
        }

        var match = RguiConfigDirectoryPattern.Match(File.ReadAllText(mainConfigPath));
        var configuredValue = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        var executableDirectory = Path.GetDirectoryName(retroArchExecutablePath) ?? string.Empty;

        if (configuredValue.Length == 0 || configuredValue == "default")
        {
            return executableDirectory;
        }

        return configuredValue[0] == ':'
            ? Path.Combine(executableDirectory, configuredValue[1..].TrimStart('\\', '/'))
            : configuredValue;
    }
}
