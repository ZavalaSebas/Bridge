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

        return Right;
    }

    private static bool TryParsePosition(string raw, out string value)
    {
        value = Normalize(raw);
        return true;
    }
}
