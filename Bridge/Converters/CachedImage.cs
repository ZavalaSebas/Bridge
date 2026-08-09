using System.Windows;
using System.Windows.Controls;

namespace Bridge.Converters;

/// <summary>
/// Attached property that loads an Image's artwork through RemoteImageCache and
/// refreshes the control the moment the decoded (frozen) image is ready.
///
/// Usage: &lt;Image converters:CachedImage.SourceUrl="{Binding Icon}" /&gt;
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

    public static string? GetSourceUrl(DependencyObject d) => (string?)d.GetValue(SourceUrlProperty);

    public static void SetSourceUrl(DependencyObject d, string? value) => d.SetValue(SourceUrlProperty, value);

    private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image)
        {
            return;
        }

        var url = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(url))
        {
            image.Source = null;
            return;
        }

        if (RemoteImageCache.Get(url) is { } cached)
        {
            image.Source = cached;
            return;
        }

        // Not decoded yet — keep blank and refresh as soon as it lands.
        image.Source = null;
        RemoteImageCache.Subscribe(url, () =>
        {
            // The container may have been recycled to a different item while the
            // download was in flight — only apply if still bound to this URL.
            if (GetSourceUrl(image) == url)
            {
                image.Source = RemoteImageCache.Get(url);
            }
        });
    }
}
