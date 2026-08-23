namespace Bridge.Emulation.Dat;

/// Identifies ROM files via No-Intro CRC databases.
public sealed class RomDatMatcher
{
    public static RomDatMatcher Disabled { get; } = new(new RomDatStore(), enabled: false);

    private readonly RomDatStore _store;
    private readonly bool _enabled;

    public RomDatMatcher(RomDatStore store, bool enabled = true)
    {
        _store = store;
        _enabled = enabled;
    }

    public bool TryResolve(string romPath, out string? canonicalName, out string? crcHex)
    {
        if (TryMatch(romPath, out var match))
        {
            canonicalName = match!.Name;
            crcHex = match.Crc;
            return true;
        }

        canonicalName = null;
        crcHex = null;
        return false;
    }

    public bool TryMatch(string romPath, out RomDatMatch? match) =>
        TryMatch(romPath, knownCrc: null, out match);

    // Overload that reuses an already-computed CRC (e.g. the one stored on a ROM
    // from the initial scan) instead of re-reading and re-hashing the file — the
    // hot path when re-identifying an existing library. With a trusted CRC we also
    // skip the extra archive open for the size check; a CRC collision across ROMs
    // of different sizes is astronomically unlikely for curated sets.
    public bool TryMatch(string romPath, string? knownCrc, out RomDatMatch? match)
    {
        match = null;

        if (!_enabled || string.IsNullOrWhiteSpace(romPath))
            return false;

        var extension = RomArchivePath.GetRomExtension(romPath);
        if (!RomDatCatalog.TryGetDatFileName(extension, out var datFileName))
            return false;

        var platformName = ResolvePlatformName(romPath);
        if (platformName is null)
            return false;

        var hasKnownCrc = !string.IsNullOrWhiteSpace(knownCrc);

        // With a stored CRC we skip re-reading/re-hashing the file, but still confirm
        // it's actually present — otherwise a deleted or unreadable ROM would keep
        // matching on its old CRC and retain stale DAT identity. RomFileExists also
        // validates the entry inside an archive. The fresh-hash path fails naturally
        // when the file can't be read.
        if (hasKnownCrc && !RomArchivePath.RomFileExists(romPath))
            return false;

        var crcHex = hasKnownCrc ? knownCrc : RomCrc32.TryComputeFromRomPath(romPath);
        if (string.IsNullOrWhiteSpace(crcHex))
            return false;

        var romSize = hasKnownCrc ? null : RomCrc32.TryGetRomSize(romPath);
        var game = _store.Lookup(datFileName, crcHex, romSize);
        if (game is null && romSize is null or <= 0)
            game = _store.Lookup(datFileName, crcHex);

        if (game is null)
            return false;

        match = new RomDatMatch(game.Name, game.Region, platformName, crcHex);
        return true;
    }

    public static string? ResolvePlatformName(string romPath)
    {
        var extension = RomArchivePath.GetRomExtension(romPath);
        return RomPlatformCatalog.TryGetByExtension(extension, out var platform)
            ? platform!.PlatformName
            : null;
    }

    public static string? ResolveRegion(string? datRegion, string? romName) =>
        datRegion ?? (string.IsNullOrWhiteSpace(romName) ? null : ClrmameDatParser.ExtractRegionFromName(romName));
}
