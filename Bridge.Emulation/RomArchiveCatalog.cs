using System.IO;
using SharpCompress.Archives;

namespace Bridge.Emulation;

public static class RomArchiveCatalog
{
    public sealed record ArchiveRomEntry(string EntryPath);

    public static IEnumerable<ArchiveRomEntry> EnumerateRomEntries(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            yield break;
        }

        IArchive archive;
        try
        {
            archive = ArchiveFactory.OpenArchive(archivePath);
        }
        catch
        {
            yield break;
        }

        using (archive)
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory || entry.IsSplitAfter)
                {
                    continue;
                }

                var entryPath = entry.Key?.Replace('\\', '/').TrimStart('/') ?? string.Empty;
                if (entryPath.Length == 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(entryPath).TrimStart('.').ToLowerInvariant();
                if (RomPlatformCatalog.TryGetByExtension(extension, out _))
                {
                    yield return new ArchiveRomEntry(entryPath);
                }
            }
        }
    }

    public static bool ContainsEntry(string archivePath, string entryPath)
    {
        var normalizedEntry = entryPath.Replace('\\', '/').TrimStart('/');
        foreach (var entry in EnumerateRomEntries(archivePath))
        {
            if (string.Equals(entry.EntryPath, normalizedEntry, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
