using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists whether the metadata Details column sits on the left or right of the
/// Overview/Images tabs in the game details content area.
/// </summary>
public static class DetailSectionPositionSettingsStore
{
    public const string Left = "Left";
    public const string Right = "Right";

    private static string SettingsFile => Config.DetailSectionPositionFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "detail-section-position.txt");

    public static string Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile, out var saved) ||
                TryLoadFromFile(LegacySettingsFile, out saved))
            {
                if (IsValid(saved))
                    return saved;
            }
        }
        catch
        {
            // Corrupt/missing settings — fall back to the default.
        }

        return Right;
    }

    public static void Save(string position)
    {
        var trimmed = position.Trim();
        string normalized;
        if (trimmed.Equals(Left, StringComparison.OrdinalIgnoreCase))
            normalized = Left;
        else if (trimmed.Equals(Right, StringComparison.OrdinalIgnoreCase))
            normalized = Right;
        else
            return;

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
        var trimmed = raw.Trim();
        if (trimmed.Equals(Left, StringComparison.OrdinalIgnoreCase))
            return Left;
        if (trimmed.Equals(Right, StringComparison.OrdinalIgnoreCase))
            return Right;

        return Right;
    }

    internal static bool IsValid(string position) =>
        position is Left or Right;

    private static bool TryLoadFromFile(string path, out string value)
    {
        value = string.Empty;
        if (!File.Exists(path))
            return false;

        value = Normalize(File.ReadAllText(path));
        return true;
    }
}
