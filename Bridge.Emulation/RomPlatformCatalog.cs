namespace Bridge.Emulation;

/// Curated ROM platform → Libretro core mapping. Versions resolve at install time.
public sealed record RomPlatformDefinition(
    string PlatformName,
    string CoreFileName,
    string? LibretroCheatFolder,
    string? RetroArchCoreName,
    params string[] Extensions)
{
    public bool SupportsCheats =>
        !string.IsNullOrWhiteSpace(LibretroCheatFolder) &&
        !string.IsNullOrWhiteSpace(RetroArchCoreName);
}

public static class RomPlatformCatalog
{
    public static readonly IReadOnlyList<RomPlatformDefinition> Platforms =
    [
        new("Nintendo Entertainment System", "fceumm_libretro.dll", "Nintendo - Nintendo Entertainment System", "FCEUmm", "nes"),
        new("Super Nintendo Entertainment System", "snes9x_libretro.dll", "Nintendo - Super Nintendo Entertainment System", "Snes9x", "sfc", "smc"),
        new("Nintendo 64", "mupen64plus_next_libretro.dll", "Nintendo - Nintendo 64", "Mupen64Plus-Next", "n64", "z64", "v64"),
        new("Game Boy", "sameboy_libretro.dll", "Nintendo - Game Boy", "SameBoy", "gb"),
        new("Game Boy Color", "sameboy_libretro.dll", "Nintendo - Game Boy Color", "SameBoy", "gbc"),
        new("Game Boy Advance", "mgba_libretro.dll", "Nintendo - Game Boy Advance", "mGBA", "gba"),
        new("Nintendo DS", "melondsds_libretro.dll", "Nintendo - Nintendo DS", "melonDS DS", "nds"),
        new("Sega Genesis / Mega Drive", "genesis_plus_gx_libretro.dll", "Sega - Mega Drive - Genesis", "Genesis Plus GX", "md", "gen", "smd"),
        new("Sega Master System", "genesis_plus_gx_libretro.dll", "Sega - Master System - Mark III", "Genesis Plus GX", "sms"),
        new("Sega Game Gear", "genesis_plus_gx_libretro.dll", "Sega - Game Gear", "Genesis Plus GX", "gg"),
        new("Atari 2600", "stella_libretro.dll", "Atari - 2600", "Stella", "a26"),
        new("Atari 7800", "prosystem_libretro.dll", "Atari - 7800", "ProSystem", "a78"),
        new("PC Engine / TurboGrafx-16", "mednafen_pce_libretro.dll", "NEC - PC Engine - TurboGrafx 16", "Beetle PCE", "pce"),
        new("Atari Lynx", "holani_libretro.dll", "Atari - Lynx", "Holani", "lnx"),
        new("WonderSwan / WonderSwan Color", "mednafen_wswan_libretro.dll", null, null, "ws", "wsc")
    ];

    private static readonly IReadOnlyDictionary<string, RomPlatformDefinition> ByExtension =
        Platforms.SelectMany(platform => platform.Extensions.Select(extension => (extension, platform)))
            .ToDictionary(pair => pair.extension, pair => pair.platform, StringComparer.OrdinalIgnoreCase);

    public static bool TryGetByExtension(string extension, out RomPlatformDefinition? platform) =>
        ByExtension.TryGetValue(extension.TrimStart('.'), out platform);

    public static RomPlatformDefinition? FindByPlatformName(string name) =>
        Platforms.FirstOrDefault(platform => string.Equals(platform.PlatformName, name, StringComparison.OrdinalIgnoreCase));
}
