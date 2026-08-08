using Bridge.Import.Steam;

namespace Bridge.Tests.Import;

// Mirrors the real Steam layout (PROJECT_FOUNDATION.md §28.26): the square
// 32x32 clienticon is stored as a 40-hex-character .jpg inside
// appcache\librarycache\{appid}\ next to the wide header/library artwork.
public class SteamLocalIconResolverTests : IDisposable
{
    private readonly string _tempDir;

    public SteamLocalIconResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bridge-steamicon-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void TryGetLocalIconPath_WithCachedClientIcon_ReturnsItsPath()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        var cacheDir = Path.Combine(steamRoot, "appcache", "librarycache", "431960");
        Directory.CreateDirectory(cacheDir);

        File.WriteAllText(Path.Combine(cacheDir, "header.jpg"), "header");
        var iconPath = Path.Combine(cacheDir, "6b0312cda02f5f777efa2f3318c307ff9acafbb5.jpg");
        File.WriteAllText(iconPath, "icon");

        var result = SteamLocalIconResolver.TryGetLocalIconPath("431960", steamRoot);

        Assert.Equal(iconPath, result);
    }

    [Fact]
    public void TryGetLocalIconPath_NoCachedIcon_ReturnsNull()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        var cacheDir = Path.Combine(steamRoot, "appcache", "librarycache", "431960");
        Directory.CreateDirectory(cacheDir);

        File.WriteAllText(Path.Combine(cacheDir, "header.jpg"), "header");

        var result = SteamLocalIconResolver.TryGetLocalIconPath("431960", steamRoot);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetLocalIconPath_AppWithoutCacheDir_ReturnsNull()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");

        var result = SteamLocalIconResolver.TryGetLocalIconPath("431960", steamRoot);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetLocalIconPath_NonNumericAppId_ReturnsNull()
    {
        var result = SteamLocalIconResolver.TryGetLocalIconPath("not-an-appid", _tempDir);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetLocalIconPath_MissingSteamInstall_ReturnsNull()
    {
        var result = SteamLocalIconResolver.TryGetLocalIconPath("431960", Path.Combine(_tempDir, "NoSteamHere"));

        Assert.Null(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
