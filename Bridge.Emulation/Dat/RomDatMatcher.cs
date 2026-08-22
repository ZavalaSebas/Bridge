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

    public bool TryMatch(string romPath, out RomDatMatch? match)
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

        var crcHex = RomCrc32.TryComputeFromRomPath(romPath);
        if (crcHex is null)
            return false;

        var romSize = RomCrc32.TryGetRomSize(romPath);
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
