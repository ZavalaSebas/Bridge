using System.IO;

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

    public static double Load(string view)
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return 0;

            foreach (var line in File.ReadAllLines(SettingsFile))
            {
                var idx = line.IndexOf('=');
                if (idx > 0
                    && line[..idx].Equals(view, StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(line[(idx + 1)..], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var offset))
                {
                    return offset;
                }
            }
        }
        catch
        {
            // Corrupt/missing settings — fall back to the top.
        }

        return 0;
    }

    public static void Save(string view, double offset)
    {
        try
        {
            if (offset < 0)
                return;

            Directory.CreateDirectory(Config.AppDataPath);
            var lines = File.Exists(SettingsFile)
                ? File.ReadAllLines(SettingsFile).Where(l => !l.StartsWith(view + "=", StringComparison.OrdinalIgnoreCase)).ToList()
                : [];
            lines.Add($"{view}={offset.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            File.WriteAllLines(SettingsFile, lines);
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }
}
