using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bridge.Storage;

/// <summary>
/// Lets `dotnet ef` scaffold migrations without launching the WPF app: the
/// runtime context is built in App.xaml.cs with the real %LOCALAPPDATA% path,
/// but EF tooling needs its own way to construct one. Points at the same
/// AppData location the app uses so `dotnet ef migrations add`/`database update`
/// operate on the same DB the running app would.
/// </summary>
public class BridgeDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BridgeDbContext>
{
    public BridgeDbContext CreateDbContext(string[] args)
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bridge");
        var databasePath = Path.Combine(appDataPath, "bridge.db");

        var options = new DbContextOptionsBuilder<BridgeDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new BridgeDbContext(options);
    }
}
