using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bridge.Assets;
using Bridge.Services;

namespace Bridge.Converters;

/// <summary>
/// Attached property that loads artwork through RemoteImageCache and sets Source
/// when the decode finishes — avoids blank icons in virtualized lists.
/// </summary>
public static class CachedImage
{
    public static readonly DependencyProperty SourceUrlProperty =
        DependencyProperty.RegisterAttached(
            "SourceUrl",
            typeof(string),
            typeof(CachedImage),
            new PropertyMetadata(null, OnSourceUrlChanged));

    public static readonly DependencyProperty FallbackArtworkProperty =
        DependencyProperty.RegisterAttached(
            "FallbackArtwork",
            typeof(GameArtworkFallback),
            typeof(CachedImage),
            new PropertyMetadata(GameArtworkFallback.None, OnDisplayOptionsChanged));

    public static readonly DependencyProperty DecodeSizeProperty =
        DependencyProperty.RegisterAttached(
            "DecodeSize",
            typeof(ArtworkDecodeSize),
            typeof(CachedImage),
            new PropertyMetadata(ArtworkDecodeSize.Native, OnDisplayOptionsChanged));

    private static readonly DependencyProperty LoadCallbackProperty =
        DependencyProperty.RegisterAttached(
            "LoadCallback",
            typeof(Action),
            typeof(CachedImage),
            new PropertyMetadata(null));

    public static string? GetSourceUrl(DependencyObject d) => (string?)d.GetValue(SourceUrlProperty);

    public static void SetSourceUrl(DependencyObject d, string? value) => d.SetValue(SourceUrlProperty, value);

    public static GameArtworkFallback GetFallbackArtwork(DependencyObject d) =>
        (GameArtworkFallback)d.GetValue(FallbackArtworkProperty);

    public static void SetFallbackArtwork(DependencyObject d, GameArtworkFallback value) =>
        d.SetValue(FallbackArtworkProperty, value);

    public static ArtworkDecodeSize GetDecodeSize(DependencyObject d) =>
        (ArtworkDecodeSize)d.GetValue(DecodeSizeProperty);

    public static void SetDecodeSize(DependencyObject d, ArtworkDecodeSize value) =>
        d.SetValue(DecodeSizeProperty, value);

    private static Action? GetLoadCallback(DependencyObject d) => (Action?)d.GetValue(LoadCallbackProperty);

    private static void SetLoadCallback(DependencyObject d, Action? value) => d.SetValue(LoadCallbackProperty, value);

    private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is string oldUrl && GetLoadCallback(d) is { } oldCallback)
        {
            RemoteImageCache.Unsubscribe(oldUrl, oldCallback, GetDecodeSize(d));
            SetLoadCallback(d, null);
        }

        ApplySourceUrl(d);
    }

    private static void OnDisplayOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ApplySourceUrl(d);

    private static void ApplySourceUrl(DependencyObject d)
    {
        var url = GetSourceUrl(d);
        var fallback = GetFallbackArtwork(d);

        switch (d)
        {
            case Image image:
                SetImageSource(image, url, fallback);
                break;

            case Border border:
                ApplyArtworkBackground(border, url, fallback);
                break;

            case Grid grid:
                ApplyArtworkBackground(grid, url, fallback);
                break;
        }
    }

    private static void SetImageSource(Image image, string? url, GameArtworkFallback fallback)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            image.Source = DefaultGameArtwork.Get(fallback);
            return;
        }

        var size = ResolveDecodeSize(image, fallback);
        if (Path.IsPathRooted(url))
        {
            image.Source = LoadLocalPath(url, size) ?? DefaultGameArtwork.Get(fallback);
            return;
        }

        if (RemoteImageCache.Get(url, size) is { } cached)
        {
            image.Source = cached;
            return;
        }

        image.Source = DefaultGameArtwork.Get(fallback);
        Action callback = () =>
        {
            if (GetSourceUrl(image) != url)
                return;

            image.Source = RemoteImageCache.Get(url, size) ?? DefaultGameArtwork.Get(GetFallbackArtwork(image));
        };
        SetLoadCallback(image, callback);
        RemoteImageCache.Subscribe(url, callback, size);
    }

    private static void ApplyArtworkBackground(DependencyObject target, string? url, GameArtworkFallback fallback)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            SetBackground(target, MakeFillBrush(DefaultGameArtwork.Get(fallback)));
            return;
        }

        var size = ResolveDecodeSize(target, fallback);
        if (Path.IsPathRooted(url))
        {
            SetBackground(target, MakeFillBrush(LoadLocalPath(url, size) ?? DefaultGameArtwork.Get(fallback)));
            return;
        }

        if (RemoteImageCache.Get(url, size) is { } cached)
        {
            SetBackground(target, MakeFillBrush(cached));
            return;
        }

        SetBackground(target, MakeFillBrush(DefaultGameArtwork.Get(fallback)));
        Action callback = () =>
        {
            if (GetSourceUrl(target) != url)
                return;

            SetBackground(target, MakeFillBrush(RemoteImageCache.Get(url, size) ?? DefaultGameArtwork.Get(GetFallbackArtwork(target))));
        };
        SetLoadCallback(target, callback);
        RemoteImageCache.Subscribe(url, callback, size);
    }

    private static ArtworkDecodeSize ResolveDecodeSize(DependencyObject target, GameArtworkFallback fallback)
    {
        var size = GetDecodeSize(target);
        // Most icon consumers only set FallbackArtwork=Icon (without DecodeSize),
        // which previously decoded at Native and duplicated preload entries.
        if (size == ArtworkDecodeSize.Native && fallback == GameArtworkFallback.Icon)
            return ArtworkDecodeSize.Icon;
        return size;
    }

    private static void SetBackground(DependencyObject target, Brush? brush)
    {
        switch (target)
        {
            case Border border:
                border.Background = brush;
                break;
            case Grid grid:
                grid.Background = brush;
                break;
        }
    }

    private static ImageBrush? MakeFillBrush(ImageSource? source)
    {
        if (source is null)
            return null;

        var brush = new ImageBrush(source)
        {
            Stretch = Stretch.UniformToFill
        };
        brush.Freeze();
        return brush;
    }

    private static ImageSource? LoadLocalPath(string path, ArtworkDecodeSize size)
    {
        try
        {
            var ext = Path.GetExtension(path);
            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return ExeIconLoader.GetIcon(path);
            }

            if (!File.Exists(path))
                return null;

            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            // Decode local artwork (e.g. Steam's 600x900 covers) at the display
            // bucket instead of full resolution, clamped so a smaller original is
            // never upscaled.
            var decodeWidth = LocalDecodeWidth(path, size);
            if (decodeWidth > 0)
                bitmap.DecodePixelWidth = decodeWidth;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    // Reads just the header to clamp the bucket to the source width, so a bucket
    // never upscales a smaller original. Returns 0 (native decode) for Native or
    // when the header can't be read.
    private static int LocalDecodeWidth(string path, ArtworkDecodeSize size)
    {
        if (size == ArtworkDecodeSize.Native)
            return 0;

        try
        {
            using var stream = File.OpenRead(path);
            var frame = System.Windows.Media.Imaging.BitmapDecoder.Create(
                stream,
                System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                System.Windows.Media.Imaging.BitmapCacheOption.None).Frames[0];
            return Math.Min((int)size, frame.PixelWidth);
        }
        catch
        {
            return 0;
        }
    }
}
