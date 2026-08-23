using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Bridge.Core.Entities;
using Bridge.ViewModels;

namespace Bridge.Views;

public partial class HomeView : UserControl
{
    private readonly DispatcherTimer _carouselTimer;
    private ScrollViewer? _draggingViewer;
    private Point _dragStartPoint;
    private double _dragStartOffset;
    private bool _isDragging;
    private bool _suppressNextClick;

    public HomeView()
    {
        InitializeComponent();
        _carouselTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _carouselTimer.Tick += (_, _) =>
        {
            if (DataContext is MainViewModel vm && vm.HomeFeaturedGames.Count > 1)
                vm.HomeNextCarouselCommand.Execute(null);
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
            oldVm.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is MainViewModel newVm)
            newVm.PropertyChanged += OnVmPropertyChanged;
        UpdateTimerState();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.NavigationSection))
            UpdateTimerState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateTimerState();
        // Ensure context menus share the global handler (like LibraryDetailView does) so right-click sets SelectedGame correctly
        if (Window.GetWindow(this) is MainWindow mw)
        {
            if (TryFindResource("Bridge.GameContextMenu") is System.Windows.Controls.ContextMenu cm)
            {
                // Avoid double subscription
                cm.Opened -= mw.HandleGameContextMenuOpened;
                cm.Opened += mw.HandleGameContextMenuOpened;
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _carouselTimer.Stop();
    }

    private void UpdateTimerState()
    {
        var isHome = ViewModel?.NavigationSection == Bridge.Core.Enums.NavigationSection.Home;
        if (isHome && ViewModel?.HomeFeaturedGames.Count > 1)
            _carouselTimer.Start();
        else
            _carouselTimer.Stop();
    }

    private void CarouselPrev_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.HomePrevCarouselCommand.Execute(null);
        _carouselTimer.Stop();
        _carouselTimer.Start();
    }

    private void CarouselNext_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.HomeNextCarouselCommand.Execute(null);
        _carouselTimer.Stop();
        _carouselTimer.Start();
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
            // Never-played games -> quick popup overlay in Home (no separate Window)
            if (game.PlaytimeSeconds == 0 && !game.IsRunning)
            {
                // Use WhatToPlayNext as navigation source so arrows cycle through never-played
                var source = vm.HomeWhatToPlayNext.Count > 0 && vm.HomeWhatToPlayNext.Contains(game)
                    ? vm.HomeWhatToPlayNext
                    : vm.HomeWhatToPlayNext;
                vm.ShowQuickView(game, source);
                return;
            }

            vm.SelectedGame = game;
            vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
        }
        else if (sender is Button { Tag: RecentAchievementRow row } && ViewModel is { } vm2)
        {
            vm2.SelectedGame = row.Game;
            vm2.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
        }
    }

    private void QuickViewOverlay_BackgroundClick(object sender, MouseButtonEventArgs e) => ViewModel?.CloseQuickView();
    private void QuickViewPopup_Click(object sender, MouseButtonEventArgs e) => e.Handled = true;
    private void QuickViewClose_Click(object sender, RoutedEventArgs e) => ViewModel?.CloseQuickView();

    private void RightPanelPlay_Click(object sender, RoutedEventArgs e)
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
            vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
            if (game.IsRunning)
                vm.StopGameCommand.Execute(game);
            else
                vm.PlayGameCommand.Execute(game);
        }
    }

    private void ViewAllWhatToPlay_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.FilterPreset = Bridge.Core.Enums.LibraryFilterPreset.NotPlayed;
        ViewModel.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
    }

    private void ViewAllContinue_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.FilterPreset = Bridge.Core.Enums.LibraryFilterPreset.RecentlyPlayed;
        ViewModel.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
    }

    private void ViewAllRecentlyAdded_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.FilterPreset = Bridge.Core.Enums.LibraryFilterPreset.All;
        // Sort by Added descending
        ViewModel.SortField = Bridge.Core.Enums.GameSortField.Added;
        // Need descending true
        ViewModel.SortDescending = true;
        ViewModel.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
    }

    private void ImportSteam_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ImportSteamLibraryCommand.Execute(null);
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow w)
            w.ShowScanInstalledDialog();
    }

    // Forward vertical wheel from horizontal rows to the main vertical scroll so rows don't block scrolling
    private void RowScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // If Shift held, allow horizontal scroll; otherwise forward to vertical host
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            return;

        var main = MainScrollHost;
        if (main is null) return;

        // Forward to main vertical scroll
        main.ScrollToVerticalOffset(main.VerticalOffset - e.Delta);
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
        // Do not capture immediately - let Button Click fire. Capture only after drag threshold.
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
        if (sv is null) return;

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
            AnimateHorizontalScroll(sv, sv.HorizontalOffset - 420);
    }

    private void RowRight_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScrollViewer sv })
            AnimateHorizontalScroll(sv, sv.HorizontalOffset + 420);
    }

    private static void AnimateHorizontalScroll(ScrollViewer sv, double target)
    {
        target = Math.Max(0, Math.Min(target, sv.ScrollableWidth));
        sv.ScrollToHorizontalOffset(target);
    }
}
