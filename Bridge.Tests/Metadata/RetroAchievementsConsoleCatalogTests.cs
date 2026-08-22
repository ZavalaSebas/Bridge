using Bridge.Emulation;
using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

public class RetroAchievementsConsoleCatalogTests
{
    [Fact]
    public void TryResolveConsoleId_MatchesBridgePlatformAliases()
    {
        var consoles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mega Drive"] = 1,
            ["SNES"] = 3,
        };

        var megaDrive = RetroAchievementsConsoleCatalog.TryResolveConsoleId(
            "Sega Genesis / Mega Drive",
            consoles);
        var snes = RetroAchievementsConsoleCatalog.TryResolveConsoleId(
            "Super Nintendo Entertainment System",
            consoles);

        Assert.Equal(1, megaDrive);
        Assert.Equal(3, snes);
    }

    [Fact]
    public void ResolveConsoleIdsForHashLookup_GbcAlsoTriesGameBoy()
    {
        var consoles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Game Boy"] = 4,
            ["Game Boy Color"] = 6,
        };

        var ids = RetroAchievementsConsoleCatalog.ResolveConsoleIdsForHashLookup(
            "Game Boy Color",
            consoles);

        Assert.Equal([6, 4], ids);
    }
}
