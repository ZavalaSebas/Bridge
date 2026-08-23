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

        // Stop growing once full; a miss past the cap simply re-extracts on demand
        // (icon extraction is cheap and rare) rather than evicting live entries.
        // If another thread cached the same path first, return the shared instance
        // so callers still converge on one BitmapSource per path.
        if (Cache.Count < MaxCacheEntries &&
            !Cache.TryAdd(exePath, icon) &&
            Cache.TryGetValue(exePath, out var raced))
        {
            return raced;
        }

        return icon;
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
