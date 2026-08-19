using System.IO;
using Microsoft.Data.Sqlite;

namespace Bridge.Services;

/// <summary>
/// Detects a corrupt <c>bridge.db</c> and restores the pre-update backup created
/// by <see cref="AppUpdateService"/> when possible.
/// </summary>
public static class BridgeDatabaseRecovery
{
    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();
    private const int QuarantineRetryCount = 5;

    public enum RecoveryResult
    {
        NotNeeded,
        RestoredFromUpdateBackup,
        BackupUnavailable,
        FileLocked
    }

    public static string UpdateBackupPath => Config.DatabasePath + ".bak-update";

    public static bool IsValidSqliteFile(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            Span<byte> header = stackalloc byte[16];
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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

        if (!TryQuarantineInvalidDatabase(databasePath))
            return RecoveryResult.FileLocked;

        try
        {
            File.Copy(backupPath, databasePath, overwrite: true);
            DeleteSidecarFiles(databasePath);
            return RecoveryResult.RestoredFromUpdateBackup;
        }
        catch (IOException)
        {
            return RecoveryResult.FileLocked;
        }
    }

    /// <summary>
    /// Moves a corrupt database aside so it can be replaced. Retries when another
    /// process (often a Bridge instance in the tray) still holds the file open.
    /// </summary>
    internal static bool TryQuarantineInvalidDatabase(string databasePath)
    {
        ReleaseDatabaseFileHandles();
        DeleteSidecarFiles(databasePath);

        if (!File.Exists(databasePath))
            return true;

        var quarantinePath = databasePath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";

        if (TryMoveWithRetries(databasePath, quarantinePath))
            return true;

        if (!TryCopyThenDelete(databasePath, quarantinePath))
            return false;

        DeleteSidecarFiles(databasePath);
        return !File.Exists(databasePath);
    }

    internal static void QuarantineInvalidDatabase(string databasePath)
    {
        if (!TryQuarantineInvalidDatabase(databasePath))
            throw new IOException($"Could not quarantine '{databasePath}' because it is in use.");
    }

    internal static void DeleteSidecarFiles(string databasePath)
    {
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            var sidecar = databasePath + suffix;
            if (!File.Exists(sidecar))
                continue;

            TryDeleteWithRetries(sidecar);
        }
    }

    private static void ReleaseDatabaseFileHandles()
    {
        try
        {
            SqliteConnection.ClearAllPools();
        }
        catch
        {
            // Best-effort — recovery must continue.
        }
    }

    private static bool TryMoveWithRetries(string sourcePath, string destinationPath)
    {
        for (var attempt = 0; attempt < QuarantineRetryCount; attempt++)
        {
            try
            {
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);

                File.Move(sourcePath, destinationPath);
                return true;
            }
            catch (IOException) when (attempt + 1 < QuarantineRetryCount)
            {
                ReleaseDatabaseFileHandles();
                Thread.Sleep(100 * (attempt + 1));
            }
        }

        return false;
    }

    private static bool TryCopyThenDelete(string sourcePath, string destinationPath)
    {
        try
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            File.Copy(sourcePath, destinationPath, overwrite: false);
        }
        catch
        {
            return false;
        }

        return TryDeleteWithRetries(sourcePath);
    }

    private static bool TryDeleteWithRetries(string path)
    {
        for (var attempt = 0; attempt < QuarantineRetryCount; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                    return true;

                File.Delete(path);
                return true;
            }
            catch (IOException) when (attempt + 1 < QuarantineRetryCount)
            {
                ReleaseDatabaseFileHandles();
                Thread.Sleep(100 * (attempt + 1));
            }
        }

        return !File.Exists(path);
    }
}
