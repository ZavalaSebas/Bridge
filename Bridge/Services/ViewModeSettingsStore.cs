using System.IO;
using Bridge.Core.Enums;

namespace Bridge.Services;

/// <summary>
/// Persists the last-selected library view (List/Covers/Table) in a small text
/// file under AppDataPath, so reopening Bridge restores the view you were using.
/// Same pattern as ThemeManager: a plain file, tolerant of corruption, and
/// saving never crashes the app.
/// </summary>
public static class ViewModeSettingsStore
{
    private static string SettingsFile => Config.ViewModeFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "viewmode.txt");

    /// <summary>
    /// Maps legacy persisted enum names to the current <see cref="ViewMode"/> values.
    /// </summary>
    internal static string NormalizeLegacyName(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Equals("Grid", StringComparison.OrdinalIgnoreCase))
            return nameof(ViewMode.Covers);

        if (Enum.TryParse<ViewMode>(trimmed, ignoreCase: true, out var mode))
            return mode.ToString();

        return trimmed;
    }

    public static ViewMode Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, ViewMode.List,
            static (string raw, out ViewMode value) => Enum.TryParse(NormalizeLegacyName(raw), out value));

    public static void Save(ViewMode mode) =>
        ScalarSettingStore.Save(SettingsFile, mode.ToString());

    /// <summary>
    /// One-time on-disk fix for legacy "Grid" persisted before ViewMode.Covers.
    /// Called from <see cref="AppDataMigrator"/>; load-time normalization remains
    /// as a fallback for corrupt partial writes.
    /// </summary>
    internal static void MigrateLegacyPersistedNames(AppDataMigrationContext ctx)
    {
        ctx.ReplaceFileTextIfExists(["viewmode.txt"], static text =>
        {
            var normalized = NormalizeLegacyName(text);
            return Enum.TryParse<ViewMode>(normalized, out var mode) ? mode.ToString() : normalized;
        });
    }
}
