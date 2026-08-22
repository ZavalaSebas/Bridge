using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

public class RetroAchievementsClientParsingTests
{
    [Fact]
    public async Task GetGameAchievementsAsync_ParsesAchievementObjectMap()
    {
        const string json = """
            {
              "NumDistinctPlayers": 1000,
              "NumAwardedToUser": 1,
              "Achievements": {
                "7": {
                  "ID": 7,
                  "Title": "First Steps",
                  "Description": "Start the game.",
                  "BadgeName": "12345",
                  "Points": 5,
                  "NumAwarded": 500,
                  "DateEarned": "2024-05-01 12:00:00"
                },
                "8": {
                  "ID": 8,
                  "Title": "??????",
                  "Description": "",
                  "BadgeName": "67890",
                  "Points": 10,
                  "NumAwarded": 50,
                  "DateEarned": ""
                }
              }
            }
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        });
        var client = new RetroAchievementsClient(new HttpClient(handler));

        var snapshot = await client.GetGameAchievementsAsync("key", "user", 123);

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.UnlockedCount);
        Assert.Equal(2, snapshot.TotalCount);
        Assert.True(snapshot.Achievements[0].IsUnlocked);
        Assert.False(snapshot.Achievements[1].IsUnlocked);
        Assert.True(snapshot.Achievements[1].IsHidden);
        Assert.Equal(50.0, snapshot.Achievements[0].GlobalUnlockPercent);
        Assert.Equal("https://media.retroachievements.org/Badge/12345.png", snapshot.Achievements[0].IconUrl);
    }

    [Fact]
    public async Task GetGameAchievementsAsync_AcceptsEmptyAchievementArray()
    {
        const string json = """{ "NumDistinctPlayers": 0, "Achievements": [] }""";

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        });
        var client = new RetroAchievementsClient(new HttpClient(handler));

        Assert.Null(await client.GetGameAchievementsAsync("key", "user", 123));
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
