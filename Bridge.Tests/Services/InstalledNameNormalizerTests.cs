using Bridge.Services;

namespace Bridge.Tests.Services;

public class InstalledNameNormalizerTests
{
    [Theory]
    [InlineData("Alan Wake", "AlanWake")]
    [InlineData("Genshin Impact", "GenshinImpact")]
    [InlineData("Detroit Become Human", "DetroitBecomeHuman")]
    [InlineData("Fallout 3 goty", "Fallout3")]
    [InlineData("The Witcher 3: Wild Hunt", "The Witcher 3 - Wild Hunt")]
    public void Normalize_MatchesEquivalentNames(string libName, string candidateName)
    {
        Assert.Equal(
            InstalledNameNormalizer.Normalize(libName),
            InstalledNameNormalizer.Normalize(candidateName));
    }

    [Fact]
    public void Normalize_DoesNotMatchUnrelatedNames()
    {
        Assert.NotEqual(
            InstalledNameNormalizer.Normalize("Alan Wake"),
            InstalledNameNormalizer.Normalize("Control"));
    }

    [Fact]
    public void Normalize_RemovesEditionAndLauncherSuffixes()
    {
        Assert.Equal("fallout3", InstalledNameNormalizer.Normalize("Fallout 3 goty"));
        Assert.Equal("fallout3", InstalledNameNormalizer.Normalize("Fallout3Launcher"));
    }

    [Fact]
    public void Normalize_StripsCompoundSuffixes()
    {
        // "Fallout 3 - Game of the Year Edition" must collapse to the same
        // token as the bare "Fallout 3" — edition AND goty are both removed.
        Assert.Equal(
            InstalledNameNormalizer.Normalize("Fallout 3"),
            InstalledNameNormalizer.Normalize("Fallout 3 - Game of the Year Edition"));
        Assert.Equal("fallout3", InstalledNameNormalizer.Normalize("Fallout 3 - Game of the Year Edition"));
        Assert.Equal("fallout3", InstalledNameNormalizer.Normalize("Fallout 3 Game of the Year Edition"));
    }
}
