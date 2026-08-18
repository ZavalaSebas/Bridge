using Bridge.Core.Entities;
using Bridge.Storage;
using Microsoft.EntityFrameworkCore;

namespace Bridge.Tests.Storage;

/// <summary>
/// Migration behavior tests: a fresh DB gets the schema applied, and a
/// pre-migrations DB (created by the old EnsureCreated era — all tables, no
/// __EFMigrationsHistory) is baselined so its existing schema + data survive
/// and future migrations apply on top.
/// </summary>
public class MigrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContextOptions<BridgeDbContext> _options;

    public MigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"bridge-migration-{Guid.NewGuid()}.db");
        _options = new DbContextOptionsBuilder<BridgeDbContext>()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;
    }

    [Fact]
    public void MigrateToLatest_FreshDatabase_CreatesSchema()
    {
        using var context = new BridgeDbContext(_options);
        context.MigrateToLatest();

        var applied = context.Database.GetAppliedMigrations();
        Assert.Contains(applied, m => m.EndsWith("InitialCreate", StringComparison.Ordinal));
        Assert.Contains(applied, m => m.EndsWith("AddUniqueIndexes", StringComparison.Ordinal));

        context.Genres.Add(new Genre { Name = "Action" });
        context.SaveChanges();
        Assert.Single(context.Genres.ToList());
    }

    [Fact]
    public void MigrateToLatest_PreMigrationDatabase_IsBaselinedAndKeepsData()
    {
        // Simulate the pre-migrations era: EnsureCreated built the schema with no
        // __EFMigrationsHistory table.
        using (var legacy = new BridgeDbContext(_options))
        {
            legacy.Database.EnsureCreated();
            legacy.Genres.Add(new Genre { Name = "RPG" });
            legacy.SaveChanges();
        }

        using var context = new BridgeDbContext(_options);
        context.MigrateToLatest();

        // Data survived the baseline.
        Assert.Equal("RPG", context.Genres.Single().Name);

        // The initial migration is recorded as applied (so a future migration
        // applies on top instead of failing to recreate the tables).
        var applied = context.Database.GetAppliedMigrations();
        Assert.Contains(applied, m => m.EndsWith("InitialCreate", StringComparison.Ordinal));
        Assert.Contains(applied, m => m.EndsWith("AddUniqueIndexes", StringComparison.Ordinal));
    }

    [Fact]
    public void AddUniqueIndexes_RejectsDuplicateGenreNames()
    {
        using var context = new BridgeDbContext(_options);
        context.MigrateToLatest();

        context.Genres.Add(new Genre { Name = "Action" });
        context.SaveChanges();

        context.Genres.Add(new Genre { Name = "Action" });
        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
