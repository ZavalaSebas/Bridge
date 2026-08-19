using Microsoft.Win32;

namespace Bridge.Services;

/// <summary>
/// Registers Bridge in the current user's Windows Run key so it launches at
/// sign-in. Only the published Bridge.exe can be registered — not dotnet-run
/// dev builds.
/// </summary>
public static class WindowsStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool CanRegister =>
        Environment.ProcessPath is { Length: > 0 } path &&
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(Config.AppName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static bool TrySetRegistered(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (key is null)
                return false;

            if (!enabled)
            {
                key.DeleteValue(Config.AppName, throwOnMissingValue: false);
                return true;
            }

            if (!CanRegister)
                return false;

            key.SetValue(Config.AppName, FormatLaunchCommand(Environment.ProcessPath!));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Keeps the Run key aligned with the saved preference (e.g. after restore).
    /// </summary>
    public static void ApplySavedPreference() =>
        TrySetRegistered(StartupSettingsStore.Load());

    internal static string FormatLaunchCommand(string exePath) =>
        $"\"{exePath}\"";
}
