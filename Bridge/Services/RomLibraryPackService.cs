using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Bridge.Core.Entities;
using Bridge.Core.Utilities;
using Bridge.Emulation;

namespace Bridge.Services;

public sealed record RomLibraryPackResult(
    bool Success,
    string? Message,
    string? FilePath = null,
    int GamesWithSaves = 0,
    int RomsIncluded = 0,
    int RomsSkipped = 0,
    int SavesRestored = 0,
    int RomsCopied = 0,
    string? RomDestination = null,
    IReadOnlyList<string>? RomDestinations = null,
    IReadOnlyDictionary<Guid, string>? RestoredSaveFolders = null);

/// <summary>
/// Portable zip of ROM SRAM/savestates plus ROM files up to
/// <see cref="MaxRomBytes"/>. Import restores saves into RetroArch folders
/// and copies ROM files back onto disk for Scan ROMs.
/// </summary>
public static class RomLibraryPackService
{
    public const long MaxRomBytes = 500L * 1024 * 1024;
    public const string ManifestFileName = "rom-pack-manifest.json";
    public const string KindValue = "bridge-rom-pack";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static RomLibraryPackResult Create(
        IEnumerable<Game> games,
        string destinationZipPath,
        string? retroArchInstallPath = null,
        string? backupsRoot = null,
        long maxRomBytes = MaxRomBytes,
        IReadOnlyDictionary<Guid, string>? customSaveFolders = null)
    {
        retroArchInstallPath ??= Config.EmulatorInstallPath;
        backupsRoot ??= RomSaveBackupService.BackupsRoot();

        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"bridge-rom-pack-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            try
            {
                var manifest = new PackManifest
                {
                    Kind = KindValue,
                    Version = 1,
                    CreatedUtc = DateTime.UtcNow,
                    MaxRomBytes = maxRomBytes
                };

                foreach (var game in games)
                {
                    if (RomSaveBackupService.IsRomGame(game))
                    {
                        var romPath = RomSaveBackupService.GetPrimaryRomPath(game)!;
                        var entry = new PackGame
                        {
                            GameId = game.Id,
                            Name = game.Name,
                            OriginalRomPath = romPath
                        };

                        var saveFiles = RomSaveBackupService.ResolveFilesForExport(
                            game,
                            retroArchInstallPath,
                            backupsRoot);
                        if (saveFiles.Count > 0)
                        {
                            var savesRoot = Path.Combine(tempRoot, "saves", game.Id.ToString("N"));
                            foreach (var file in saveFiles)
                            {
                                var role = RoleFolder(file.Role);
                                var destination = PathContainment.TryResolveUnderRoot(
                                    Path.Combine(savesRoot, role),
                                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                                if (destination is null)
                                    continue;

                                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                                File.Copy(file.Path, destination, overwrite: true);
                                entry.Saves.Add(new PackSaveFile
                                {
                                    Role = role,
                                    RelativePath = file.RelativePath,
                                    FileName = Path.GetFileName(file.Path)
                                });
                            }
                        }

                        TryIncludeRom(romPath, game.Id, tempRoot, maxRomBytes, entry);
                        if (entry.Saves.Count == 0 && !entry.RomIncluded)
                            continue;

                        manifest.Games.Add(entry);
                        continue;
                    }

                    if (customSaveFolders is null ||
                        !customSaveFolders.TryGetValue(game.Id, out var saveFolder) ||
                        string.IsNullOrWhiteSpace(saveFolder))
                    {
                        continue;
                    }

                    var folderFiles = RomSaveBackupService.EnumerateFolderSources(saveFolder);
                    if (folderFiles.Count == 0)
                        continue;

                    var folderEntry = new PackGame
                    {
                        GameId = game.Id,
                        Name = game.Name,
                        OriginalSaveFolder = saveFolder
                    };
                    var packSaves = Path.Combine(tempRoot, "saves", game.Id.ToString("N"));
                    foreach (var file in folderFiles)
                    {
                        var destination = PathContainment.TryResolveUnderRoot(
                            Path.Combine(packSaves, file.Role),
                            file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                        if (destination is null)
                            continue;

                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        File.Copy(file.Path, destination, overwrite: true);
                        folderEntry.Saves.Add(new PackSaveFile
                        {
                            Role = file.Role,
                            RelativePath = file.RelativePath,
                            FileName = Path.GetFileName(file.Path)
                        });
                    }

                    if (folderEntry.Saves.Count == 0)
                        continue;

                    manifest.Games.Add(folderEntry);
                }

                File.WriteAllText(
                    Path.Combine(tempRoot, ManifestFileName),
                    JsonSerializer.Serialize(manifest, JsonOptions));

                var directory = Path.GetDirectoryName(destinationZipPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(destinationZipPath))
                    File.Delete(destinationZipPath);

                ZipFile.CreateFromDirectory(tempRoot, destinationZipPath, CompressionLevel.Optimal, false);

                return new RomLibraryPackResult(
                    true,
                    null,
                    destinationZipPath,
                    GamesWithSaves: manifest.Games.Count(game => game.Saves.Count > 0),
                    RomsIncluded: manifest.Games.Count(game => game.RomIncluded),
                    RomsSkipped: manifest.Games.Count(game => !game.RomIncluded && !string.IsNullOrWhiteSpace(game.RomSkipReason)));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }
        catch (Exception ex)
        {
            return new RomLibraryPackResult(false, ex.Message);
        }
    }

    public static RomLibraryPackResult Validate(string zipPath)
    {
        if (!File.Exists(zipPath))
            return new RomLibraryPackResult(false, "The pack file was not found.");

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.Entries.FirstOrDefault(item =>
                item.Name.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return new RomLibraryPackResult(false, "The archive is not a Bridge ROM pack.");

            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var manifest = JsonSerializer.Deserialize<PackManifest>(reader.ReadToEnd(), JsonOptions);
            if (manifest is null || !string.Equals(manifest.Kind, KindValue, StringComparison.Ordinal))
                return new RomLibraryPackResult(false, "The archive is not a Bridge ROM pack.");

            return new RomLibraryPackResult(true, null, zipPath);
        }
        catch (Exception ex)
        {
            return new RomLibraryPackResult(false, ex.Message);
        }
    }

    public static RomLibraryPackResult Import(
        string zipPath,
        string romFallbackDirectory,
        string? retroArchInstallPath = null)
    {
        var validation = Validate(zipPath);
        if (!validation.Success)
            return validation;

        retroArchInstallPath ??= Config.EmulatorInstallPath;

        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"bridge-rom-pack-in-{Guid.NewGuid():N}");
            ZipFile.ExtractToDirectory(zipPath, tempRoot);

            try
            {
                var manifestPath = Path.Combine(tempRoot, ManifestFileName);
                var manifest = JsonSerializer.Deserialize<PackManifest>(File.ReadAllText(manifestPath), JsonOptions);
                if (manifest is null)
                    return new RomLibraryPackResult(false, "The archive is not a Bridge ROM pack.");

                Directory.CreateDirectory(retroArchInstallPath);
                Directory.CreateDirectory(romFallbackDirectory);

                var savesRoot = RetroArchSaveLocator.ResolveSaveDirectory(retroArchInstallPath);
                var statesRoot = RetroArchSaveLocator.ResolveStateDirectory(retroArchInstallPath);
                var savesRestored = 0;
                var romsCopied = 0;
                var romDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var restoredFolders = new Dictionary<Guid, string>();

                foreach (var game in manifest.Games)
                {
                    var packSaves = Path.Combine(tempRoot, "saves", game.GameId.ToString("N"));
                    var contentDir = RetroArchSaveLocator.TryGetContentDirectory(game.OriginalRomPath) ?? savesRoot;
                    var restoredFolderForGame = false;

                    foreach (var save in game.Saves)
                    {
                        var roleFolder = string.IsNullOrWhiteSpace(save.Role) ? "saves" : save.Role;
                        var source = PathContainment.TryResolveUnderRoot(
                            Path.Combine(packSaves, roleFolder),
                            (save.RelativePath ?? save.FileName).Replace('/', Path.DirectorySeparatorChar));
                        if (source is null || !File.Exists(source))
                            continue;

                        var destinationRoot = roleFolder.Equals(RomSaveBackupService.FolderRole, StringComparison.OrdinalIgnoreCase)
                            ? game.OriginalSaveFolder
                            : roleFolder.Equals("states", StringComparison.OrdinalIgnoreCase)
                                ? statesRoot
                                : roleFolder.Equals("content", StringComparison.OrdinalIgnoreCase)
                                    ? contentDir
                                    : savesRoot;
                        if (string.IsNullOrWhiteSpace(destinationRoot))
                            continue;

                        if (roleFolder.Equals(RomSaveBackupService.FolderRole, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                Directory.CreateDirectory(destinationRoot);
                            }
                            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                            {
                                continue;
                            }
                        }
                        var relative = string.IsNullOrWhiteSpace(save.RelativePath)
                            ? save.FileName
                            : save.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                        var destination = PathContainment.TryResolveUnderRoot(destinationRoot, relative)
                            ?? Path.Combine(destinationRoot, Path.GetFileName(source));

                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        File.Copy(source, destination, overwrite: true);
                        savesRestored++;
                        if (roleFolder.Equals(RomSaveBackupService.FolderRole, StringComparison.OrdinalIgnoreCase))
                            restoredFolderForGame = true;
                    }

                    if (restoredFolderForGame && !string.IsNullOrWhiteSpace(game.OriginalSaveFolder))
                        restoredFolders[game.GameId] = game.OriginalSaveFolder;

                    if (!game.RomIncluded || string.IsNullOrWhiteSpace(game.RomRelativePath))
                        continue;

                    var romSource = PathContainment.TryResolveUnderRoot(
                        tempRoot,
                        game.RomRelativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (romSource is null || !File.Exists(romSource))
                        continue;

                    var romDestinationDir = ResolveRomDestinationDirectory(game.OriginalRomPath, romFallbackDirectory);
                    Directory.CreateDirectory(romDestinationDir);
                    var romDestination = Path.Combine(romDestinationDir, Path.GetFileName(romSource));
                    File.Copy(romSource, romDestination, overwrite: true);
                    romsCopied++;
                    romDestinations.Add(romDestinationDir);
                }

                return new RomLibraryPackResult(
                    true,
                    null,
                    zipPath,
                    SavesRestored: savesRestored,
                    RomsCopied: romsCopied,
                    RomDestination: romDestinations.FirstOrDefault() ?? romFallbackDirectory,
                    RomDestinations: romDestinations.ToList(),
                    RestoredSaveFolders: restoredFolders);
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }
        catch (Exception ex)
        {
            return new RomLibraryPackResult(false, ex.Message);
        }
    }

    private static void TryIncludeRom(
        string romPath,
        Guid gameId,
        string tempRoot,
        long maxRomBytes,
        PackGame entry)
    {
        var diskPath = RomArchivePath.TrySplit(romPath, out var archivePath, out _)
            ? archivePath
            : romPath;
        if (!File.Exists(diskPath))
        {
            entry.RomSkipReason = "missing";
            return;
        }

        long length;
        try
        {
            length = new FileInfo(diskPath).Length;
        }
        catch (IOException)
        {
            entry.RomSkipReason = "missing";
            return;
        }

        if (length > maxRomBytes)
        {
            entry.RomSkipReason = "too-large";
            entry.RomBytes = length;
            return;
        }

        var relative = Path.Combine("roms", gameId.ToString("N"), Path.GetFileName(diskPath));
        var destination = Path.Combine(tempRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(diskPath, destination, overwrite: true);
        entry.RomIncluded = true;
        entry.RomBytes = length;
        entry.RomRelativePath = relative.Replace('\\', '/');
        entry.RomFileName = Path.GetFileName(diskPath);
    }

    private static string ResolveRomDestinationDirectory(string? originalRomPath, string fallbackDirectory)
    {
        if (string.IsNullOrWhiteSpace(originalRomPath))
            return fallbackDirectory;

        var diskPath = RomArchivePath.TrySplit(originalRomPath, out var archivePath, out _)
            ? archivePath
            : originalRomPath;
        var parent = Path.GetDirectoryName(diskPath);
        return !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent)
            ? parent
            : fallbackDirectory;
    }

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

    private sealed class PackManifest
    {
        public string Kind { get; set; } = KindValue;
        public int Version { get; set; } = 1;
        public DateTime CreatedUtc { get; set; }
        public long MaxRomBytes { get; set; }
        public List<PackGame> Games { get; set; } = [];
    }

    private sealed class PackGame
    {
        public Guid GameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? OriginalRomPath { get; set; }
        public string? OriginalSaveFolder { get; set; }
        public bool RomIncluded { get; set; }
        public string? RomSkipReason { get; set; }
        public long RomBytes { get; set; }
        public string? RomRelativePath { get; set; }
        public string? RomFileName { get; set; }
        public List<PackSaveFile> Saves { get; set; } = [];
    }

    private sealed class PackSaveFile
    {
        public string Role { get; set; } = "saves";
        public string RelativePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
