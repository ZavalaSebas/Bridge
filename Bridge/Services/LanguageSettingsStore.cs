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

    public static AppLanguage Load() =>
        ScalarSettingStore.Load(SettingsFile, LegacySettingsFile, AppLanguage.English,
            static (string raw, out AppLanguage value) => Enum.TryParse(raw, out value));

    public static void Save(AppLanguage language) =>
        ScalarSettingStore.Save(SettingsFile, language.ToString());

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
