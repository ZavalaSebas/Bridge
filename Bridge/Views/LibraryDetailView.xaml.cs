using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Bridge.Controls;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Services;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge.Views;

public partial class LibraryDetailView : UserControl
{
    private static readonly TimeSpan FavoriteStarMotion = TimeSpan.FromMilliseconds(180);

    private readonly DispatcherTimer _favoriteHideTimer;
    private bool _suppressTableResize;
    private bool _suppressTableSelectionSync;
    private bool _menuHandlersWired;
    private MainViewModel? _subscribedViewModel;

    public LibraryDetailView()
    {
        InitializeComponent();

        _favoriteHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _favoriteHideTimer.Tick += (_, _) =>
        {
            _favoriteHideTimer.Stop();
            if (CoverFavoriteButton.IsChecked != true && CoverHost.IsMouseOver is false)
                AnimateFavoriteStar(inView: false);
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        CoversViewsHost.SizeChanged += OnCoversViewsHostSizeChanged;
        CompactInfoPanel.IsVisibleChanged += OnCompactInfoPanelIsVisibleChanged;
    }

    private void OnCompactInfoPanelIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RefreshCoversLayout();
    }

    private void OnCoversViewsHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged && ViewModel?.ViewMode == ViewMode.Covers)
        {
            CoversList.InvalidateMeasure();
            CoversList.InvalidateArrange();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow mainWindow)
            return;

        if (!_menuHandlersWired)
        {
            if (FindResource("Bridge.GameContextMenu") is ContextMenu contextMenu)
                contextMenu.Opened += mainWindow.HandleGameContextMenuOpened;

            if (FindResource("Bridge.CompletionStatusMenuItemStyle") is Style completionStyle)
                completionStyle.Setters.Add(new EventSetter(MenuItem.ClickEvent, new RoutedEventHandler(CompletionStatusMenuItem_Click)));

            _menuHandlersWired = true;
        }

        if (FindResource("VmProxy") is BindingProxy proxy && mainWindow.DataContext is MainViewModel vm)
        {
            proxy.Data = vm;
            SubscribeToViewModel(vm);
        }

        ApplyDetailPanelPosition();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CoversViewsHost.SizeChanged -= OnCoversViewsHostSizeChanged;
        CompactInfoPanel.IsVisibleChanged -= OnCompactInfoPanelIsVisibleChanged;
        UnsubscribeFromViewModel();
    }

    private void SubscribeToViewModel(MainViewModel vm)
    {
        if (ReferenceEquals(_subscribedViewModel, vm))
            return;

        UnsubscribeFromViewModel();
        _subscribedViewModel = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.DetailedRows.CollectionChanged += OnDetailedRowsChanged;
        SyncTableSelection();
    }

    private void UnsubscribeFromViewModel()
    {
        if (_subscribedViewModel is null)
            return;

        _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribedViewModel.DetailedRows.CollectionChanged -= OnDetailedRowsChanged;
        _subscribedViewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.SelectedGame) or nameof(MainViewModel.ViewMode))
            SyncTableSelection();
    }

    private void OnDetailedRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SyncTableSelection();

    private void SyncTableSelection()
    {
        if (_suppressTableSelectionSync || ViewModel?.ViewMode != ViewMode.Table)
            return;

        if (ViewModel.SelectedGame is not { } selected)
        {
            if (TableList.SelectedItem is not null)
                TableList.SelectedItem = null;
            return;
        }

        var row = ViewModel.DetailedRows.FirstOrDefault(r => r.Game.Id == selected.Id);
        if (row is null)
        {
            if (TableList.SelectedItem is not null)
                TableList.SelectedItem = null;
            return;
        }

        if (!ReferenceEquals(TableList.SelectedItem, row))
            TableList.SelectedItem = row;
    }

    private MainViewModel? ViewModel => Window.GetWindow(this)?.DataContext as MainViewModel;

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.HandleMenuButtonClick(sender, e);
    }

    private void CompletionStatusMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: string status } || ViewModel is not { } vm)
            return;

        vm.SetCompletionStatusCommand.Execute(status);
    }

    private void EditGame_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.HandleEditGameClick(sender, e);
    }

    private void EmptyScanInstalled_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.HandleScanInstalledClick(sender, e);
    }

    private void EmptyScanRom_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.HandleScanRomClick(sender, e);
    }

    private void CloseCompactInfo_Click(object sender, RoutedEventArgs e)
    {
        CompactInfoPanel.Visibility = Visibility.Collapsed;
    }

    private void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTableSelectionSync
            || sender is not ListView { SelectedItem: GameDetailRow row }
            || ViewModel is not { } vm)
        {
            return;
        }

        if (ReferenceEquals(vm.SelectedGame, row.Game))
            return;

        _suppressTableSelectionSync = true;
        try
        {
            vm.SelectedGame = row.Game;
        }
        finally
        {
            _suppressTableSelectionSync = false;
        }
    }

    private void TableList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not ListView listView
            || listView.View is not GridView gridView
            || gridView.Columns.Count < 2)
            return;

        if (_suppressTableResize)
            return;

        _suppressTableResize = true;

        double totalFixed = 0;
        for (int i = 1; i < gridView.Columns.Count; i++)
            totalFixed += gridView.Columns[i].Width;

        double available = listView.ActualWidth
                           - SystemParameters.VerticalScrollBarWidth
                           - totalFixed;

        if (available < 100)
        {
            _suppressTableResize = false;
            return;
        }

        var nameColumn = gridView.Columns[0];
        if (Math.Abs(nameColumn.Width - available) > 0.5)
        {
            double capture = available;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(() =>
                {
                    nameColumn.Width = capture;
                    ScrollPositionSettingsStore.SaveTableNameWidth(capture);
                    _suppressTableResize = false;
                }));
        }
        else
        {
            _suppressTableResize = false;
        }
    }

    private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBox listBox
            && listBox.SelectedItem is Game game
            && ViewModel is { } vm)
        {
            vm.PlayGameCommand.Execute(game);
        }
    }

    private void GameInfo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Game game }
            && ViewModel is { } viewModel)
        {
            viewModel.SelectedGame = game;
            CompactInfoPanel.Visibility = Visibility.Visible;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(() => CoversList.ScrollIntoView(game)));
        }
    }

    private void CoverFavorite_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _favoriteHideTimer.Stop();
        AnimateFavoriteStar(inView: true);
    }

    private void CoverFavorite_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (CoverFavoriteButton.IsChecked != true)
            _favoriteHideTimer.Start();
    }

    private void CoverFavorite_Checked(object sender, RoutedEventArgs e)
    {
        _favoriteHideTimer.Stop();
        AnimateFavoriteStar(inView: true);
        if (CoverFavoriteButton.IsMouseOver)
        {
            PlayFavoritePop();
            PersistFavorite();
        }
    }

    private void CoverFavorite_Unchecked(object sender, RoutedEventArgs e)
    {
        if (CoverHost.IsMouseOver is false)
            AnimateFavoriteStar(inView: false);

        if (CoverFavoriteButton.IsMouseOver)
            PersistFavorite();
    }

    private void CompactFavorite_Checked(object sender, RoutedEventArgs e)
    {
        if (!CompactFavoriteButton.IsMouseOver)
            return;

        PlayCompactFavoritePop();
        PersistFavorite();
    }

    private void CompactFavorite_Unchecked(object sender, RoutedEventArgs e)
    {
        if (CompactFavoriteButton.IsMouseOver)
            PersistFavorite();
    }

    private void PersistFavorite()
    {
        ViewModel?.PersistFavorite();
    }

    private void PlayCompactFavoritePop()
    {
        if (CompactFavoriteScale is null)
            return;

        var pop = BuildPopAnimation();
        CompactFavoriteScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        CompactFavoriteScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private void AnimateFavoriteStar(bool inView)
    {
        if (CoverFavoriteButton is null)
            return;

        CoverFavoriteTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(inView ? 0 : 26, FavoriteStarMotion) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        CoverFavoriteButton.BeginAnimation(OpacityProperty, new DoubleAnimation(inView ? 1 : 0, FavoriteStarMotion));
        CoverFavoriteButton.IsHitTestVisible = inView;
    }

    private void PlayFavoritePop()
    {
        var pop = BuildPopAnimation();
        CoverFavoriteScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        CoverFavoriteScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private static DoubleAnimationUsingKeyFrames BuildPopAnimation()
    {
        var animation = new DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.35, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        return animation;
    }

    public double? GetScrollOffset(ViewMode mode) => mode switch
    {
        ViewMode.Covers => GetScrollViewer(CoversList)?.VerticalOffset,
        ViewMode.List => GetScrollViewer(GamesList)?.VerticalOffset,
        ViewMode.Table => GetScrollViewer(TableList)?.VerticalOffset,
        _ => null
    };

    public void SetScrollOffset(ViewMode mode, double offset)
    {
        if (mode == ViewMode.Covers)
            GetScrollViewer(CoversList)?.ScrollToVerticalOffset(offset);
        else if (mode == ViewMode.List)
            GetScrollViewer(GamesList)?.ScrollToVerticalOffset(offset);
        else if (mode == ViewMode.Table)
            GetScrollViewer(TableList)?.ScrollToVerticalOffset(offset);
    }

    public void ApplyViewModeLayout(ViewMode mode)
    {
        CompactInfoPanel.Visibility = Visibility.Collapsed;
        ApplyDetailPanelPosition();

        if (mode == ViewMode.Covers)
        {
            CoversList.InvalidateMeasure();
            Dispatcher.BeginInvoke(() =>
            {
                CoversList.InvalidateMeasure();
                CoversList.InvalidateArrange();
            }, DispatcherPriority.Loaded);
        }
    }

    public void ApplyDetailPanelPosition(string? position = null)
    {
        position = DetailPanelPositionSettingsStore.Normalize(position ?? DetailPanelPositionSettingsStore.Load());
        var showListDetail = ViewModel?.ViewMode == ViewMode.List;
        ApplyListDetailLayout(position, showListDetail);
        ApplyCompactDetailLayout(position);
    }

    private void ApplyListDetailLayout(string position, bool visible)
    {
        ResetListDetailGrid();

        if (!visible)
        {
            ViewsColumn.Width = new GridLength(1, GridUnitType.Star);
            ViewsColumn.MinWidth = 200;
            DetailColumn.Width = new GridLength(0);
            DetailColumn.MinWidth = 0;
            DetailSeparator.Visibility = Visibility.Collapsed;
            DetailSplitter.Visibility = Visibility.Collapsed;
            PlaceViewsHost(column: 0, row: 0);
            PlaceDetailHost(column: 2, row: 0);
            return;
        }

        DetailSeparator.Visibility = Visibility.Visible;
        DetailSplitter.Visibility = Visibility.Visible;
        DetailSplitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
        DetailSplitter.ResizeDirection = GridResizeDirection.Columns;
        DetailSplitter.Width = 4;
        DetailSplitter.Height = double.NaN;
        DetailSeparator.Width = 1;
        DetailSeparator.Height = double.NaN;

        if (position == DetailPanelPositionSettingsStore.Left)
        {
            // Column 0 uses ViewsColumn, column 2 uses DetailColumn — swap widths
            // so the details panel gets the flexible star column on the left.
            ViewsColumn.Width = new GridLength(1, GridUnitType.Star);
            ViewsColumn.MinWidth = 320;
            DetailColumn.Width = new GridLength(360);
            DetailColumn.MinWidth = 200;
            PlaceDetailHost(column: 0, row: 0);
            PlaceListChrome(column: 1, row: 0);
            PlaceViewsHost(column: 2, row: 0);
            SetDetailPanelBorder(DetailPanelPositionSettingsStore.Left);
            return;
        }

        ViewsColumn.Width = new GridLength(360);
        ViewsColumn.MinWidth = 200;
        DetailColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailColumn.MinWidth = 320;
        PlaceViewsHost(column: 0, row: 0);
        PlaceListChrome(column: 1, row: 0);
        PlaceDetailHost(column: 2, row: 0);
        SetDetailPanelBorder(DetailPanelPositionSettingsStore.Right);
    }

    private void ApplyCompactDetailLayout(string position)
    {
        Grid.SetRow(CoversViewsHost, 0);
        Grid.SetRow(CompactInfoPanel, 0);
        CompactInfoPanel.Width = 320;
        CompactInfoPanel.Height = double.NaN;
        CompactInfoPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        CompactInfoPanel.VerticalAlignment = VerticalAlignment.Stretch;

        if (position == DetailPanelPositionSettingsStore.Left)
        {
            CoversColumn0.Width = GridLength.Auto;
            CoversColumn1.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(CompactInfoPanel, 0);
            Grid.SetColumn(CoversViewsHost, 1);
            SetCompactPanelBorder(DetailPanelPositionSettingsStore.Left);
            RefreshCoversLayout();
            return;
        }

        CoversColumn0.Width = new GridLength(1, GridUnitType.Star);
        CoversColumn1.Width = GridLength.Auto;
        Grid.SetColumn(CoversViewsHost, 0);
        Grid.SetColumn(CompactInfoPanel, 1);
        SetCompactPanelBorder(DetailPanelPositionSettingsStore.Right);
        RefreshCoversLayout();
    }

    private void RefreshCoversLayout()
    {
        if (ViewModel?.ViewMode != ViewMode.Covers)
            return;

        CoversLayoutRoot.InvalidateMeasure();
        CoversLayoutRoot.InvalidateArrange();
        CoversViewsHost.InvalidateMeasure();
        CoversList.InvalidateMeasure();
        CoversList.InvalidateArrange();
    }

    private void ResetListDetailGrid()
    {
        ViewsColumn.Width = new GridLength(360);
        ViewsColumn.MinWidth = 200;
        DetailColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailColumn.MinWidth = 320;
        ViewsRow.Height = new GridLength(1, GridUnitType.Star);
        DetailRow.Height = GridLength.Auto;
        DetailRow.MinHeight = 0;
    }

    private void PlaceViewsHost(int column, int row)
    {
        Grid.SetColumn(ViewsHost, column);
        Grid.SetRow(ViewsHost, row);
        Grid.SetColumnSpan(ViewsHost, 1);
        Grid.SetRowSpan(ViewsHost, 1);
    }

    private void PlaceDetailHost(int column, int row)
    {
        Grid.SetColumn(DetailPanelHost, column);
        Grid.SetRow(DetailPanelHost, row);
        Grid.SetColumnSpan(DetailPanelHost, 1);
        Grid.SetRowSpan(DetailPanelHost, 1);
    }

    private void PlaceListChrome(int column, int row)
    {
        Grid.SetColumn(DetailSeparator, column);
        Grid.SetRow(DetailSeparator, row);
        Grid.SetColumn(DetailSplitter, column);
        Grid.SetRow(DetailSplitter, row);
        DetailSeparator.HorizontalAlignment = HorizontalAlignment.Center;
        DetailSeparator.VerticalAlignment = VerticalAlignment.Stretch;
        DetailSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
        DetailSplitter.VerticalAlignment = VerticalAlignment.Stretch;
    }

    private void SetDetailPanelBorder(string position)
    {
        DetailPanelHost.BorderThickness = position == DetailPanelPositionSettingsStore.Left
            ? new Thickness(0, 0, 1, 0)
            : new Thickness(1, 0, 0, 0);
    }

    private void SetCompactPanelBorder(string position)
    {
        CompactInfoPanel.BorderThickness = position == DetailPanelPositionSettingsStore.Left
            ? new Thickness(0, 0, 1, 0)
            : new Thickness(1, 0, 0, 0);
    }

    public void ScrollSelectedCoverIntoView()
    {
        if (ViewModel?.SelectedGame is { } game)
            CoversList.ScrollIntoView(game);
    }

    public void RestoreTableNameWidth(double width)
    {
        if (TableList.View is not GridView gridView || gridView.Columns.Count < 1 || width <= 0)
            return;

        gridView.Columns[0].Width = width;
    }

    public void SaveTableNameWidth()
    {
        if (TableList.View is not GridView gridView || gridView.Columns.Count < 1)
            return;

        ScrollPositionSettingsStore.SaveTableNameWidth(gridView.Columns[0].Width);
    }

    private static ScrollViewer? GetScrollViewer(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
                return viewer;
            if (GetScrollViewer(child) is { } found)
                return found;
        }

        return null;
    }
}
