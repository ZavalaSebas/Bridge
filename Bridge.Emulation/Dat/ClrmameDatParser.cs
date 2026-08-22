using System.Text.RegularExpressions;

namespace Bridge.Emulation.Dat;

public sealed record RomDatRom(uint Size, string Crc);

public sealed record RomDatGame(string Name, string? Region, IReadOnlyList<RomDatRom> Roms);

/// Parses clrmamepro / No-Intro DAT files into CRC lookup tables.
public static partial class ClrmameDatParser
{
    public static IReadOnlyDictionary<string, RomDatGame> ParseFile(string path)
    {
        var text = File.ReadAllText(path);
        return Parse(text);
    }

    public static IReadOnlyDictionary<string, RomDatGame> Parse(string text)
    {
        var byCrc = new Dictionary<string, RomDatGame>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in SplitGameBlocks(text))
        {
            var nameMatch = GameNameRegex().Match(block);
            if (!nameMatch.Success)
                continue;

            var gameName = nameMatch.Groups[1].Value.Trim();
            if (gameName.Length == 0)
                continue;

            var roms = new List<RomDatRom>();
            foreach (Match romMatch in RomLineRegex().Matches(block))
            {
                if (!uint.TryParse(romMatch.Groups[1].Value, out var size))
                    continue;

                var crc = romMatch.Groups[2].Value.ToUpperInvariant();
                roms.Add(new RomDatRom(size, crc));
            }

            if (roms.Count == 0)
                continue;

            var region = ParseRegion(block, gameName);
            var game = new RomDatGame(gameName, region, roms);
            foreach (var rom in roms)
                byCrc[rom.Crc] = game;
        }

        return byCrc;
    }

    private static IEnumerable<string> SplitGameBlocks(string text)
    {
        var starts = new List<int>();
        var index = 0;
        while ((index = text.IndexOf("game (", index, StringComparison.Ordinal)) >= 0)
        {
            starts.Add(index);
            index += "game (".Length;
        }

        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1] : text.Length;
            yield return text[start..end].TrimEnd();
        }
    }

    private static string? ParseRegion(string block, string gameName)
    {
        var regionMatch = RegionRegex().Match(block);
        if (regionMatch.Success)
        {
            var region = regionMatch.Groups[1].Value.Trim();
            if (region.Length > 0)
                return region;
        }

        return ExtractRegionFromName(gameName);
    }

    internal static string? ExtractRegionFromName(string gameName)
    {
        var match = TitleRegionRegex().Match(gameName);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"^\s*name\s+""([^""]+)""", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GameNameRegex();

    [GeneratedRegex(@"^\s*region\s+""([^""]+)""", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex RegionRegex();

    [GeneratedRegex(@"\(([^)]+)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TitleRegionRegex();

    [GeneratedRegex(@"\bsize\s+(\d+)\s+crc\s+([0-9A-Fa-f]{8})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RomLineRegex();
}
