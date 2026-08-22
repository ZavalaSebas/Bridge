using System.IO;
using System.Text.RegularExpressions;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Emulation.Dat;

namespace Bridge.Emulation;

/// Recursively imports ROMs recognized by <see cref="RomPlatformCatalog"/>,
/// including ROM files stored inside supported archives (.zip, .7z).
/// Persistence is handled by the caller.
public partial class RomScanner(RomDatMatcher datMatcher)
{
    private readonly RomDatMatcher _datMatcher = datMatcher;
    public IReadOnlyList<Game> Scan(string directory, IEnumerable<Game> existingGames)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"ROM folder not found: {directory}");
        }

        var alreadyImported = existingGames
            .SelectMany(g => g.Roms)
            .Select(r => RomArchivePath.Normalize(r.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<Game>();
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            if (IsCompanionFile(extension))
            {
                continue;
            }

            if (RomArchivePath.IsContainerExtension(extension))
            {
                foreach (var entry in RomArchiveCatalog.EnumerateRomEntries(file))
                {
                    var romPath = RomArchivePath.Combine(file, entry.EntryPath);
                    TryAddRom(alreadyImported, results, romPath, ResolveDisplayName(romPath));
                }

                continue;
            }

            if (!RomPlatformCatalog.TryGetByExtension(extension, out _))
            {
                continue;
            }

            TryAddRom(alreadyImported, results, file, ResolveDisplayName(file));
        }

        return results;
    }

    private string ResolveDisplayName(string romPath)
    {
        if (_datMatcher.TryMatch(romPath, out var match) &&
            !string.IsNullOrWhiteSpace(match!.Name))
        {
            return match.Name.Trim();
        }

        return SanitizeName(Path.GetFileNameWithoutExtension(RomArchivePath.GetRomFileName(romPath)));
    }

    private void TryAddRom(
        ISet<string> alreadyImported,
        ICollection<Game> results,
        string romPath,
        string displayName)
    {
        var normalized = RomArchivePath.Normalize(romPath);
        if (alreadyImported.Contains(normalized))
        {
            return;
        }

        var crcHex = RomCrc32.TryComputeFromRomPath(normalized);
        string? datRegion = null;
        var datPlatform = RomDatMatcher.ResolvePlatformName(normalized);
        if (_datMatcher.TryMatch(normalized, out var match))
        {
            datRegion = match!.Region;
            datPlatform = match.PlatformName;
            crcHex = match.Crc;
        }

        var game = new Game { Name = displayName };
        game.Roms.Add(new GameRom
        {
            Name = displayName,
            Path = normalized,
            Crc = crcHex,
            DatRegion = datRegion,
            DatPlatform = datPlatform
        });
        game.GameActions.Add(new GameAction
        {
            Name = "Bridge RetroArch",
            Type = GameActionType.Emulator,
            IsPlayAction = true
        });

        results.Add(game);
    }

    private static bool IsCompanionFile(string extension) =>
        extension is "sav" or "srm"
        || (extension.StartsWith("state", StringComparison.Ordinal) && extension[5..].All(char.IsDigit))
        || (extension.StartsWith("ss", StringComparison.Ordinal) && extension.Length > 2 && extension[2..].All(char.IsDigit));

    // Strips [region]/ (flags) tags and trademark symbols; normalizes underscores.
    [GeneratedRegex(@"\[(.*?)\]|\((.*?)\)", RegexOptions.Compiled)]
    private static partial Regex RomPropsRegex();

    [GeneratedRegex(@"\s+(Español|Spanish|Ingles|English|Français|Frances|Deutsch|Italiano|Italian)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LanguageSuffixRegex();

    [GeneratedRegex(@"^From TV Animation\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TvAnimationPrefixRegex();

    public static string SanitizeName(string name)
        => LanguageSuffixRegex().Replace(
                RomPropsRegex().Replace(name, string.Empty)
                    .Replace('\u2019', '\'')
                    .Replace("\u2122", string.Empty) // ™
                    .Replace("\u00A9", string.Empty) // ©
                    .Replace("\u00AE", string.Empty) // ®
                    .Replace("_", " ")
                    .Trim(),
                string.Empty)
            .Trim();

    public static string ToSearchName(string name)
    {
        var sanitized = SanitizeName(name);
        return string.Join(' ', sanitized
            .Replace(" - ", " ")
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }

    // Common Spanish ROM title words mapped to IGDB-friendly English equivalents.
    private static readonly (string From, string To)[] TitleWordHints =
    [
        ("Amarillo", "Yellow"),
        ("Amarilla", "Yellow"),
        ("Rojo", "Red"),
        ("Roja", "Red"),
        ("Azul", "Blue"),
        ("Verde", "Green"),
        ("Esmeralda", "Emerald"),
        ("Rubí", "Ruby"),
        ("Rubi", "Ruby"),
        ("Zafiro", "Sapphire"),
        ("Oro", "Gold"),
        ("Plata", "Silver"),
        ("Platino", "Platinum"),
        ("Cristal", "Crystal"),
        ("Edicion", "Edition"),
        ("Versión", "Version"),
    ];

    // Ordered IGDB search candidates for a ROM display name: normalized title,
    // Spanish-to-English hints, then common suffixes (e.g. Pokémon + Version).
    public static IReadOnlyList<string> GetMetadataSearchNames(string displayName, string? datCanonicalName = null)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var search = ToSearchName(raw);
            if (string.IsNullOrWhiteSpace(search))
                return;

            if (seen.Add(search))
                results.Add(search);
        }

        if (!string.IsNullOrWhiteSpace(datCanonicalName))
            TryAdd(datCanonicalName);

        var prepared = PrepareForMetadataSearch(displayName);
        TryAdd(prepared);
        TryAddPokemonCanonicalNames(prepared, TryAdd);

        var hinted = ApplyTitleWordHints(prepared);
        if (!string.Equals(hinted, prepared, StringComparison.OrdinalIgnoreCase))
            TryAdd(hinted);

        if (hinted.Contains("Pokemon", StringComparison.OrdinalIgnoreCase) &&
            !hinted.Contains("Version", StringComparison.OrdinalIgnoreCase) &&
            !results.Any(name => name.Contains("Version", StringComparison.OrdinalIgnoreCase)))
        {
            TryAdd($"{hinted} Version");
        }

        return results;
    }

    private static string PrepareForMetadataSearch(string displayName)
    {
        var result = SanitizeName(displayName);
        result = TvAnimationPrefixRegex().Replace(result, string.Empty).Trim();
        return result;
    }

    private static void TryAddPokemonCanonicalNames(string prepared, Action<string?> tryAdd)
    {
        if (!prepared.Contains("Pokemon", StringComparison.OrdinalIgnoreCase))
            return;

        var hinted = ApplyTitleWordHints(prepared);
        if (ContainsWord(hinted, "Yellow", "Amarillo", "Amarilla"))
            tryAdd("Pokemon Yellow Version");
        else if (ContainsWord(hinted, "Crystal", "Cristal"))
            tryAdd("Pokemon Crystal Version");
        else if (ContainsWord(hinted, "Platinum", "Platino"))
            tryAdd("Pokemon Platinum Version");
        else if (ContainsWord(hinted, "Emerald", "Esmeralda"))
            tryAdd("Pokemon Emerald Version");
        else if (ContainsWord(hinted, "Ruby", "Rubi", "Rubí"))
            tryAdd("Pokemon Ruby Version");
        else if (ContainsWord(hinted, "Sapphire", "Zafiro"))
            tryAdd("Pokemon Sapphire Version");
        else if (ContainsWord(hinted, "Red", "Rojo", "Roja"))
            tryAdd("Pokemon Red Version");
        else if (ContainsWord(hinted, "Blue", "Azul"))
            tryAdd("Pokemon Blue Version");
        else if (ContainsWord(hinted, "Gold", "Oro"))
            tryAdd("Pokemon Gold Version");
        else if (ContainsWord(hinted, "Silver", "Plata"))
            tryAdd("Pokemon Silver Version");
    }

    private static bool ContainsWord(string text, params string[] words) =>
        words.Any(word => Regex.IsMatch(
            text,
            $@"\b{Regex.Escape(word)}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    private static string ApplyTitleWordHints(string name)
    {
        var result = name;
        foreach (var (from, to) in TitleWordHints)
        {
            result = Regex.Replace(
                result,
                $@"\b{Regex.Escape(from)}\b",
                to,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return result;
    }
}
