using Bridge.Core.Entities;

namespace Bridge.Tests.Core;

public class HeroBackgroundTests
{
    [Fact]
    public void BlackSentinel_RoundTripsKind()
    {
        Assert.True(HeroBackground.IsBlack(HeroBackground.BlackSentinel));
        Assert.Equal(HeroBackground.Kind.Black, HeroBackground.KindFromValue(HeroBackground.BlackSentinel));
        Assert.Equal(HeroBackground.BlackSentinel, HeroBackground.ValueFromKind(HeroBackground.Kind.Black));
    }

    [Fact]
    public void CustomUrl_IsDetected()
    {
        const string url = "https://example.com/banner.jpg";
        Assert.True(HeroBackground.IsCustom(url));
        Assert.Equal(HeroBackground.Kind.Custom, HeroBackground.KindFromValue(url));
        Assert.Equal(url, HeroBackground.ValueFromKind(HeroBackground.Kind.Custom, url));
    }

    [Fact]
    public void Empty_IsDefault()
    {
        Assert.True(HeroBackground.IsDefault(null));
        Assert.True(HeroBackground.IsDefault(string.Empty));
        Assert.Equal(HeroBackground.Kind.Default, HeroBackground.KindFromValue(string.Empty));
    }

    [Fact]
    public void ShouldFillHeroFromSteamLocal_RespectsCustomAndBlack()
    {
        const string custom = "https://example.com/banner.jpg";
        Assert.False(HeroBackground.ShouldFillHeroFromSteamLocal(custom, overwrite: false));
        Assert.False(HeroBackground.ShouldFillHeroFromSteamLocal(HeroBackground.BlackSentinel, overwrite: false));
        Assert.True(HeroBackground.ShouldFillHeroFromSteamLocal(string.Empty, overwrite: false));
        Assert.True(HeroBackground.ShouldFillHeroFromSteamLocal(custom, overwrite: true));
    }

    [Fact]
    public void ShouldFillArtwork_OnlyFillsMissingUnlessOverwrite()
    {
        Assert.True(HeroBackground.ShouldFillArtwork(string.Empty, overwrite: false));
        Assert.False(HeroBackground.ShouldFillArtwork("cover.jpg", overwrite: false));
        Assert.True(HeroBackground.ShouldFillArtwork("cover.jpg", overwrite: true));
    }
}
