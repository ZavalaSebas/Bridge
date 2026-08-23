using Bridge.Settings;

namespace Bridge.Services;

/// <summary>
/// Numbered AppData migration steps. Each method upgrades from version N to N+1
/// and must be safe to re-run (idempotent) so a failed step can retry on the
/// next launch without corrupting user data.
/// </summary>
internal static class AppDataMigrations
{
    private static readonly (string LegacyFileName, string[] DestinationSegments)[] V2Moves =
    [
        ("theme.json", ["config", "theme.json"]),
        ("viewmode.txt", ["config", "viewmode.txt"]),
        ("scrollpositions.txt", ["config", "scrollpositions.txt"]),
        ("update-channel.txt", ["config", "update-channel.txt"]),
        ("language.txt", ["config", "language.txt"]),
        ("startup.txt", ["config", "startup.txt"]),
        ("tray-icon.txt", ["config", "tray-icon.txt"]),
        ("keep-selection-across-views.txt", ["config", "keep-selection-across-views.txt"]),
        ("detail-panel-position.txt", ["config", "detail-panel-position.txt"]),
        ("detail-section-position.txt", ["config", "detail-section-position.txt"]),
        ("sidebar-translucent.txt", ["config", "sidebar-translucent.txt"]),
        ("translucent-background.txt", ["config", "translucent-background.txt"]),
        ("whats-new-seen.txt", ["config", "whats-new-seen.txt"]),
        ("rom-scan-folder.txt", ["config", "rom-scan-folder.txt"]),
        ("installed-scan-folder.txt", ["config", "installed-scan-folder.txt"]),
        ("setup-complete.txt", ["config", "setup-complete.txt"]),
        ("auto-apply-cheats-on-launch.txt", ["config", "auto-apply-cheats-on-launch.txt"]),
        ("user-profile.json", ["config", "user-profile.json"]),
        ("game-display-preferences.json", ["config", "game-display-preferences.json"]),
        ("igdb-settings.json", ["config", "secrets", "igdb-settings.json"]),
        ("steamgriddb-settings.json", ["config", "secrets", "steamgriddb-settings.json"]),
        ("retroachievements-settings.json", ["config", "secrets", "retroachievements-settings.json"]),
    ];

    /// <summary>
    /// v0 → v1: ensure the standard folder layout, consolidate legacy paths, and
    /// persist one-time format fixes that used to happen only at load time.
    /// </summary>
    public static void V1_InitializeLayoutAndLegacyCleanup(AppDataMigrationContext ctx)
    {
        ctx.EnsureDirectory("image-cache");
        ctx.EnsureDirectory("emulators");
        ctx.EnsureDirectory("emulator-downloads");
        ctx.EnsureDirectory("logs");

        // Older Bridge builds (or unrelated "Bridge" folders) used PascalCase.
        ctx.MergeDirectoryContentsIfExists(["ImageCache"], ["image-cache"]);

        ViewModeSettingsStore.MigrateLegacyPersistedNames(ctx);
        ScrollPositionSettingsStore.MigrateLegacyViewKeys(ctx);
        IgdbSettingsStore.MigratePlainTextToProtectedFormat(ctx);
        SteamGridDbSettingsStore.MigratePlainTextToProtectedFormat(ctx);

        // Obsolete paths from pre-layout era; safe no-ops when absent.
        ctx.DeleteFileIfExists("settings.json");
    }

    /// <summary>
    /// v1 → v2: move loose config files under config/ (and secrets under
    /// config/secrets/) while preserving conflicts for manual inspection.
    /// </summary>
    public static void V2_MoveLooseConfigFilesToConfigDirectory(AppDataMigrationContext ctx)
    {
        ctx.EnsureDirectory("config");
        ctx.EnsureDirectory("config", "secrets");

        foreach (var move in V2Moves)
        {
            try
            {
                ctx.MoveFileToIfExists(
                    [move.LegacyFileName],
                    move.DestinationSegments,
                    ["config", "migration-conflicts"]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.LogException(ex);
            }
        }
    }
}
