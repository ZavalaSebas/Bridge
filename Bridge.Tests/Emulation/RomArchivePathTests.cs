using System.IO.Compression;
using Bridge.Emulation;

namespace Bridge.Tests.Emulation;

public class RomArchivePathTests
{
    [Fact]
    public void Combine_UsesHashDelimiterAndForwardSlashesForEntry()
    {
        var path = RomArchivePath.Combine(@"C:\Roms\game.zip", @"folder\Super Mario.sfc");

        Assert.Equal(@"C:\Roms\game.zip#folder/Super Mario.sfc", path);
    }

    [Fact]
    public void GetCheatBaseName_UsesInternalRomNameForArchivePaths()
    {
        var path = RomArchivePath.Combine(@"C:\Roms\smw.zip", "Super Mario World (USA).sfc");

        Assert.Equal("Super Mario World (USA)", RomArchivePath.GetCheatBaseName(path));
    }

    [Fact]
    public void GetRomExtension_ReadsExtensionFromArchiveEntry()
    {
        var path = RomArchivePath.Combine(@"C:\Roms\smw.zip", "Super Mario World (USA).sfc");

        Assert.Equal("sfc", RomArchivePath.GetRomExtension(path));
    }
}
