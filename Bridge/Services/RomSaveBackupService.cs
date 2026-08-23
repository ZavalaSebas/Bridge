using System.IO;
using System.Text.Json;
using Bridge.Core.Entities;
using Bridge.Core.Utilities;
using Bridge.Emulation;

namespace Bridge.Services;

public enum RomSaveBackupKind
{
    Automatic,
    Manual
}

public sealed record RomSaveBackupResult(
    bool Success,
    string? Message,
    string? DirectoryPath = null,
    int FileCount = 0,
    DateTime? CreatedUtc = null,
    bool Unchanged = false);

public sealed class RomSaveBackupListItem
{
    public required string DirectoryPath { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public required RomSaveBackupKind Kind { get; init; }
    public required int FileCount { get; init; }
    public required string Header { get; init; }
}

public readonly record struct SnapshotSource(string Path, string Role, string RelativePath);

/// <summary>
/// Dated per-game copies of RetroArch SRAM and savestates under
/// <c>save-backups/</c>. Automatic snapshots are taken when a ROM session ends;
/// manual ones from the game menu. Restores files even if RetroArch was removed.
/// </summary>
public static class RomSaveBackupService
{
    public const int MaxAutomaticBackups = 5;
    public const string ManifestFileName = "manifest.json";
    public const string FilesDirectoryName = "files";
    public const string FolderRole = "folder";
    public const long MaxFolderFileBytes = 64L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string BackupsRoot(string? appDataPath = null) =>
        Path.Combine(appDataPath ?? Config.AppDataPath, "save-backups");

    public static string? GetPrimaryRomPath(Game game) =>
        game.Roms.FirstOrDefault(rom => !string.IsNullOrWhiteSpace(rom.Path))?.Path;

    public static bool IsRomGame(Game game) =>
        GetPrimaryRomPath(game) is not null;

    public static RomSaveBackupResult Create(
        Game game,
        RomSaveBackupKind kind,
        string? retroArchInstallPath = null,
        string? backupsRoot = null,
        string? customSaveFolder = null)
    {
        backupsRoot ??= BackupsRoot();
        if (IsRomGame(game))
        {
            retroArchInstallPath ??= Config.EmulatorInstallPath;
            var romPath = GetPrimaryRomPath(game)!;
            var files = RetroArchSaveLocator.EnumerateSaveFiles(retroArchInstallPath, romPath)
                .Select(file => new SnapshotSource(file.Path, RoleFolder(file.Role), file.RelativePath))
                .ToList();
            return CreateFromSources(game, kind, files, backupsRoot, romPath);
        }

        if (string.IsNullOrWhiteSpace(customSaveFolder) || !Directory.Exists(customSaveFolder))
            return new RomSaveBackupResult(false, "The game has no save folder.");

        return CreateFromSources(game, kind, EnumerateFolderSources(customSaveFolder), backupsRoot, romPath: null);
    }

    public static IReadOnlyList<SnapshotSource> EnumerateFolderSources(string folder)
    {
        var files = new List<SnapshotSource>();
        if (!Directory.Exists(folder))
            return files;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                long length;
                try
                {
                    length = new FileInfo(file).Length;
                }
                catch (IOException)
                {
                    continue;
                }

                if (length > MaxFolderFileBytes)
                    continue;

                var relative = Path.GetRelativePath(folder, file);
                if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                    continue;

                files.Add(new SnapshotSource(file, FolderRole, relative.Replace('\\', '/')));
            }
        }
        catch (IOException)
        {
            // Unreadable tree.
        }

        return files;
    }

    private static RomSaveBackupResult CreateFromSources(
        Game game,
        RomSaveBackupKind kind,
        IReadOnlyList<SnapshotSource> files,
        string backupsRoot,
        string? romPath)
    {
        if (files.Count == 0)
            return new RomSaveBackupResult(false, "No save files found.");

        if (kind == RomSaveBackupKind.Automatic &&
            IsSameAsLatestAutomatic(game.Id, files, backupsRoot))
        {
            var latest = List(game.Id, backupsRoot).FirstOrDefault(item => item.Kind == RomSaveBackupKind.Automatic);
            return new RomSaveBackupResult(true, null, latest?.DirectoryPath, files.Count, latest?.CreatedUtc, Unchanged: true);
        }

        var createdUtc = DateTime.UtcNow;
        var stamp = createdUtc.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
        var kindName = kind == RomSaveBackupKind.Manual ? "manual" : "automatic";
        var snapshotDir = Path.Combine(backupsRoot, game.Id.ToString("N"), $"{stamp}-{kindName}");
        var filesRoot = Path.Combine(snapshotDir, FilesDirectoryName);

        try
        {
            foreach (var file in files)
            {
                var destination = PathContainment.TryResolveUnderRoot(
                    Path.Combine(filesRoot, file.Role),
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (destination is null)
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file.Path, destination, overwrite: true);
            }

            var manifest = new SnapshotManifest
            {
                Version = 1,
                Kind = kindName,
                CreatedUtc = createdUtc,
                GameId = game.Id,
                GameName = game.Name,
                RomPath = romPath,
                RomBaseName = romPath is null ? null : RomArchivePath.GetCheatBaseName(romPath),
                Files = files.Select(file => new SnapshotFile
                {
                    Role = file.Role,
                    RelativePath = file.RelativePath,
                    FileName = Path.GetFileName(file.Path)
                }).ToList()
            };

            File.WriteAllText(
                Path.Combine(snapshotDir, ManifestFileName),
                JsonSerializer.Serialize(manifest, JsonOptions));

            if (kind == RomSaveBackupKind.Automatic)
                PruneAutomatic(game.Id, backupsRoot);

            return new RomSaveBackupResult(true, null, snapshotDir, files.Count, createdUtc);
        }
        catch (Exception ex)
        {
            TryDeleteDirectory(snapshotDir);
            return new RomSaveBackupResult(false, ex.Message);
        }
    }

    public static IReadOnlyList<RomSaveBackupListItem> List(Guid gameId, string? backupsRoot = null)
    {
        backupsRoot ??= BackupsRoot();
        var gameDir = Path.Combine(backupsRoot, gameId.ToString("N"));
        if (!Directory.Exists(gameDir))
            return [];

        var items = new List<RomSaveBackupListItem>();
        foreach (var directory in Directory.EnumerateDirectories(gameDir))
        {
            if (TryReadManifest(directory) is not { } manifest)
                continue;

            items.Add(new RomSaveBackupListItem
            {
                DirectoryPath = directory,
                CreatedUtc = manifest.CreatedUtc,
                Kind = ParseKind(manifest.Kind),
                FileCount = manifest.Files.Count,
                Header = string.Empty
            });
        }

        return items
            .OrderByDescending(item => item.CreatedUtc)
            .ToList();
    }

    public static RomSaveBackupResult Restore(
        string snapshotDirectory,
        string? romPath,
        string? retroArchInstallPath = null,
        string? customSaveFolder = null)
    {
        retroArchInstallPath ??= Config.EmulatorInstallPath;
        if (TryReadManifest(snapshotDirectory) is not { } manifest)
            return new RomSaveBackupResult(false, "The backup is missing or invalid.");

        var filesRoot = Path.Combine(snapshotDirectory, FilesDirectoryName);
        if (!Directory.Exists(filesRoot))
            return new RomSaveBackupResult(false, "The backup has no files.");

        try
        {
            Directory.CreateDirectory(retroArchInstallPath);
            var savesRoot = RetroArchSaveLocator.ResolveSaveDirectory(retroArchInstallPath);
            var statesRoot = RetroArchSaveLocator.ResolveStateDirectory(retroArchInstallPath);
            var contentDir = RetroArchSaveLocator.TryGetContentDirectory(romPath) ?? savesRoot;
            var folderRoot = customSaveFolder;
            if (!string.IsNullOrWhiteSpace(folderRoot))
                Directory.CreateDirectory(folderRoot);

            var restored = 0;

            foreach (var entry in manifest.Files)
            {
                var roleFolder = string.IsNullOrWhiteSpace(entry.Role) ? "saves" : entry.Role;
                var source = PathContainment.TryResolveUnderRoot(
                    Path.Combine(filesRoot, roleFolder),
                    (entry.RelativePath ?? entry.FileName).Replace('/', Path.DirectorySeparatorChar));
                if (source is null || !File.Exists(source))
                    continue;

                string? destinationRoot;
                if (roleFolder.Equals(FolderRole, StringComparison.OrdinalIgnoreCase))
                {
                    destinationRoot = folderRoot;
                    if (string.IsNullOrWhiteSpace(destinationRoot))
                        continue;
                }
                else
                {
                    destinationRoot = roleFolder.Equals("states", StringComparison.OrdinalIgnoreCase)
                        ? statesRoot
                        : roleFolder.Equals("content", StringComparison.OrdinalIgnoreCase)
                            ? contentDir
                            : savesRoot;
                }

                var relative = string.IsNullOrWhiteSpace(entry.RelativePath)
                    ? entry.FileName
                    : entry.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                var destination = PathContainment.TryResolveUnderRoot(destinationRoot, relative)
                    ?? Path.Combine(destinationRoot, Path.GetFileName(source));

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: true);
                restored++;
            }

            return restored == 0
                ? new RomSaveBackupResult(false, "The backup has no files.")
                : new RomSaveBackupResult(true, null, snapshotDirectory, restored, manifest.CreatedUtc);
        }
        catch (Exception ex)
        {
            return new RomSaveBackupResult(false, ex.Message);
        }
    }

    public static IReadOnlyList<RomSaveFile> ResolveFilesForExport(
        Game game,
        string retroArchInstallPath,
        string? backupsRoot = null)
    {
        var romPath = GetPrimaryRomPath(game);
        if (romPath is null)
            return [];

        var live = RetroArchSaveLocator.EnumerateSaveFiles(retroArchInstallPath, romPath);
        if (live.Count > 0)
            return live;

        var latest = List(game.Id, backupsRoot).FirstOrDefault();
        if (latest is null || TryReadManifest(latest.DirectoryPath) is not { } manifest)
            return [];

        var filesRoot = Path.Combine(latest.DirectoryPath, FilesDirectoryName);
        var restored = new List<RomSaveFile>();
        foreach (var entry in manifest.Files)
        {
            var roleFolder = string.IsNullOrWhiteSpace(entry.Role) ? "saves" : entry.Role;
            var source = PathContainment.TryResolveUnderRoot(
                Path.Combine(filesRoot, roleFolder),
                (entry.RelativePath ?? entry.FileName).Replace('/', Path.DirectorySeparatorChar));
            if (source is null || !File.Exists(source))
                continue;

            var role = roleFolder.Equals("states", StringComparison.OrdinalIgnoreCase)
                ? RomSaveRole.States
                : roleFolder.Equals("content", StringComparison.OrdinalIgnoreCase)
                    ? RomSaveRole.Content
                    : RomSaveRole.Saves;
            restored.Add(new RomSaveFile(source, role, entry.RelativePath ?? Path.GetFileName(source)));
        }

        return restored;
    }

    private static bool IsSameAsLatestAutomatic(
        Guid gameId,
        IReadOnlyList<SnapshotSource> files,
        string backupsRoot)
    {
        var latest = List(gameId, backupsRoot)
            .FirstOrDefault(item => item.Kind == RomSaveBackupKind.Automatic);
        if (latest is null || TryReadManifest(latest.DirectoryPath) is not { } manifest)
            return false;

        if (manifest.Files.Count != files.Count)
            return false;

        var filesRoot = Path.Combine(latest.DirectoryPath, FilesDirectoryName);
        foreach (var file in files)
        {
            var snapshotPath = PathContainment.TryResolveUnderRoot(
                Path.Combine(filesRoot, file.Role),
                file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (snapshotPath is null || !File.Exists(snapshotPath))
                return false;

            try
            {
                if (!FilesHaveSameContent(file.Path, snapshotPath))
                    return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        return true;
    }

    private static bool FilesHaveSameContent(string leftPath, string rightPath)
    {
        using var left = File.OpenRead(leftPath);
        using var right = File.OpenRead(rightPath);
        if (left.Length != right.Length)
            return false;

        Span<byte> leftBuffer = stackalloc byte[8192];
        Span<byte> rightBuffer = stackalloc byte[8192];
        while (true)
        {
            var leftRead = left.Read(leftBuffer);
            var rightRead = right.Read(rightBuffer);
            if (leftRead != rightRead)
                return false;
            if (leftRead == 0)
                return true;
            if (!leftBuffer[..leftRead].SequenceEqual(rightBuffer[..rightRead]))
                return false;
        }
    }

    private static void PruneAutomatic(Guid gameId, string backupsRoot)
    {
        var automatic = List(gameId, backupsRoot)
            .Where(item => item.Kind == RomSaveBackupKind.Automatic)
            .Skip(MaxAutomaticBackups)
            .ToList();

        foreach (var item in automatic)
            TryDeleteDirectory(item.DirectoryPath);
    }

    private static SnapshotManifest? TryReadManifest(string snapshotDirectory)
    {
        var path = Path.Combine(snapshotDirectory, ManifestFileName);
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SnapshotManifest>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RomSaveBackupKind ParseKind(string? kind) =>
        string.Equals(kind, "manual", StringComparison.OrdinalIgnoreCase)
            ? RomSaveBackupKind.Manual
            : RomSaveBackupKind.Automatic;

    private static string RoleFolder(RomSaveRole role) => role switch
    {
        RomSaveRole.States => "states",
        RomSaveRole.Content => "content",
        _ => "saves"
    };

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

    private sealed class SnapshotManifest
    {
        public int Version { get; set; } = 1;
        public string Kind { get; set; } = "automatic";
        public DateTime CreatedUtc { get; set; }
        public Guid GameId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public string? RomPath { get; set; }
        public string? RomBaseName { get; set; }
        public List<SnapshotFile> Files { get; set; } = [];
    }

    private sealed class SnapshotFile
    {
        public string Role { get; set; } = "saves";
        public string RelativePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
