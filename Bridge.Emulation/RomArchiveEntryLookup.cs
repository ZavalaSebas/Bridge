using SharpCompress.Archives;

namespace Bridge.Emulation;

internal static class RomArchiveEntryLookup
{
    internal static string? TryComputeHexFromArchiveEntry(
        string archivePath,
        string entryPath,
        Func<Stream, string> computeHex)
    {
        if (!File.Exists(archivePath))
            return null;

        using var archive = ArchiveFactory.OpenArchive(archivePath);
        var normalizedEntry = entryPath.Replace('\\', '/').TrimStart('/');
        var entry = archive.Entries.FirstOrDefault(candidate =>
            !candidate.IsDirectory &&
            string.Equals(candidate.Key?.Replace('\\', '/').TrimStart('/'), normalizedEntry, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return null;

        using var stream = entry.OpenEntryStream();
        return computeHex(stream);
    }
}
