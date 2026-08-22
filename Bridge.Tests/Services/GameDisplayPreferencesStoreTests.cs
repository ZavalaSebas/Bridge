using Bridge.Services;

namespace Bridge.Tests.Services;

public class GameDisplayPreferencesStoreTests
{
    [Fact]
    public void HeroCoverLarge_RoundTripsPerGame()
    {
        var gameId = Guid.NewGuid();

        GameDisplayPreferencesStore.SetHeroCoverLarge(gameId, true);
        Assert.True(GameDisplayPreferencesStore.GetHeroCoverLarge(gameId));

        GameDisplayPreferencesStore.SetHeroCoverLarge(gameId, false);
        Assert.False(GameDisplayPreferencesStore.GetHeroCoverLarge(gameId));
    }
}
