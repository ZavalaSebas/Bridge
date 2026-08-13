using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

// Tests the Bridge Cloudflare Worker IGDB proxy integration. These hit the real
// endpoint (like the app does) — network tests, kept small and focused on the
// mapping shape.
public class BridgeIgdbProviderTests
{
    [Fact]
    public async Task Map_RealGenshinResponse_ProducesMetadata()
    {
        var provider = new BridgeIgdbProvider(new HttpClient());
        var metadata = await provider.SearchAsync("Genshin Impact");

        Assert.NotNull(metadata);
        Assert.Equal("Genshin Impact", metadata.Name);
        Assert.False(string.IsNullOrWhiteSpace(metadata.Description));
        Assert.NotNull(metadata.ReleaseDate);
        Assert.NotNull(metadata.CoverImage);
        Assert.NotNull(metadata.BackgroundImage);
        Assert.NotEmpty(metadata.Genres);
        Assert.NotEmpty(metadata.Developers);
        Assert.NotEmpty(metadata.Links);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNull_ForGibberish()
    {
        var provider = new BridgeIgdbProvider(new HttpClient());

        var metadata = await provider.SearchAsync("zxcvbnmasdfghjklqwertyuiop");

        Assert.Null(metadata);
    }
}
