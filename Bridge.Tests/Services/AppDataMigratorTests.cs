using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bridge.Metadata;
using Bridge.Services;
using Bridge.Settings;

namespace Bridge.Tests.Services;

public class AppDataMigratorTests : IDisposable
{
    private readonly string _appDataPath;

    public AppDataMigratorTests()
    {
        _appDataPath = Path.Combine(Path.GetTempPath(), $"bridge-appdata-{Guid.NewGuid()}");
        Directory.CreateDirectory(_appDataPath);
    }

    [Fact]
    public void MigrateToLatest_FreshInstall_CreatesLayoutAndVersionFile()
    {
        AppDataMigrator.MigrateToLatest(_appDataPath);

        Assert.Equal(AppDataMigrator.LatestVersion, AppDataMigrator.ReadVersion(_appDataPath));
        Assert.True(Directory.Exists(Path.Combine(_appDataPath, "image-cache")));
        Assert.True(Directory.Exists(Path.Combine(_appDataPath, "emulators")));
        Assert.True(Directory.Exists(Path.Combine(_appDataPath, "emulator-downloads")));
        Assert.True(Directory.Exists(Path.Combine(_appDataPath, "logs")));
        Assert.True(File.Exists(Path.Combine(_appDataPath, Config.AppDataVersionFileName)));
    }

    [Fact]
    public void MigrateToLatest_IsIdempotent()
    {
        AppDataMigrator.MigrateToLatest(_appDataPath);
        var versionAfterFirst = AppDataMigrator.ReadVersion(_appDataPath);

        AppDataMigrator.MigrateToLatest(_appDataPath);

        Assert.Equal(versionAfterFirst, AppDataMigrator.ReadVersion(_appDataPath));
        Assert.Equal(AppDataMigrator.LatestVersion, versionAfterFirst);
    }

    [Fact]
    public void MigrateToLatest_MergesLegacyImageCacheFolder()
    {
        var legacyDir = Path.Combine(_appDataPath, "ImageCache");
        Directory.CreateDirectory(legacyDir);
        File.WriteAllText(Path.Combine(legacyDir, "cover.bin"), "cached");

        AppDataMigrator.MigrateToLatest(_appDataPath);

        Assert.False(Directory.Exists(legacyDir));
        Assert.True(File.Exists(Path.Combine(_appDataPath, "image-cache", "cover.bin")));
    }

    [Fact]
    public void MigrateToLatest_RewritesLegacyViewModeName()
    {
        File.WriteAllText(Path.Combine(_appDataPath, "viewmode.txt"), "Grid");

        AppDataMigrator.MigrateToLatest(_appDataPath);

        Assert.Equal("Covers", File.ReadAllText(Path.Combine(_appDataPath, "viewmode.txt")).Trim());
    }

    [Fact]
    public void MigrateToLatest_RewritesLegacyScrollPositionKeys()
    {
        File.WriteAllLines(
            Path.Combine(_appDataPath, "scrollpositions.txt"),
            ["Grid=120.5", "List=40"]);

        AppDataMigrator.MigrateToLatest(_appDataPath);

        var lines = File.ReadAllLines(Path.Combine(_appDataPath, "scrollpositions.txt"));
        Assert.Contains("Covers=120.5", lines);
        Assert.Contains("List=40", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("Grid=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MigrateToLatest_ProtectsLegacyIgdbSettingsJson()
    {
        var settings = new IgdbSettings { ClientId = "abc", ClientSecret = "secret" };
        File.WriteAllText(
            Path.Combine(_appDataPath, "igdb-settings.json"),
            JsonSerializer.Serialize(settings));

        AppDataMigrator.MigrateToLatest(_appDataPath);

        var bytes = File.ReadAllBytes(Path.Combine(_appDataPath, "igdb-settings.json"));
        Assert.Equal((byte)1, bytes[0]);

        var json = Encoding.UTF8.GetString(
            ProtectedData.Unprotect(bytes[1..], "Bridge.IgdbSettings.v1"u8.ToArray(), DataProtectionScope.CurrentUser));
        var loaded = JsonSerializer.Deserialize<IgdbSettings>(json);
        Assert.Equal("abc", loaded!.ClientId);
        Assert.Equal("secret", loaded.ClientSecret);
    }

    [Fact]
    public void MigrateToLatest_ExistingDatabase_StillRunsPendingSteps()
    {
        File.WriteAllBytes(Path.Combine(_appDataPath, "bridge.db"), [0x01, 0x02]);
        File.WriteAllText(Path.Combine(_appDataPath, "viewmode.txt"), "Grid");

        AppDataMigrator.MigrateToLatest(_appDataPath);

        Assert.Equal(AppDataMigrator.LatestVersion, AppDataMigrator.ReadVersion(_appDataPath));
        Assert.Equal("Covers", File.ReadAllText(Path.Combine(_appDataPath, "viewmode.txt")).Trim());
        Assert.True(File.Exists(Path.Combine(_appDataPath, "bridge.db")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_appDataPath))
            Directory.Delete(_appDataPath, recursive: true);
    }
}
