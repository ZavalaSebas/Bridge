using System.Collections.Concurrent;
using System.Windows.Media.Imaging;

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
    private static readonly ConcurrentDictionary<string, byte> InFlight = new();

    private static readonly object CallbacksLock = new();
    private static readonly Dictionary<string, List<Action>> PendingCallbacks = new();

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

    private static void BeginLoad(string url)
    {
        if (!InFlight.TryAdd(url, 0))
        {
            return; // already loading
        }

        var loadTask = Task.Run(() => LoadSynchronously(url));
        _ = loadTask.ContinueWith(
            completed =>
            {
                InFlight.TryRemove(url, out _);

                var image = completed.IsCompletedSuccessfully ? completed.Result : null;
                if (image is not null)
                {
                    if (Cache.Count >= MaxCachedImages)
                    {
                        Cache.Clear();
                    }

                    Cache[url] = image;
                }

                // Fire the pending callbacks (this continuation runs on the UI
                // thread via FromCurrentSynchronizationContext). Cleared even on
                // failure so a never-resolving URL can't accumulate callbacks.
                List<Action>? callbacks = null;
                lock (CallbacksLock)
                {
                    if (PendingCallbacks.TryGetValue(url, out callbacks))
                    {
                        PendingCallbacks.Remove(url);
                    }
                }

                if (callbacks is not null)
                {
                    foreach (var callback in callbacks)
                    {
                        callback();
                    }
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static BitmapImage? LoadSynchronously(string url)
    {
        try
        {
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
}
