using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Bridge.Converters;

namespace Bridge.Controls;

/// <summary>
/// Cinematic screenshot gallery for the details view. A large main image with a
/// frosted backdrop (the same screenshot, blurred and darkened) cross-fades
/// between full-resolution Steam screenshots, with a thumbnail strip, counter,
/// arrow buttons, keyboard navigation and click-to-expand (fullscreen overlay).
/// </summary>
public partial class ScreenshotGallery : UserControl
{
    private readonly List<string> _urls = [];
    private int _index;
    private double _dragStartX;
    private double _dragStartOffset;

    // Auto-advance: slides the carousel every few seconds so the gallery feels
    // alive. Stops while the user is dragging or hovering the main image, and
    // never runs when there's only one screenshot.
    private readonly DispatcherTimer _autoTimer;
    private bool _autoPaused;

    public ScreenshotGallery()
    {
        InitializeComponent();
        _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _autoTimer.Tick += (_, _) => { if (!_autoPaused) ShowAt(_index + 1); };
        // React to the selected game changing, not just the ItemsSource binding:
        // when SelectedGame swaps the control's DataContext is briefly null, and
        // relying on the binding alone leaves the gallery showing the previous
        // game's screenshots. DataContextChanged fires reliably with the new Game.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is Bridge.Core.Entities.Game game)
            {
                LoadUrls(game.Screenshots);
            }
        };
        // Pause auto-advance while the mouse is over the main image (the user is
        // inspecting it), resume on leave. Drag also pauses via _autoPaused.
        MainImageHost.MouseEnter += (_, _) => _autoPaused = true;
        MainImageHost.MouseLeave += (_, _) => _autoPaused = false;
        CommandBindings.Add(new CommandBinding(ScreenshotGalleryCommands.PreviousCommand, (_, _) => ShowAt(_index - 1)));
        CommandBindings.Add(new CommandBinding(ScreenshotGalleryCommands.NextCommand, (_, _) => ShowAt(_index + 1)));
        CommandBindings.Add(new CommandBinding(ScreenshotGalleryCommands.CloseFullscreenCommand, (_, _) => CloseFullscreen()));
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable<string>),
        typeof(ScreenshotGallery),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public IEnumerable<string>? ItemsSource
    {
        get => (IEnumerable<string>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty CounterTextProperty = DependencyProperty.Register(
        nameof(CounterText),
        typeof(string),
        typeof(ScreenshotGallery),
        new PropertyMetadata(string.Empty));

    /// <summary>"n / total" for the selected screenshot, shown in the header.</summary>
    public string CounterText
    {
        get => (string)GetValue(CounterTextProperty);
        set => SetValue(CounterTextProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gallery = (ScreenshotGallery)d;
        gallery.LoadUrls(e.NewValue as IEnumerable<string>);
    }

    private void LoadUrls(IEnumerable<string>? urls)
    {
        _urls.Clear();
        if (urls is not null)
        {
            _urls.AddRange(urls);
        }

        // Assign a NEW list instance: ItemsControl only refreshes when the
        // ItemsSource reference changes, and _urls is reused across games — the
        // same reference would leave the old thumbnails on screen.
        ThumbnailStrip.ItemsSource = _urls.ToList();
        FullscreenThumbs.ItemsSource = _urls.ToList();
        GalleryContent.Visibility = _urls.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowAt(0);

        // Auto-advance only makes sense with multiple screenshots.
        if (_urls.Count > 1)
        {
            _autoPaused = false;
            _autoTimer.Start();
        }
        else
        {
            _autoTimer.Stop();
        }
    }

    private void ShowAt(int index)
    {
        if (_urls.Count == 0)
        {
            MainImage.SourceUrl = null;
            BackdropImage.Source = null;
            FullscreenImage.SourceUrl = null;
            CounterText = string.Empty;
            return;
        }

        _index = ((index % _urls.Count) + _urls.Count) % _urls.Count;

        var url = _urls[_index];
        MainImage.SourceUrl = url;
        FullscreenImage.SourceUrl = url;
        BackdropImage.Source = RemoteImageCache.Get(url);
        RemoteImageCache.Subscribe(url, () =>
        {
            if (_urls.Count > 0 && _urls[_index] == url)
            {
                BackdropImage.Source = RemoteImageCache.Get(url);
            }
        });

        CounterText = $"{_index + 1} / {_urls.Count}";
        PrevButton.IsEnabled = _urls.Count > 1;
        NextButton.IsEnabled = _urls.Count > 1;
        FullscreenPrev.IsEnabled = _urls.Count > 1;
        FullscreenNext.IsEnabled = _urls.Count > 1;
        UpdateSelectedThumbnail();
    }

    // Highlights the active thumbnail in both strips by swapping its border brush.
    private void UpdateSelectedThumbnail()
    {
        foreach (var container in new[] { ThumbnailStrip, FullscreenThumbs })
        {
            if (container.ItemContainerGenerator.ContainerFromIndex(_index) is FrameworkElement element)
            {
                var border = FindVisualChild<System.Windows.Controls.Border>(element);
                if (border is not null)
                {
                    border.BorderBrush = (System.Windows.Media.Brush)FindResource("Bridge.SystemAccentBrush");
                    border.BorderThickness = new Thickness(2);
                }
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualChild<T>(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void Prev_Click(object sender, RoutedEventArgs e) => ShowAt(_index - 1);

    private void Next_Click(object sender, RoutedEventArgs e) => ShowAt(_index + 1);

    private void Thumbnail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url })
        {
            var idx = _urls.IndexOf(url);
            if (idx >= 0)
            {
                ShowAt(idx);
            }
        }
    }

    private void MainImage_Click(object sender, RoutedEventArgs e)
    {
        if (_urls.Count == 0)
        {
            return;
        }

        FullscreenImage.SourceUrl = _urls[_index];
        FullscreenCounter.Text = CounterText;
        _autoPaused = true;

        // Overlay dentro de la propia ventana: el Popup cubre toda la ventana con
        // un fondo oscuro translúcido, y el panel de la galería (con su Margin
        // interno) queda más pequeño centrado con borde del fondo visible.
        if (Window.GetWindow(this) is { } window)
        {
            FullscreenPopup.PlacementTarget = window;
            FullscreenPopup.Width = window.ActualWidth;
            FullscreenPopup.Height = window.ActualHeight;
        }
        else
        {
            FullscreenPopup.Width = SystemParameters.PrimaryScreenWidth;
            FullscreenPopup.Height = SystemParameters.PrimaryScreenHeight;
        }

        FullscreenPopup.IsOpen = true;
        FullscreenRoot.Focus();
    }

    private void CloseFullscreen()
    {
        FullscreenPopup.IsOpen = false;
        _autoPaused = false;
    }

    private void CloseFullscreen_Click(object sender, RoutedEventArgs e) => CloseFullscreen();

    private void Fullscreen_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                ShowAt(_index - 1);
                e.Handled = true;
                break;
            case Key.Right:
                ShowAt(_index + 1);
                e.Handled = true;
                break;
            case Key.Escape:
                CloseFullscreen();
                e.Handled = true;
                break;
        }
    }

    // Drag-to-scroll for the thumbnail strips: pressing and dragging the mouse
    // pans the horizontal ScrollViewer instead of using a scrollbar.
    private ScrollViewer? _dragScroll;

    private void ThumbScroll_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => StartDrag(sender, e);

    private void ThumbScroll_MouseMove(object sender, MouseEventArgs e)
        => ContinueDrag(sender, e);

    private void ThumbScroll_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => EndDrag(sender, e);

    private void ThumbScroll_MouseLeave(object sender, MouseEventArgs e)
        => CancelDrag();

    private void FullscreenThumbScroll_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => StartDrag(sender, e);

    private void FullscreenThumbScroll_MouseMove(object sender, MouseEventArgs e)
        => ContinueDrag(sender, e);

    private void FullscreenThumbScroll_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => EndDrag(sender, e);

    private void FullscreenThumbScroll_MouseLeave(object sender, MouseEventArgs e)
        => CancelDrag();

    private void StartDrag(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scroll)
        {
            return;
        }

        _dragScroll = scroll;
        _dragStartX = e.GetPosition(scroll).X;
        _dragStartOffset = scroll.HorizontalOffset;
        e.Handled = false; // let a plain click reach the thumbnail Button
    }

    private void ContinueDrag(object sender, MouseEventArgs e)
    {
        if (_dragScroll is not { } scroll)
        {
            return;
        }

        var delta = _dragStartX - e.GetPosition(scroll).X;
        if (Math.Abs(delta) > 3)
        {
            // Real drag: take over the mouse so the thumbnail Button's click
            // never fires, and scroll the strip.
            scroll.CaptureMouse();
            scroll.ScrollToHorizontalOffset(_dragStartOffset + delta);
            e.Handled = true;
        }
    }

    private void EndDrag(object sender, MouseButtonEventArgs e)
    {
        if (_dragScroll is { } scroll)
        {
            scroll.ReleaseMouseCapture();
        }
        _dragScroll = null;
    }

    private void CancelDrag()
    {
        if (_dragScroll is { } scroll)
        {
            scroll.ReleaseMouseCapture();
        }
        _dragScroll = null;
    }
}

/// <summary>Static routed commands shared by every ScreenshotGallery instance.</summary>
public static class ScreenshotGalleryCommands
{
    public static readonly RoutedCommand PreviousCommand = new(nameof(PreviousCommand), typeof(ScreenshotGallery));
    public static readonly RoutedCommand NextCommand = new(nameof(NextCommand), typeof(ScreenshotGallery));
    public static readonly RoutedCommand CloseFullscreenCommand = new(nameof(CloseFullscreenCommand), typeof(ScreenshotGallery));
}
