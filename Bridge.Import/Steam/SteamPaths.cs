using Microsoft.Win32;

namespace Bridge.Import.Steam;

/// <summary>Reads the SteamPath value under HKCU\Software\Valve\Steam.</summary>
public static class SteamPaths
{
    public static string? GetInstallationPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }
}
