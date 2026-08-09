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

    private Image? activeImage;
    private string? currentUrl;

    public FadeImage()
    {
        InitializeComponent();
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
}
