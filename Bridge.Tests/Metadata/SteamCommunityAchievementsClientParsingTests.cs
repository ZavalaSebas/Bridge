using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

public class SteamCommunityAchievementsClientParsingTests
{
    [Fact]
    public async Task GetCatalogAsync_ParsesAchievementRows()
    {
        const string html = """
            <div class="achieveRow ">
                <div class="achieveImgHolder">
                    <img src="https://example/icon.jpg" width="64" height="64" border="0" />
                </div>
                <div class="achieveTxtHolder">
                    <div class="achievePercent">90.6%</div>
                    <div class="achieveTxt">
                        <h3>Elite Slayer</h3>
                        <h5>Defeat an Elite-type monster.</h5>
                    </div>
                </div>
            </div>
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(html),
        });
        var client = new SteamCommunityAchievementsClient(new HttpClient(handler));

        var snapshot = await client.GetCatalogAsync("632360", "english");

        Assert.NotNull(snapshot);
        Assert.False(snapshot!.TracksProgress);
        Assert.Equal(0, snapshot.UnlockedCount);
        var achievement = Assert.Single(snapshot.Achievements);
        Assert.Equal("Elite Slayer", achievement.Name);
        Assert.Equal("Defeat an Elite-type monster.", achievement.Description);
        Assert.Equal(90.6, achievement.GlobalUnlockPercent);
        Assert.Equal("https://example/icon.jpg", achievement.IconUrl);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
