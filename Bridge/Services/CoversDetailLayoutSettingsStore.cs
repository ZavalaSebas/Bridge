using System.IO;

namespace Bridge.Services;

/// <summary>
/// How game details open in Covers view: the compact 320px info panel, or the
/// full details panel at half the window width (List view still uses a larger pane).
/// </summary>
public static class CoversDetailLayoutSettingsStore
{
    public const string Compact = "Compact";
    public const string Standard = "Standard";

    private static string SettingsFile => Config.CoversDetailLayoutFilePath;

    public static string Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile, out var saved) && IsValid(saved))
                return saved;
        }
        catch
        {
            // Corrupt/missing settings — fall back to the default.
        }

        return Compact;
    }

    public static bool UsesCompact() => Load() == Compact;

    public static void Save(string layout)
    {
        var normalized = Normalize(layout);
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, normalized);
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    internal static string Normalize(string raw)
    {
        if (raw.Trim().Equals(Standard, StringComparison.OrdinalIgnoreCase))
            return Standard;

        return Compact;
    }

    internal static bool IsValid(string layout) =>
        layout is Compact or Standard;

    private static bool TryLoadFromFile(string path, out string value)
    {
        value = string.Empty;
        if (!File.Exists(path))
            return false;

        value = Normalize(File.ReadAllText(path));
        return true;
    }
}
