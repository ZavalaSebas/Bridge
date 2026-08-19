using Bridge.Core.Entities;
using Bridge.Emulation;

namespace Bridge.Tests.Emulation;

public class RomCheatNameResolverTests
{
    [Fact]
    public void GetCheatBaseName_UsesRomFileNameIncludingRegionTags()
    {
        var game = new Game { Name = "Super Mario World" };
        game.Roms.Add(new GameRom { Path = @"D:\roms\Super Mario World (USA).sfc" });

        Assert.Equal("Super Mario World (USA)", RomCheatNameResolver.GetCheatBaseName(game));
    }

    [Fact]
    public void GetCheatBaseName_UsesInternalRomNameInsideArchive()
    {
        var game = new Game { Name = "Super Mario World" };
        game.Roms.Add(new GameRom
        {
            Path = RomArchivePath.Combine(@"D:\roms\smw.zip", "Super Mario World (USA).sfc")
        });

        Assert.Equal("Super Mario World (USA)", RomCheatNameResolver.GetCheatBaseName(game));
    }

    [Fact]
    public void GetCheatBaseName_FallsBackToDisplayNameWhenNoRomPath()
    {
        var game = new Game { Name = "Test Game" };

        Assert.Equal("Test Game", RomCheatNameResolver.GetCheatBaseName(game));
    }
}
