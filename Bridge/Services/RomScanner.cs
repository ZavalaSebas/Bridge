using System.IO;
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
public class RomScanner
{
    public IReadOnlyList<Game> Scan(string directory, Guid emulatorId, EmulatorProfile profile, IEnumerable<Game> existingGames)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"ROM folder not found: {directory}");
        }

        var alreadyImported = existingGames
            .SelectMany(g => g.Roms)
            .Select(r => r.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var extensions = profile.ImageExtensions.Count > 0
            ? profile.ImageExtensions.Select(e => e.TrimStart('.').ToLowerInvariant()).ToHashSet()
            : null;

        var results = new List<Game>();
        foreach (var file in Directory.GetFiles(directory))
        {
            if (alreadyImported.Contains(file))
            {
                continue;
            }

            var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            if (extensions is not null && !extensions.Contains(extension))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(file);
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
}
