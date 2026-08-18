namespace Bridge.Resources;

/// <summary>
/// English UI strings. Backed by <c>Strings.resx</c> for future localization.
/// </summary>
public static class Strings
{
    public static string Cancel => Get(nameof(Cancel));
    public static string Save => Get(nameof(Save));
    public static string Close => Get(nameof(Close));
    public static string Select => Get(nameof(Select));
    public static string Search => Get(nameof(Search));
    public static string Name => Get(nameof(Name));
    public static string ClientId => Get(nameof(ClientId));
    public static string ClientSecret => Get(nameof(ClientSecret));
    public static string InstallDirectory => Get(nameof(InstallDirectory));
    public static string CheckForUpdates => Get(nameof(CheckForUpdates));
    public static string Searching => Get(nameof(Searching));
    public static string NoResults => Get(nameof(NoResults));
    public static string SearchFailed => Get(nameof(SearchFailed));
    public static string ResultsCountFormat => Get(nameof(ResultsCountFormat));
    public static string EmulationSettingsTitle => Get(nameof(EmulationSettingsTitle));
    public static string EmulationManagedRetroArchHeading => Get(nameof(EmulationManagedRetroArchHeading));
    public static string EmulationManagedRetroArchDescription => Get(nameof(EmulationManagedRetroArchDescription));
    public static string EmulationSupportedSystems => Get(nameof(EmulationSupportedSystems));
    public static string IgdbSettingsTitle => Get(nameof(IgdbSettingsTitle));
    public static string IgdbSettingsHelp => Get(nameof(IgdbSettingsHelp));
    public static string ConfigureEmulatorTitle => Get(nameof(ConfigureEmulatorTitle));
    public static string ExecutableRelativeOrAbsolute => Get(nameof(ExecutableRelativeOrAbsolute));
    public static string ArgumentsRomPathPlaceholder => Get(nameof(ArgumentsRomPathPlaceholder));
    public static string RomExtensionsCommaSeparated => Get(nameof(RomExtensionsCommaSeparated));
    public static string SearchImagesTitle => Get(nameof(SearchImagesTitle));
    public static string SearchImagesQueryName => Get(nameof(SearchImagesQueryName));
    public static string SearchImagesQueryTooltip => Get(nameof(SearchImagesQueryTooltip));
    public static string CustomThemeColorTitle => Get(nameof(CustomThemeColorTitle));
    public static string CustomThemeColorDescription => Get(nameof(CustomThemeColorDescription));
    public static string ViewList => Get(nameof(ViewList));
    public static string ViewCovers => Get(nameof(ViewCovers));
    public static string ViewTable => Get(nameof(ViewTable));
    public static string SearchGames => Get(nameof(SearchGames));
    public static string SearchGamesTooltip => Get(nameof(SearchGamesTooltip));
    public static string BridgeMenu => Get(nameof(BridgeMenu));
    public static string FilterPresets => Get(nameof(FilterPresets));
    public static string SortSettings => Get(nameof(SortSettings));
    public static string GroupSettings => Get(nameof(GroupSettings));
    public static string SelectRandomGame => Get(nameof(SelectRandomGame));
    public static string Library => Get(nameof(Library));
    public static string Statistics => Get(nameof(Statistics));

    private static string Get(string key) =>
        StringsResourceManager.GetString(key) ?? key;
}
