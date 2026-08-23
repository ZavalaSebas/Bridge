using Bridge.Import.Steam;

namespace Bridge.Tests.Import;

public class SteamCloudSaveLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bridge-steam-saves-" + Guid.NewGuid().ToString("N"));

    public SteamCloudSaveLocatorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup */ }
    }

    [Fact]
    public void TryFind_ReturnsModernRemoteFolder()
    {
        var remote = Path.Combine(_root, "userdata", "12345", "remote", "730");
        Directory.CreateDirectory(remote);

        var found = SteamCloudSaveLocator.TryFind(_root, "730");

        Assert.Equal(remote, found);
    }

    [Fact]
    public void TryFind_FallsBackToLegacyAppRemoteFolder()
    {
        var remote = Path.Combine(_root, "userdata", "12345", "570", "remote");
        Directory.CreateDirectory(remote);

        var found = SteamCloudSaveLocator.TryFind(_root, "570");

        Assert.Equal(remote, found);
    }

    [Fact]
    public void TryFind_PrefersNewerAccountFolder()
    {
        var older = Path.Combine(_root, "userdata", "111", "remote", "400");
        var newer = Path.Combine(_root, "userdata", "222", "remote", "400");
        Directory.CreateDirectory(older);
        Directory.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-2));
        Directory.CreateDirectory(newer);
        Directory.SetLastWriteTimeUtc(newer, DateTime.UtcNow);

        var found = SteamCloudSaveLocator.TryFind(_root, "400");

        Assert.Equal(newer, found);
    }

    [Fact]
    public void TryFind_IgnoresNonNumericUserdataFolders()
    {
        Directory.CreateDirectory(Path.Combine(_root, "userdata", "ac", "remote", "10"));

        Assert.Null(SteamCloudSaveLocator.TryFind(_root, "10"));
    }

    [Fact]
    public void TryFind_ReturnsNullWhenMissing()
    {
        Directory.CreateDirectory(Path.Combine(_root, "userdata", "1"));

        Assert.Null(SteamCloudSaveLocator.TryFind(_root, "999"));
    }
}
