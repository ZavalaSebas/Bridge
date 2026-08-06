using Microsoft.Win32;

namespace Bridge.Import.Steam;

/// <summary>Matches Playnite's real Steam.InstallationPath (PROJECT_FOUNDATION.md §28.26) exactly: reads the SteamPath value under HKCU\Software\Valve\Steam.</summary>
public static class SteamPaths
{
    public static string? GetInstallationPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }
}
