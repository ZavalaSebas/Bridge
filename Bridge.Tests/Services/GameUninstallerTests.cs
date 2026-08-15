using Bridge.Core.Entities;
using Bridge.Services;

namespace Bridge.Tests.Services;

// Resolve picks the per-source uninstaller. Registry matching (ResolveFromRegistry)
// needs a real Windows registry, so it's covered by launching the app; these cover
// the deterministic Steam/Epic URIs and the fallback shape.
public class GameUninstallerTests
{
    [Fact]
    public void Resolve_SteamAppId_ReturnsSteamUninstallUri()
    {
        var game = new Game { ExternalId = "730", Name = "Counter-Strike 2" };

        Assert.Equal("steam://uninstall/730", GameUninstaller.Resolve(game, "Steam"));
    }

    [Fact]
    public void Resolve_SteamWithNonNumericId_FallsThrough()
    {
        var game = new Game { ExternalId = "not-an-appid" };

        // No Steam URI for a bogus id — it must fall through to the registry
        // path, which (with no registry entries) yields null.
        Assert.Null(GameUninstaller.Resolve(game, "Steam"));
    }

    [Fact]
    public void Resolve_EpicGame_OpensLibraryPage()
    {
        var game = new Game { ExternalId = "Fortnite", Name = "Fortnite" };

        // Epic's launcher protocol has no uninstall action (only
        // launch/updatecheck/installer) — the resolver opens the library page so
        // the user uninstalls from the client, and the watcher detects it.
        Assert.Equal(
            "com.epicgames.launcher://store/library",
            GameUninstaller.Resolve(game, "Epic"));
    }

    [Fact]
    public void Resolve_ManualGame_WithoutRegistryEntry_ReturnsNull()
    {
        var game = new Game { Name = "Sonic Fan Game" };

        // A manually-added game has no launcher id and (typically) no registry
        // entry — the caller reports "no uninstaller found".
        Assert.Null(GameUninstaller.Resolve(game, "Manual"));
    }
}
