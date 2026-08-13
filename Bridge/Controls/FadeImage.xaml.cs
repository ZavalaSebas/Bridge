using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Bridge.Converters;

namespace Bridge.Controls;

/// <summary>
/// Two-stack image that cross-fades between sources — the transition Playnite
/// uses for its game backgrounds (FadeImage in Playnite.Controls). When
/// <see cref="SourceUrl"/> changes, the new artwork fades in over the old one
/// instead of blinking to blank. Images load through <see cref="RemoteImageCache"/>
/// so the decode happens on a background thread and the swap is instant.
/// The BlurEffect/darkening live outside this control (on the element), so the
/// whole fade is blurred together — that's what makes the frosted-glass look.
/// </summary>
public partial class FadeImage : UserControl
{
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(400);

    public static readonly DependencyProperty SourceUrlProperty = DependencyProperty.Register(
        nameof(SourceUrl),
        typeof(string),
        typeof(FadeImage),
        new PropertyMetadata(null, OnSourceUrlChanged));

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(FadeImage),
        new PropertyMetadata(Stretch.UniformToFill));

    public static readonly DependencyProperty CoverByWidthProperty = DependencyProperty.Register(
        nameof(CoverByWidth),
        typeof(bool),
        typeof(FadeImage),
        new PropertyMetadata(false, OnCoverByWidthChanged));

    public string? SourceUrl
    {
        get => (string?)GetValue(SourceUrlProperty);
        set => SetValue(SourceUrlProperty, value);
    }

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    /// When true the images are forced to fill the control's full width and their
    /// height is set to width/aspect, so the artwork always spans the window with
    /// no side letterbox bars and the vertical excess is clipped by the parent's
    /// ClipToBounds (Playnite's "cover" background look). When false the normal
    /// <see cref="Stretch"/> behavior applies. Defaults to false.
    /// </summary>
    public bool CoverByWidth
    {
        get => (bool)GetValue(CoverByWidthProperty);
        set => SetValue(CoverByWidthProperty, value);
    }

    private static void OnCoverByWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FadeImage)d;
        if ((bool)e.NewValue)
        {
            control.ApplyCoverByWidthSizing();
            control.ApplyShortFrameFade();
        }
        else
        {
            control.ClearCoverByWidthSizing();
            control.ClearShortFrameFade();
        }
    }

    private Image? activeImage;
    private string? currentUrl;
    private double image1Aspect;
    private double image2Aspect;

    /// <summary>Raised after the visible image changes (so hosts can adapt layout).</summary>
    public event EventHandler? ImageChanged;

    /// <summary>Width/Height of the currently loaded image, or null.</summary>
    public double? ImageAspect { get; private set; }

    public FadeImage()
    {
        InitializeComponent();
        // Recompute the forced cover-by-width size whenever the control's width
        // changes (window resize): height = width / aspect so the image always
        // fills the width and the vertical excess is clipped below — never side
        // letterbox bars. No-op when CoverByWidth is off.
        SizeChanged += (_, _) =>
        {
            if (CoverByWidth)
            {
                ApplyCoverByWidthSizing();
                ApplyShortFrameFade();
            }
        };
    }

    private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FadeImage)d).OnSourceChanged((string?)e.NewValue);

    private void OnSourceChanged(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            currentUrl = null;
            FadeOutActive();
            return;
        }

        if (url == currentUrl)
        {
            return;
        }

        currentUrl = url;

        if (RemoteImageCache.Get(url) is { } cached)
        {
            ShowImage(cached);
            return;
        }

        RemoteImageCache.Subscribe(url, () =>
        {
            if (currentUrl == url && RemoteImageCache.Get(url) is { } image)
            {
                ShowImage(image);
            }
        });
    }

    // Smooth crossfade between the two Image frames. Each image keeps its own
    // aspect-based size and its own bottom fade, so two different-sized sources
    // blend without a hard edge: the shorter frame's bottom is faded out by its
    // own per-image mask instead of ending in a hard line.
    private void ShowImage(BitmapSource image)
    {
        var aspect = image.PixelWidth / (double)image.PixelHeight;
        ImageAspect = aspect;

        var next = activeImage is null || ReferenceEquals(activeImage, Image2) ? Image1 : Image2;
        var previous = activeImage;
        activeImage = next;

        if (ReferenceEquals(next, Image1))
            image1Aspect = aspect;
        else
            image2Aspect = aspect;

        if (CoverByWidth)
        {
            ApplyCoverByWidthSizing();
            ApplyShortFrameFade();
        }
        else if (Stretch == Stretch.Uniform && Height > 0)
        {
            // Cover-style sizing (e.g. the 170px cover over the hero): keep both
            // frames at the INCOMING image's size (width = Height * aspect) from
            // the very start of the cross-fade. The Grid then never resizes mid-
            // transition, so switching between different-ratio covers (Genshin's
            // wide art vs Steam's 2:3) shows no black bars and no layout jump —
            // the incoming frame fills its box exactly, the outgoing one just
            // fades away inside the same box.
            var coverWidth = Height * aspect;
            ApplyCoverHeightSizingTo(Image1, coverWidth, Height);
            ApplyCoverHeightSizingTo(Image2, coverWidth, Height);
        }
        ImageChanged?.Invoke(this, EventArgs.Empty);

        next.Source = image;
        next.BeginAnimation(OpacityProperty, null);
        next.Opacity = 0;

        var fadeIn = new DoubleAnimation(0, 1, TransitionDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        fadeIn.Completed += (_, _) =>
        {
            if (previous is not null && !ReferenceEquals(previous, activeImage))
            {
                previous.BeginAnimation(OpacityProperty, null);
                previous.Opacity = 0;
                // Clear the outgoing frame's source deferred to the next
                // dispatcher pass. Two things depend on it:
                //  - Clearing it at the exact end of the fade made the frosted
                //    blur re-render in one frame (a visible pop), so we defer
                //    until after that render.
                //  - Leaving it forever kept the shared Grid at the old frame's
                //    size, so a different-ratio cover (e.g. Genshin's wide art)
                //    left black bars around the next, narrower cover. After the
                //    fade the Grid collapses to the new frame and the cover
                //    fits edge-to-edge.
                Dispatcher.BeginInvoke(() =>
                {
                    // Only clear if this frame wasn't reused for a new image
                    // while the deferred clear was pending.
                    if (previous.Source is not null && ReferenceEquals(previous, activeImage) is false)
                    {
                        previous.Source = null;
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };

        if (previous is not null)
        {
            previous.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TransitionDuration));
        }

        next.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void FadeOutActive()
    {
        if (activeImage is null)
        {
            return;
        }
        var target = activeImage;
        var fadeOut = new DoubleAnimation(1, 0, TransitionDuration);
        fadeOut.Completed += (_, _) =>
        {
            if (ReferenceEquals(target, activeImage))
            {
                target.Source = null;
                target.BeginAnimation(OpacityProperty, null);
                target.Opacity = 0;
                activeImage = null;
            }
        };

        target.BeginAnimation(OpacityProperty, fadeOut);
    }

    // Forces each image to the control's width and height=width/aspect, so the
    // artwork always fills the full width (no side letterbox bars, whatever the
    // source ratio or window size) and any vertical excess is clipped below by
    // the parent's ClipToBounds. Only used when CoverByWidth is on (the hero).
    // Each image uses its own aspect so the fade between different-ratio sources
    // never makes the outgoing image jump size.
    private void ApplyCoverByWidthSizing()
    {
        var width = Math.Max(ActualWidth, 1);
        ApplyCoverByWidthSizingTo(Image1, image1Aspect, width);
        ApplyCoverByWidthSizingTo(Image2, image2Aspect, width);
    }

    private static void ApplyCoverByWidthSizingTo(Image image, double aspect, double width)
    {
        if (aspect <= 0)
        {
            return;
        }

        image.Width = width;
        image.Height = width / aspect;
    }

    // Cover-style sizing: sets both frames to a fixed height with width derived
    // from the incoming image's aspect, so the Grid is stable across the fade.
    private static void ApplyCoverHeightSizingTo(Image image, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        image.Width = width;
        image.Height = height;
    }

    // The hero's shared OpacityMask (in MainWindow) fades the bottom of the hero
    // from 35% of the hero height down. Taller frames reach that fade zone and
    // are fully handled by the shared mask. A frame SHORTER than the hero ends
    // above that zone with a hard bottom edge — this gives those frames their
    // own fade, anchored to the same absolute start (35% of the hero height) so
    // the blur edge stays consistent across games.
    private void ApplyShortFrameFade()
    {
        var heroHeight = Math.Max(ActualHeight, 1);
        var heroFadeStart = 0.35 * heroHeight;

        foreach (var image in new[] { Image1, Image2 })
        {
            var imageHeight = image.Height;
            if (imageHeight <= 0 || imageHeight >= heroHeight)
            {
                image.OpacityMask = null;
                continue;
            }

            var fadeStart = Math.Min(heroFadeStart / imageHeight, 0.999);

            image.OpacityMask = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Colors.Black, 0.0),
                    new GradientStop(Colors.Black, fadeStart),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            };
        }
    }

    private void ClearShortFrameFade()
    {
        foreach (var image in new[] { Image1, Image2 })
        {
            image.OpacityMask = null;
        }
    }

    // Restores automatic sizing (Stretch/UniformToFill) when CoverByWidth is
    // turned off — clears the explicit Width/Height we forced on the images.
    private void ClearCoverByWidthSizing()
    {
        foreach (var image in new[] { Image1, Image2 })
        {
            image.ClearValue(Image.WidthProperty);
            image.ClearValue(Image.HeightProperty);
        }
    }
}
