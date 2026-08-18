using System.IO;
using System.Text.RegularExpressions;
using Bridge.Core.Entities;
using Bridge.Core.Enums;

namespace Bridge.Emulation;

/// Recursively imports ROMs recognized by <see cref="RomPlatformCatalog"/>.
/// Persistence is handled by the caller.
public partial class RomScanner
{
    public IReadOnlyList<Game> Scan(string directory, IEnumerable<Game> existingGames)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"ROM folder not found: {directory}");
        }

        // Dedup by full path: the same folder could have been scanned with a
        // relative vs absolute path (or differing separators), which would make
        // exact string comparison miss and re-import the same ROM. Normalizing
        // both sides to a full path keeps the string comparison reliable.
        var alreadyImported = existingGames
            .SelectMany(g => g.Roms)
            .Select(r => NormalizePath(r.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<Game>();
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (alreadyImported.Contains(NormalizePath(file)))
            {
                continue;
            }

            var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            if (IsCompanionFile(extension) || !RomPlatformCatalog.TryGetByExtension(extension, out _))
            {
                continue;
            }

            var name = SanitizeName(Path.GetFileNameWithoutExtension(file));
            var game = new Game { Name = name };
            game.Roms.Add(new GameRom { Name = name, Path = file });
            game.GameActions.Add(new GameAction
            {
                // The ids are resolved immediately before launch, once Bridge
                // has installed/updated the managed RetroArch profile.
                Name = "Bridge RetroArch",
                Type = GameActionType.Emulator,
                IsPlayAction = true
            });

            results.Add(game);
        }

        return results;
    }

    private static bool IsCompanionFile(string extension) =>
        extension is "sav" or "srm"
        || (extension.StartsWith("state", StringComparison.Ordinal) && extension[5..].All(char.IsDigit))
        || (extension.StartsWith("ss", StringComparison.Ordinal) && extension.Length > 2 && extension[2..].All(char.IsDigit));

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            // Unparseable path (bad chars) — fall back to the raw string so the
            // scan can't crash on it.
            return path;
        }
    }

    // Strips [region]/ (flags) tags and trademark symbols; normalizes underscores.
    [GeneratedRegex(@"\[(.*?)\]|\((.*?)\)", RegexOptions.Compiled)]
    private static partial Regex RomPropsRegex();

    public static string SanitizeName(string name)
        => RomPropsRegex().Replace(name, string.Empty)
            .Replace('\u2019', '\'')
            .Replace("\u2122", string.Empty) // ™
            .Replace("\u00A9", string.Empty) // ©
            .Replace("\u00AE", string.Empty) // ®
            .Replace("_", " ")
            .Trim();

    // Normaliza un nombre de ROM para buscarlo en IGDB. Sobre SanitizeName
    // además reemplaza guiones separadores ("Pokemon - Emerald Version" ->
    // "Pokemon Emerald Version") y colapsa espacios múltiples. El worker de IGDB
    // usa su endpoint `search` (texto libre/fuzzy), que con un nombre sin guiones
    // sueltos empareja mucho mejor con el título real de IGDB ("Pokémon Emerald
    // Version"). Las etiquetas de región/versión entre corchetes o paréntesis ya
    // las eliminó SanitizeName.
    public static string ToSearchName(string name)
    {
        var sanitized = SanitizeName(name);
        return string.Join(' ', sanitized
            .Replace(" - ", " ")
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }
}
