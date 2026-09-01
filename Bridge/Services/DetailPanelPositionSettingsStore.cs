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
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "detail-panel-position.txt");

    public static string Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, Right, TryParsePosition);

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

        ScalarSettingStore.Save(SettingsFile, normalized);
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

    private static bool TryParsePosition(string raw, out string value)
    {
        value = Normalize(raw);
        return true;
    }
}
