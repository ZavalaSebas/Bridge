using System.IO.Compression;
using Bridge.Services;
using Microsoft.Data.Sqlite;

namespace Bridge.Tests.Services;

public class AppDataBackupServiceTests : IDisposable
{
    private readonly string _appDataPath;
    private readonly string _zipPath;

    public AppDataBackupServiceTests()
    {
        _appDataPath = Path.Combine(Path.GetTempPath(), $"bridge-backup-src-{Guid.NewGuid()}");
        _zipPath = Path.Combine(Path.GetTempPath(), $"bridge-backup-out-{Guid.NewGuid()}.zip");
        Directory.CreateDirectory(_appDataPath);
    }

    [Fact]
    public void CreateBackup_IncludesDatabaseAndSettings()
    {
        var databasePath = Path.Combine(_appDataPath, "bridge.db");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE test (id INTEGER PRIMARY KEY)";
            command.ExecuteNonQuery();
        }

        File.WriteAllText(Path.Combine(_appDataPath, "theme.json"), "{\"accent\":\"#007ACC\"}");
        File.WriteAllText(Path.Combine(_appDataPath, "viewmode.txt"), "List");

        var result = AppDataBackupService.CreateBackup(_zipPath, _appDataPath);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(_zipPath));

        using var archive = ZipFile.OpenRead(_zipPath);
        Assert.Contains(archive.Entries, e => e.FullName == "theme.json");
        Assert.Contains(archive.Entries, e => e.FullName == "viewmode.txt");
        Assert.Contains(archive.Entries, e => e.FullName == "backup-manifest.json");
        Assert.Contains(archive.Entries, e => e.FullName == "bridge.db");
    }

    [Fact]
    public void CreateBackup_IncludesImageCacheFolder()
    {
        var cacheDir = Path.Combine(_appDataPath, "image-cache");
        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(Path.Combine(cacheDir, "cover.bin"), "cached");

        var result = AppDataBackupService.CreateBackup(_zipPath, _appDataPath);

        Assert.True(result.Success, result.Message);
        using var archive = ZipFile.OpenRead(_zipPath);
        Assert.Contains(archive.Entries, e => e.FullName == "image-cache/cover.bin");
    }

    [Fact]
    public void ValidateBackupArchive_RejectsMissingDatabase()
    {
        ZipFile.CreateFromDirectory(_appDataPath, _zipPath);

        var result = AppDataBackupService.ValidateBackupArchive(_zipPath);

        Assert.False(result.Success);
    }

    [Fact]
    public void ScheduleRestoreAndApplyPendingRestore_ReplacesLibraryData()
    {
        var databasePath = Path.Combine(_appDataPath, "bridge.db");
        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE old (id INTEGER PRIMARY KEY)";
            command.ExecuteNonQuery();
        }

        File.WriteAllText(Path.Combine(_appDataPath, "viewmode.txt"), "List");
        var backupResult = AppDataBackupService.CreateBackup(_zipPath, _appDataPath);
        Assert.True(backupResult.Success, backupResult.Message);

        File.WriteAllText(Path.Combine(_appDataPath, "viewmode.txt"), "Covers");
        var scheduleResult = AppDataBackupService.ScheduleRestore(_zipPath, _appDataPath);
        Assert.True(scheduleResult.Success, scheduleResult.Message);

        SqliteConnection.ClearAllPools();
        var applied = AppDataBackupService.ApplyPendingRestore(_appDataPath);

        Assert.True(applied);
        Assert.Equal("List", File.ReadAllText(Path.Combine(_appDataPath, "viewmode.txt")).Trim());
        Assert.True(BridgeDatabaseRecovery.IsValidSqliteFile(databasePath));
        Assert.False(File.Exists(AppDataBackupService.RestorePendingMarkerPath(_appDataPath)));
        Assert.False(Directory.Exists(AppDataBackupService.RestoreStagingPath(_appDataPath)));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_zipPath))
            File.Delete(_zipPath);
        if (Directory.Exists(_appDataPath))
            Directory.Delete(_appDataPath, recursive: true);
    }
}
