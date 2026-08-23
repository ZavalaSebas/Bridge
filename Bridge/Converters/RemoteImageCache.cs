using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using Bridge;
using Bridge.Core.Utilities;

namespace Bridge.Converters;

/// <summary>
/// Frozen bitmap cache for library artwork. Virtualized lists often miss async
/// UriSource repaints; pre-decoded frozen images update through Subscribe callbacks.
/// </summary>
public static class RemoteImageCache
{
    // Cap the in-memory decoded cache by BYTES (not count): a wall of full-size
    // covers would otherwise balloon RAM. Entries are evicted least-recently-used.
    private const long MaxCacheBytes = 128L * 1024 * 1024; // ~128 MB of decoded artwork

    // Max images decoded concurrently during a bulk preload (see PreloadAndWaitAsync).
    private const int MaxPreloadConcurrency = 8;

    // Keyed by URL *and* decode size: the same artwork can live at a thumbnail
    // size and a hero size without one serving the other blurry/oversized.
    private readonly record struct CacheKey(string Url, ArtworkDecodeSize Size);

    // LRU cache guarded by CacheLock: the linked list orders entries from
    // least-recently-used (First) to most-recently-used (Last); the index gives
    // O(1) lookup. Get bumps an entry to the back; inserts evict from the front
    // until the total decoded size is back under the cap.
    private sealed record CacheEntry(CacheKey Key, BitmapImage Image, long Bytes);

    private static readonly object CacheLock = new();
    private static readonly Dictionary<CacheKey, LinkedListNode<CacheEntry>> CacheIndex = new();
    private static readonly LinkedList<CacheEntry> CacheLru = new();
    private static long _cacheBytes;
    // In-flight loads keyed by URL+size, storing the decode task so a preload can
    // await it (the window waits for artwork before its first paint).
    private static readonly ConcurrentDictionary<CacheKey, Task> InFlight = new();

    private static readonly object CallbacksLock = new();
    private static readonly Dictionary<CacheKey, List<Action>> PendingCallbacks = new();

    // Captured once on the UI thread at startup and used for every decode
    // continuation, so callbacks always marshal back to the UI thread regardless
    // of which thread started the load (Preload/Get from the UI thread today).
    internal static TaskScheduler UiScheduler = TaskScheduler.Default;

    // WPF's BitmapImage downloader needs a Dispatcher, so an HTTP UriSource on a
    // pool thread never completes (the image stays blank — "no cover loaded").
    // Downloading the bytes here and decoding from a stream keeps the whole load
    // synchronous and thread-agnostic. HttpClient is thread-safe; share one.
    private static readonly HttpClient DownloadClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Returns the cached frozen image for <paramref name="url"/>, or null and
    /// starts a background decode when it isn't cached yet.
    /// </summary>
    public static BitmapImage? Get(string url, ArtworkDecodeSize size = ArtworkDecodeSize.Native)
    {
        var key = new CacheKey(url, size);
        if (CacheTryGet(key) is { } cached)
        {
            return cached;
        }

        BeginLoad(key);
        return null;
    }

    /// <summary>Returns true when a decoded image for <paramref name="url"/> is in memory.</summary>
    public static bool IsCached(string url, ArtworkDecodeSize size = ArtworkDecodeSize.Native) =>
        CacheContains(new CacheKey(url, size));

    // TEMP: memory diagnostics — count and decoded-byte total of the live image
    // cache. Reversible: delete this method and the MemoryDiagnostics caller.
    internal static (int Count, long ApproxBytes) MemorySnapshot()
    {
        lock (CacheLock)
        {
            return (CacheIndex.Count, _cacheBytes);
        }
    }

    /// <summary>
    /// Synchronously decodes <paramref name="url"/> into the (url, size) cache from
    /// LOCAL sources only — a local file path, or a remote image whose bytes are
    /// already in the on-disk cache. Never downloads: returns false when a remote
    /// image isn't on disk yet, so startup can warm the selected hero without ever
    /// blocking on the network. Safe to call on the UI thread.
    /// NOTE: the decode below is duplicated from LoadSynchronously on purpose —
    /// LoadSynchronously (the live cache path) is left untouched to avoid any risk.
    /// </summary>
    public static bool TryWarmFromDisk(string url, ArtworkDecodeSize size = ArtworkDecodeSize.Native)
    {
        var key = new CacheKey(url, size);
        if (CacheContains(key))
            return true;

        try
        {
            BitmapImage image;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https" &&
                UrlValidator.IsSafeHttpUrl(uri.AbsoluteUri))
            {
                // Remote artwork: only the on-disk byte cache — never download here.
                var bytes = TryReadDiskBytes(uri);
                if (bytes is null)
                    return false;

                var decodeWidth = DecodeWidthFor(size, () => new MemoryStream(bytes));

                using var stream = new MemoryStream(bytes);
                var remote = new BitmapImage();
                remote.BeginInit();
                remote.CacheOption = BitmapCacheOption.OnLoad;
                if (decodeWidth > 0)
                    remote.DecodePixelWidth = decodeWidth;
                remote.StreamSource = stream;
                remote.EndInit();
                remote.Freeze();
                image = remote;
            }
            else
            {
                // Local file path — a direct disk read, no network.
                var localWidth = DecodeWidthFor(size, () => File.OpenRead(new Uri(url).LocalPath));
                var local = new BitmapImage();
                local.BeginInit();
                local.CacheOption = BitmapCacheOption.OnLoad;
                if (localWidth > 0)
                    local.DecodePixelWidth = localWidth;
                local.UriSource = new Uri(url);
                local.EndInit();
                local.Freeze();
                image = local;
            }

            CacheInsert(key, image);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // The on-disk read half of GetBytes, without the download fallback — so this
    // path never touches the network. Returns null when the image isn't cached yet.
    private static byte[]? TryReadDiskBytes(Uri uri)
    {
        var file = Path.Combine(Config.ImageCachePath, CacheKeyFor(uri.AbsoluteUri));
        if (!File.Exists(file))
            return null;

        try
        {
            return File.ReadAllBytes(file);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Warms the cache for a set of URLs (e.g. every game's icon at startup).</summary>
    public static void Preload(IEnumerable<string> urls, ArtworkDecodeSize size = ArtworkDecodeSize.Native)
    {
        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            BeginLoad(new CacheKey(url, size));
        }
    }

    /// <summary>
    /// Warms the cache like <see cref="Preload"/> and returns a task that
    /// completes when every decode has finished (failed URLs included). The
    /// caller (startup) awaits this so the first paint already has the artwork.
    /// </summary>
    public static async Task PreloadAndWaitAsync(
        IEnumerable<string> urls,
        IProgress<(int Completed, int Total)>? progress = null,
        ArtworkDecodeSize size = ArtworkDecodeSize.Native)
    {
        var distinct = urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = distinct.Count;
        progress?.Report((0, total));
        if (total == 0)
            return;

        var completed = 0;
        // Bound how many images decode at once: without this, warming a large
        // library would fan out one thread-pool decode per URL and hold every
        // downloaded byte buffer in memory simultaneously.
        using var throttle = new SemaphoreSlim(MaxPreloadConcurrency);
        var tasks = distinct.Select(async url =>
        {
            await throttle.WaitAsync().ConfigureAwait(false);
            try
            {
                await BeginLoad(new CacheKey(url, size)).ConfigureAwait(false);
            }
            finally
            {
                throttle.Release();
                var done = Interlocked.Increment(ref completed);
                progress?.Report((done, total));
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes <paramref name="callback"/> (on the UI thread) once the image for
    /// <paramref name="url"/> is available — immediately if it's already cached,
    /// otherwise as soon as the background decode finishes. Used by CachedImage
    /// so a control re-renders the moment its image is ready.
    /// </summary>
    public static void Subscribe(string url, Action callback, ArtworkDecodeSize size = ArtworkDecodeSize.Native)
    {
        var key = new CacheKey(url, size);
        bool loaded;
        lock (CallbacksLock)
        {
            loaded = CacheContains(key);
            if (!loaded)
            {
                if (!PendingCallbacks.TryGetValue(key, out var callbacks))
                {
                    callbacks = [];
                    PendingCallbacks[key] = callbacks;
                }

                callbacks.Add(callback);
            }
        }

        if (loaded)
        {
            callback();
            return;
        }

        BeginLoad(key);
    }

    /// <summary>Removes a callback registered by <see cref="Subscribe"/>.</summary>
    public static void Unsubscribe(string url, Action callback, ArtworkDecodeSize size = ArtworkDecodeSize.Native)
    {
        var key = new CacheKey(url, size);
        lock (CallbacksLock)
        {
            if (PendingCallbacks.TryGetValue(key, out var callbacks))
            {
                callbacks.Remove(callback);
                if (callbacks.Count == 0)
                {
                    PendingCallbacks.Remove(key);
                }
            }
        }
    }

    private static Task BeginLoad(CacheKey key)
    {
        if (InFlight.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var loadTask = Task.Run(() => LoadSynchronously(key));
        if (!InFlight.TryAdd(key, loadTask))
        {
            return InFlight[key];
        }

        // Populate the cache and fire callbacks on the continuation. This runs
        // on the thread pool (NOT UiScheduler): startup awaits these tasks with
        // GetResult() on the UI thread, and a UI-scheduled continuation can't
        // run while the UI thread is blocked — the cache would never fill and
        // the await would deadlock. The callbacks themselves are marshaled to
        // the UI thread below (they touch Image.Source).
        _ = loadTask.ContinueWith(
            completed =>
            {
                InFlight.TryRemove(key, out _);

                var image = completed.IsCompletedSuccessfully ? completed.Result : null;
                if (image is not null)
                {
                    CacheInsert(key, image);
                }

                // Collect the pending callbacks, then run them on the UI thread.
                List<Action>? callbacks = null;
                lock (CallbacksLock)
                {
                    if (PendingCallbacks.TryGetValue(key, out callbacks))
                    {
                        PendingCallbacks.Remove(key);
                    }
                }

                if (callbacks is not null && callbacks.Count > 0)
                {
                    // Callbacks touch Image.Source (UI thread only) — marshal the
                    // invocation to the UI scheduler.
                    var toRun = callbacks;
                    Task.Factory.StartNew(
                        () =>
                        {
                            foreach (var callback in toRun)
                            {
                                callback();
                            }
                        },
                        CancellationToken.None,
                        TaskCreationOptions.None,
                        UiScheduler);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);

        return loadTask;
    }

    private static BitmapImage? LoadSynchronously(CacheKey key)
    {
        var url = key.Url;
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https" &&
                UrlValidator.IsSafeHttpUrl(uri.AbsoluteUri))
            {
                // Remote artwork: serve from the on-disk cache when present,
                // otherwise download the bytes ourselves (HttpClient works on
                // any thread) and store them for the next run. Setting an HTTP
                // UriSource here would hand the download to WPF's dispatcher-
                // bound Downloader, which never finishes on a pool thread.
                var bytes = GetBytes(uri);
                if (bytes is null)
                    return null;

                var decodeWidth = DecodeWidthFor(key.Size, () => new MemoryStream(bytes));

                using var stream = new MemoryStream(bytes);
                var remote = new BitmapImage();
                remote.BeginInit();
                remote.CacheOption = BitmapCacheOption.OnLoad;
                if (decodeWidth > 0)
                    remote.DecodePixelWidth = decodeWidth;
                remote.StreamSource = stream;
                remote.EndInit();
                remote.Freeze();
                return remote;
            }

            // Local file path — BitmapImage reads it directly, no download.
            var localWidth = DecodeWidthFor(key.Size, () => File.OpenRead(new Uri(url).LocalPath));
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (localWidth > 0)
                image.DecodePixelWidth = localWidth;
            image.UriSource = new Uri(url);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            // Unreachable/broken URL, unsupported format, relative URI — skip.
            return null;
        }
    }

    // Reads only the header to find the source width, so a bucket never upscales
    // a smaller original. Returns 0 (native decode) for Native or when the header
    // can't be read.
    private static int DecodeWidthFor(ArtworkDecodeSize size, Func<Stream> openSource)
    {
        if (size == ArtworkDecodeSize.Native)
            return 0;

        try
        {
            using var stream = openSource();
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            return Math.Min((int)size, frame.PixelWidth);
        }
        catch
        {
            return 0;
        }
    }

    // Serves a remote image's bytes from the on-disk cache when available,
    // otherwise downloads and persists them. The file name is the URL's SHA-1
    // so the same artwork always maps to the same file regardless of URL
    // formatting. Failures fall back to a fresh download attempt next time.
    private static byte[]? GetBytes(Uri uri)
    {
        var url = uri.AbsoluteUri;
        var cacheDir = Config.ImageCachePath;
        var file = Path.Combine(cacheDir, CacheKeyFor(url));

        if (File.Exists(file))
        {
            try
            {
                return File.ReadAllBytes(file);
            }
            catch
            {
                // Corrupt/unreadable cached file — re-download below.
            }
        }

        try
        {
            var bytes = DownloadImageBytes(uri);
            if (bytes is null)
                return null;

            try
            {
                Directory.CreateDirectory(cacheDir);
                File.WriteAllBytes(file, bytes);
            }
            catch
            {
                // Persisting must never break the load.
            }

            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? DownloadImageBytes(Uri uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        request.Headers.Accept.ParseAdd("image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
        request.Headers.Referrer = new Uri($"{uri.Scheme}://{uri.Host}/");

        using var response = DownloadClient.SendAsync(request).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            return null;

        return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
    }

    // Looks up an entry and, on a hit, moves it to the most-recently-used end so
    // the LRU eviction keeps the artwork the user is actually looking at.
    private static BitmapImage? CacheTryGet(CacheKey key)
    {
        lock (CacheLock)
        {
            if (CacheIndex.TryGetValue(key, out var node))
            {
                CacheLru.Remove(node);
                CacheLru.AddLast(node);
                return node.Value.Image;
            }
        }

        return null;
    }

    private static bool CacheContains(CacheKey key)
    {
        lock (CacheLock)
        {
            return CacheIndex.ContainsKey(key);
        }
    }

    // Inserts (or replaces) an entry, then evicts least-recently-used entries until
    // the total decoded size is back under the cap (always keeping at least one).
    private static void CacheInsert(CacheKey key, BitmapImage image)
    {
        var bytes = EstimateBytes(image);
        lock (CacheLock)
        {
            if (CacheIndex.TryGetValue(key, out var existing))
            {
                _cacheBytes -= existing.Value.Bytes;
                CacheLru.Remove(existing);
            }

            var node = CacheLru.AddLast(new CacheEntry(key, image, bytes));
            CacheIndex[key] = node;
            _cacheBytes += bytes;

            while (_cacheBytes > MaxCacheBytes && CacheLru.Count > 1 && CacheLru.First is { } lru)
            {
                CacheLru.RemoveFirst();
                CacheIndex.Remove(lru.Value.Key);
                _cacheBytes -= lru.Value.Bytes;
            }
        }
    }

    // Decoded BGRA32 footprint estimate: width * height * 4 bytes.
    private static long EstimateBytes(BitmapImage image)
    {
        try
        {
            return (long)image.PixelWidth * image.PixelHeight * 4;
        }
        catch
        {
            return 0;
        }
    }

    private static string CacheKeyFor(string url)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash).ToLowerInvariant() + ".img";
    }
}
