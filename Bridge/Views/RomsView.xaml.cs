using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.ViewModels;

namespace Bridge.Views;

public partial class RomsView : UserControl
{
    private ScrollViewer? _draggingViewer;
    private Point _dragStartPoint;
    private double _dragStartOffset;
    private bool _isDragging;
    private bool _suppressNextClick;

    public RomsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Share the global context-menu handler so right-click sets SelectedGame
        // correctly, exactly like HomeView and LibraryDetailView do.
        if (Window.GetWindow(this) is MainWindow mw &&
            TryFindResource("Bridge.GameContextMenu") is ContextMenu cm)
        {
            cm.Opened -= mw.HandleGameContextMenuOpened;
            cm.Opened += mw.HandleGameContextMenuOpened;
        }
    }

    private void GameCard_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            e.Handled = true;
            return;
        }

        if (sender is Button { Tag: Game game } && ViewModel is { } vm)
        {
            vm.SelectedGame = game;
            vm.NavigationSection = NavigationSection.Library;
        }
    }

    private async void ScanRoms_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow owner)
            return;

        var background = ViewModel?.SelectedGame?.BackgroundImage;
        var window = new ScanRomWindow(background) { Owner = owner };
        if (window.ShowDialog() != true)
            return;

        if (ViewModel is { } vm)
            await vm.ScanRomFolderAsync(window.RomFolder);
    }

    // --- Horizontal shelf drag/scroll (mirrors HomeView's row behavior) ---

    private void RowScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            return;

        if (MainScrollHost is null)
            return;

        MainScrollHost.ScrollToVerticalOffset(MainScrollHost.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void RowScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer sv || e.ChangedButton != MouseButton.Left)
            return;

        _draggingViewer = sv;
        _dragStartPoint = e.GetPosition(sv);
        _dragStartOffset = sv.HorizontalOffset;
        _isDragging = false;
        e.Handled = false;
    }

    private void RowScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        var sv = _draggingViewer;
        if (sv is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(sv);
        var delta = current.X - _dragStartPoint.X;
        if (!_isDragging && Math.Abs(delta) > 5)
        {
            _isDragging = true;
            try { sv.CaptureMouse(); } catch { }
            try { sv.Cursor = Cursors.ScrollWE; } catch { }
        }

        if (_isDragging)
        {
            try { sv.ScrollToHorizontalOffset(_dragStartOffset - delta); } catch { }
            e.Handled = true;
        }
    }

    private void RowScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        var sv = _draggingViewer;
        if (sv is null)
            return;

        var wasDragging = _isDragging;
        _draggingViewer = null;
        _isDragging = false;
        try { sv.ReleaseMouseCapture(); } catch { }
        try { sv.Cursor = Cursors.Hand; } catch { }

        if (wasDragging)
        {
            _suppressNextClick = true;
            e.Handled = true;
        }
    }

    private void RowScrollViewer_MouseLeave(object sender, MouseEventArgs e)
    {
        var sv = _draggingViewer;
        if (sv is not null && e.LeftButton != MouseButtonState.Pressed)
        {
            _draggingViewer = null;
            _isDragging = false;
            try { sv.ReleaseMouseCapture(); } catch { }
            try { sv.Cursor = Cursors.Hand; } catch { }
        }
    }

    private void RowLeft_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScrollViewer sv })
            sv.ScrollToHorizontalOffset(Math.Max(0, sv.HorizontalOffset - 480));
    }

    private void RowRight_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScrollViewer sv })
            sv.ScrollToHorizontalOffset(Math.Min(sv.ScrollableWidth, sv.HorizontalOffset + 480));
    }
}
