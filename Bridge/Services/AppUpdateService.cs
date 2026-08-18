using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Bridge.Services;

public enum AppUpdateStatus
{
    UpToDate,
    UpdateAvailable,
    NotApplicable,
    Failed
}

public sealed record AppUpdateInfo(Version Version, string DownloadUrl);

public sealed record AppUpdateCheckResult(
    AppUpdateStatus Status,
    AppUpdateInfo? Update = null,
    string? Message = null);

public sealed record AppUpdateProgress(string Message, double? Percent = null);

/// <summary>
/// Checks GitHub Releases for a newer Bridge.exe and applies updates with the
/// safe swap documented in DEVELOPMENT.md (rename running exe → .old, move
/// downloaded → current, restart, delete .old on next launch).
/// </summary>
public sealed class AppUpdateService
{
    private const long MaximumDownloadBytes = 256L * 1024 * 1024;
    private static readonly HashSet<string> AllowedDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.github.com",
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com"
    };

    private static bool IsAllowedDownloadHost(string host) =>
        AllowedDownloadHosts.Contains(host) ||
        host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private readonly HttpClient _httpClient;

    public AppUpdateService(HttpClient httpClient) => _httpClient = httpClient;

    public static void CleanupOldExe()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe))
        {
            return;
        }

        var oldExe = currentExe + ".old";
        try
        {
            if (File.Exists(oldExe))
            {
                File.Delete(oldExe);
            }
        }
        catch
        {
            // Best-effort; a locked .old from a failed delete must not block startup.
        }
    }

    public bool CanSelfUpdate =>
        Environment.ProcessPath is { Length: > 0 } path &&
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    public async Task<AppUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSelfUpdate)
        {
            return new AppUpdateCheckResult(
                AppUpdateStatus.NotApplicable,
                Message: "Updates apply only to the published Bridge.exe.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Config.GitHubApiUrl);
            request.Headers.UserAgent.ParseAdd(UserAgent());
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var root = document.RootElement;
            var tagName = root.GetProperty("tag_name").GetString();
            if (string.IsNullOrWhiteSpace(tagName) ||
                !Version.TryParse(tagName.TrimStart('v'), out var remoteVersion))
            {
                return new AppUpdateCheckResult(
                    AppUpdateStatus.Failed,
                    Message: "The latest release version could not be read.");
            }

            var currentVersion = Config.AssemblyVersion;
            if (remoteVersion <= currentVersion)
            {
                return new AppUpdateCheckResult(AppUpdateStatus.UpToDate);
            }

            var downloadUrl = FindAssetDownloadUrl(root);
            if (downloadUrl is null)
            {
                return new AppUpdateCheckResult(
                    AppUpdateStatus.Failed,
                    Message: $"Release v{remoteVersion.ToString(3)} has no {Config.UpdateAssetName} asset.");
            }

            return new AppUpdateCheckResult(
                AppUpdateStatus.UpdateAvailable,
                new AppUpdateInfo(remoteVersion, downloadUrl));
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return new AppUpdateCheckResult(
                AppUpdateStatus.Failed,
                Message: "No published release was found on GitHub.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new AppUpdateCheckResult(
                AppUpdateStatus.Failed,
                Message: $"Could not reach GitHub: {ex.Message}");
        }
    }

    public async Task ApplyUpdateAsync(
        AppUpdateInfo update,
        IProgress<AppUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Bridge could not resolve its executable path.");

        if (!CanSelfUpdate)
        {
            throw new InvalidOperationException("Updates apply only to the published Bridge.exe.");
        }

        var tempExe = Path.Combine(Path.GetTempPath(), $"Bridge_update_{Guid.NewGuid():N}.exe");
        var oldExe = currentExe + ".old";

        try
        {
            progress?.Report(new AppUpdateProgress("Downloading update...", 0));
            await DownloadFileAsync(update.DownloadUrl, tempExe, progress, cancellationToken);

            progress?.Report(new AppUpdateProgress("Installing update...", null));

            if (File.Exists(oldExe))
            {
                File.Delete(oldExe);
            }

            File.Move(currentExe, oldExe);
            File.Move(tempExe, currentExe);

            Process.Start(new ProcessStartInfo
            {
                FileName = currentExe,
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
        catch
        {
            RollbackSwap(currentExe, oldExe, tempExe);
            throw;
        }
    }

    private static void RollbackSwap(string currentExe, string oldExe, string tempExe)
    {
        try
        {
            if (File.Exists(oldExe) && !File.Exists(currentExe))
            {
                File.Move(oldExe, currentExe);
            }
            else if (File.Exists(oldExe) && File.Exists(currentExe))
            {
                File.Delete(currentExe);
                File.Move(oldExe, currentExe);
            }
        }
        catch
        {
            // Rollback is best-effort; the user may need to reinstall manually.
        }

        try
        {
            if (File.Exists(tempExe))
            {
                File.Delete(tempExe);
            }
        }
        catch
        {
            // Ignore leftover temp files.
        }
    }

    private static string? FindAssetDownloadUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets))
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var name) &&
                string.Equals(name.GetString(), Config.UpdateAssetName, StringComparison.OrdinalIgnoreCase) &&
                asset.TryGetProperty("browser_download_url", out var url))
            {
                return url.GetString();
            }
        }

        return null;
    }

    private static async Task DownloadFileAsync(
        string url,
        string destination,
        IProgress<AppUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromMinutes(15)
        };

        var current = new Uri(url);
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            if (current.Scheme != Uri.UriSchemeHttps || !IsAllowedDownloadHost(current.Host))
            {
                throw new InvalidOperationException($"Bridge refused an untrusted update host: {current.Host}.");
            }

            using var response = await client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                current = response.Headers.Location is { IsAbsoluteUri: true } absolute
                    ? absolute
                    : new Uri(current, response.Headers.Location!);
                continue;
            }

            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length > MaximumDownloadBytes)
            {
                throw new InvalidOperationException("The update download is larger than Bridge's safety limit.");
            }

            long written = 0;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(destination);
            var buffer = new byte[81920];
            var total = response.Content.Headers.ContentLength ?? 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                written += read;
                if (written > MaximumDownloadBytes)
                {
                    throw new InvalidOperationException("The update download exceeded Bridge's safety limit.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if (total > 0)
                {
                    var percent = written * 100.0 / total;
                    progress?.Report(new AppUpdateProgress($"Downloading update... {percent:F0}%", percent));
                }
            }

            return;
        }

        throw new InvalidOperationException("The update download did not resolve after too many redirects.");
    }

    private static string UserAgent()
    {
        var version = Config.AssemblyVersion.ToString(3);
        return $"Bridge/{version}";
    }
}
