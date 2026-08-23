using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace Bridge.Services;

/// Extracts and caches exe/lnk icons as BitmapSource (local paths, not RemoteImageCache).
public static class ExeIconLoader
{
    private static readonly ConcurrentDictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    // Upper bound on cached icons. The set of distinct exe/lnk paths in a library
    // is naturally small, but capping keeps the cache from growing without limit
    // across a long session where games are added and removed repeatedly.
    private const int MaxCacheEntries = 512;

    public static BitmapSource? GetIcon(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return null;

        if (Cache.TryGetValue(exePath, out var cached))
            return cached;

        var icon = Load(exePath);

        // Cache below the cap; a miss past the cap re-extracts on demand rather than
        // evicting live entries (icon extraction is cheap and rare). Either way,
        // re-read afterwards so that if another thread cached this path first we
        // return that shared instance — callers converge on one BitmapSource per
        // path. Only fall back to the locally loaded value when nothing is cached,
        // i.e. when the capacity cap prevented the insert.
        if (Cache.Count < MaxCacheEntries)
            Cache.TryAdd(exePath, icon);

        return Cache.TryGetValue(exePath, out var winner) ? winner : icon;
    }

    private static BitmapSource? Load(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
                return null;

            using var stream = new MemoryStream();
            icon.Save(stream);
            stream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
