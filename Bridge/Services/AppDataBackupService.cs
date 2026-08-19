using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Bridge.Services;

public sealed record AppDataBackupResult(bool Success, string? Message, string? FilePath = null);

/// <summary>
/// Creates and restores portable .zip backups of the user's library database,
/// preferences, and artwork cache under AppData. RetroArch installs and logs
/// are excluded. Restore is staged and applied on the next app restart so the
/// database is not replaced while EF connections are open.
/// </summary>
public static class AppDataBackupService
{
    public const string RestoreStagingDirectoryName = ".restore-staging";
    public const string RestorePendingMarkerFileName = ".restore-pending";

    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();
    private static readonly string[] SettingFileNames =
    [
        "theme.json",
        "viewmode.txt",
        "scrollpositions.txt",
        "igdb-settings.json",
        "update-channel.txt",
        "language.txt",
        "startup.txt",
        "tray-icon.txt",
        Config.AppDataVersionFileName
    ];

    public static string RestoreStagingPath(string appDataPath) =>
        Path.Combine(appDataPath, RestoreStagingDirectoryName);

    public static string RestorePendingMarkerPath(string appDataPath) =>
        Path.Combine(appDataPath, RestorePendingMarkerFileName);

    /// <summary>
    /// Validates that <paramref name="zipPath"/> contains a SQLite
    /// <c>bridge.db</c> created by <see cref="CreateBackup"/>.
    /// </summary>
    public static AppDataBackupResult ValidateBackupArchive(string zipPath)
    {
        if (!File.Exists(zipPath))
            return new AppDataBackupResult(false, "The backup file was not found.");

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = FindDatabaseEntry(archive);
            if (entry is null)
                return new AppDataBackupResult(false, "The archive does not contain bridge.db.");

            using var stream = entry.Open();
            Span<byte> header = stackalloc byte[16];
            if (stream.Read(header) != 16 || !header.SequenceEqual(SqliteHeader))
                return new AppDataBackupResult(false, "bridge.db is not a valid SQLite database.");

            return new AppDataBackupResult(true, null, zipPath);
        }
        catch (Exception ex)
        {
            return new AppDataBackupResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Extracts <paramref name="zipPath"/> to AppData staging and arms a pending
    /// restore for the next launch. The caller should restart Bridge afterward.
    /// </summary>
    public static AppDataBackupResult ScheduleRestore(string zipPath, string? appDataPath = null)
    {
        appDataPath ??= Config.AppDataPath;
        var validation = ValidateBackupArchive(zipPath);
        if (!validation.Success)
            return validation;

        try
        {
            var stagingPath = RestoreStagingPath(appDataPath);
            if (Directory.Exists(stagingPath))
                Directory.Delete(stagingPath, recursive: true);

            ZipFile.ExtractToDirectory(zipPath, stagingPath);

            if (!BridgeDatabaseRecovery.IsValidSqliteFile(Path.Combine(stagingPath, "bridge.db")))
            {
                TryDeleteDirectory(stagingPath);
                return new AppDataBackupResult(false, "bridge.db is not a valid SQLite database.");
            }

            File.WriteAllText(RestorePendingMarkerPath(appDataPath), DateTime.UtcNow.ToString("O"));
            return new AppDataBackupResult(true, null, zipPath);
        }
        catch (Exception ex)
        {
            return new AppDataBackupResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Applies a staged restore at startup, before the database is opened.
    /// Returns true when a pending restore was applied.
    /// </summary>
    public static bool ApplyPendingRestore(string? appDataPath = null)
    {
        appDataPath ??= Config.AppDataPath;
        var stagingPath = RestoreStagingPath(appDataPath);
        var markerPath = RestorePendingMarkerPath(appDataPath);

        if (!File.Exists(markerPath) || !Directory.Exists(stagingPath))
            return false;

        try
        {
            var databasePath = Path.Combine(appDataPath, "bridge.db");
            if (File.Exists(databasePath))
                BridgeDatabaseRecovery.QuarantineInvalidDatabase(databasePath);
            else
                BridgeDatabaseRecovery.DeleteSidecarFiles(databasePath);

            File.Copy(Path.Combine(stagingPath, "bridge.db"), databasePath, overwrite: true);
            RestoreSettingFiles(stagingPath, appDataPath);
            RestoreImageCache(stagingPath, appDataPath);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            CleanupRestoreArtifacts(appDataPath);
        }
    }

    private static void RestoreSettingFiles(string stagingRoot, string appDataPath)
    {
        foreach (var name in SettingFileNames)
        {
            var source = Path.Combine(stagingRoot, name);
            if (!File.Exists(source))
                continue;

            File.Copy(source, Path.Combine(appDataPath, name), overwrite: true);
        }
    }

    private static void RestoreImageCache(string stagingRoot, string appDataPath)
    {
        var source = Path.Combine(stagingRoot, "image-cache");
        var destination = Path.Combine(appDataPath, "image-cache");
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        CopyDirectoryIfExists(source, destination);
    }

    private static void CleanupRestoreArtifacts(string appDataPath)
    {
        TryDeleteFile(RestorePendingMarkerPath(appDataPath));
        TryDeleteDirectory(RestoreStagingPath(appDataPath));
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    /// <param name="destinationZipPath">Full path for the output .zip file.</param>
    /// <param name="appDataPath">Override for unit tests; production uses <see cref="Config.AppDataPath"/>.</param>
    public static AppDataBackupResult CreateBackup(string destinationZipPath, string? appDataPath = null)
    {
        appDataPath ??= Config.AppDataPath;
        var databasePath = Path.Combine(appDataPath, "bridge.db");

        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"bridge-backup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            try
            {
                if (File.Exists(databasePath))
                    BackupDatabase(databasePath, Path.Combine(tempRoot, "bridge.db"));

                CopySettingFiles(appDataPath, tempRoot);
                CopyDirectoryIfExists(
                    Path.Combine(appDataPath, "image-cache"),
                    Path.Combine(tempRoot, "image-cache"));

                WriteManifest(tempRoot);

                var directory = Path.GetDirectoryName(destinationZipPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(destinationZipPath))
                    File.Delete(destinationZipPath);

                ZipFile.CreateFromDirectory(tempRoot, destinationZipPath, CompressionLevel.Optimal, false);
                return new AppDataBackupResult(true, null, destinationZipPath);
            }
            finally
            {
                try { Directory.Delete(tempRoot, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
        catch (Exception ex)
        {
            return new AppDataBackupResult(false, ex.Message);
        }
    }

    private static void BackupDatabase(string sourcePath, string destinationPath)
    {
        using var source = new SqliteConnection($"Data Source={sourcePath};Pooling=False");
        using var destination = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static void CopySettingFiles(string appDataPath, string destinationRoot)
    {
        foreach (var name in SettingFileNames)
        {
            var source = Path.Combine(appDataPath, name);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(destinationRoot, name), overwrite: true);
        }
    }

    private static void CopyDirectoryIfExists(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
            return;

        Directory.CreateDirectory(destinationDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var target = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void WriteManifest(string tempRoot)
    {
        var manifest = new
        {
            bridgeVersion = Config.AssemblyVersion.ToString(3),
            createdUtc = DateTime.UtcNow,
            contents = new[]
            {
                "bridge.db",
                "theme.json",
                "viewmode.txt",
                "scrollpositions.txt",
                "igdb-settings.json",
                "update-channel.txt",
                Config.AppDataVersionFileName,
                "image-cache/"
            }
        };

        File.WriteAllText(
            Path.Combine(tempRoot, "backup-manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static ZipArchiveEntry? FindDatabaseEntry(ZipArchive archive)
    {
        foreach (var entry in archive.Entries)
        {
            if (entry.Name.Equals("bridge.db", StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }
}
