using System.IO;

namespace Bridge.Services;

/// <summary>
/// Shared load/save mechanics for the family of single-value preference files
/// (a bool, enum, or short string persisted as the entire contents of a small
/// text file under the config directory).
///
/// <para>
/// Roughly two dozen <c>*SettingsStore</c> classes used to copy-paste the same
/// four things: create the config directory, try the primary file then a legacy
/// path, swallow any IO/parse error and fall back to a default, and never let a
/// failed write crash the app. That boilerplate now lives here once. Each store
/// keeps its <b>own</b> parse delegate, so per-store semantics — case handling,
/// legacy-name normalization, subset validation — are preserved exactly rather
/// than flattened into a single shared parser.
/// </para>
///
/// <para>
/// The helper deliberately takes explicit paths (it never reaches into
/// <see cref="Config"/>) so it stays a pure, unit-testable unit that tests can
/// point at temp files instead of the real AppData location.
/// </para>
/// </summary>
internal static class ScalarSettingStore
{
    /// <summary>
    /// Parses the trimmed file contents into <typeparamref name="T"/>. Returns
    /// false when the raw text isn't a valid value, so the caller falls through
    /// to the legacy file and ultimately the default.
    /// </summary>
    internal delegate bool ValueParser<T>(string raw, out T value);

    /// <summary>
    /// Loads from <paramref name="primaryPath"/>, then <paramref name="legacyPath"/>
    /// (may be null when a store has no legacy location), returning
    /// <paramref name="fallback"/> when neither yields a valid value or any error
    /// occurs. The raw contents are trimmed before <paramref name="parse"/> sees them.
    /// </summary>
    public static T Load<T>(string primaryPath, string? legacyPath, T fallback, ValueParser<T> parse)
    {
        try
        {
            if (TryLoad(primaryPath, parse, out var value) ||
                TryLoad(legacyPath, parse, out value))
            {
                return value;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Failed to load setting from '{primaryPath}'.", ex);
        }

        return fallback;
    }

    /// <summary>
    /// Writes <paramref name="content"/> as the whole file at <paramref name="path"/>,
    /// creating the containing directory first. Persisting a preference must never
    /// crash the app, so failures are logged and swallowed.
    /// </summary>
    public static void Save(string path, string content)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, content);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Failed to save setting to '{path}'.", ex);
        }
    }

    private static bool TryLoad<T>(string? path, ValueParser<T> parse, out T value)
    {
        value = default!;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        return parse(File.ReadAllText(path).Trim(), out value);
    }
}
