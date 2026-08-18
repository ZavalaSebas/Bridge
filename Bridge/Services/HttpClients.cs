using System.Net.Http;

namespace Bridge.Services;

/// <summary>
/// Short-lived metadata/API calls (IGDB, Steam store, GitHub release checks).
/// </summary>
public sealed class MetadataHttpClient : IDisposable
{
    public HttpClient Client { get; }

    public MetadataHttpClient()
    {
        Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Config.MetadataRequestTimeoutSeconds)
        };
    }

    public void Dispose() => Client.Dispose();
}

/// <summary>
/// Large or long-running downloads (RetroArch frontend/cores). Kept separate
/// so a slow emulator download never shares timeout state with metadata calls.
/// </summary>
public sealed class DownloadHttpClient : IDisposable
{
    public HttpClient Client { get; }

    public DownloadHttpClient()
    {
        Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Config.DownloadRequestTimeoutSeconds)
        };
    }

    public void Dispose() => Client.Dispose();
}
