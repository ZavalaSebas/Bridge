namespace Bridge.Emulation.Dat;

/// Maps ROM extensions to libretro No-Intro DAT file names.
public static class RomDatCatalog
{
    private const string DatBaseUrl =
        "https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/";

    private static readonly IReadOnlyDictionary<string, string> ExtensionToDatFile =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nes"] = "Nintendo - Nintendo Entertainment System.dat",
            ["sfc"] = "Nintendo - Super Nintendo Entertainment System.dat",
            ["smc"] = "Nintendo - Super Nintendo Entertainment System.dat",
            ["n64"] = "Nintendo - Nintendo 64.dat",
            ["z64"] = "Nintendo - Nintendo 64.dat",
            ["v64"] = "Nintendo - Nintendo 64.dat",
            ["gb"] = "Nintendo - Game Boy.dat",
            ["gbc"] = "Nintendo - Game Boy Color.dat",
            ["gba"] = "Nintendo - Game Boy Advance.dat",
            ["nds"] = "Nintendo - Nintendo DS.dat",
            ["md"] = "Sega - Mega Drive - Genesis.dat",
            ["gen"] = "Sega - Mega Drive - Genesis.dat",
            ["smd"] = "Sega - Mega Drive - Genesis.dat",
            ["sms"] = "Sega - Master System - Mark III.dat",
            ["gg"] = "Sega - Game Gear.dat",
            ["a26"] = "Atari - 2600.dat",
            ["a78"] = "Atari - 7800.dat",
            ["pce"] = "NEC - PC Engine - TurboGrafx 16.dat",
            ["lnx"] = "Atari - Lynx.dat",
            ["ws"] = "Bandai - WonderSwan.dat",
            ["wsc"] = "Bandai - WonderSwan Color.dat",
        };

    public static bool TryGetDatFileName(string extension, out string datFileName) =>
        ExtensionToDatFile.TryGetValue(extension.TrimStart('.'), out datFileName!);

    public static string GetDownloadUrl(string datFileName) =>
        DatBaseUrl + Uri.EscapeDataString(datFileName);
}
