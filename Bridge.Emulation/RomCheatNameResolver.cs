using Bridge.Core.Entities;

namespace Bridge.Emulation;

/// RetroArch and libretro-database identify cheat files by the ROM filename
/// (without extension), including region tags like "(USA)". Bridge's display
/// name strips those tags via <see cref="RomScanner.SanitizeName"/>.
public static class RomCheatNameResolver
{
    public static string GetCheatBaseName(Game game)
    {
        var romPath = game.Roms.FirstOrDefault()?.Path;
        if (!string.IsNullOrWhiteSpace(romPath))
        {
            return RomArchivePath.GetCheatBaseName(romPath);
        }

        return game.Name;
    }
}
