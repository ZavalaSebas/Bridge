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
/// Shared, bounded cache of fully-decoded (frozen) images for the library's
/// artwork. The problem it solves: a BitmapImage handed to a virtualized
/// ListBox/ListView with the default async UriSource loading often doesn't
/// repaint when the download completes — the icon stays blank until the window
/// is forced to re-render (minimize/restore). Decoding with CacheOption.OnLoad
/// on a background thread and handing back a frozen, already-decoded bitmap
/// fixes that, because the Image element receives a complete image and renders
/// it immediately.
///
/// Loads are keyed by URL, deduplicated (one in-flight task per URL), bounded
/// (MaxCachedImages), and announced through <see cref="Subscribe"/> so an Image
/// can refresh the exact moment its artwork lands — no reliance on binding
/// re-evaluation.
/// </summary>
public static class RemoteImageCache
{
    private const int MaxCachedImages = 512;

    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new();
    private static readonly ConcurrentQueue<string> CacheOrder = new();
    // In-flight loads keyed by URL, storing the decode task so a preload can
    // await it (the window waits for artwork before its first paint).
    private static readonly ConcurrentDictionary<string, Task> InFlight = new();

    private static readonly object CallbacksLock = new();
    private static readonly Dictionary<string, List<Action>> PendingCallbacks = new();

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
    public static BitmapImage? Get(string url)
    {
        if (Cache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        BeginLoad(url);
        return null;
    }

    /// <summary>Warms the cache for a set of URLs (e.g. every game's icon at startup).</summary>
    public static void Preload(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            BeginLoad(url);
        }
    }

    /// <summary>
    /// Warms the cache like <see cref="Preload"/> and returns a task that
    /// completes when every decode has finished (failed URLs included). The
    /// caller (startup) awaits this so the first paint already has the artwork.
    /// </summary>
    public static async Task PreloadAndWaitAsync(
        IEnumerable<string> urls,
        IProgress<(int Completed, int Total)>? progress = null)
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
        var tasks = distinct.Select(async url =>
        {
            try
            {
                await BeginLoad(url).ConfigureAwait(false);
            }
            finally
            {
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
    public static void Subscribe(string url, Action callback)
    {
        bool loaded;
        lock (CallbacksLock)
        {
            loaded = Cache.ContainsKey(url);
            if (!loaded)
            {
                if (!PendingCallbacks.TryGetValue(url, out var callbacks))
                {
                    callbacks = [];
                    PendingCallbacks[url] = callbacks;
                }

                callbacks.Add(callback);
            }
        }

        if (loaded)
        {
            callback();
            return;
        }

        BeginLoad(url);
    }

    /// <summary>Removes a callback registered by <see cref="Subscribe"/>.</summary>
    public static void Unsubscribe(string url, Action callback)
    {
        lock (CallbacksLock)
        {
            if (PendingCallbacks.TryGetValue(url, out var callbacks))
            {
                callbacks.Remove(callback);
                if (callbacks.Count == 0)
                {
                    PendingCallbacks.Remove(url);
                }
            }
        }
    }

    private static Task BeginLoad(string url)
    {
        if (InFlight.TryGetValue(url, out var existing))
        {
            return existing;
        }

        var loadTask = Task.Run(() => LoadSynchronously(url));
        if (!InFlight.TryAdd(url, loadTask))
        {
            return InFlight[url];
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
                InFlight.TryRemove(url, out _);

                var image = completed.IsCompletedSuccessfully ? completed.Result : null;
                if (image is not null)
                {
                    Cache[url] = image;
                    CacheOrder.Enqueue(url);
                    TrimCache();
                }

                // Collect the pending callbacks, then run them on the UI thread.
                List<Action>? callbacks = null;
                lock (CallbacksLock)
                {
                    if (PendingCallbacks.TryGetValue(url, out callbacks))
                    {
                        PendingCallbacks.Remove(url);
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

    private static BitmapImage? LoadSynchronously(string url)
    {
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

                using var stream = new MemoryStream(bytes);
                var remote = new BitmapImage();
                remote.BeginInit();
                remote.CacheOption = BitmapCacheOption.OnLoad;
                remote.StreamSource = stream;
                remote.EndInit();
                remote.Freeze();
                return remote;
            }

            // Local file path — BitmapImage reads it directly, no download.
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
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
            var bytes = DownloadClient.GetByteArrayAsync(uri).GetAwaiter().GetResult();
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

    private static void TrimCache()
    {
        while (Cache.Count > MaxCachedImages && CacheOrder.TryDequeue(out var oldest))
        {
            Cache.TryRemove(oldest, out _);
        }
    }

    private static string CacheKeyFor(string url)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash).ToLowerInvariant() + ".img";
    }
}
