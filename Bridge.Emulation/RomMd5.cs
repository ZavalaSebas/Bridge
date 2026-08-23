using System.Security.Cryptography;
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

            using var stream = File.OpenRead(romPath);
            return ComputeHex(stream);
        }
        catch
        {
            return null;
        }
    }

    // Streaming hash — MD5 consumes the stream directly, so a large ROM never
    // lands in memory whole.
    private static string ComputeHex(Stream stream) =>
        Convert.ToHexString(MD5.HashData(stream)).ToLowerInvariant();

    private static string? TryComputeFromArchiveEntry(string archivePath, string entryPath)
        => RomArchiveEntryLookup.TryComputeHexFromArchiveEntry(archivePath, entryPath, ComputeHex);
}
