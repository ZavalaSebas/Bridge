using System.Net;
using System.Text;
using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

public class IgdbAuthClientTests
{
    [Fact]
    public async Task GetAccessTokenAsync_ReturnsTokenFromResponse()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"access_token":"fake-token-123","expires_in":3600,"token_type":"bearer"}""", Encoding.UTF8, "application/json")
        });
        var client = new IgdbAuthClient(new HttpClient(handler), new IgdbSettings { ClientId = "id", ClientSecret = "secret" });

        var token = await client.GetAccessTokenAsync();

        Assert.Equal("fake-token-123", token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_CachesToken_DoesNotRefetchWhileValid()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"access_token":"fake-token-123","expires_in":3600,"token_type":"bearer"}""", Encoding.UTF8, "application/json")
        });
        var client = new IgdbAuthClient(new HttpClient(handler), new IgdbSettings { ClientId = "id", ClientSecret = "secret" });

        await client.GetAccessTokenAsync();
        await client.GetAccessTokenAsync();
        await client.GetAccessTokenAsync();

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ThrowsWhenNotConfigured()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new IgdbAuthClient(new HttpClient(handler), new IgdbSettings());

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAccessTokenAsync());
    }
}
