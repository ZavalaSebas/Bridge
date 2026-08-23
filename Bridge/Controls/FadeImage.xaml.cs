using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Bridge.Assets;
using Bridge.Converters;

namespace Bridge.Controls;

/// <summary>
/// Cross-fades between background images when <see cref="SourceUrl"/> changes.
/// Loads through <see cref="RemoteImageCache"/> so decoded bitmaps swap in without a blank flash.
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

    public static readonly DependencyProperty FallbackArtworkProperty = DependencyProperty.Register(
        nameof(FallbackArtwork),
        typeof(GameArtworkFallback),
        typeof(FadeImage),
        new PropertyMetadata(GameArtworkFallback.None, OnFallbackArtworkChanged));

    public static readonly DependencyProperty DecodeSizeProperty = DependencyProperty.Register(
        nameof(DecodeSize),
        typeof(ArtworkDecodeSize),
        typeof(FadeImage),
        new PropertyMetadata(ArtworkDecodeSize.Native));

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
    /// When true, images fill the control width and clip vertically — full-bleed hero background.
    /// </summary>
    public bool CoverByWidth
    {
        get => (bool)GetValue(CoverByWidthProperty);
        set => SetValue(CoverByWidthProperty, value);
    }

    public GameArtworkFallback FallbackArtwork
    {
        get => (GameArtworkFallback)GetValue(FallbackArtworkProperty);
        set => SetValue(FallbackArtworkProperty, value);
    }

    /// <summary>Decode bucket for the loaded artwork; Native keeps full resolution.</summary>
    public ArtworkDecodeSize DecodeSize
    {
        get => (ArtworkDecodeSize)GetValue(DecodeSizeProperty);
        set => SetValue(DecodeSizeProperty, value);
    }

    private static void OnFallbackArtworkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FadeImage)d;
        if (!control.IsLoaded)
            return;

        control.OnSourceChanged(control.SourceUrl);
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
    private Action? _loadCallback;
    private double image1Aspect;
    private double image2Aspect;

    /// <summary>Raised after the visible image changes (so hosts can adapt layout).</summary>
    public event EventHandler? ImageChanged;

    /// <summary>Width/Height of the currently loaded image, or null.</summary>
    public double? ImageAspect { get; private set; }

    public FadeImage()
    {
        InitializeComponent();
        // Recompute cover-by-width sizing on window resize.
        SizeChanged += (_, _) =>
        {
            if (CoverByWidth)
            {
                ApplyCoverByWidthSizing();
                ApplyShortFrameFade();
            }
        };
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_loadCallback is { } callback && currentUrl is { } url)
        {
            RemoteImageCache.Unsubscribe(url, callback, DecodeSize);
            _loadCallback = null;
        }
    }

    private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FadeImage)d).OnSourceChanged((string?)e.NewValue);

    private void OnSourceChanged(string? url)
    {
        if (_loadCallback is { } oldCallback && currentUrl is { } oldUrl)
        {
            RemoteImageCache.Unsubscribe(oldUrl, oldCallback, DecodeSize);
            _loadCallback = null;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            // Null means the binding is not applied yet — not an explicit default hero.
            if (url is null)
                return;

            currentUrl = null;
            ShowFallbackOrFadeOut();
            return;
        }

        if (url == currentUrl)
        {
            return;
        }

        currentUrl = url;

        if (RemoteImageCache.Get(url, DecodeSize) is { } cached)
        {
            ShowImage(cached);
            return;
        }

        Action callback = () =>
        {
            if (currentUrl != url)
                return;

            if (RemoteImageCache.Get(url, DecodeSize) is { } image)
                ShowImage(image);
            else
                FadeOutActive();
        };
        _loadCallback = callback;
        RemoteImageCache.Subscribe(url, callback, DecodeSize);
    }

    private void ShowFallbackOrFadeOut()
    {
        if (DefaultGameArtwork.Get(FallbackArtwork) is { } fallback)
            ShowImage(fallback);
        else
            FadeOutActive();
    }

    // Crossfade between two Image frames; each keeps its own aspect and bottom fade.
    private void ShowImage(ImageSource image)
    {
        var aspect = GetAspect(image);
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

            var heroHeight = Math.Max(ActualHeight, 1);
            var incomingHeight = Math.Max(ActualWidth, 1) / aspect;
            var incomingShort = incomingHeight > 0 && incomingHeight < heroHeight;
            var outgoingTall = previous?.Source is not null && previous.Height >= heroHeight;

            ApplyShortFrameFadeTo(Image1, animate: false);
            ApplyShortFrameFadeTo(Image2, animate: false);

            if (incomingShort)
                ApplyShortFrameFadeTo(next, animate: true);

            if (outgoingTall && previous is not null)
                AnimateOutgoingTallFade(previous, incomingHeight, heroHeight);
        }
        else if (Stretch == Stretch.Uniform && Height > 0)
        {
            // Fixed cover box sized to the incoming image — no layout jump mid-fade.
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
                // Hero mode keeps both frames sized; fixed-height cover clears the stale frame later.
                if (!CoverByWidth)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (previous.Source is not null && ReferenceEquals(previous, activeImage) is false)
                        {
                            previous.Source = null;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
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

    private static double GetAspect(ImageSource image)
    {
        if (image is BitmapSource bitmap && bitmap.PixelHeight > 0)
            return bitmap.PixelWidth / (double)bitmap.PixelHeight;

        if (image is DrawingImage { Drawing: { Bounds: { Width: > 0, Height: > 0 } bounds } })
            return bounds.Width / bounds.Height;

        return 16.0 / 9.0;
    }

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

    private static void ApplyCoverHeightSizingTo(Image image, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        image.Width = width;
        image.Height = height;
    }

    // Short hero frames get their own bottom fade so the edge matches the shared mask.
    private void ApplyShortFrameFade()
    {
        ApplyShortFrameFadeTo(Image1, animate: false);
        ApplyShortFrameFadeTo(Image2, animate: false);
    }

    // Animate the outgoing tall frame's bottom fade when cross-fading into a shorter one.
    private void AnimateOutgoingTallFade(Image outgoing, double incomingHeight, double heroHeight)
    {
        if (incomingHeight >= heroHeight)
            return;

        var targetStart = ComputeShortFadeStartOffset(incomingHeight, heroHeight);

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop(Colors.Black, 0.0),
                new GradientStop(Colors.Black, 1.0)
            }
        };
        outgoing.OpacityMask = brush;

        brush.GradientStops[1].BeginAnimation(
            GradientStop.OffsetProperty,
            new DoubleAnimation(1.0, targetStart, TransitionDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private static double ComputeShortFadeStartOffset(double imageHeight, double heroHeight)
    {
        if (imageHeight <= 0 || imageHeight >= heroHeight)
            return 0.999;

        var heroFadeStart = 0.35 * heroHeight;
        var shortage = (heroHeight - imageHeight) / heroHeight;
        var blend = Math.Clamp(shortage / 0.25, 0.0, 1.0);
        var fullFadeStart = Math.Min(heroFadeStart / imageHeight, 0.999);
        return fullFadeStart + (1.0 - fullFadeStart) * (1.0 - blend);
    }

    private void ApplyShortFrameFadeTo(Image image, bool animate = true)
    {
        var heroHeight = Math.Max(ActualHeight, 1);

        var imageHeight = image.Height;
        if (imageHeight <= 0 || imageHeight >= heroHeight)
        {
            image.OpacityMask = null;
            return;
        }

        var fadeStart = ComputeShortFadeStartOffset(imageHeight, heroHeight);

        var brush = new LinearGradientBrush
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
        image.OpacityMask = brush;

        if (animate)
        {
            var fadeStop = brush.GradientStops[1];
            fadeStop.BeginAnimation(
                GradientStop.OffsetProperty,
                new DoubleAnimation(1.0, fadeStart, TransitionDuration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
        }
    }

    private void ClearShortFrameFade()
    {
        foreach (var image in new[] { Image1, Image2 })
        {
            image.OpacityMask = null;
        }
    }

    private void ClearCoverByWidthSizing()
    {
        foreach (var image in new[] { Image1, Image2 })
        {
            image.ClearValue(Image.WidthProperty);
            image.ClearValue(Image.HeightProperty);
        }
    }
}
