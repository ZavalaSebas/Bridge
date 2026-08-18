using System.Diagnostics;
using System.IO;
using Bridge.Core.Utilities;

namespace Bridge.Services;

/// <summary>
/// Central place for opening external URLs and running uninstall commands with
/// basic validation — keeps Process.Start call sites consistent.
/// </summary>
public static class SafeLauncher
{
    public static bool TryOpenUrl(string? url)
    {
        if (!UrlValidator.IsSafeToOpen(url))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(url!.Trim()) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return false;
        }
    }

    /// <summary>
    /// Parses a registry UninstallString into an executable and arguments, then
    /// runs it when the executable exists on disk. Rejects bare cmd/script chains.
    /// </summary>
    public static bool TryRunUninstallCommand(string? uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString))
            return false;

        if (!TryParseUninstallCommand(uninstallString, out var fileName, out var arguments))
            return false;

        if (!File.Exists(fileName) && !fileName.EndsWith("msiexec.exe", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return false;
        }
    }

    internal static bool TryParseUninstallCommand(string command, out string fileName, out string arguments)
    {
        fileName = string.Empty;
        arguments = string.Empty;

        var trimmed = command.Trim();
        if (trimmed.Length == 0)
            return false;

        if (trimmed.StartsWith("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("cmd /c", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("powershell", StringComparison.OrdinalIgnoreCase))
            return false;

        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote < 0)
                return false;

            fileName = trimmed[1..endQuote];
            arguments = trimmed[(endQuote + 1)..].TrimStart();
        }
        else
        {
            var space = trimmed.IndexOf(' ');
            if (space < 0)
            {
                fileName = trimmed;
            }
            else
            {
                fileName = trimmed[..space];
                arguments = trimmed[(space + 1)..];
            }
        }

        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        // Expand environment variables in the executable path only.
        fileName = Environment.ExpandEnvironmentVariables(fileName);
        return true;
    }
}
