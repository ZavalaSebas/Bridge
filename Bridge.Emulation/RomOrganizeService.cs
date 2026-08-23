using System.IO;
using System.Text.RegularExpressions;
using Bridge.Core.Utilities;

namespace Bridge.Emulation;

public readonly record struct RomOrganizeTarget(
    string RomPath,
    string OfficialName,
    string? PlatformName,
    bool Skip);

public sealed record RomOrganizeChange(string OriginalRomPath, string NewRomPath);

public sealed record RomOrganizeResult(
    IReadOnlyList<RomOrganizeChange> Changes,
    int Unchanged,
    int Skipped,
    int Failed);

/// <summary>
/// Moves ROM files into <c>{scanRoot}/{platform}/{official name}{ext}</c> and
/// brings sidecar SRAM/savestates along. Archives used by more than one library
/// game are only sorted into the platform folder (the zip name stays shared).
/// </summary>
public static class RomOrganizeService
{
    public static RomOrganizeResult Organize(IReadOnlyList<RomOrganizeTarget> targets, string scanRoot)
    {
        var changes = new List<RomOrganizeChange>();
        var unchanged = 0;
        var skipped = 0;
        var failed = 0;
        if (string.IsNullOrWhiteSpace(scanRoot) || !Directory.Exists(scanRoot))
            return new RomOrganizeResult(changes, unchanged, skipped, targets.Count);

        var root = Path.GetFullPath(scanRoot);
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in targets.GroupBy(target => GetDiskPath(target.RomPath), StringComparer.OrdinalIgnoreCase))
        {
            var members = group.ToList();
            if (members.Exists(member => member.Skip))
            {
                skipped += members.Count;
                continue;
            }

            var diskPath = Path.GetFullPath(group.Key);
            if (!File.Exists(diskPath) || !PathContainment.IsUnderRoot(diskPath, root))
            {
                skipped += members.Count;
                continue;
            }

            var platformName = members
                .Select(member => member.PlatformName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            if (string.IsNullOrWhiteSpace(platformName) &&
                RomPlatformCatalog.TryGetByExtension(RomArchivePath.GetRomExtension(members[0].RomPath), out var platform))
            {
                platformName = platform!.PlatformName;
            }

            if (string.IsNullOrWhiteSpace(platformName))
            {
                skipped += members.Count;
                continue;
            }

            var renameToOfficial = members.Count == 1;
            var officialName = members[0].OfficialName;
            var planned = PlanDestination(diskPath, platformName, officialName, renameToOfficial, root, reserved);
            if (planned is null)
            {
                failed += members.Count;
                continue;
            }

            reserved.Add(planned);
            if (PathsEqual(diskPath, planned))
            {
                unchanged += members.Count;
                continue;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(planned)!);
                MoveFile(diskPath, planned);
                MoveSidecars(diskPath, members[0].RomPath, planned, renameToOfficial);
                TryDeleteEmptyParents(Path.GetDirectoryName(diskPath), root);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed += members.Count;
                continue;
            }

            foreach (var member in members)
            {
                var newRomPath = ReplaceDiskPath(member.RomPath, planned);
                if (string.Equals(member.RomPath, newRomPath, StringComparison.OrdinalIgnoreCase))
                    unchanged++;
                else
                    changes.Add(new RomOrganizeChange(member.RomPath, newRomPath));
            }
        }

        return new RomOrganizeResult(changes, unchanged, skipped, failed);
    }

    public static string SanitizeFileName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return "ROM";

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(trimmed.Select(ch => Array.IndexOf(invalid, ch) >= 0 ? ' ' : ch).ToArray());
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(cleaned) ? "ROM" : cleaned;
    }

    private static string? PlanDestination(
        string diskPath,
        string platformName,
        string officialName,
        bool renameToOfficial,
        string scanRoot,
        HashSet<string> reserved)
    {
        var folder = SanitizeFileName(platformName);
        var extension = Path.GetExtension(diskPath);
        var fileName = renameToOfficial
            ? SanitizeFileName(officialName) + extension
            : Path.GetFileName(diskPath);
        var relative = Path.Combine(folder, fileName);
        var destination = PathContainment.TryResolveUnderRoot(scanRoot, relative);
        if (destination is null)
            return null;

        return Uniquify(destination, diskPath, reserved);
    }

    private static string Uniquify(string destination, string source, HashSet<string> reserved)
    {
        if (IsAvailable(destination, source, reserved))
            return destination;

        var directory = Path.GetDirectoryName(destination)!;
        var stem = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);
        for (var index = 2; index < 1000; index++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({index}){extension}");
            if (IsAvailable(candidate, source, reserved))
                return candidate;
        }

        return destination;
    }

    private static bool IsAvailable(string candidate, string source, HashSet<string> reserved)
    {
        if (reserved.Contains(candidate) && !PathsEqual(candidate, source))
            return false;

        return !File.Exists(candidate) || PathsEqual(candidate, source);
    }

    private static void MoveFile(string source, string destination)
    {
        if (PathsEqual(source, destination))
            return;

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            var temp = destination + ".bridge-rename";
            File.Move(source, temp);
            File.Move(temp, destination);
            return;
        }

        File.Move(source, destination);
    }

    private static void MoveSidecars(string sourceDiskPath, string romPath, string destinationDiskPath, bool renameToOfficial)
    {
        var sourceDir = Path.GetDirectoryName(sourceDiskPath);
        var destDir = Path.GetDirectoryName(destinationDiskPath);
        if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(destDir) || !Directory.Exists(sourceDir))
            return;

        var oldBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFileNameWithoutExtension(sourceDiskPath),
            RomArchivePath.GetCheatBaseName(romPath)
        };
        var newBase = Path.GetFileNameWithoutExtension(destinationDiskPath);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            if (PathsEqual(file, sourceDiskPath))
                continue;

            var extension = Path.GetExtension(file);
            if (!RomScanner.IsSidecarFile(extension))
                continue;

            var stem = Path.GetFileNameWithoutExtension(file);
            var matchedBase = oldBases
                .Where(baseName =>
                    stem.Equals(baseName, StringComparison.OrdinalIgnoreCase) ||
                    stem.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(baseName => baseName.Length)
                .FirstOrDefault();
            if (matchedBase is null)
                continue;

            var fileName = Path.GetFileName(file);
            var destName = renameToOfficial
                ? newBase + fileName[matchedBase.Length..]
                : fileName;

            var dest = Path.Combine(destDir, destName);
            if (PathsEqual(file, dest))
                continue;

            try
            {
                if (File.Exists(dest) && !PathsEqual(file, dest))
                    continue;
                MoveFile(file, dest);
            }
            catch (IOException)
            {
                // Leave the sidecar; the ROM itself already moved.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void TryDeleteEmptyParents(string? directory, string scanRoot)
    {
        while (!string.IsNullOrWhiteSpace(directory) &&
               PathContainment.IsUnderRoot(directory, scanRoot) &&
               !PathsEqual(directory, scanRoot))
        {
            try
            {
                if (Directory.EnumerateFileSystemEntries(directory).Any())
                    return;

                Directory.Delete(directory);
            }
            catch
            {
                return;
            }

            directory = Path.GetDirectoryName(directory);
        }
    }

    private static string GetDiskPath(string romPath) =>
        RomArchivePath.TrySplit(romPath, out var archivePath, out _)
            ? archivePath
            : romPath;

    private static string ReplaceDiskPath(string romPath, string newDiskPath)
    {
        if (!RomArchivePath.TrySplit(romPath, out _, out var entryPath) || string.IsNullOrWhiteSpace(entryPath))
            return newDiskPath;

        return RomArchivePath.Combine(newDiskPath, entryPath);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
