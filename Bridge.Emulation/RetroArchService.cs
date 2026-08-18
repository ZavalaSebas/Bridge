using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using SharpCompress.Archives;

namespace Bridge.Emulation;

/// <summary>
/// Progress for Bridge-managed emulator operations. <see cref="Percent"/> is
/// null while the phase is indeterminate (e.g. resolving the release) and set
/// (0-100) while something countable runs (download, extraction).
/// </summary>
public sealed record EmulatorProgress(string Message, double? Percent = null);

/// <summary>
/// Installs and maintains Bridge's own RetroArch instance. Unlike a pinned
/// manifest, this intentionally resolves the newest official frontend release
/// and core builds at install time. Since stable RetroArch releases stopped
/// shipping Windows binaries on GitHub (they now live on Libretro's buildbot as
/// .7z archives), the frontend version is resolved from the GitHub release tag
/// and the archive is downloaded from
/// https://buildbot.libretro.com/stable/{version}/windows/x86_64/RetroArch.7z.
/// The buildbot publishes no digest for that archive, so the frontend is
/// constrained to its HTTPS host, a known .7z shape and one expected
/// retroarch.exe, then swapped atomically — the same no-published-digest policy
/// already used for the rolling cores.
/// </summary>
public sealed class RetroArchService
{
    private const string ManagedActionName = "Bridge RetroArch";
    private const long MaximumDownloadBytes = 512L * 1024 * 1024;
    private static readonly HashSet<string> FrontendHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.github.com", "buildbot.libretro.com"
    };
    private static readonly HashSet<string> CoreHosts = new(StringComparer.OrdinalIgnoreCase) { "buildbot.libretro.com" };

    private readonly IRepository<Emulator> _emulatorRepository;
    private readonly IRepository<Platform> _platformRepository;
    private readonly HttpClient _httpClient;
    private readonly EmulationPaths _paths;

    // Serializes install/refresh work so two quick Play clicks (or a Play racing
    // a forced update) can never extract over the same install directory.
    private readonly SemaphoreSlim _installGate = new(1, 1);

    public RetroArchService(
        IRepository<Emulator> emulatorRepository,
        IRepository<Platform> platformRepository,
        HttpClient httpClient,
        EmulationPaths paths)
    {
        _emulatorRepository = emulatorRepository;
        _platformRepository = platformRepository;
        _httpClient = httpClient;
        _paths = paths;
        CleanupOrphanedArtifacts();
    }

    // Removes leftovers from an interrupted install (a crash or kill mid-download
    // leaves a partial .archive; a kill mid-extraction leaves retroarch-staging-*;
    // ReplaceInstallation can leave an .old backup). Runs once at startup so a
    // broken session never leaks hundreds of MB or a stale staging dir.
    private void CleanupOrphanedArtifacts()
    {
        try
        {
            if (Directory.Exists(_paths.DownloadPath))
            {
                foreach (var file in Directory.EnumerateFiles(_paths.DownloadPath, "*.archive"))
                {
                    DeleteFile(file);
                }
            }

            var parent = Path.GetDirectoryName(_paths.InstallPath);
            if (parent is not null && Directory.Exists(parent))
            {
                foreach (var directory in Directory.EnumerateDirectories(parent, "retroarch-staging-*"))
                {
                    DeleteDirectory(directory);
                }
            }

            DeleteDirectory(_paths.InstallPath + ".old");
        }
        catch
        {
            // Cleanup is best-effort; a locked file must never break startup.
        }
    }

    public bool IsManagedRom(Game game) => game.GameActions.Any(action =>
        action.Type == Core.Enums.GameActionType.Emulator && action.Name == ManagedActionName);

    // True when a managed ROM can't launch yet because the frontend or its core
    // is missing — the Play button shows "Download" in that case. Pure disk
    // check: the same platform resolution EnsureReadyAsync uses, minus any I/O.
    public bool NeedsInstall(Game game)
    {
        if (!IsManagedRom(game))
        {
            return false;
        }

        var executable = Path.Combine(_paths.InstallPath, "retroarch.exe");
        if (!File.Exists(executable))
        {
            return true;
        }

        var platform = game.PlatformIds.Select(_platformRepository.Get).FirstOrDefault(item => item is not null);
        var definition = platform is null ? null : RomPlatformCatalog.FindByPlatformName(platform.Name);
        if (definition is null)
        {
            return false;
        }

        return !File.Exists(Path.Combine(_paths.InstallPath, "cores", definition.CoreFileName));
    }

    public async Task EnsureReadyAsync(Game game, IProgress<EmulatorProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var platform = game.PlatformIds.Select(_platformRepository.Get).FirstOrDefault(item => item is not null)
            ?? throw new InvalidOperationException($"'{game.Name}' has no recognised ROM platform.");
        var definition = RomPlatformCatalog.FindByPlatformName(platform.Name)
            ?? throw new InvalidOperationException($"Bridge does not yet manage an emulator core for '{platform.Name}'.");

        var emulator = await EnsureFrontendAsync(progress, cancellationToken);
        // A core already on disk is left alone on Play; refreshing cores to
        // the latest nightly is the explicit "Check for updates" action.
        var corePath = await EnsureCoreAsync(definition, progress, cancellationToken);
        var profile = emulator.Profiles.FirstOrDefault(item => item.Name == platform.Name);
        if (profile is null)
        {
            profile = new EmulatorProfile { Id = $"bridge-retroarch-{platform.Id:N}", Name = platform.Name };
            emulator.Profiles.Add(profile);
        }

        profile.Executable = emulator.ExecutablePath();
        profile.Arguments = "-L {CorePath} {RomPath}";
        profile.CorePath = corePath;
        profile.ImageExtensions = definition.Extensions.ToList();
        _emulatorRepository.Update(emulator);

        var action = game.GameActions.First(item => item.Type == Core.Enums.GameActionType.Emulator && item.Name == ManagedActionName);
        action.EmulatorId = emulator.Id;
        action.EmulatorProfileId = profile.Id;
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var executable = Path.Combine(_paths.InstallPath, "retroarch.exe");
        if (!File.Exists(executable))
        {
            return "RetroArch is not installed. It will be installed automatically the first time you play a recognised ROM.";
        }

        var coreDirectory = Path.Combine(_paths.InstallPath, "cores");
        var count = Directory.Exists(coreDirectory) ? Directory.EnumerateFiles(coreDirectory, "*_libretro.dll").Count() : 0;
        await Task.CompletedTask;
        return $"RetroArch is installed with {count} managed core(s). Cores are checked for updates when needed.";
    }

    public async Task UpdateInstalledAsync(IProgress<EmulatorProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var managed = _emulatorRepository.GetAll().FirstOrDefault(item => item.Name == "Bridge RetroArch");
        if (managed is null)
        {
            await EnsureFrontendAsync(progress, cancellationToken);
            return;
        }

        await EnsureFrontendAsync(progress, cancellationToken, force: true);
        foreach (var profile in managed.Profiles.Where(profile => !string.IsNullOrWhiteSpace(profile.CorePath)))
        {
            var definition = RomPlatformCatalog.FindByPlatformName(profile.Name);
            if (definition is not null)
            {
                await EnsureCoreAsync(definition, progress, cancellationToken, force: true);
            }
        }
    }

    private async Task<Emulator> EnsureFrontendAsync(IProgress<EmulatorProgress>? progress, CancellationToken cancellationToken, bool force = false)
    {
        var executable = Path.Combine(_paths.InstallPath, "retroarch.exe");
        // Fast path: an installed frontend is left alone on Play. Repeated Play
        // clicks must never hit the network or re-install anything.
        if (!force && File.Exists(executable))
        {
            return GetOrCreateManagedEmulator(executable);
        }

        await _installGate.WaitAsync(cancellationToken);
        try
        {
            // Re-check under the gate: a concurrent Play may have just installed it.
            if (!force && File.Exists(executable))
            {
                return GetOrCreateManagedEmulator(executable);
            }

            ReleaseAsset asset;
            try
            {
                progress?.Report(new EmulatorProgress("Finding the latest RetroArch release..."));
                asset = await GetLatestWindowsAssetAsync(cancellationToken);
            }
            catch when (File.Exists(executable) && !force)
            {
                // A temporary network problem must never make an already working
                // ROM library unplayable.
                return GetOrCreateManagedEmulator(executable);
            }

            // The buildbot publishes no digest for RetroArch.7z, so the installed
            // version string is the change signal: same resolved version → already
            // current, keep the existing install.
            if (File.Exists(executable) && File.Exists(_paths.VersionMarkerPath) &&
                string.Equals(await File.ReadAllTextAsync(_paths.VersionMarkerPath, cancellationToken), asset.Version, StringComparison.OrdinalIgnoreCase))
            {
                return GetOrCreateManagedEmulator(executable);
            }

            progress?.Report(new EmulatorProgress($"Downloading RetroArch {asset.Version}...", 0));
            try
            {
                var archive = await DownloadAsync(asset.Url, FrontendHosts, asset.Size, progress, cancellationToken);
                try
                {
                    progress?.Report(new EmulatorProgress("Installing RetroArch...", null));
                    // The 7z extraction of a ~200 MB build with tens of thousands of
                    // files takes ~30s and must never block the UI thread. All the
                    // I/O-heavy work (extract + atomic swap) runs on pool threads;
                    // only the progress reports marshal back to the caller.
                    var staging = Path.Combine(Path.GetDirectoryName(_paths.InstallPath)!, $"retroarch-staging-{Guid.NewGuid():N}");
                    try
                    {
                        Directory.CreateDirectory(staging);
                        await Task.Run(() =>
                        {
                            ExtractArchiveSafely(archive, staging);
                            var nestedExecutable = Directory.EnumerateFiles(staging, "retroarch.exe", SearchOption.AllDirectories).FirstOrDefault()
                                ?? throw new InvalidOperationException("The RetroArch archive did not contain retroarch.exe.");
                            ReplaceInstallation(Path.GetDirectoryName(nestedExecutable)!);
                        }, cancellationToken);
                    }
                    finally
                    {
                        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
                    }
                    await File.WriteAllTextAsync(_paths.VersionMarkerPath, asset.Version, cancellationToken);
                }
                finally
                {
                    DeleteFile(archive);
                }
            }
            catch when (File.Exists(executable) && !cancellationToken.IsCancellationRequested)
            {
                // A network drop mid-download (or a bad archive) must keep the
                // installed frontend working; the old version is untouched because
                // ReplaceInstallation only runs after a fully successful extract.
                progress?.Report(new EmulatorProgress("Could not update RetroArch; keeping the installed version."));
                return GetOrCreateManagedEmulator(executable);
            }

            return GetOrCreateManagedEmulator(executable);
        }
        finally
        {
            _installGate.Release();
        }
    }

    private async Task<string> EnsureCoreAsync(RomPlatformDefinition definition, IProgress<EmulatorProgress>? progress, CancellationToken cancellationToken, bool force = false)
    {
        var coresDirectory = Path.Combine(_paths.InstallPath, "cores");
        var corePath = Path.Combine(coresDirectory, definition.CoreFileName);
        if (File.Exists(corePath) && !force)
        {
            return corePath;
        }

        await _installGate.WaitAsync(cancellationToken);
        try
        {
            // Re-check under the gate: a concurrent Play may have just installed it.
            if (File.Exists(corePath) && !force)
            {
                return corePath;
            }

            try
            {
                progress?.Report(new EmulatorProgress($"Downloading latest {definition.PlatformName} core...", 0));
                // Core archives on the buildbot are named after the DLL
                // ("mgba_libretro.dll.zip") — the ".dll" must stay in the URL.
                var url = $"https://buildbot.libretro.com/nightly/windows/x86_64/latest/{definition.CoreFileName}.zip";
                var archive = await DownloadAsync(url, CoreHosts, 64L * 1024 * 1024, progress, cancellationToken);
                try
                {
                    Directory.CreateDirectory(coresDirectory);
                    var temporaryCore = corePath + ".new";
                    ExtractSingleFile(archive, definition.CoreFileName, temporaryCore);
                    File.Move(temporaryCore, corePath, overwrite: true);
                }
                finally
                {
                    DeleteFile(archive);
                }
            }
            catch when (File.Exists(corePath) && !cancellationToken.IsCancellationRequested)
            {
                progress?.Report(new EmulatorProgress($"Could not refresh {definition.PlatformName}; using the installed core."));
            }

            return corePath;
        }
        finally
        {
            _installGate.Release();
        }
    }

    private Emulator GetOrCreateManagedEmulator(string executable)
    {
        var emulator = _emulatorRepository.GetAll().FirstOrDefault(item => item.Name == "Bridge RetroArch");
        if (emulator is null)
        {
            emulator = new Emulator { Name = "Bridge RetroArch", InstallDirectory = _paths.InstallPath };
            _emulatorRepository.Add(emulator);
        }
        else if (!string.Equals(emulator.InstallDirectory, _paths.InstallPath, StringComparison.OrdinalIgnoreCase))
        {
            emulator.InstallDirectory = _paths.InstallPath;
            _emulatorRepository.Update(emulator);
        }

        return emulator;
    }

    private async Task<ReleaseAsset> GetLatestWindowsAssetAsync(CancellationToken cancellationToken)
    {
        // Stable RetroArch releases no longer ship Windows binaries as GitHub
        // release assets (v1.22.x only publishes the source tarball). The
        // official Windows frontend lives on Libretro's buildbot at
        // stable/{version}/windows/x86_64/RetroArch.7z — we resolve the newest
        // version from the GitHub release tag and build the buildbot URL from it.
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/libretro/RetroArch/releases/latest");
        request.Headers.UserAgent.ParseAdd("Bridge/0.2");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var tagName = document.RootElement.GetProperty("tag_name").GetString();
        var version = tagName?.TrimStart('v');
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("The latest official RetroArch release could not be resolved.");
        }

        return new ReleaseAsset(
            $"RetroArch {version}",
            $"https://buildbot.libretro.com/stable/{version}/windows/x86_64/RetroArch.7z",
            MaximumDownloadBytes,
            version);
    }

    private async Task<string> DownloadAsync(string url, IReadOnlySet<string> allowedHosts, long maximumBytes, IProgress<EmulatorProgress>? progress, CancellationToken cancellationToken)
    {
        // The default HttpClient timeout (100s) would abort a ~200 MB frontend
        // download halfway on a slow connection. Streaming a large archive needs
        // a generous ceiling; the per-chunk progress and maximumBytes guard below
        // are what actually bound the work. Redirects are handled by hand, so
        // auto-redirect must stay off.
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        var current = new Uri(url);
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            if (current.Scheme != Uri.UriSchemeHttps || !allowedHosts.Contains(current.Host))
            {
                throw new InvalidOperationException($"Bridge refused an untrusted emulator download host: {current.Host}.");
            }

            using var response = await client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                current = response.Headers.Location is { IsAbsoluteUri: true } absolute ? absolute : new Uri(current, response.Headers.Location!);
                continue;
            }

            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length > maximumBytes)
            {
                throw new InvalidOperationException("The emulator download is larger than Bridge's safety limit.");
            }

            Directory.CreateDirectory(_paths.DownloadPath);
            // Content format isn't determined by the extension (SharpCompress
            // detects .7z vs .zip from the file itself), so a neutral extension
            // keeps the frontend .7z and core .zip downloads interchangeable.
            var destination = Path.Combine(_paths.DownloadPath, $"{Guid.NewGuid():N}.archive");
            long written = 0;
            try
            {
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(destination);
                var buffer = new byte[81920];
                int read;
                var total = response.Content.Headers.ContentLength ?? 0;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    if (written > maximumBytes) throw new InvalidOperationException("The emulator download exceeded Bridge's safety limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    if (total > 0 && written % (4L * 1024 * 1024) < buffer.Length)
                    {
                        progress?.Report(new EmulatorProgress(
                            $"Downloading {(written >= total ? "done" : $"{written / 1048576.0:0.0} / {total / 1048576.0:0.0} MB")}...",
                            written * 100.0 / total));
                    }
                }
                return destination;
            }
            catch
            {
                DeleteFile(destination);
                throw;
            }
        }

        throw new InvalidOperationException("The emulator download redirected too many times.");
    }

    private static void ExtractArchiveSafely(string archivePath, string destination)
    {
        // A 7z is usually a solid archive: decompressing a single entry
        // re-reads every earlier entry, making per-entry extraction O(n²)
        // (minutes to hours). WriteToDirectory streams the whole archive in
        // one forward pass (measured: ~25s for the 14.8k-file RetroArch build).
        // SharpCompress 0.48.0 includes the GHSA-6c8g-7p36-r338 fix, so
        // WriteToDirectory now guards directory entries against traversal.
        // The caller runs this on a pool thread, never the UI.
        ArchiveFactory.WriteToDirectory(archivePath, destination, new SharpCompress.Common.ExtractionOptions
        {
            ExtractFullPath = true,
            Overwrite = true
        });
    }

    private static void ExtractSingleFile(string archivePath, string fileName, string destination)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.Where(entry => string.Equals(Path.GetFileName(entry.FullName), fileName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (entries.Count != 1) throw new InvalidOperationException($"The downloaded core did not contain exactly one '{fileName}'.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        entries[0].ExtractToFile(destination, overwrite: true);
    }

    private void ReplaceInstallation(string payload)
    {
        var target = _paths.InstallPath;
        var backup = target + ".old";
        DeleteDirectory(backup);
        if (Directory.Exists(target)) Directory.Move(target, backup);
        try { Directory.Move(payload, target); DeleteDirectory(backup); }
        catch { if (Directory.Exists(backup) && !Directory.Exists(target)) Directory.Move(backup, target); throw; }
    }

    private static void DeleteDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
    private static void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }

    private sealed record ReleaseAsset(string Name, string Url, long Size, string Version);
}

internal static class BridgeEmulatorExtensions
{
    public static string ExecutablePath(this Emulator emulator) => Path.Combine(emulator.InstallDirectory, "retroarch.exe");
}
