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

    public static string Load() =>
        ScalarSettingStore.Load(SettingsFile, null, Compact, TryParseLayout);

    public static bool UsesCompact() => Load() == Compact;

    public static void Save(string layout) =>
        ScalarSettingStore.Save(SettingsFile, Normalize(layout));

    internal static string Normalize(string raw)
    {
        if (raw.Trim().Equals(Standard, StringComparison.OrdinalIgnoreCase))
            return Standard;

        return Compact;
    }

    private static bool TryParseLayout(string raw, out string value)
    {
        value = Normalize(raw);
        return true;
    }
}
