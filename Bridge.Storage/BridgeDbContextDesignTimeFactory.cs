using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bridge.Storage;

/// <summary>
/// Lets `dotnet ef` scaffold migrations without launching the WPF app. Uses
/// BRIDGE_DEV_DB when set, otherwise a temp file under %TEMP% — never the live
/// AppData bridge.db unless explicitly pointed there.
/// </summary>
public class BridgeDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BridgeDbContext>
{
    public BridgeDbContext CreateDbContext(string[] args)
    {
        var databasePath = Environment.GetEnvironmentVariable("BRIDGE_DEV_DB");
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            databasePath = Path.Combine(Path.GetTempPath(), "bridge-design.db");
        }

        var options = new DbContextOptionsBuilder<BridgeDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new BridgeDbContext(options);
    }
}
