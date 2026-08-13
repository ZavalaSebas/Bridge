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
            control.ApplyCoverByWidthSizing();
        else
            control.ClearCoverByWidthSizing();
    }

    private Image? activeImage;
    private string? currentUrl;

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
                ApplyCoverByWidthSizing();
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

    // Cross-fades to the other Image. The previous image keeps its frame until
    // the fade-in completes so a rapid selection change never blanks the screen.
    private void ShowImage(BitmapSource image)
    {
        ImageAspect = image.PixelWidth / (double)image.PixelHeight;
        if (CoverByWidth)
            ApplyCoverByWidthSizing();
        ImageChanged?.Invoke(this, EventArgs.Empty);

        var next = activeImage is null || ReferenceEquals(activeImage, Image2) ? Image1 : Image2;
        var previous = activeImage;
        activeImage = next;

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
                previous.Source = null;
                previous.BeginAnimation(OpacityProperty, null);
                previous.Opacity = 0;
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

    // Forces the images to the control's width and height=width/aspect, so the
    // artwork always fills the full width (no side letterbox bars, whatever the
    // source ratio or window size) and any vertical excess is clipped below by
    // the parent's ClipToBounds. Only used when CoverByWidth is on (the hero).
    private void ApplyCoverByWidthSizing()
    {
        if (ImageAspect is not { } aspect || aspect <= 0)
        {
            return;
        }

        var width = Math.Max(ActualWidth, 1);
        var height = width / aspect;

        foreach (var image in new[] { Image1, Image2 })
        {
            image.Width = width;
            image.Height = height;
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
