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

    private static Action? GetLoadCallback(DependencyObject d) => (Action?)d.GetValue(LoadCallbackProperty);

    private static void SetLoadCallback(DependencyObject d, Action? value) => d.SetValue(LoadCallbackProperty, value);

    private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is string oldUrl && GetLoadCallback(d) is { } oldCallback)
        {
            RemoteImageCache.Unsubscribe(oldUrl, oldCallback);
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

        if (Path.IsPathRooted(url))
        {
            image.Source = LoadLocalPath(url) ?? DefaultGameArtwork.Get(fallback);
            return;
        }

        if (RemoteImageCache.Get(url) is { } cached)
        {
            image.Source = cached;
            return;
        }

        image.Source = DefaultGameArtwork.Get(fallback);
        Action callback = () =>
        {
            if (GetSourceUrl(image) != url)
                return;

            image.Source = RemoteImageCache.Get(url) ?? DefaultGameArtwork.Get(GetFallbackArtwork(image));
        };
        SetLoadCallback(image, callback);
        RemoteImageCache.Subscribe(url, callback);
    }

    private static void ApplyArtworkBackground(DependencyObject target, string? url, GameArtworkFallback fallback)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            SetBackground(target, MakeFillBrush(DefaultGameArtwork.Get(fallback)));
            return;
        }

        if (Path.IsPathRooted(url))
        {
            SetBackground(target, MakeFillBrush(LoadLocalPath(url) ?? DefaultGameArtwork.Get(fallback)));
            return;
        }

        if (RemoteImageCache.Get(url) is { } cached)
        {
            SetBackground(target, MakeFillBrush(cached));
            return;
        }

        SetBackground(target, MakeFillBrush(DefaultGameArtwork.Get(fallback)));
        Action callback = () =>
        {
            if (GetSourceUrl(target) != url)
                return;

            SetBackground(target, MakeFillBrush(RemoteImageCache.Get(url) ?? DefaultGameArtwork.Get(GetFallbackArtwork(target))));
        };
        SetLoadCallback(target, callback);
        RemoteImageCache.Subscribe(url, callback);
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

    private static ImageSource? LoadLocalPath(string path)
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
}
