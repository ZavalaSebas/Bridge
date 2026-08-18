using Bridge.Core.Utilities;

namespace Bridge.Tests.Core;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("https://store.steampowered.com/app/570", true)]
    [InlineData("steam://rungameid/570", true)]
    [InlineData("com.epicgames.launcher://store/library", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    [InlineData("", false)]
    public void IsSafeToOpen_respects_scheme_allowlist(string url, bool expected)
    {
        Assert.Equal(expected, UrlValidator.IsSafeToOpen(url));
    }

    [Theory]
    [InlineData("https://images.igdb.com/igdb/image/upload/t_cover_big/co1.jpg", true)]
    [InlineData("http://127.0.0.1/secret", false)]
    [InlineData("http://localhost/image.png", false)]
    [InlineData("http://10.0.0.1/image.png", false)]
    [InlineData("http://192.168.1.50/image.png", false)]
    [InlineData("http://169.254.0.1/image.png", false)]
    public void IsSafeHttpUrl_blocks_private_and_loopback_hosts(string url, bool expected)
    {
        Assert.Equal(expected, UrlValidator.IsSafeHttpUrl(url));
    }

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("https://example.com/page", "https://example.com/page")]
    [InlineData("http://metadata.google.internal/secret", false)]
    public void SanitizePersistedUrl_rejects_unsafe_urls(string url, object? expected)
    {
        if (expected is false)
        {
            Assert.False(UrlValidator.IsSafeHttpUrl(url));
            return;
        }

        Assert.Equal(expected, UrlValidator.SanitizePersistedUrl(url));
    }
}
