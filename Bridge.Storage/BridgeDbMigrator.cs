using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bridge.Storage;

/// <summary>
/// Applies EF Core migrations to bridge.db, including the one-time baseline of
/// databases created by the pre-migrations era (EnsureCreated + raw-SQL
/// EnsureColumn). An EnsureCreated DB has all 14 tables but no
/// __EFMigrationsHistory table, so a bare Database.Migrate() would try to
/// recreate tables that already exist and fail. MigrateToLatest detects that
/// case (tables present, no history) and baselines only <c>InitialCreate</c>
/// first — later migrations (unique indexes, runtime-flag cleanup, etc.) still
/// run via <see cref="DatabaseFacade.Migrate"/>.
/// </summary>
public static class BridgeDbMigrator
{
    private static readonly string[] RequiredBaselineTables =
    [
        "Games",
        "GameSources",
        "Genres"
    ];

    /// <summary>Migrates the DB to the latest schema, baselining pre-migrations DBs first.</summary>
    public static void MigrateToLatest(this BridgeDbContext context)
    {
        var applied = context.Database.GetAppliedMigrations().ToList();
        var migrations = context.Database.GetMigrations().ToList();

        // Fast path for the common case (every startup after the first): the
        // database already has a migration history and every known migration is
        // applied. Returning here skips Database.Migrate(), which otherwise
        // re-queries the history table and spins up the whole migration pipeline on
        // each launch even when the schema is already current.
        //
        // The HasPendingModelChanges() guard is essential, not just an optimization
        // gate. Migrate() validates the runtime model against the migration snapshot
        // even on a no-op run, so skipping it unconditionally would let a build that
        // forgot a required migration run against a stale schema until normal queries
        // fail. Falling through whenever the model has drifted preserves that
        // validation. (Caveat for the future: a UseSeeding seeder would also run on
        // no-op migrations, so this shortcut would have to account for it if one is
        // ever added.)
        if (applied.Count > 0 &&
            migrations.All(applied.Contains) &&
            !context.Database.HasPendingModelChanges())
        {
            return;
        }

        // Baseline: the DB already has the InitialCreate schema (EnsureCreated
        // era) but no migration history. Record only InitialCreate as applied —
        // do NOT mark later migrations applied, or AddUniqueIndexes and friends
        // would never run their SQL on real user databases.
        if (applied.Count == 0 && migrations.Count > 0 && DatabaseHasBaselineSchema(context))
        {
            var history = context.GetService<IHistoryRepository>();
            history.CreateIfNotExists();
            var productVersion = typeof(BridgeDbContext).Assembly.GetName().Version?.ToString() ?? string.Empty;

            foreach (var migration in DetermineBaselinedMigrations(context, migrations))
            {
                context.Database.ExecuteSqlRaw(
                    history.GetInsertScript(new HistoryRow(migration, productVersion)));
            }
        }

        context.Database.Migrate();
    }

    // Legacy EnsureCreated databases differ by era: the oldest lack unique-name
    // indexes and still store runtime flags; a recent EnsureCreated against the
    // current model may already have both migrations' effects baked in. Baseline
    // every migration whose schema is already present so Migrate() only runs
    // what's actually missing — never re-create indexes that exist.
    private static IEnumerable<string> DetermineBaselinedMigrations(
        BridgeDbContext context,
        IReadOnlyList<string> migrations)
    {
        var baselined = new List<string>();

        var initial = migrations.FirstOrDefault(m => m.EndsWith("InitialCreate", StringComparison.Ordinal));
        if (initial is not null)
            baselined.Add(initial);

        var addUnique = migrations.FirstOrDefault(m => m.EndsWith("AddUniqueIndexes", StringComparison.Ordinal));
        if (addUnique is not null && HasUniqueNameIndexes(context))
            baselined.Add(addUnique);

        var ignoreRuntime = migrations.FirstOrDefault(m => m.EndsWith("IgnoreGameRuntimeFlags", StringComparison.Ordinal));
        if (ignoreRuntime is not null && RuntimeFlagsDropped(context))
            baselined.Add(ignoreRuntime);

        var addTimeToBeat = migrations.FirstOrDefault(m => m.EndsWith("AddGameTimeToBeat", StringComparison.Ordinal));
        if (addTimeToBeat is not null && TimeToBeatColumnsPresent(context))
            baselined.Add(addTimeToBeat);

        var addMetadataSync = migrations.FirstOrDefault(m => m.EndsWith("AddMetadataSyncMarkers", StringComparison.Ordinal));
        if (addMetadataSync is not null && MetadataSyncMarkersColumnsPresent(context))
            baselined.Add(addMetadataSync);

        var addStatisticsHistory = migrations.FirstOrDefault(m => m.EndsWith("AddStatisticsHistory", StringComparison.Ordinal));
        if (addStatisticsHistory is not null && StatisticsHistoryColumnsPresent(context))
            baselined.Add(addStatisticsHistory);

        var addLogo = migrations.FirstOrDefault(m => m.EndsWith("AddGameLogo", StringComparison.Ordinal));
        if (addLogo is not null && GamesColumnExists(context, "LogoImage"))
            baselined.Add(addLogo);

        return baselined;
    }

    private static bool HasUniqueNameIndexes(BridgeDbContext context)
    {
        using var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'IX_Tags_Name'";
            var sql = command.ExecuteScalar() as string;
            return !string.IsNullOrWhiteSpace(sql) &&
                   sql.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (!wasOpen)
                connection.Close();
        }
    }

    private static bool RuntimeFlagsDropped(BridgeDbContext context)
    {
        using var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(Games)";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1).Equals("IsRunning", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
        finally
        {
            if (!wasOpen)
                connection.Close();
        }
    }

    private static bool TimeToBeatColumnsPresent(BridgeDbContext context)
    {
        return GamesColumnExists(context, "TimeToBeatMainSeconds");
    }

    private static bool MetadataSyncMarkersColumnsPresent(BridgeDbContext context)
    {
        return GamesColumnExists(context, "LinksSyncedAt");
    }

    private static bool StatisticsHistoryColumnsPresent(BridgeDbContext context)
    {
        return GamesColumnExists(context, "CompletedAt") && GamesColumnExists(context, "PlaySessions");
    }

    private static bool GamesColumnExists(BridgeDbContext context, string columnName)
    {
        using var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(Games)";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        finally
        {
            if (!wasOpen)
                connection.Close();
        }
    }

    private static bool DatabaseHasBaselineSchema(BridgeDbContext context)
    {
        using var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            using var reader = command.ExecuteReader();
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }

            return RequiredBaselineTables.All(tables.Contains);
        }
        finally
        {
            if (!wasOpen)
            {
                connection.Close();
            }
        }
    }
}
