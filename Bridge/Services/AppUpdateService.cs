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

    // The update handshake relies on marker files living next to the exe:
    //
    //   <exe>.update-pending   written by ApplyUpdateAsync right after the swap,
    //                          before the new exe is launched. Means "a new exe
    //                          was just swapped in and has not yet proven it starts".
    //   <exe>.old              the previously working exe, kept as the rollback copy.
    //   <exe>.failed           a broken new exe moved aside by a rollback.
    //
    // Runs at the very start of OnStartup (before any window/DB/DI work). It
    // either keeps the rollback copy armed (pending + old present), or cleans up
    // a confirmed/abandoned update.
    public static void HandleUpdateHandshake()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe))
        {
            return;
        }

        var oldExe = currentExe + ".old";
        var pendingMarker = currentExe + ".update-pending";
        var failedExe = currentExe + ".failed";
        try
        {
            if (File.Exists(pendingMarker))
            {
                // Pending + old present: the new exe is running for the first
                // time after a swap and the previous exe is still armed as the
                // rollback copy. Do NOT delete it here — ConfirmUpdateApplied()
                // clears it after a successful startup, or RollbackToPrevious()
                // restores it if startup fails.
                if (File.Exists(oldExe))
                {
                    return;
                }

                // Pending marker with no old exe: the swap was confirmed on a
                // previous run (or abandoned); the marker is just stale.
                File.Delete(pendingMarker);
                return;
            }

            // No pending update: clear leftovers from a previous swap. The old
            // exe is a leftover when the new one already ran fine; a .failed is
            // the broken exe a rollback moved aside.
            if (File.Exists(oldExe))
            {
                File.Delete(oldExe);
            }

            if (File.Exists(failedExe))
            {
                File.Delete(failedExe);
            }
        }
        catch
        {
            // Best-effort; a locked .old must not block startup.
        }
    }

    // Called after the new exe's startup succeeded (main window shown). The new
    // exe has proven it runs, so the rollback copy and handshake markers go away.
    public static void ConfirmUpdateApplied()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe))
        {
            return;
        }

        var oldExe = currentExe + ".old";
        var pendingMarker = currentExe + ".update-pending";
        try
        {
            if (File.Exists(oldExe))
            {
                File.Delete(oldExe);
            }

            if (File.Exists(pendingMarker))
            {
                File.Delete(pendingMarker);
            }
        }
        catch
        {
            // Best-effort; a locked .old must not break the running app.
        }
    }

    // Called from OnStartup's catch when the NEW exe failed to start (bad XAML,
    // DB/DI failure, etc.). If a pending update is armed, restores the previous
    // exe over the broken one and relaunches it. Returns true when a rollback
    // happened (the caller should not continue starting the current process).
    public static bool RollbackToPrevious()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe))
        {
            return false;
        }

        var oldExe = currentExe + ".old";
        var pendingMarker = currentExe + ".update-pending";
        if (!File.Exists(oldExe) || !File.Exists(pendingMarker))
        {
            return false;
        }

        var failedExe = currentExe + ".failed";
        try
        {
            // Move the broken new exe aside, restore the previous working exe,
            // clear the handshake, and relaunch it. The .failed leftover is
            // removed on the next HandleUpdateHandshake.
            File.Move(currentExe, failedExe);
            File.Move(oldExe, currentExe);
            File.Delete(pendingMarker);

            Process.Start(new ProcessStartInfo
            {
                FileName = currentExe,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            // Best-effort rollback; the user may need to restore manually.
            return false;
        }
    }

    public bool CanSelfUpdate =>
        Environment.ProcessPath is { Length: > 0 } path &&
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    public async Task<AppUpdateCheckResult> CheckForUpdateAsync(
        UpdateChannel? channel = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanSelfUpdate)
        {
            return new AppUpdateCheckResult(
                AppUpdateStatus.NotApplicable,
                Message: "Updates apply only to the published Bridge.exe.");
        }

        var effectiveChannel = channel ?? UpdateChannelSettingsStore.Load();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Config.GitHubReleasesUrl);
            request.Headers.UserAgent.ParseAdd(UserAgent());
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var root = document.RootElement;
            var release = SelectRelease(root, effectiveChannel);
            if (release is null)
            {
                return new AppUpdateCheckResult(
                    AppUpdateStatus.Failed,
                    Message: effectiveChannel == UpdateChannel.Beta
                        ? "No compatible beta release was found on GitHub."
                        : "No compatible release was found on GitHub.");
            }

            var tagName = release.Value.TryGetProperty("tag_name", out var tagProperty)
                ? tagProperty.GetString()
                : null;
            if (!TryParseTagVersion(tagName, out var remoteVersion))
            {
                return new AppUpdateCheckResult(
                    AppUpdateStatus.Failed,
                    Message: "The latest release version could not be read.");
            }

            var currentVersion = Config.AssemblyVersion;
            if (remoteVersion <= currentVersion)
            {
                // On the stable channel the newest release overall may still be a
                // prerelease that's newer than what the user runs — say so instead
                // of a bare "up to date", so testers learn where betas opt in.
                var betaHint = effectiveChannel == UpdateChannel.Stable
                    ? DescribeNewerPrerelease(root, currentVersion)
                    : null;
                return new AppUpdateCheckResult(
                    AppUpdateStatus.UpToDate,
                    Message: betaHint);
            }

            var downloadUrl = FindAssetDownloadUrl(release.Value);
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

    // The releases endpoint returns the newest release first. Pick the first
    // one that matches the channel and carries a parseable version tag: the
    // stable channel skips prereleases, the beta channel accepts them.
    private static JsonElement? SelectRelease(JsonElement root, UpdateChannel channel)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var release in root.EnumerateArray())
        {
            if (channel == UpdateChannel.Stable &&
                release.TryGetProperty("prerelease", out var prerelease) &&
                prerelease.ValueKind is JsonValueKind.True)
            {
                continue;
            }

            var tag = release.TryGetProperty("tag_name", out var tagProperty)
                ? tagProperty.GetString()
                : null;
            if (TryParseTagVersion(tag, out _))
            {
                return release;
            }
        }

        return null;
    }

    // Prerelease tags carry a suffix (v0.3.0-beta1); the numeric prefix is the
    // version Bridge compares against. Comparison is deliberately numeric: a
    // beta tagged 0.3.0-beta1 never "beats" an installed 0.3.0 stable, so a
    // stable user can't be downgraded onto a beta. The channel decision is the
    // GitHub `prerelease` flag, not the tag text.
    private static bool TryParseTagVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var text = tag.Trim().TrimStart('v', 'V');
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            text = text[..dash];
        }

        var plus = text.IndexOf('+');
        if (plus >= 0)
        {
            text = text[..plus];
        }

        var parsed = Version.TryParse(text, out var parsedVersion);
        version = parsedVersion ?? new Version(0, 0, 0, 0);
        return parsed;
    }

    // When the stable channel is up to date but a newer prerelease exists, tell
    // the user where it went instead of leaving them wondering why betas never
    // arrive on their machine.
    private static string? DescribeNewerPrerelease(JsonElement root, Version currentVersion)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var release in root.EnumerateArray())
        {
            if (!release.TryGetProperty("prerelease", out var prerelease) ||
                prerelease.ValueKind is not JsonValueKind.True)
            {
                continue;
            }

            var tag = release.TryGetProperty("tag_name", out var tagProperty)
                ? tagProperty.GetString()
                : null;
            if (TryParseTagVersion(tag, out var version) && version > currentVersion)
            {
                return "A beta build is available. Enable the beta channel in About to receive it.";
            }
        }

        return null;
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

            // Back up the library DB before touching the exe. If the new version
            // corrupts or migrates the DB and then fails, the user can restore
            // this copy by hand (documented in DEVELOPMENT.md). Best-effort: a
            // locked DB must never abort the update.
            BackupDatabase();

            if (File.Exists(oldExe))
            {
                File.Delete(oldExe);
            }

            File.Move(currentExe, oldExe);
            File.Move(tempExe, currentExe);

            // Arm the handshake so the new exe knows to keep the rollback copy
            // until it has proven it starts. Written after the swap so a failure
            // mid-swap leaves no pending marker.
            try
            {
                File.WriteAllText(currentExe + ".update-pending", update.Version.ToString());
            }
            catch
            {
                // Best-effort; without the marker the .old is cleaned up next
                // launch, losing the rollback copy but not breaking the update.
            }

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

    // Copies the library DB (bridge.db) to bridge.db.bak-update next to it before
    // an update applies. The copy is a manual-recovery safety net, not an
    // auto-restored state: if an update corrupts the DB, the user (or support)
    // can restore this file. Kept next to the DB so it lives with the data.
    private static void BackupDatabase()
    {
        try
        {
            if (!File.Exists(Config.DatabasePath))
            {
                return;
            }

            File.Copy(Config.DatabasePath, Config.DatabasePath + ".bak-update", overwrite: true);
        }
        catch
        {
            // Best-effort; a locked DB must never abort an update.
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
