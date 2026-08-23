using Bridge.Core.Entities;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class GameSaveLocationResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bridge-save-resolve-" + Guid.NewGuid().ToString("N"));

    public GameSaveLocationResolverTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup */ }
    }

    [Fact]
    public void TryResolve_PrefersRetroArchForManagedRoms()
    {
        var saves = Path.Combine(_root, "retroarch", "saves");
        Directory.CreateDirectory(saves);
        var steamRemote = Path.Combine(_root, "steam", "userdata", "1", "remote", "123");
        Directory.CreateDirectory(steamRemote);

        var game = new Game
        {
            Name = "Mario",
            ExternalId = "123",
            Roms = [new GameRom { Path = @"C:\Roms\Mario.sfc" }]
        };

        var found = GameSaveLocationResolver.TryResolve(game, new GameSaveLocationOptions
        {
            IsManagedRom = true,
            RetroArchInstallPath = Path.Combine(_root, "retroarch"),
            SteamInstallPath = Path.Combine(_root, "steam")
        });

        Assert.Equal(saves, found);
    }

    [Fact]
    public void TryResolve_UsesRomSavesWithoutManagedFlag()
    {
        var saves = Path.Combine(_root, "retroarch", "saves");
        Directory.CreateDirectory(saves);

        var game = new Game
        {
            Name = "Zelda",
            Roms = [new GameRom { Path = @"C:\Roms\Zelda.sfc" }]
        };

        var found = GameSaveLocationResolver.TryResolve(game, new GameSaveLocationOptions
        {
            RetroArchInstallPath = Path.Combine(_root, "retroarch")
        });

        Assert.Equal(saves, found);
    }

    [Fact]
    public void TryResolve_UsesSteamCloudWhenPresent()
    {
        var remote = Path.Combine(_root, "steam", "userdata", "99", "remote", "730");
        Directory.CreateDirectory(remote);

        var game = new Game { Name = "CS2", ExternalId = "730" };

        var found = GameSaveLocationResolver.TryResolve(game, new GameSaveLocationOptions
        {
            SteamInstallPath = Path.Combine(_root, "steam")
        });

        Assert.Equal(remote, found);
    }

    [Fact]
    public void TryResolve_UsesEpicCloudWhenPresent()
    {
        var cloud = Path.Combine(_root, "local", "EpicGamesLauncher", "Saved", "Cloud", "acct", "Hades");
        Directory.CreateDirectory(cloud);

        var game = new Game { Name = "Hades", ExternalId = "Hades" };

        var found = GameSaveLocationResolver.TryResolve(game, new GameSaveLocationOptions
        {
            LocalApplicationData = Path.Combine(_root, "local"),
            UserProfile = Path.Combine(_root, "empty-profile"),
            Documents = Path.Combine(_root, "empty-docs"),
            ApplicationData = Path.Combine(_root, "empty-app")
        });

        Assert.Equal(cloud, found);
    }

    [Fact]
    public void TryResolve_UsesInstallSavesFolder()
    {
        var install = Path.Combine(_root, "game");
        var saves = Path.Combine(install, "saves");
        Directory.CreateDirectory(saves);

        var game = new Game { Name = "Indie", InstallDirectory = install };

        var found = GameSaveLocationResolver.TryResolve(game, new GameSaveLocationOptions());

        Assert.Equal(saves, found);
    }

    [Fact]
    public void TryResolve_UsesSavedGamesUnderUserProfile()
    {
        var profile = Path.Combine(_root, "profile");
        var saved = Path.Combine(profile, "Saved Games", "Celeste");
        Directory.CreateDirectory(saved);

        var game = new Game { Name = "Celeste" };

        var found = GameSaveLocationResolver.TryResolve(game, new GameSaveLocationOptions
        {
            UserProfile = profile,
            Documents = Path.Combine(_root, "docs-missing"),
            ApplicationData = Path.Combine(_root, "appdata-missing"),
            LocalApplicationData = Path.Combine(_root, "local-missing")
        });

        Assert.Equal(saved, found);
    }

    [Fact]
    public void TryResolve_ReturnsNullWhenNothingMatches()
    {
        var game = new Game { Name = "Unknown Title" };

        var found = GameSaveLocationResolver.TryResolve(game, new GameSaveLocationOptions
        {
            UserProfile = Path.Combine(_root, "empty-profile"),
            Documents = Path.Combine(_root, "empty-docs"),
            ApplicationData = Path.Combine(_root, "empty-app"),
            LocalApplicationData = Path.Combine(_root, "empty-local")
        });

        Assert.Null(found);
    }
}
