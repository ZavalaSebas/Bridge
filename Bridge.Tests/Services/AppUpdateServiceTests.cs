using System.Net;
using System.Net.Http;
using System.Text;
using Bridge.Services;
using Bridge.Tests.Metadata;

namespace Bridge.Tests.Services;

public class AppUpdateServiceTests
{
    private const string ReleaseJson =
        """
        {
          "tag_name": "v0.3.0",
          "assets": [
            {
              "name": "Bridge.exe",
              "browser_download_url": "https://objects.githubusercontent.com/bridge/Bridge.exe"
            }
          ]
        }
        """;

    [Fact]
    public async Task CheckForUpdateAsync_WhenRemoteIsNewer_ReturnsUpdateAvailable()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            Assert.Equal(Config.GitHubApiUrl, request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ReleaseJson, Encoding.UTF8, "application/json")
            };
        });

        var service = new AppUpdateService(new HttpClient(handler));
        var result = await service.CheckForUpdateAsync();

        Assert.Equal(AppUpdateStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.Update);
        Assert.Equal(new Version(0, 3, 0), result.Update!.Version);
        Assert.Equal("https://objects.githubusercontent.com/bridge/Bridge.exe", result.Update.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRemoteMatchesCurrent_ReturnsUpToDate()
    {
        var current = Config.AssemblyVersion.ToString(3);
        var json = ReleaseJson.Replace("0.3.0", current);
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var service = new AppUpdateService(new HttpClient(handler));
        var result = await service.CheckForUpdateAsync();

        Assert.Equal(AppUpdateStatus.UpToDate, result.Status);
        Assert.Null(result.Update);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenAssetMissing_ReturnsFailed()
    {
        const string json =
            """
            {
              "tag_name": "v9.0.0",
              "assets": [
                { "name": "other.zip", "browser_download_url": "https://example.com/other.zip" }
              ]
            }
            """;

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var service = new AppUpdateService(new HttpClient(handler));
        var result = await service.CheckForUpdateAsync();

        Assert.Equal(AppUpdateStatus.Failed, result.Status);
        Assert.Contains("Bridge.exe", result.Message);
    }
}
