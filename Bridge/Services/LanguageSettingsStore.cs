using System.Globalization;
using System.IO;
using Bridge.Resources;

namespace Bridge.Services;

public enum AppLanguage
{
    English,
    Spanish
}

/// <summary>
/// Persists the UI language in a small text file under AppDataPath, same
/// pattern as <see cref="UpdateChannelSettingsStore"/>. English is the default.
/// </summary>
public static class LanguageSettingsStore
{
    private static string SettingsFile => Config.LanguageFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "language.txt");

    public static AppLanguage Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile, out var saved) ||
                TryLoadFromFile(LegacySettingsFile, out saved))
            {
                return saved;
            }
        }
        catch
        {
            // Corrupt/missing settings — fall back to the default.
        }

        return AppLanguage.English;
    }

    public static void Save(AppLanguage language)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, language.ToString());
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static bool TryLoadFromFile(string path, out AppLanguage language)
    {
        language = AppLanguage.English;
        return File.Exists(path) &&
            Enum.TryParse<AppLanguage>(File.ReadAllText(path).Trim(), out language);
    }

    public static CultureInfo CultureFor(AppLanguage language) =>
        language switch
        {
            AppLanguage.Spanish => CultureInfo.GetCultureInfo("es"),
            _ => CultureInfo.GetCultureInfo("en")
        };

    /// <summary>
    /// Applies the saved language to the current thread and resource lookups.
    /// Call once at startup before any UI is created.
    /// </summary>
    public static void ApplySavedLanguage()
    {
        var culture = CultureFor(Load());
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        StringsResourceManager.Culture = culture;
    }
}
