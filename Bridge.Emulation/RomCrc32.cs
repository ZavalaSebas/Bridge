using System.Buffers;
namespace Bridge.Emulation;

/// CRC-32 (PKZIP / clrmame / No-Intro polynomial) for ROM identification.
public static class RomCrc32
{
    private static readonly uint[] Table = CreateTable();

    public static string ComputeHex(ReadOnlySpan<byte> data)
    {
        var crc = Compute(data);
        return crc.ToString("X8");
    }

    // Streaming overload: hashes the stream in chunks so a multi-GB ROM never has
    // to be read into memory all at once.
    public static string ComputeHex(Stream stream)
    {
        var crc = Compute(stream);
        return crc.ToString("X8");
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

    public static long? TryGetRomSize(string romPath)
    {
        if (string.IsNullOrWhiteSpace(romPath))
            return null;

        try
        {
            if (RomArchivePath.TrySplit(romPath, out var archivePath, out var entryPath) &&
                !string.IsNullOrWhiteSpace(entryPath))
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);
                var normalizedEntry = entryPath.Replace('\\', '/').TrimStart('/');
                var entry = archive.Entries.FirstOrDefault(candidate =>
                    !candidate.IsDirectory &&
                    string.Equals(candidate.Key?.Replace('\\', '/').TrimStart('/'), normalizedEntry, StringComparison.OrdinalIgnoreCase));
                return entry?.Size;
            }

            return File.Exists(romPath) ? new FileInfo(romPath).Length : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryComputeFromArchiveEntry(string archivePath, string entryPath)
        => RomArchiveEntryLookup.TryComputeHexFromArchiveEntry(archivePath, entryPath, ComputeHex);

    private static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            var index = (byte)(crc ^ value);
            crc = Table[index] ^ (crc >> 8);
        }

        return ~crc;
    }

    // Incremental CRC over a stream — reads into a pooled buffer so large ROMs
    // don't allocate a whole-file byte array.
    private static uint Compute(Stream stream)
    {
        var crc = 0xFFFFFFFFu;
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    var index = (byte)(crc ^ buffer[i]);
                    crc = Table[index] ^ (crc >> 8);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return ~crc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0
                    ? 0xEDB88320u ^ (value >> 1)
                    : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
