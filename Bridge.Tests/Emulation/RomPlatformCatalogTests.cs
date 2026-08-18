using Bridge.Emulation;

namespace Bridge.Tests.Emulation;

public class RomPlatformCatalogTests
{
    [Theory]
    [InlineData("nes", "Nintendo Entertainment System", "fceumm_libretro.dll")]
    [InlineData("sfc", "Super Nintendo Entertainment System", "snes9x_libretro.dll")]
    [InlineData("gba", "Game Boy Advance", "mgba_libretro.dll")]
    public void TryGetByExtension_RecognisesSupportedSystems(string extension, string platformName, string coreFile)
    {
        Assert.True(RomPlatformCatalog.TryGetByExtension(extension, out var platform));
        Assert.NotNull(platform);
        Assert.Equal(platformName, platform!.PlatformName);
        Assert.Equal(coreFile, platform.CoreFileName);
    }

    [Fact]
    public void TryGetByExtension_UnknownExtension_ReturnsFalse() =>
        Assert.False(RomPlatformCatalog.TryGetByExtension("txt", out _));

    [Fact]
    public void FindByPlatformName_IsCaseInsensitive()
    {
        var platform = RomPlatformCatalog.FindByPlatformName("game boy advance");
        Assert.NotNull(platform);
        Assert.Equal("mgba_libretro.dll", platform!.CoreFileName);
    }
}
