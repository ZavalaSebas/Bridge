using System.IO;
using Bridge.Core.Enums;

namespace Bridge.Services;

/// <summary>
/// Persists each library view's vertical scroll offset in a small text file
/// under AppDataPath, so switching views (or reopening Bridge) comes back to
/// where you were scrolled instead of resetting to the top. Same pattern as
/// ViewModeSettingsStore/ThemeManager: a plain file, tolerant of corruption,
/// and saving never crashes the app. Keys are the ViewMode names.
/// </summary>
public static class ScrollPositionSettingsStore
{
    private static string SettingsFile => Path.Combine(Config.AppDataPath, "scrollpositions.txt");

    // The Table view's auto-fill Name column width is persisted too, so opening
    // Bridge straight into Table already has the correct column size from the
    // first frame instead of resizing visibly on startup. Keyed separately from
    // the per-view scroll offsets (it isn't a view name).
    private const string TableNameWidthKey = "TableNameWidth";

    public static double Load(string view)
    {
        return LoadValue(NormalizeLegacyViewKey(view));
    }

    public static void Save(string view, double offset)
    {
        SaveValue(NormalizeLegacyViewKey(view), offset);
    }

    /// <summary>Maps legacy view keys (e.g. Grid) to current enum names (Covers).</summary>
    internal static string NormalizeLegacyViewKey(string view)
    {
        var trimmed = view.Trim();
        if (trimmed.Equals("Grid", StringComparison.OrdinalIgnoreCase))
            return nameof(ViewMode.Covers);

        if (Enum.TryParse<ViewMode>(trimmed, ignoreCase: true, out var mode))
            return mode.ToString();

        return trimmed;
    }

    public static double LoadTableNameWidth()
    {
        return LoadValue(TableNameWidthKey);
    }

    public static void SaveTableNameWidth(double width)
    {
        SaveValue(TableNameWidthKey, width);
    }

    private static double LoadValue(string key)
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return 0;

            foreach (var line in File.ReadAllLines(SettingsFile))
            {
                var idx = line.IndexOf('=');
                if (idx > 0
                    && line[..idx].Equals(key, StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(line[(idx + 1)..], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    return value;
                }
            }
        }
        catch
        {
            // Corrupt/missing settings — fall back to the default.
        }

        return 0;
    }

    private static void SaveValue(string key, double value)
    {
        try
        {
            if (value < 0)
                return;

            Directory.CreateDirectory(Config.AppDataPath);
            var lines = File.Exists(SettingsFile)
                ? File.ReadAllLines(SettingsFile).Where(l => !l.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)).ToList()
                : [];
            lines.Add($"{key}={value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            File.WriteAllLines(SettingsFile, lines);
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
