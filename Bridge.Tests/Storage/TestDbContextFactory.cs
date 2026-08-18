using Bridge.Storage;
using Microsoft.EntityFrameworkCore;

namespace Bridge.Tests.Storage;

/// <summary>
/// Wraps pre-built options so tests can construct repositories that expect
/// <see cref="IDbContextFactory{BridgeDbContext}"/> while still using a
/// real on-disk SQLite file per test class.
/// </summary>
internal sealed class TestDbContextFactory(DbContextOptions<BridgeDbContext> options)
    : IDbContextFactory<BridgeDbContext>
{
    public BridgeDbContext CreateDbContext() => new(options);
}
