namespace Bridge.Emulation;

/// Curated ROM platform → Libretro core mapping. Versions resolve at install time.
public sealed record RomPlatformDefinition(string PlatformName, string CoreFileName, params string[] Extensions);

public static class RomPlatformCatalog
{
    public static readonly IReadOnlyList<RomPlatformDefinition> Platforms =
    [
        new("Nintendo Entertainment System", "fceumm_libretro.dll", "nes"),
        new("Super Nintendo Entertainment System", "snes9x_libretro.dll", "sfc", "smc"),
        new("Nintendo 64", "mupen64plus_next_libretro.dll", "n64", "z64", "v64"),
        new("Game Boy", "sameboy_libretro.dll", "gb"),
        new("Game Boy Color", "sameboy_libretro.dll", "gbc"),
        new("Game Boy Advance", "mgba_libretro.dll", "gba"),
        new("Nintendo DS", "melondsds_libretro.dll", "nds"),
        new("Sega Genesis / Mega Drive", "genesis_plus_gx_libretro.dll", "md", "gen", "smd"),
        new("Sega Master System", "genesis_plus_gx_libretro.dll", "sms"),
        new("Sega Game Gear", "genesis_plus_gx_libretro.dll", "gg"),
        new("Atari 2600", "stella_libretro.dll", "a26"),
        new("Atari 7800", "prosystem_libretro.dll", "a78"),
        new("PC Engine / TurboGrafx-16", "mednafen_pce_libretro.dll", "pce"),
        new("Atari Lynx", "holani_libretro.dll", "lnx"),
        new("WonderSwan / WonderSwan Color", "mednafen_wswan_libretro.dll", "ws", "wsc")
    ];

    private static readonly IReadOnlyDictionary<string, RomPlatformDefinition> ByExtension =
        Platforms.SelectMany(platform => platform.Extensions.Select(extension => (extension, platform)))
            .ToDictionary(pair => pair.extension, pair => pair.platform, StringComparer.OrdinalIgnoreCase);

    public static bool TryGetByExtension(string extension, out RomPlatformDefinition? platform) =>
        ByExtension.TryGetValue(extension.TrimStart('.'), out platform);

    public static RomPlatformDefinition? FindByPlatformName(string name) =>
        Platforms.FirstOrDefault(platform => string.Equals(platform.PlatformName, name, StringComparison.OrdinalIgnoreCase));
}
