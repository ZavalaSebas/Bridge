using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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
    private const double DefaultListColumnWidth = LayoutMetrics.ListColumnWidth;
    private const double CoverCompletionBadgeGap = 6;
    private const double CoverCompletionBadgeCoverGap = 8;
    private const double HeroCoverHeightDefault = 170;
    // private const double HeroCoverLargeScale = 1.25;
    private const double HeroCoverDockMargin = 40;
    private const double HeroCoverTitleReserve = 168;
    private const double HeroCoverStarReserve = 52;
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
        CoverContentRow.SizeChanged += OnCoverCompletionLayoutChanged;
        CoverFavoriteButton.SizeChanged += OnCoverCompletionLayoutChanged;
        CoverCompletionBadge.SizeChanged += OnCoverCompletionLayoutChanged;
        HeroHeader.SizeChanged += OnHeroHeaderSizeChanged;
        DetailsCoverImage.ImageChanged += OnDetailsCoverImageChanged;
    }

    private void OnCompactInfoPanelIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RefreshCoversLayout();
        if (CompactInfoPanel.IsVisible)
            CompactHeroImage.EnsureLoaded();
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
        ApplyDetailSectionPosition();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            ApplyHeroCoverHeight();
            UpdateHeroCoverScale();
            PositionCoverCompletionBadge();
        });
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _favoriteHideTimer.Stop();
        CoversViewsHost.SizeChanged -= OnCoversViewsHostSizeChanged;
        CompactInfoPanel.IsVisibleChanged -= OnCompactInfoPanelIsVisibleChanged;
        CoverContentRow.SizeChanged -= OnCoverCompletionLayoutChanged;
        CoverFavoriteButton.SizeChanged -= OnCoverCompletionLayoutChanged;
        CoverCompletionBadge.SizeChanged -= OnCoverCompletionLayoutChanged;
        HeroHeader.SizeChanged -= OnHeroHeaderSizeChanged;
        DetailsCoverImage.ImageChanged -= OnDetailsCoverImageChanged;
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

        if (e.PropertyName is nameof(MainViewModel.SelectedGame))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                ApplyHeroCoverHeight();
                PositionCoverCompletionBadge();
            });
        }
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

    private void AddBanner_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.HandleEditGameMediaClick(sender, e);
    }

    private void HeroChangeArt_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.HandleEditGameMediaClick(sender, e);
    }

    private void CoverChangeArt_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.HandleEditGameMediaClick(sender, e);
    }

    private void HeroContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        var hasBanner = ViewModel?.SelectedGame is { BackgroundImage: var background }
                        && !HeroBackground.IsDefault(background);

        if (menu.Items.Count >= 2)
        {
            if (menu.Items[0] is MenuItem changeArt)
                changeArt.Visibility = hasBanner ? Visibility.Visible : Visibility.Collapsed;

            if (menu.Items[1] is Separator separator)
                separator.Visibility = hasBanner ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdatePositionMenuChecks(menu, DetailPanelPositionSettingsStore.Load());
    }

    /*
    private void HeroCoverContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || ViewModel?.SelectedGame is not { } game)
            return;

        if (menu.Items.Count >= 2 && menu.Items[1] is MenuItem bigCovers)
            bigCovers.IsChecked = GameDisplayPreferencesStore.GetHeroCoverLarge(game.Id);
    }

    private void BigCovers_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { IsChecked: var isChecked } || ViewModel?.SelectedGame is not { } game)
            return;

        GameDisplayPreferencesStore.SetHeroCoverLarge(game.Id, isChecked);
        ApplyHeroCoverHeight();
    }
    */

    private void ApplyHeroCoverHeight()
    {
        if (DetailsCoverImage is null || CoverScaleHost is null || CoverLayoutScale is null)
            return;

        CoverLayoutScale.ScaleX = 1;
        CoverLayoutScale.ScaleY = 1;
        DetailsCoverImage.Height = HeroCoverHeightDefault;
        CoverScaleHost.MaxHeight = HeroCoverHeightDefault;
        UpdateHeroCoverScale();
        PositionCoverCompletionBadge();
    }

    private static double GetHeroCoverScale() => 1.0;
    /*
    private double GetHeroCoverScale()
    {
        var gameId = ViewModel?.SelectedGame?.Id ?? Guid.Empty;
        return gameId != Guid.Empty && GameDisplayPreferencesStore.GetHeroCoverLarge(gameId)
            ? HeroCoverLargeScale
            : 1.0;
    }
    */

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

    private void DetailPanelPositionMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        UpdatePositionMenuChecks(menu, DetailPanelPositionSettingsStore.Load());
    }

    private void DetailPanelPositionMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag })
            return;

        SetDetailPanelPosition(tag);
    }

    private void DetailSectionPositionMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        UpdatePositionMenuChecks(menu, DetailSectionPositionSettingsStore.Load());
    }

    private void DetailSectionPositionMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag })
            return;

        SetDetailSectionPosition(tag);
    }

    private static void UpdatePositionMenuChecks(ContextMenu menu, string current)
    {
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is string tag && item.IsCheckable)
                item.IsChecked = tag.Equals(current, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SetDetailPanelPosition(string position)
    {
        position = DetailPanelPositionSettingsStore.Normalize(position);
        if (position.Equals(DetailPanelPositionSettingsStore.Load(), StringComparison.OrdinalIgnoreCase))
            return;

        DetailPanelPositionSettingsStore.Save(position);
        ApplyDetailPanelPosition(position);
    }

    private void SetDetailSectionPosition(string position)
    {
        position = DetailSectionPositionSettingsStore.Normalize(position);
        if (position.Equals(DetailSectionPositionSettingsStore.Load(), StringComparison.OrdinalIgnoreCase))
            return;

        DetailSectionPositionSettingsStore.Save(position);
        ApplyDetailSectionPosition(position);
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
            if (CoversDetailLayoutSettingsStore.UsesCompact())
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

    private void OnCoverCompletionLayoutChanged(object sender, SizeChangedEventArgs e)
        => PositionCoverCompletionBadge();

    private void OnHeroHeaderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
            UpdateHeroCoverScale();
    }

    private void OnDetailsCoverImageChanged(object? sender, EventArgs e)
        => UpdateHeroCoverScale();

    private void UpdateHeroCoverScale()
    {
        if (CoverScaleHost is null || HeroHeader is null)
            return;

        var available = HeroHeader.ActualWidth
            - HeroCoverDockMargin
            - HeroCoverTitleReserve
            - HeroCoverStarReserve;

        var scale = GetHeroCoverScale();
        CoverScaleHost.MaxWidth = Math.Max(72, available / scale);
    }

    private void PositionCoverCompletionBadge()
    {
        if (CoverCompletionBadge is null || CoverFavoriteButton is null || CoverContentRow is null)
            return;

        if (CoverCompletionBadge.Visibility != Visibility.Visible)
            return;

        var rowHeight = CoverContentRow.ActualHeight;
        var starHeight = CoverFavoriteButton.ActualHeight;
        var starWidth = CoverFavoriteButton.ActualWidth;
        var badgeWidth = CoverCompletionBadge.ActualWidth;
        if (rowHeight <= 0 || starHeight <= 0 || badgeWidth <= 0)
            return;

        // Layout slot of the star (VerticalAlignment=Center). Ignore RenderTransform
        // so the slide/pop animation does not move the badge.
        var starTop = (rowHeight - starHeight) / 2;
        Canvas.SetTop(CoverCompletionBadge, starTop + starHeight + CoverCompletionBadgeGap);

        // Prefer centered under the star, but shift left so the pill never covers the artwork.
        var preferredLeft = (starWidth - badgeWidth) / 2;
        var coverLeft = starWidth + CoverFavoriteButton.Margin.Right;
        var maxLeft = coverLeft - badgeWidth - CoverCompletionBadgeCoverGap;
        Canvas.SetLeft(CoverCompletionBadge, Math.Min(preferredLeft, maxLeft));
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

    /// <summary>
    /// Forces the detail overview tab through layout while the window is still
    /// hidden so text and description images are ready on the first visible frame.
    /// </summary>
    public void WarmupDetailContent()
    {
        if (ViewModel?.SelectedGame is null)
            return;

        ApplyDetailSectionPosition();
        UpdateLayout();
        DetailScrollViewer.UpdateLayout();
        DetailOverviewTabs.UpdateLayout();
        OverviewScrollViewer.UpdateLayout();
    }

    public void ApplyDetailSectionPosition(string? position = null)
    {
        position = DetailSectionPositionSettingsStore.Normalize(
            position ?? DetailSectionPositionSettingsStore.Load());
        var detailsOnLeft = position == DetailSectionPositionSettingsStore.Left;

        if (DetailsSection is not null && OverviewTabsSection is not null
            && OverviewTabsColumn is not null && DetailsMetadataColumn is not null)
        {
            if (detailsOnLeft)
            {
                OverviewTabsColumn.Width = new GridLength(220);
                OverviewTabsColumn.MinWidth = 220;
                DetailsMetadataColumn.Width = new GridLength(1, GridUnitType.Star);
                DetailsMetadataColumn.MinWidth = 300;

                Grid.SetColumn(DetailsSection, 0);
                Grid.SetColumn(OverviewTabsSection, 1);
                DetailsSection.Margin = new Thickness(0, 0, 20, 0);
                OverviewTabsSection.Margin = new Thickness(0);
            }
            else
            {
                OverviewTabsColumn.Width = new GridLength(1, GridUnitType.Star);
                OverviewTabsColumn.MinWidth = 300;
                DetailsMetadataColumn.Width = new GridLength(220);
                DetailsMetadataColumn.MinWidth = 220;

                Grid.SetColumn(OverviewTabsSection, 0);
                Grid.SetColumn(DetailsSection, 1);
                OverviewTabsSection.Margin = new Thickness(0, 0, 20, 0);
                DetailsSection.Margin = new Thickness(0);
            }

            DetailContentDock.InvalidateMeasure();
            DetailContentDock.InvalidateArrange();
        }

        if (CompactContentStack is null || CompactScreenshotsSection is null
            || CompactDetailsSection is null || CompactDescriptionSection is null)
            return;

        CompactContentStack.Children.Clear();
        CompactContentStack.Children.Add(CompactScreenshotsSection);

        if (detailsOnLeft)
        {
            CompactContentStack.Children.Add(CompactDetailsSection);
            CompactContentStack.Children.Add(CompactDescriptionSection);
        }
        else
        {
            CompactContentStack.Children.Add(CompactDescriptionSection);
            CompactContentStack.Children.Add(CompactDetailsSection);
        }
    }

    public void ApplyDetailPanelPosition(string? position = null)
    {
        position = DetailPanelPositionSettingsStore.Normalize(position ?? DetailPanelPositionSettingsStore.Load());
        var viewMode = ViewModel?.ViewMode;
        var coversHalfWidth = viewMode == ViewMode.Covers && !CoversDetailLayoutSettingsStore.UsesCompact();
        var showFullDetail = viewMode == ViewMode.List || coversHalfWidth;
        ApplyListDetailLayout(position, showFullDetail, coversHalfWidth);

        if (coversHalfWidth)
            CompactInfoPanel.Visibility = Visibility.Collapsed;

        ApplyCompactDetailLayout(position);
    }

    private void ApplyListDetailLayout(string position, bool visible, bool halfWidth)
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
            ViewsColumn.Width = new GridLength(1, GridUnitType.Star);
            ViewsColumn.MinWidth = halfWidth ? 200 : 320;
            DetailColumn.Width = halfWidth
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(360);
            DetailColumn.MinWidth = halfWidth ? 240 : 200;
            PlaceDetailHost(column: 0, row: 0);
            PlaceListChrome(column: 1, row: 0);
            PlaceViewsHost(column: 2, row: 0);
            SetDetailPanelBorder(DetailPanelPositionSettingsStore.Left);
            return;
        }

        ViewsColumn.Width = halfWidth
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(DefaultListColumnWidth);
        ViewsColumn.MinWidth = 200;
        DetailColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailColumn.MinWidth = halfWidth ? 240 : 320;
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
        ViewsColumn.Width = new GridLength(DefaultListColumnWidth);
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
