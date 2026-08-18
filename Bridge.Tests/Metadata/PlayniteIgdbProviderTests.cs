using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

// Integration tests against the hosted IGDB proxy — Category=Integration, not default CI.
public class PlayniteIgdbProviderTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Map_RealGenshinResponse_ProducesMetadata()
    {
        var provider = new PlayniteIgdbProvider(new HttpClient());
        var metadata = await provider.SearchAsync("Genshin Impact");

        Assert.NotNull(metadata);
        Assert.Equal("Genshin Impact", metadata.Name);
        Assert.False(string.IsNullOrWhiteSpace(metadata.Description));
        Assert.NotNull(metadata.ReleaseDate);
        Assert.NotNull(metadata.CoverImage);
        Assert.NotNull(metadata.BackgroundImage);
        Assert.Contains(metadata.Links, l => l.Name.Equals("Wikipedia", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(metadata.Links, l => l.Name.Equals("Facebook", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchAsync_ReturnsNull_ForGibberish()
    {
        var provider = new PlayniteIgdbProvider(new HttpClient());

        // Nonsense search terms should return null, not throw.
        var metadata = await provider.SearchAsync("zxcvbnmasdfghjklqwertyuiop");

        Assert.Null(metadata);
    }
}
