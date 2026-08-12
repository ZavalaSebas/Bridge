using System.IO;
using System.Text.RegularExpressions;
using Bridge.Core.Entities;
using Bridge.Core.Enums;

namespace Bridge.Services;

/// <summary>
/// MVP ROM import — PLAN.md's current scope is explicitly "simple ROMs
/// (single emulator, single folder)", not Playnite's full CRC/serial/DAT
/// matching pipeline against emulation databases (that's Future Scope — see
/// PROJECT_FOUNDATION.md §28.4 for the real reference algorithm to build
/// against when it's time). This walks one folder (non-recursive), matches
/// by file extension against the profile's ImageExtensions, and creates one
/// Game per unmatched file. Dedup is "does any existing game already have a
/// Rom with this exact path" — nothing fuzzier, no checksum involved.
/// </summary>
public partial class RomScanner
{
    public IReadOnlyList<Game> Scan(string directory, Guid emulatorId, EmulatorProfile profile, IEnumerable<Game> existingGames)
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

        var extensions = profile.ImageExtensions.Count > 0
            ? profile.ImageExtensions.Select(e => e.TrimStart('.').ToLowerInvariant()).ToHashSet()
            : null;

        var results = new List<Game>();
        foreach (var file in Directory.GetFiles(directory))
        {
            if (alreadyImported.Contains(NormalizePath(file)))
            {
                continue;
            }

            var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            if (extensions is not null && !extensions.Contains(extension))
            {
                continue;
            }

            var name = SanitizeName(Path.GetFileNameWithoutExtension(file));
            var game = new Game { Name = name };
            game.Roms.Add(new GameRom { Name = name, Path = file });
            game.GameActions.Add(new GameAction
            {
                Name = "Play",
                Type = GameActionType.Emulator,
                IsPlayAction = true,
                EmulatorId = emulatorId,
                EmulatorProfileId = profile.Id
            });

            results.Add(game);
        }

        return results;
    }

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

    // Mirrors Playnite's RomName.SanitizeName (Scanner.cs): strips bracketed
    // group/region/language tags and parenthesized flags ("Super Mario [U][!]"
    // -> "Super Mario"), removes trademark symbols entirely and normalizes
    // underscores to spaces. Curly apostrophes are flattened to straight ones.
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
}
