using System.IO;

namespace Bridge.Services;

/// <summary>
/// Persists where the game details panel is docked (Left/Right) for List view
/// and the compact info panel in Covers view.
/// </summary>
public static class DetailPanelPositionSettingsStore
{
    public const string Left = "Left";
    public const string Right = "Right";

    private static string SettingsFile => Config.DetailPanelPositionFilePath;

    public static string Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var saved = Normalize(File.ReadAllText(SettingsFile));
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
            Directory.CreateDirectory(Config.AppDataPath);
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

        // Legacy Top/Bottom values fall back to Right.
        return Right;
    }

    internal static bool IsValid(string position) =>
        position is Left or Right;
}
