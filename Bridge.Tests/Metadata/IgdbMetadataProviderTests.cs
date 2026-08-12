using System.Net;
using System.Text;
using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

public class IgdbMetadataProviderTests
{
    private const string TokenJson = """{"access_token":"fake-token","expires_in":3600,"token_type":"bearer"}""";

    private const string GamesJson = """
        [
          {
            "name": "Half-Life 2",
            "summary": "A dystopian sci-fi shooter.",
            "first_release_date": 1100736000,
            "cover": { "url": "//images.igdb.com/igdb/image/upload/t_thumb/abc123.jpg" },
            "genres": [ { "name": "Shooter" }, { "name": "Adventure" } ]
          }
        ]
        """;

    private static (IgdbMetadataProvider Provider, FakeHttpMessageHandler Handler) BuildProvider(string gamesResponseJson)
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var body = request.RequestUri!.Host.Contains("twitch")
                ? TokenJson
                : gamesResponseJson;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler);
        var settings = new IgdbSettings { ClientId = "id", ClientSecret = "secret" };
        var authClient = new IgdbAuthClient(httpClient, settings);
        return (new IgdbMetadataProvider(httpClient, settings, authClient), handler);
    }

    [Fact]
    public async Task SearchAsync_MapsAllFieldsFromARealisticResponse()
    {
        var (provider, _) = BuildProvider(GamesJson);

        var metadata = await provider.SearchAsync("Half-Life 2");

        Assert.NotNull(metadata);
        Assert.Equal("Half-Life 2", metadata.Name);
        Assert.Equal("A dystopian sci-fi shooter.", metadata.Description);
        Assert.Equal(new Core.Entities.ReleaseDate(2004, 11, 18), metadata.ReleaseDate);
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/abc123.jpg", metadata.CoverImage);
        Assert.Equal(["Shooter", "Adventure"], metadata.Genres);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNull_WhenNoResults()
    {
        var (provider, _) = BuildProvider("[]");

        var metadata = await provider.SearchAsync("Some Game That Does Not Exist");

        Assert.Null(metadata);
    }

    [Fact]
    public async Task SearchAsync_ThrowsWhenNotConfigured()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var settings = new IgdbSettings(); // no credentials
        var provider = new IgdbMetadataProvider(httpClient, settings, new IgdbAuthClient(httpClient, settings));

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SearchAsync("Anything"));
    }

    [Theory]
    [InlineData("//images.igdb.com/igdb/image/upload/t_thumb/abc123.jpg", "https://images.igdb.com/igdb/image/upload/t_cover_big/abc123.jpg")]
    [InlineData("https://images.igdb.com/igdb/image/upload/t_thumb/xyz.png", "https://images.igdb.com/igdb/image/upload/t_cover_big/xyz.png")]
    public void UpgradeImageUrl_AddsSchemeAndUpgradesSize(string input, string expected)
    {
        Assert.Equal(expected, IgdbMetadataProvider.UpgradeImageUrl(input));
    }

    [Fact]
    public void MapToGameMetadata_HandlesMissingOptionalFields()
    {
        var game = new IgdbGame { Name = "Minimal Game" };

        var metadata = IgdbMetadataProvider.MapToGameMetadata(game);

        Assert.Equal("Minimal Game", metadata.Name);
        Assert.Equal(string.Empty, metadata.Description);
        Assert.Null(metadata.ReleaseDate);
        Assert.Null(metadata.CoverImage);
        Assert.Empty(metadata.Genres);
    }

    [Fact]
    public void MapToGameMetadata_IgnoresOutOfRangeTimestamp()
    {
        // A corrupt first_release_date must not discard the whole result.
        var game = new IgdbGame { Name = "Bad Date", FirstReleaseDate = long.MaxValue };

        var metadata = IgdbMetadataProvider.MapToGameMetadata(game);

        Assert.Equal("Bad Date", metadata.Name);
        Assert.Null(metadata.ReleaseDate);
    }
}
