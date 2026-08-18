using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace Bridge.Services;

/// Extracts and caches exe/lnk icons as BitmapSource (local paths, not RemoteImageCache).
public static class ExeIconLoader
{
    private static readonly ConcurrentDictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static BitmapSource? GetIcon(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return null;

        return Cache.GetOrAdd(exePath, Load);
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
