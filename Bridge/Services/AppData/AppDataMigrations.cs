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
        ("theme.json", new[] { "config", "theme.json" }),
        ("viewmode.txt", new[] { "config", "viewmode.txt" }),
        ("scrollpositions.txt", new[] { "config", "scrollpositions.txt" }),
        ("update-channel.txt", new[] { "config", "update-channel.txt" }),
        ("language.txt", new[] { "config", "language.txt" }),
        ("startup.txt", new[] { "config", "startup.txt" }),
        ("tray-icon.txt", new[] { "config", "tray-icon.txt" }),
        ("keep-selection-across-views.txt", new[] { "config", "keep-selection-across-views.txt" }),
        ("detail-panel-position.txt", new[] { "config", "detail-panel-position.txt" }),
        ("detail-section-position.txt", new[] { "config", "detail-section-position.txt" }),
        ("sidebar-translucent.txt", new[] { "config", "sidebar-translucent.txt" }),
        ("translucent-background.txt", new[] { "config", "translucent-background.txt" }),
        ("whats-new-seen.txt", new[] { "config", "whats-new-seen.txt" }),
        ("rom-scan-folder.txt", new[] { "config", "rom-scan-folder.txt" }),
        ("installed-scan-folder.txt", new[] { "config", "installed-scan-folder.txt" }),
        ("setup-complete.txt", new[] { "config", "setup-complete.txt" }),
        ("auto-apply-cheats-on-launch.txt", new[] { "config", "auto-apply-cheats-on-launch.txt" }),
        ("user-profile.json", new[] { "config", "user-profile.json" }),
        ("game-display-preferences.json", new[] { "config", "game-display-preferences.json" }),
        ("igdb-settings.json", new[] { "config", "secrets", "igdb-settings.json" }),
        ("steamgriddb-settings.json", new[] { "config", "secrets", "steamgriddb-settings.json" }),
        ("retroachievements-settings.json", new[] { "config", "secrets", "retroachievements-settings.json" }),
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
                    new[] { move.LegacyFileName },
                    move.DestinationSegments,
                    new[] { "config", "migration-conflicts" });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.LogException(ex);
            }
        }
    }
}
