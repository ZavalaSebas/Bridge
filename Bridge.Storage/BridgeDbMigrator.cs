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
/// case (tables present, no history) and baselines it first: it creates the
/// history table and records the current migrations as already applied, so
/// Migrate() becomes a no-op for those DBs and only future migrations apply.
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

        // Baseline: the DB already has the schema (EnsureCreated era) but no
        // migration history. Mark the current migrations as applied without
        // running their SQL — the tables already exist. Require the core Bridge
        // tables so a partial/corrupt DB is not silently marked as migrated.
        if (applied.Count == 0 && migrations.Count > 0 && DatabaseHasBaselineSchema(context))
        {
            var history = context.GetService<IHistoryRepository>();
            history.CreateIfNotExists();
            var productVersion = typeof(BridgeDbContext).Assembly.GetName().Version?.ToString() ?? string.Empty;
            foreach (var migrationId in context.Database.GetMigrations())
            {
                context.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow(migrationId, productVersion)));
            }
        }

        context.Database.Migrate();
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
