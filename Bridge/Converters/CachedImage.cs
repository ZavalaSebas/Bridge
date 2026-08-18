using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bridge.Services;

namespace Bridge.Converters;

/// <summary>
/// Attached property that loads an Image's artwork through RemoteImageCache and
/// refreshes the control the moment the decoded (frozen) image is ready.
///
/// Usage: &lt;Image converters:CachedImage.SourceUrl="{Binding Icon}" /&gt;
/// or, for a centered cover-crop on a card:
/// &lt;Grid converters:CachedImage.SourceUrl="{Binding CoverImage}"&gt;…&lt;/Grid&gt;
/// (the Grid/Border variant paints the art as a UniformToFill ImageBrush, which
/// centers its crop — an Image element does not; see ApplyCoverBackground).
///
/// This is more reliable than a converter returning a still-downloading
/// BitmapImage: virtualized list containers don't re-evaluate bindings when the
/// download completes (and replacing a collection item with the same reference
/// doesn't either), so the image would stay blank until a forced re-render.
/// Here the Image element itself subscribes and sets Source directly.
/// </summary>
public static class CachedImage
{
    public static readonly DependencyProperty SourceUrlProperty =
        DependencyProperty.RegisterAttached(
            "SourceUrl",
            typeof(string),
            typeof(CachedImage),
            new PropertyMetadata(null, OnSourceUrlChanged));

    private static readonly DependencyProperty LoadCallbackProperty =
        DependencyProperty.RegisterAttached(
            "LoadCallback",
            typeof(Action),
            typeof(CachedImage),
            new PropertyMetadata(null));

    public static string? GetSourceUrl(DependencyObject d) => (string?)d.GetValue(SourceUrlProperty);

    public static void SetSourceUrl(DependencyObject d, string? value) => d.SetValue(SourceUrlProperty, value);

    private static Action? GetLoadCallback(DependencyObject d) => (Action?)d.GetValue(LoadCallbackProperty);

    private static void SetLoadCallback(DependencyObject d, Action? value) => d.SetValue(LoadCallbackProperty, value);

    private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is string oldUrl && GetLoadCallback(d) is { } oldCallback)
        {
            RemoteImageCache.Unsubscribe(oldUrl, oldCallback);
            SetLoadCallback(d, null);
        }

        var url = e.NewValue as string;
        switch (d)
        {
            case Image image:
                SetImageSource(image, url);
                break;

            case Border border:
                ApplyCoverBackground(border, url);
                break;

            case Grid grid:
                ApplyCoverBackground(grid, url);
                break;
        }
    }

    private static void SetImageSource(Image image, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            image.Source = null;
            return;
        }

        // Local disk paths (Steam's cached art, an Epic game's .exe) don't go
        // through the remote cache.
        if (Path.IsPathRooted(url))
        {
            image.Source = LoadLocalPath(url);
            return;
        }

        if (RemoteImageCache.Get(url) is { } cached)
        {
            image.Source = cached;
            return;
        }

        // Not decoded yet — keep blank and refresh as soon as it lands.
        image.Source = null;
        Action callback = () =>
        {
            // The container may have been recycled to a different item while the
            // download was in flight — only apply if still bound to this URL.
            if (GetSourceUrl(image) == url)
            {
                image.Source = RemoteImageCache.Get(url);
            }
        };
        SetLoadCallback(image, callback);
        RemoteImageCache.Subscribe(url, callback);
    }

    // Grid/Border variant used by the covers cards: paints the artwork as the
    // element's Background with an ImageBrush(UniformToFill). Unlike an Image
    // element with the same Stretch — which anchors the crop to the top-left,
    // leaving non-2:3 covers visibly off-center in the card — a TileBrush
    // centers its content in its viewport by default, so the crop is centered.
    private static void ApplyCoverBackground(DependencyObject target, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            SetBackground(target, null);
            return;
        }

        if (Path.IsPathRooted(url))
        {
            SetBackground(target, MakeFillBrush(LoadLocalPath(url)));
            return;
        }

        if (RemoteImageCache.Get(url) is { } cached)
        {
            SetBackground(target, MakeFillBrush(cached));
            return;
        }

        // Not decoded yet — paint nothing and refresh as soon as it lands.
        SetBackground(target, null);
        Action callback = () =>
        {
            if (GetSourceUrl(target) == url)
            {
                SetBackground(target, MakeFillBrush(RemoteImageCache.Get(url)));
            }
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
        {
            return null;
        }

        var brush = new ImageBrush(source)
        {
            Stretch = Stretch.UniformToFill
        };
        brush.Freeze();
        return brush;
    }

    // Loads a local disk path: an executable's embedded icon (Epic games store
    // the .exe path as the icon) or a local image file (Steam's cached art).
    private static System.Windows.Media.ImageSource? LoadLocalPath(string path)
    {
        try
        {
            var ext = System.IO.Path.GetExtension(path);
            if (ext.Equals(".exe", System.StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".lnk", System.StringComparison.OrdinalIgnoreCase))
            {
                return ExeIconLoader.GetIcon(path);
            }

            if (!System.IO.File.Exists(path))
            {
                return null;
            }

            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.UriSource = new System.Uri(path);
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
