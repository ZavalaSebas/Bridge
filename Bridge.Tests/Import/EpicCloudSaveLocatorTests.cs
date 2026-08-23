using Bridge.Import.Epic;

namespace Bridge.Tests.Import;

public class EpicCloudSaveLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bridge-epic-saves-" + Guid.NewGuid().ToString("N"));

    public EpicCloudSaveLocatorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup */ }
    }

    [Fact]
    public void TryFind_ReturnsCloudFolderForAppName()
    {
        var cloud = Path.Combine(_root, "EpicGamesLauncher", "Saved", "Cloud", "account1", "Fortnite");
        Directory.CreateDirectory(cloud);

        var found = EpicCloudSaveLocator.TryFind(_root, "Fortnite");

        Assert.Equal(cloud, found);
    }

    [Fact]
    public void TryFind_MatchesAppNameCaseInsensitively()
    {
        var cloud = Path.Combine(_root, "EpicGamesLauncher", "Saved", "Cloud", "account1", "Hades2");
        Directory.CreateDirectory(cloud);

        var found = EpicCloudSaveLocator.TryFind(_root, "hades2");

        Assert.Equal(cloud, found);
    }

    [Fact]
    public void TryFind_PrefersNewerAccountFolder()
    {
        var older = Path.Combine(_root, "EpicGamesLauncher", "Saved", "Cloud", "aaa", "Game");
        var newer = Path.Combine(_root, "EpicGamesLauncher", "Saved", "Cloud", "bbb", "Game");
        Directory.CreateDirectory(older);
        Directory.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-2));
        Directory.CreateDirectory(newer);
        Directory.SetLastWriteTimeUtc(newer, DateTime.UtcNow);

        var found = EpicCloudSaveLocator.TryFind(_root, "Game");

        Assert.Equal(newer, found);
    }

    [Fact]
    public void TryFind_ReturnsNullWhenMissing()
    {
        Directory.CreateDirectory(Path.Combine(_root, "EpicGamesLauncher", "Saved", "Cloud", "account1"));

        Assert.Null(EpicCloudSaveLocator.TryFind(_root, "Missing"));
    }

    [Fact]
    public void TryFind_ReturnsNullWhenAppNameEmpty()
    {
        Assert.Null(EpicCloudSaveLocator.TryFind(_root, " "));
    }
}
