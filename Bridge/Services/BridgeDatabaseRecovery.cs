using System.IO;

namespace Bridge.Services;

/// <summary>
/// Detects a corrupt <c>bridge.db</c> and restores the pre-update backup created
/// by <see cref="AppUpdateService"/> when possible.
/// </summary>
public static class BridgeDatabaseRecovery
{
    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();

    public enum RecoveryResult
    {
        NotNeeded,
        RestoredFromUpdateBackup,
        BackupUnavailable
    }

    public static string UpdateBackupPath => Config.DatabasePath + ".bak-update";

    public static bool IsValidSqliteFile(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            Span<byte> header = stackalloc byte[16];
            using var stream = File.OpenRead(path);
            return stream.Read(header) == 16 && header.SequenceEqual(SqliteHeader);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// When the database file exists but is not a SQLite database, quarantine it
    /// and copy the pre-update backup if that backup is valid.
    /// </summary>
    /// <param name="databasePath">Override for unit tests; production uses <see cref="Config.DatabasePath"/>.</param>
    public static RecoveryResult TryRestoreFromUpdateBackup(string? databasePath = null)
    {
        databasePath ??= Config.DatabasePath;
        var backupPath = databasePath + ".bak-update";

        if (!File.Exists(databasePath) || IsValidSqliteFile(databasePath))
            return RecoveryResult.NotNeeded;

        if (!IsValidSqliteFile(backupPath))
            return RecoveryResult.BackupUnavailable;

        QuarantineInvalidDatabase(databasePath);
        File.Copy(backupPath, databasePath, overwrite: true);
        DeleteSidecarFiles(databasePath);
        return RecoveryResult.RestoredFromUpdateBackup;
    }

    internal static void QuarantineInvalidDatabase(string databasePath)
    {
        DeleteSidecarFiles(databasePath);

        var quarantinePath = databasePath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        if (File.Exists(databasePath))
            File.Move(databasePath, quarantinePath);
    }

    internal static void DeleteSidecarFiles(string databasePath)
    {
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            var sidecar = databasePath + suffix;
            if (File.Exists(sidecar))
            {
                try { File.Delete(sidecar); }
                catch { /* best-effort */ }
            }
        }
    }
}
