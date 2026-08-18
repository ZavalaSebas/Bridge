using Bridge.Settings;

namespace Bridge.Services;

/// <summary>
/// Numbered AppData migration steps. Each method upgrades from version N to N+1
/// and must be safe to re-run (idempotent) so a failed step can retry on the
/// next launch without corrupting user data.
/// </summary>
internal static class AppDataMigrations
{
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

        // Obsolete paths from pre-layout era; safe no-ops when absent.
        ctx.DeleteFileIfExists("settings.json");
    }
}
