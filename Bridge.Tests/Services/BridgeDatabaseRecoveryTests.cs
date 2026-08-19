using Bridge.Services;

namespace Bridge.Tests.Services;

public class BridgeDatabaseRecoveryTests : IDisposable
{
    private readonly string _appDataPath;
    private readonly string _databasePath;

    public BridgeDatabaseRecoveryTests()
    {
        _appDataPath = Path.Combine(Path.GetTempPath(), $"bridge-db-recovery-{Guid.NewGuid()}");
        Directory.CreateDirectory(_appDataPath);
        _databasePath = Path.Combine(_appDataPath, "bridge.db");
    }

    [Fact]
    public void IsValidSqliteFile_ReturnsFalseForGarbageBytes()
    {
        File.WriteAllBytes(_databasePath, [0x01, 0x02, 0x03]);

        Assert.False(BridgeDatabaseRecovery.IsValidSqliteFile(_databasePath));
    }

    [Fact]
    public void TryRestoreFromUpdateBackup_RestoresWhenMainFileIsCorrupt()
    {
        File.WriteAllBytes(_databasePath, [0x01, 0x02]);
        File.WriteAllBytes(_databasePath + ".bak-update", CreateMinimalSqliteHeader());

        var result = BridgeDatabaseRecovery.TryRestoreFromUpdateBackup(_databasePath);

        Assert.Equal(BridgeDatabaseRecovery.RecoveryResult.RestoredFromUpdateBackup, result);
        Assert.True(BridgeDatabaseRecovery.IsValidSqliteFile(_databasePath));
    }

    [Fact]
    public void TryRestoreFromUpdateBackup_NoOpWhenDatabaseIsValid()
    {
        File.WriteAllBytes(_databasePath, CreateMinimalSqliteHeader());

        var result = BridgeDatabaseRecovery.TryRestoreFromUpdateBackup(_databasePath);

        Assert.Equal(BridgeDatabaseRecovery.RecoveryResult.NotNeeded, result);
    }

    [Fact]
    public void TryRestoreFromUpdateBackup_ReturnsUnavailableWhenBackupMissing()
    {
        File.WriteAllBytes(_databasePath, [0x01, 0x02]);

        var result = BridgeDatabaseRecovery.TryRestoreFromUpdateBackup(_databasePath);

        Assert.Equal(BridgeDatabaseRecovery.RecoveryResult.BackupUnavailable, result);
    }

    private static byte[] CreateMinimalSqliteHeader() => "SQLite format 3\0"u8.ToArray();

    public void Dispose()
    {
        if (Directory.Exists(_appDataPath))
            Directory.Delete(_appDataPath, recursive: true);
    }
}
