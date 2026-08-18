using System.IO;
using Microsoft.Win32;

namespace Bridge.Import.Epic;

/// <summary>
/// Epic launcher install path (uninstall registry or default locations) and
/// game data under %PROGRAMDATA%\Epic.
/// </summary>
public static class EpicPaths
{
    public static string ProgramDataRoot =>
        Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%"), "Epic");

    public static string InstalledAppListPath =>
        Path.Combine(ProgramDataRoot, "UnrealEngineLauncher", "LauncherInstalled.dat");

    public static string ManifestsDirectory =>
        Path.Combine(ProgramDataRoot, "EpicGamesLauncher", "Data", "Manifests");

    public static string? GetInstallationPath()
    {
        var fromRegistry = Registry.LocalMachine
            .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
            ?.GetSubKeyNames()
            .Select(name => Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{name}"))
            .FirstOrDefault(key =>
                key?.GetValue("DisplayName") as string == "Epic Games Launcher" &&
                key.GetValue("InstallLocation") is string loc &&
                File.Exists(GetExecutablePath(loc)));

        if (fromRegistry?.GetValue("InstallLocation") is string registryPath)
        {
            return registryPath;
        }

        // Registry keys are sometimes missing — try default install paths.
        foreach (var candidate in new[]
                 {
                     @"C:\Program Files (x86)\Epic Games\",
                     @"C:\Program Files\Epic Games\"
                 })
        {
            if (File.Exists(GetExecutablePath(candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

    public static bool IsInstalled =>
        !string.IsNullOrWhiteSpace(GetInstallationPath());

    public static string GetExecutablePath(string rootPath)
    {
        // Prefer the 32-bit launcher exe when both exist.
        var p32 = Path.Combine(rootPath, "Launcher", "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe");
        return File.Exists(p32)
            ? p32
            : Path.Combine(rootPath, "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe");
    }
}
