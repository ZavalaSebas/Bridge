using System.Security.Cryptography;
using SharpCompress.Archives;

namespace Bridge.Emulation;

/// MD5 hash of ROM bytes — used by RetroAchievements for game identification.
public static class RomMd5
{
    public static string ComputeHex(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(data, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string? TryComputeFromRomPath(string romPath)
    {
        if (string.IsNullOrWhiteSpace(romPath))
            return null;

        try
        {
            if (RomArchivePath.TrySplit(romPath, out var archivePath, out var entryPath) &&
                !string.IsNullOrWhiteSpace(entryPath))
            {
                return TryComputeFromArchiveEntry(archivePath, entryPath);
            }

            if (!File.Exists(romPath))
                return null;

            return ComputeHex(File.ReadAllBytes(romPath));
        }
        catch
        {
            return null;
        }
    }

    private static string? TryComputeFromArchiveEntry(string archivePath, string entryPath)
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
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return ComputeHex(buffer.ToArray());
    }
}
