using Bridge.Assets;

namespace Bridge.Tests.Assets;

public class DefaultGameArtworkTests
{
    [Theory]
    [InlineData(GameArtworkFallback.Icon)]
    [InlineData(GameArtworkFallback.Cover)]
    [InlineData(GameArtworkFallback.Background)]
    public void Get_ReturnsFrozenImage(GameArtworkFallback fallback)
    {
        var source = DefaultGameArtwork.Get(fallback);

        Assert.NotNull(source);
        Assert.True(source!.IsFrozen);
    }

    [Fact]
    public void Get_None_ReturnsNull() =>
        Assert.Null(DefaultGameArtwork.Get(GameArtworkFallback.None));
}
