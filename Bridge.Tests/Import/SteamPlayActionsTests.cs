using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Import.Steam;

namespace Bridge.Tests.Import;

public class SteamPlayActionsTests
{
    [Fact]
    public void CreatePlayAction_SteamGameWithAppId_ReturnsUrlAction()
    {
        var game = new Game
        {
            ExternalId = "730",
            SourceId = Guid.NewGuid(),
            InstallDirectory = @"C:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive"
        };

        var action = SteamPlayActions.CreatePlayAction(game);

        Assert.NotNull(action);
        Assert.Equal(GameActionType.Url, action.Type);
        Assert.True(action.IsPlayAction);
        Assert.Equal("steam://rungameid/730", action.Path);
        Assert.Equal(TrackingMode.Directory, action.TrackingMode);
        Assert.Equal("Play via Steam", action.Name);
    }

    [Fact]
    public void CreatePlayAction_CustomGame_ReturnsNull()
    {
        var game = new Game
        {
            ExternalId = "730",
            SourceId = GameSource.ManualId // IsCustomGame == true
        };

        Assert.Null(SteamPlayActions.CreatePlayAction(game));
    }

    [Fact]
    public void CreatePlayAction_NonNumericExternalId_ReturnsNull()
    {
        var game = new Game
        {
            ExternalId = "not-an-appid",
            SourceId = Guid.NewGuid()
        };

        Assert.Null(SteamPlayActions.CreatePlayAction(game));
    }

    [Fact]
    public void CreatePlayAction_EmptyExternalId_ReturnsNull()
    {
        var game = new Game
        {
            ExternalId = "",
            SourceId = Guid.NewGuid()
        };

        Assert.Null(SteamPlayActions.CreatePlayAction(game));
    }
}
