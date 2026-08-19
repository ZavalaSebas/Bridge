using System.IO;
using System.Text.RegularExpressions;
using Bridge.Core.Entities;
using Bridge.Core.Enums;

namespace Bridge.Emulation;

/// Recursively imports ROMs recognized by <see cref="RomPlatformCatalog"/>,
/// including ROM files stored inside supported archives (.zip, .7z).
/// Persistence is handled by the caller.
public partial class RomScanner
{
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
                    TryAddRom(
                        alreadyImported,
                        results,
                        RomArchivePath.Combine(file, entry.EntryPath),
                        SanitizeName(Path.GetFileNameWithoutExtension(entry.EntryPath)));
                }

                continue;
            }

            if (!RomPlatformCatalog.TryGetByExtension(extension, out _))
            {
                continue;
            }

            var name = SanitizeName(Path.GetFileNameWithoutExtension(file));
            TryAddRom(alreadyImported, results, file, name);
        }

        return results;
    }

    private static void TryAddRom(
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

        var game = new Game { Name = displayName };
        game.Roms.Add(new GameRom { Name = displayName, Path = normalized });
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

    public static string SanitizeName(string name)
        => RomPropsRegex().Replace(name, string.Empty)
            .Replace('\u2019', '\'')
            .Replace("\u2122", string.Empty) // ™
            .Replace("\u00A9", string.Empty) // ©
            .Replace("\u00AE", string.Empty) // ®
            .Replace("_", " ")
            .Trim();

    public static string ToSearchName(string name)
    {
        var sanitized = SanitizeName(name);
        return string.Join(' ', sanitized
            .Replace(" - ", " ")
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }
}
