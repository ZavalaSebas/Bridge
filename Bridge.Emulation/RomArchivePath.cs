namespace Bridge.Emulation;

/// RetroArch archive paths use a <c>#</c> delimiter:
/// <c>C:\Roms\game.zip#Super Mario World (USA).sfc</c>
public static class RomArchivePath
{
    public const char Delimiter = '#';

    public static bool IsContainerExtension(string extension) =>
        extension is "zip" or "7z" or "rar";

    public static bool IsArchiveContentPath(string path) =>
        path.Contains(Delimiter, StringComparison.Ordinal);

    public static string Combine(string archivePath, string entryPath) =>
        $"{archivePath}{Delimiter}{entryPath.Replace('\\', '/')}";

    public static bool TrySplit(string path, out string archivePath, out string? entryPath)
    {
        var index = path.IndexOf(Delimiter);
        if (index < 0)
        {
            archivePath = path;
            entryPath = null;
            return false;
        }

        archivePath = path[..index];
        entryPath = path[(index + 1)..];
        return true;
    }

    public static string GetRomExtension(string path)
    {
        var segment = TrySplit(path, out _, out var entry) && !string.IsNullOrWhiteSpace(entry)
            ? entry
            : path;
        return Path.GetExtension(segment).TrimStart('.').ToLowerInvariant();
    }

    public static string GetRomFileName(string path)
    {
        var segment = TrySplit(path, out _, out var entry) && !string.IsNullOrWhiteSpace(entry)
            ? entry
            : path;
        return Path.GetFileName(segment.Replace('/', Path.DirectorySeparatorChar));
    }

    public static string GetCheatBaseName(string path) =>
        Path.GetFileNameWithoutExtension(GetRomFileName(path));

    public static bool RomFileExists(string path)
    {
        if (!TrySplit(path, out var archivePath, out var entryPath))
        {
            return File.Exists(path);
        }

        if (!File.Exists(archivePath))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(entryPath)
            || RomArchiveCatalog.ContainsEntry(archivePath, entryPath);
    }

    public static string Normalize(string path)
    {
        if (!TrySplit(path, out var archivePath, out var entryPath))
        {
            return NormalizePlainPath(path);
        }

        var normalizedArchive = NormalizePlainPath(archivePath);
        return string.IsNullOrWhiteSpace(entryPath)
            ? normalizedArchive
            : Combine(normalizedArchive, entryPath);
    }

    private static string NormalizePlainPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
