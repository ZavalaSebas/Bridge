using System.Net;
using System.Net.Http;
using System.Text;
using Bridge.Services;
using Bridge.Tests.Metadata;

namespace Bridge.Tests.Services;

public class AppUpdateServiceTests
{
    private static string NewerVersion(int bump = 1)
    {
        var current = Config.AssemblyVersion;
        return $"{current.Major}.{current.Minor}.{current.Build + bump}";
    }

    private static string Release(string tag, bool prerelease, bool hasBridgeAsset = true)
    {
        var assetName = hasBridgeAsset ? "Bridge.exe" : "other.zip";
        var url = hasBridgeAsset
            ? "https://objects.githubusercontent.com/bridge/Bridge.exe"
            : "https://example.com/other.zip";
        return $$"""
            {
              "tag_name": "{{tag}}",
              "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
              "assets": [
                {
                  "name": "{{assetName}}",
                  "browser_download_url": "{{url}}"
                }
              ]
            }
            """;
    }

    private static string Releases(params string[] releases) =>
        $"[{string.Join(",", releases)}]";

    private static AppUpdateService ServiceResponding(Func<HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            Assert.Equal(Config.GitHubReleasesUrl, request.RequestUri?.ToString());
            return respond();
        });
        return new AppUpdateService(new HttpClient(handler));
    }

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task CheckForUpdateAsync_WhenRemoteIsNewer_ReturnsUpdateAvailable()
    {
        var service = ServiceResponding(() => Ok(Releases(Release($"v{NewerVersion()}", prerelease: false))));
        var result = await service.CheckForUpdateAsync();

        Assert.Equal(AppUpdateStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.Update);
        Assert.Equal(new Version(NewerVersion()), result.Update!.Version);
        Assert.Equal("https://objects.githubusercontent.com/bridge/Bridge.exe", result.Update.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRemoteMatchesCurrent_ReturnsUpToDate()
    {
        var service = ServiceResponding(() =>
            Ok(Releases(Release($"v{Config.AssemblyVersion.ToString(3)}", prerelease: false))));
        var result = await service.CheckForUpdateAsync();

        Assert.Equal(AppUpdateStatus.UpToDate, result.Status);
        Assert.Null(result.Update);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenAssetMissing_ReturnsFailed()
    {
        var service = ServiceResponding(() =>
            Ok(Releases(Release($"v{NewerVersion(100)}", prerelease: false, hasBridgeAsset: false))));
        var result = await service.CheckForUpdateAsync();

        Assert.Equal(AppUpdateStatus.Failed, result.Status);
        Assert.Contains("Bridge.exe", result.Message);
    }

    [Fact]
    public async Task CheckForUpdateAsync_StableChannel_SkipsPrerelease()
    {
        var service = ServiceResponding(() => Ok(Releases(
            Release($"v{NewerVersion(5)}-beta1", prerelease: true),
            Release($"v{NewerVersion()}", prerelease: false))));
        var result = await service.CheckForUpdateAsync(UpdateChannel.Stable);

        Assert.Equal(AppUpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(NewerVersion()), result.Update!.Version);
    }

    [Fact]
    public async Task CheckForUpdateAsync_BetaChannel_AcceptsPrerelease()
    {
        var service = ServiceResponding(() => Ok(Releases(
            Release($"v{NewerVersion()}-beta1", prerelease: true))));
        var result = await service.CheckForUpdateAsync(UpdateChannel.Beta);

        Assert.Equal(AppUpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(NewerVersion()), result.Update!.Version);
        Assert.Equal("https://objects.githubusercontent.com/bridge/Bridge.exe", result.Update!.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_BetaChannel_DoesNotDowngradeOntoPrerelease()
    {
        // A prerelease tagged with the same numeric version as the running
        // stable must never be offered (numeric prefix comparison).
        var service = ServiceResponding(() => Ok(Releases(
            Release($"v{Config.AssemblyVersion.ToString(3)}-beta1", prerelease: true))));
        var result = await service.CheckForUpdateAsync(UpdateChannel.Beta);

        Assert.Equal(AppUpdateStatus.UpToDate, result.Status);
        Assert.Null(result.Update);
    }

    [Fact]
    public async Task CheckForUpdateAsync_StableChannel_WhenOnlyNewerPrereleaseExists_ReportsBetaAvailable()
    {
        var service = ServiceResponding(() => Ok(Releases(
            Release($"v{NewerVersion()}-beta1", prerelease: true),
            Release($"v{Config.AssemblyVersion.ToString(3)}", prerelease: false))));
        var result = await service.CheckForUpdateAsync(UpdateChannel.Stable);

        Assert.Equal(AppUpdateStatus.UpToDate, result.Status);
        Assert.NotNull(result.Message);
        Assert.Contains("beta", result.Message);
    }
}