using Bridge.Core.Entities;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class GameDetailLinkResolverTests
{
    [Fact]
    public void GetSteamStoreUrl_ValidAppId_ReturnsStorePage()
    {
        var game = new Game { ExternalId = "1551360" };

        Assert.Equal("https://store.steampowered.com/app/1551360/", GameDetailLinkResolver.GetSteamStoreUrl(game));
    }

    [Fact]
    public void GetMetacriticUrl_UsesStoredLinkWhenPresent()
    {
        var game = new Game
        {
            Name = "Test Game",
            Links =
            [
                new Link { Name = "Metacritic", Url = "https://www.metacritic.com/game/forza-horizon-5/xbox-series-x/" }
            ]
        };

        Assert.Equal("https://www.metacritic.com/game/forza-horizon-5/", GameDetailLinkResolver.GetMetacriticUrl(game));
    }

    [Fact]
    public void GetMetacriticUrl_NormalizesSteamPcMetacriticLink()
    {
        var game = new Game
        {
            Name = "Baldur's Gate 3",
            Links =
            [
                new Link { Name = "Metacritic", Url = "https://www.metacritic.com/game/pc/baldurs-gate-3/" }
            ]
        };

        Assert.Equal("https://www.metacritic.com/game/baldurs-gate-3/", GameDetailLinkResolver.GetMetacriticUrl(game));
    }

    [Fact]
    public void GetMetacriticUrl_IgnoresPlatformOnlyStoredLink()
    {
        var game = new Game
        {
            Name = "Fallout 4",
            Links =
            [
                new Link { Name = "Metacritic", Url = "https://www.metacritic.com/game/pc/" }
            ]
        };

        Assert.Equal("https://www.metacritic.com/game/fallout-4/", GameDetailLinkResolver.GetMetacriticUrl(game));
    }

    [Fact]
    public void GetMetacriticUrl_FallsBackToSlug()
    {
        var game = new Game { Name = "Fallout 4" };

        Assert.Equal("https://www.metacritic.com/game/fallout-4/", GameDetailLinkResolver.GetMetacriticUrl(game));
    }

    [Fact]
    public void BuildMetacriticSlug_NormalizesTitle()
    {
        Assert.Equal("the-legend-of-zelda-breath-of-the-wild", GameDetailLinkResolver.BuildMetacriticSlug("The Legend of Zelda: Breath of the Wild"));
    }

    [Fact]
    public void TryResolveSteamAppId_UsesSteamStoreLinkWhenExternalIdMissing()
    {
        var game = new Game
        {
            Links =
            [
                new Link { Name = "Steam Store", Url = "https://store.steampowered.com/app/632360/" }
            ]
        };

        Assert.True(GameDetailLinkResolver.TryResolveSteamAppId(game, out var appId));
        Assert.Equal(632360u, appId);
    }

    [Fact]
    public void GetCommunityScoreUrl_SteamImportedGame_ReturnsSteamReviews()
    {
        var game = new Game { ExternalId = "570", SourceId = Guid.NewGuid() };

        Assert.Equal(
            "https://steamcommunity.com/app/570/reviews/",
            GameDetailLinkResolver.GetCommunityScoreUrl(game, "Steam"));
    }

    [Fact]
    public void GetCommunityScoreUrl_BridgeGameWithSteamLink_ReturnsSteamReviews()
    {
        var game = new Game
        {
            SourceId = GameSource.BridgeId,
            Name = "Risk of Rain 2",
            Links =
            [
                new Link { Name = "Steam Store", Url = "https://store.steampowered.com/app/632360/" }
            ]
        };

        Assert.Equal(
            "https://steamcommunity.com/app/632360/reviews/",
            GameDetailLinkResolver.GetCommunityScoreUrl(game, "Bridge"));
    }

    [Fact]
    public void GetEpicLibraryUrl_OpensEpicLauncherLibrary()
    {
        var game = new Game { ExternalId = "Flame" };

        Assert.Equal("com.epicgames.launcher://store/library", GameDetailLinkResolver.GetEpicLibraryUrl(game));
    }

    [Fact]
    public void GetCommunityScoreUrl_NonSteamGame_ReturnsMetacritic()
    {
        var game = new Game { Name = "Fallout 4", ExternalId = "igdb-123" };

        Assert.Equal(
            "https://www.metacritic.com/game/fallout-4/",
            GameDetailLinkResolver.GetCommunityScoreUrl(game, "Epic"));
    }
}
