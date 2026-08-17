using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using Bridge.Core.Entities;
using Bridge.Import.Epic;
using Bridge.Import.Steam;
using Bridge.Services;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace Bridge
{
    public partial class MainWindow : FluentWindow
    {
        // The favorite star sits to the cover's left, tucked behind its edge
        // (translated toward the cover, which renders on top and clips it).
        // Hovering the cover area slides it out; once checked it stays visible.
        // Unchecking it while not hovering tucks it back away.
        private static readonly TimeSpan FavoriteStarMotion = TimeSpan.FromMilliseconds(180);
        private readonly DispatcherTimer _favoriteHideTimer;

        public MainWindow()
        {
            InitializeComponent();

            // Restore the saved view's layout once the DataContext is assigned
            // (App.xaml.cs sets it after construction): List keeps the detail
            // panel, Grid/Table collapse it.
            Loaded += (_, _) =>
            {
                ApplyViewModeLayout();

                // Restore this view's saved scroll position on open, so Bridge
                // comes back to where you were instead of the top. Also scrolls
                // to the selected game on a fresh library (no saved position yet).
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    RestoreTableNameWidth(vm.ViewMode);
                    RestoreScrollPosition(vm.ViewMode);
                    if (ScrollPositionSettingsStore.Load(vm.ViewMode.ToString()) <= 0)
                        ScrollToSelectedGame();
                }
            };

            // Persist the Table view's Name-column width and the current view's
            // scroll position on close, so the next open (straight into the same
            // view) restores exactly where you left it instead of jumping.
            Closing += (_, _) =>
            {
                SaveTableNameWidth();
                if (DataContext is ViewModels.MainViewModel vm)
                    SaveScrollPosition(vm.ViewMode);
            };

            // Debounce the tuck-away: hovering near the seam between the star
            // and the cover fires MouseLeave/MouseEnter in rapid succession
            // (the revealed star shifts the hovered area), which would make the
            // star flicker in and out. Only hide once the mouse has actually
            // left the cover for a beat.
            _favoriteHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _favoriteHideTimer.Tick += (_, _) =>
            {
                _favoriteHideTimer.Stop();
                if (CoverFavoriteButton.IsChecked != true && CoverHost.IsMouseOver is false)
                    AnimateFavoriteStar(inView: false);
            };
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
            // Only pop on a real click — a favorited game picked from the list
            // also fires Checked via binding, and popping then would be noise.
            if (CoverFavoriteButton.IsMouseOver)
            {
                PlayFavoritePop();
                PersistFavorite();
            }
        }

        private void CoverFavorite_Unchecked(object sender, RoutedEventArgs e)
        {
            // Only tuck it away when the mouse isn't over the cover — otherwise
            // unchecking while hovering would hide the very thing being hovered.
            if (CoverHost.IsMouseOver is false)
                AnimateFavoriteStar(inView: false);

            // Same guard as Checked: binding fires this on game selection too,
            // so only persist an actual user click.
            if (CoverFavoriteButton.IsMouseOver)
                PersistFavorite();
        }

        private void PersistFavorite()
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.PersistFavorite();
        }

        // Same persistence pattern as the hero star, for the compact panel's
        // favorite star: binding fires Checked/Unchecked on game selection too,
        // so only persist an actual user click (IsMouseOver on the star). The
        // pop plays only on a real click for the same reason.
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

        // Same spring-back "pop" as the hero star (reuses BuildPopAnimation).
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
            // A tucked-away star must not catch the mouse (it would sit over the
            // seam and block the cover's hover); only the visible one is clickable.
            CoverFavoriteButton.IsHitTestVisible = inView;
        }

        // The star "pops" when favorited: it overshoots past its final size and
        // springs back, like a sticker being pressed on. Driven by keyframes so
        // the peak is part of the same timeline instead of two chained tweens.
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

        private void ToggleSortDirection_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.SortDescending = !vm.SortDescending;
        }

        private void SetViewModeList_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.MainViewModel vm)
                return;

            SwitchView(vm, Bridge.Core.Enums.ViewMode.List);
        }

        private void SetViewModeGrid_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.MainViewModel vm)
                return;

            SwitchView(vm, Bridge.Core.Enums.ViewMode.Grid);
        }

        private void SetViewModeTable_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.MainViewModel vm)
                return;

            SwitchView(vm, Bridge.Core.Enums.ViewMode.Table);
        }

        // Switching views: save the outgoing view's scroll offset, swap to the
        // new view, then restore that view's saved offset. Persisting each view's
        // position means coming back to Details/Covers/List/Table always lands
        // where you left it, instead of resetting to the top (and instead of the
        // selection re-assertion approach, which re-rendered the covers and
        // flickered).
        private void SwitchView(ViewModels.MainViewModel vm, Bridge.Core.Enums.ViewMode newMode)
        {
            var oldMode = vm.ViewMode;
            if (oldMode == newMode)
                return;

            SaveScrollPosition(oldMode);

            vm.ViewMode = newMode;
            ApplyViewModeLayout();

            RestoreScrollPosition(newMode);
        }

        // Captures the current scroll offset of a view and persists it, so the
        // position survives switching away (and closing the app).
        private void SaveScrollPosition(Bridge.Core.Enums.ViewMode mode)
        {
            var offset = GetScrollOffset(mode);
            if (offset is null)
                return;

            ScrollPositionSettingsStore.Save(mode.ToString(), offset.Value);
        }

        // Restores a view's saved scroll offset. Runs synchronously from Loaded
        // (before the window's first paint), so opening back into a view lands on
        // the saved position without a visible jump from the top.
        private void RestoreScrollPosition(Bridge.Core.Enums.ViewMode mode)
        {
            var offset = ScrollPositionSettingsStore.Load(mode.ToString());
            if (offset <= 0)
                return;

            SetScrollOffset(mode, offset);
        }

        // Applies the Table view's saved Name-column width before the first
        // render, so opening straight into Table doesn't visibly resize the
        // column from its XAML default. Runs in Loaded, which fires before the
        // window is first painted.
        private void RestoreTableNameWidth(Bridge.Core.Enums.ViewMode mode)
        {
            if (mode != Bridge.Core.Enums.ViewMode.Table
                || TableList.View is not System.Windows.Controls.GridView gridView
                || gridView.Columns.Count < 1)
                return;

            var width = ScrollPositionSettingsStore.LoadTableNameWidth();
            if (width <= 0)
                return;

            gridView.Columns[0].Width = width;
        }

        // Persists the Table view's current Name-column width, so the last used
        // width is what the next open restores. Called on close; the auto-fill
        // resize also persists as it adjusts.
        private void SaveTableNameWidth()
        {
            if (TableList.View is not System.Windows.Controls.GridView gridView
                || gridView.Columns.Count < 1)
                return;

            ScrollPositionSettingsStore.SaveTableNameWidth(gridView.Columns[0].Width);
        }

        private double? GetScrollOffset(Bridge.Core.Enums.ViewMode mode)
        {
            return mode switch
            {
                Bridge.Core.Enums.ViewMode.Grid => GetScrollViewer(CoversList)?.VerticalOffset,
                Bridge.Core.Enums.ViewMode.List => GetScrollViewer(GamesList)?.VerticalOffset,
                Bridge.Core.Enums.ViewMode.Table => GetScrollViewer(TableList)?.VerticalOffset,
                _ => null
            };
        }

        private void SetScrollOffset(Bridge.Core.Enums.ViewMode mode, double offset)
        {
            if (mode == Bridge.Core.Enums.ViewMode.Grid)
                GetScrollViewer(CoversList)?.ScrollToVerticalOffset(offset);
            else if (mode == Bridge.Core.Enums.ViewMode.List)
                GetScrollViewer(GamesList)?.ScrollToVerticalOffset(offset);
            else if (mode == Bridge.Core.Enums.ViewMode.Table)
                GetScrollViewer(TableList)?.ScrollToVerticalOffset(offset);
        }

        // Finds the ScrollViewer WPF wraps inside a ListBox/ListView.
        private static ScrollViewer? GetScrollViewer(DependencyObject root)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is ScrollViewer viewer)
                    return viewer;
                if (GetScrollViewer(child) is { } found)
                    return found;
            }

            return null;
        }

        // Applies the per-view layout the click handlers used to hard-code:
        // List keeps the full detail panel, Grid/Table collapse it. Extracted so
        // startup can restore the saved view with the same visual state.
        private void ApplyViewModeLayout()
        {
            if (DataContext is not ViewModels.MainViewModel vm)
                return;

            switch (vm.ViewMode)
            {
                case Bridge.Core.Enums.ViewMode.List:
                    ShowFullWidthDetail();
                    CompactInfoPanel.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case Bridge.Core.Enums.ViewMode.Grid:
                    CompactInfoPanel.Visibility = System.Windows.Visibility.Collapsed;
                    HideDetailPanel();
                    break;
                case Bridge.Core.Enums.ViewMode.Table:
                    CompactInfoPanel.Visibility = System.Windows.Visibility.Collapsed;
                    HideDetailPanel();
                    break;
            }
        }

        // After startup selects the last-played game, the Covers (Grid) view may
        // open scrolled to the top with the selection out of view (it can be
        // hundreds of rows down). Bring it into the viewport before the first
        // paint, so the selected cover is already visible without a visible
        // scroll from the top. Only Grid is scrolled here — List/Table restore
        // their saved scroll position (ScrollPositionSettingsStore), and forcing
        // layout on them at startup can mis-size the Table's auto-fill Name column.
        private void ScrollToSelectedGame()
        {
            if (DataContext is not ViewModels.MainViewModel vm
                || vm.SelectedGame is not { } game
                || vm.ViewMode != Bridge.Core.Enums.ViewMode.Grid)
            {
                return;
            }

            CoversList.ScrollIntoView(game);
        }

        // The Details view keeps the full detail panel on the right; the covers
        // (Grid) and the List (Table) views run full-screen without it.
        private void ShowFullWidthDetail()
        {
            ViewsColumn.Width = new GridLength(360);
            DetailColumn.MinWidth = 320;
            DetailColumn.Width = new GridLength(1, GridUnitType.Star);
            DetailSeparator.Visibility = System.Windows.Visibility.Visible;
            DetailSplitter.Visibility = System.Windows.Visibility.Visible;
        }

        private void HideDetailPanel()
        {
            ViewsColumn.Width = new GridLength(1, GridUnitType.Star);
            DetailColumn.MinWidth = 0;
            DetailColumn.Width = new GridLength(0);
            DetailSeparator.Visibility = System.Windows.Visibility.Collapsed;
            DetailSplitter.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void CloseCompactInfo_Click(object sender, RoutedEventArgs e)
        {
            CompactInfoPanel.Visibility = System.Windows.Visibility.Collapsed;
        }

        // Table view: rows bind to GameDetailRow (not Game directly), so
        // SelectedItem needs an explicit handler to sync SelectedGame.
        private void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ListView { SelectedItem: GameDetailRow row }
                && DataContext is ViewModels.MainViewModel vm)
            {
                vm.SelectedGame = row.Game;
            }
        }

        // Table view: dynamically adjusts Name column width to fill
        // remaining space after fixed-width columns. Uses Width (not
        // ActualWidth) for stable values. Deferred to Loaded priority
        // to avoid layout race conditions with selection triggers.
        private void TableList_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ListView listView
                || listView.View is not System.Windows.Controls.GridView gridView
                || gridView.Columns.Count < 2)
                return;

            if (_suppressTableResize)
                return;

            _suppressTableResize = true;

            double totalFixed = 0;
            for (int i = 1; i < gridView.Columns.Count; i++)
                totalFixed += gridView.Columns[i].Width;

            double available = listView.ActualWidth
                               - System.Windows.SystemParameters.VerticalScrollBarWidth
                               - totalFixed;

            // If the available width isn't a sane positive value, the list is
            // mid-layout (startup, or the detail panel collapsing) — skip this
            // pass and let the next SizeChanged adjust with real dimensions.
            // Clamping to a minimum here would permanently shrink the Name
            // column to that minimum on startup.
            if (available < 100)
            {
                _suppressTableResize = false;
                return;
            }

            var nameColumn = gridView.Columns[0];
            if (Math.Abs(nameColumn.Width - available) > 0.5)
            {
                double capture = available;
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() =>
                    {
                        nameColumn.Width = capture;
                        // Persist so the next open (straight into Table) starts
                        // with this width instead of resizing visibly.
                        ScrollPositionSettingsStore.SaveTableNameWidth(capture);
                        _suppressTableResize = false;
                    }));
            }
            else
            {
                _suppressTableResize = false;
            }
        }

        private bool _suppressTableResize;


        // Opens the Support menu links (Ko-fi / GitHub Sponsors) in the browser.
        private void OpenSupportLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem { Tag: string url } && !string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch
                {
                    // Missing browser/URL — nothing to do.
                }
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var background = (DataContext as MainViewModel)?.SelectedGame?.BackgroundImage;
            var window = new AboutWindow(background) { Owner = this };
            window.ShowDialog();
        }

        private string _sidebarPosition = "Left";

        // View > Sidebar: show/hide the sidebar (and its divider). The state
        // lives in the VM so the menus' icons (Eye / EyeOff) reflect it.
        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SidebarVisible = !vm.SidebarVisible;
                ApplySidebarVisibility();
            }
        }

        private void ApplySidebarVisibility()
        {
            var visible = (DataContext as MainViewModel)?.SidebarVisible ?? true;
            SidebarHost.Visibility = visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            SidebarSeparator.Visibility = visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        // Keeps both Sidebar menus (main menu + right-click) in sync with the
        // actual sidebar state when they open.
        private void SidebarMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.ContextMenu menu)
            {
                SyncSidebarMenu(menu);
            }
        }

        private void MainMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.ContextMenu menu)
            {
                foreach (var child in menu.Items.OfType<System.Windows.Controls.MenuItem>())
                {
                    if (child.Header?.ToString() == "Sidebar")
                    {
                        SyncSidebarMenu(child);
                    }
                    else if (child.Header?.ToString() == "Theme")
                    {
                        SyncThemeMenu(child);
                    }
                    else if (child.Header?.ToString() == "3rd party clients")
                    {
                        SyncThirdPartyClientsMenu(child);
                    }
                }
            }
        }

        private void SyncThemeMenu(System.Windows.Controls.ItemsControl themeMenu)
        {
            var current = Services.ThemeManager.ToHex(Services.ThemeManager.CurrentAccent);
            foreach (var item in themeMenu.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                item.IsChecked = item.Tag?.ToString() == current;
            }
        }

        private void SyncSidebarMenu(System.Windows.Controls.ItemsControl menu)
        {
            foreach (var child in menu.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                if (child.Header?.ToString() == "Position")
                {
                    foreach (var position in child.Items.OfType<System.Windows.Controls.MenuItem>())
                    {
                        position.IsChecked = position.Tag?.ToString() == _sidebarPosition;
                    }
                }
            }
        }

        // "3rd party clients": show the launcher clients that are installed on
        // this machine (Steam and/or Epic) and open the one the user picks.
        private static void SyncThirdPartyClientsMenu(System.Windows.Controls.ItemsControl menu)
        {
            menu.Items.Clear();

            var steamPath = SteamPaths.GetInstallationPath();
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                var steamExe = Path.Combine(steamPath, "steam.exe");
                if (File.Exists(steamExe))
                {
                    menu.Items.Add(CreateClientMenuItem("Steam", steamExe));
                }
            }

            var epicPath = EpicPaths.GetInstallationPath();
            if (!string.IsNullOrWhiteSpace(epicPath))
            {
                var epicExe = EpicPaths.GetExecutablePath(epicPath);
                if (File.Exists(epicExe))
                {
                    menu.Items.Add(CreateClientMenuItem("Epic", epicExe));
                }
            }
        }

        private static System.Windows.Controls.MenuItem CreateClientMenuItem(string name, string executable)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = name,
                Tag = executable
            };
            item.Click += OpenThirdPartyClient_Click;
            item.Icon = new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = Wpf.Ui.Controls.SymbolRegular.Cloud24,
                FontSize = 16,
                Foreground = System.Windows.Application.Current.TryFindResource("SystemAccentColorPrimaryBrush") as System.Windows.Media.Brush
                    ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
            };
            return item;
        }

        private static void OpenThirdPartyClient_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem { Tag: string executable }
                && File.Exists(executable))
            {
                Process.Start(new ProcessStartInfo { FileName = executable, UseShellExecute = true });
            }
        }

        // View > Sidebar > Position: dock the sidebar on any edge. The sidebar
        // stays a vertical rail for Left/Right and becomes a horizontal bar for
        // Top/Bottom; the divider follows to the facing edge.
        private void SetSidebarPosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem { Tag: string position } item)
            {
                return;
            }

            if (item.Parent is System.Windows.Controls.MenuItem positionMenu)
            {
                foreach (var child in positionMenu.Items.OfType<System.Windows.Controls.MenuItem>())
                {
                    child.IsChecked = false;
                }

                item.IsChecked = true;
            }

            _sidebarPosition = position;

            switch (position)
            {
                case "Right":
                    DockPanel.SetDock(SidebarHost, Dock.Right);
                    DockPanel.SetDock(SidebarSeparator, Dock.Right);
                    SidebarHost.Width = 52;
                    SidebarHost.Height = double.NaN;
                    SidebarSeparator.Width = 1;
                    SidebarSeparator.Height = double.NaN;
                    SidebarStack.Orientation = Orientation.Vertical;
                    break;
                case "Top":
                    DockPanel.SetDock(SidebarHost, Dock.Top);
                    DockPanel.SetDock(SidebarSeparator, Dock.Top);
                    SidebarHost.Height = 52;
                    SidebarHost.Width = double.NaN;
                    SidebarSeparator.Height = 1;
                    SidebarSeparator.Width = double.NaN;
                    SidebarStack.Orientation = Orientation.Horizontal;
                    break;
                case "Bottom":
                    DockPanel.SetDock(SidebarHost, Dock.Bottom);
                    DockPanel.SetDock(SidebarSeparator, Dock.Bottom);
                    SidebarHost.Height = 52;
                    SidebarHost.Width = double.NaN;
                    SidebarSeparator.Height = 1;
                    SidebarSeparator.Width = double.NaN;
                    SidebarStack.Orientation = Orientation.Horizontal;
                    break;
                default: // Left
                    DockPanel.SetDock(SidebarHost, Dock.Left);
                    DockPanel.SetDock(SidebarSeparator, Dock.Left);
                    SidebarHost.Width = 52;
                    SidebarHost.Height = double.NaN;
                    SidebarSeparator.Width = 1;
                    SidebarSeparator.Height = double.NaN;
                    SidebarStack.Orientation = Orientation.Vertical;
                    break;
            }

            // The active-item indicator border follows the sidebar edge.
            var indicator = position switch
            {
                "Right" => new Thickness(0, 0, 3, 0),
                "Top" => new Thickness(0, 3, 0, 0),
                "Bottom" => new Thickness(0, 0, 0, 3),
                _ => new Thickness(3, 0, 0, 0)
            };
            foreach (var button in SidebarStack.Children.OfType<System.Windows.Controls.Button>())
            {
                button.BorderThickness = indicator;
            }
        }

        // Theme menu: apply a preset accent (the whole palette recomputes).
        private void SetThemeColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem { Tag: string hex }
                && Services.ThemeManager.TryParseHex(hex, out var color))
            {
                Services.ThemeManager.Apply(color);
            }
        }

        // Theme menu: open the custom color picker.
        private void CustomThemeColor_Click(object sender, RoutedEventArgs e)
        {
            var window = new ThemeColorWindow { Owner = this };
            window.ShowDialog();
        }

        private void ShowLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
        }

        private void ShowStatistics_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Statistics;
        }

        // Opens the sender's ContextMenu on left-click (used by icon
        // buttons in the top panel).
        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element && element.ContextMenu is { } menu)
            {
                menu.PlacementTarget = element;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                // The context menu lives in a Popup outside the visual tree, so
                // ElementName bindings can't reach the window. Feed it the
                // window's DataContext (the MainViewModel) explicitly — the
                // buttons live under panels whose DataContext is SelectedGame.
                menu.DataContext = Window.GetWindow(element)?.DataContext;
                menu.IsOpen = true;
            }
        }

        private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox
                && listBox.SelectedItem is Game game
                && DataContext is ViewModels.MainViewModel vm)
            {
                vm.PlayGameCommand.Execute(game);
            }
        }

        private void FilterPresetMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm
                && sender is System.Windows.Controls.MenuItem item
                && item.Tag is string tag)
            {
                vm.FilterPreset = tag switch
                {
                    "Favorite" => Bridge.Core.Enums.LibraryFilterPreset.Favorite,
                    "Installed" => Bridge.Core.Enums.LibraryFilterPreset.Installed,
                    "NotPlayed" => Bridge.Core.Enums.LibraryFilterPreset.NotPlayed,
                    "RecentlyPlayed" => Bridge.Core.Enums.LibraryFilterPreset.RecentlyPlayed,
                    _ => Bridge.Core.Enums.LibraryFilterPreset.All
                };

                // A checkable MenuItem toggles its own IsChecked on click even
                // when the source value doesn't change (clicking the already
                // active entry), which would visually untick it while the filter
                // stays on. Re-assert every entry's check from the real state.
                ReassertMenuChecks(item, tag);
            }
        }

        private void SortFieldMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm
                && sender is System.Windows.Controls.MenuItem item
                && item.Tag is string tag)
            {
                vm.SortField = tag switch
                {
                    "PlaytimeSeconds" => Bridge.Core.Enums.GameSortField.PlaytimeSeconds,
                    "PlayCount" => Bridge.Core.Enums.GameSortField.PlayCount,
                    "LastPlayed" => Bridge.Core.Enums.GameSortField.LastPlayed,
                    "ReleaseDate" => Bridge.Core.Enums.GameSortField.ReleaseDate,
                    "Developer" => Bridge.Core.Enums.GameSortField.Developer,
                    "Publisher" => Bridge.Core.Enums.GameSortField.Publisher,
                    "Source" => Bridge.Core.Enums.GameSortField.Source,
                    "CriticScore" => Bridge.Core.Enums.GameSortField.CriticScore,
                    _ => Bridge.Core.Enums.GameSortField.Name
                };

                ReassertMenuChecks(item, tag);
            }
        }

        private void GroupFieldMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm
                && sender is System.Windows.Controls.MenuItem item
                && item.Tag is string tag)
            {
                vm.GroupField = tag switch
                {
                    "Library" => Bridge.Core.Enums.GameGroupField.Library,
                    "Developer" => Bridge.Core.Enums.GameGroupField.Developer,
                    "Publisher" => Bridge.Core.Enums.GameGroupField.Publisher,
                    "Platform" => Bridge.Core.Enums.GameGroupField.Platform,
                    "Genre" => Bridge.Core.Enums.GameGroupField.Genre,
                    "IsInstalled" => Bridge.Core.Enums.GameGroupField.IsInstalled,
                    "CompletionStatus" => Bridge.Core.Enums.GameGroupField.CompletionStatus,
                    "PlaytimeSeconds" => Bridge.Core.Enums.GameGroupField.PlaytimeSeconds,
                    "PlayCount" => Bridge.Core.Enums.GameGroupField.PlayCount,
                    "ReleaseYear" => Bridge.Core.Enums.GameGroupField.ReleaseYear,
                    "LastPlayed" => Bridge.Core.Enums.GameGroupField.LastPlayed,
                    _ => Bridge.Core.Enums.GameGroupField.None
                };

                ReassertMenuChecks(item, tag);
            }
        }

        // A checkable MenuItem toggles its own IsChecked on click even when the
        // bound source value doesn't change (e.g. clicking the already-active
        // filter/sort/group). That would visually untick the active entry while
        // it stays applied. Re-assert the check on every sibling entry from the
        // tag of the one that was just clicked.
        private static void ReassertMenuChecks(System.Windows.Controls.MenuItem clicked, string activeTag)
        {
            // Find the ItemsControl (ContextMenu or submenu) that owns the
            // clicked item, then tick exactly the entry whose Tag matches.
            System.Windows.Controls.ItemsControl? owner =
                System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(clicked);
            if (owner is null)
            {
                return;
            }

            foreach (System.Windows.Controls.MenuItem sibling in owner.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                sibling.IsChecked = sibling.Tag is string siblingTag && siblingTag == activeTag;
            }
        }

        // The shared right-click menu (Bridge.GameContextMenu) is one instance
        // shared by every row in the three list views. Its PlacementTarget is the
        // row that was right-clicked; resolve the clicked game from its
        // DataContext (Game in List/Covers, GameDetailRow in Table) so the menu
        // commands act on THAT game, not whatever was last selected. Feeding the
        // menu the window's DataContext makes the command bindings resolve — a
        // ContextMenu lives in a Popup outside the visual tree, so ElementName
        // bindings can't reach the window on their own.
        private void GameContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ContextMenu menu
                || menu.PlacementTarget is not System.Windows.FrameworkElement row
                || DataContext is not ViewModels.MainViewModel vm)
            {
                return;
            }

            var game = row.DataContext switch
            {
                Game g => g,
                GameDetailRow detail => detail.Game,
                _ => null
            };
            if (game is not null)
            {
                vm.SelectedGame = game;
            }

            menu.DataContext = DataContext;
        }

        // Opens a game link from a More-menu "Links" submenu item. The submenu
        // items are generated from SelectedGameLinks, so the item's DataContext
        // is the Link itself.
        private void OpenLinkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm
                && sender is System.Windows.Controls.MenuItem { DataContext: Link link })
            {
                vm.OpenLinkCommand.Execute(link);
            }
        }

        // Applies a completion status from the More-menu submenu. The items are
        // generated from CompletionStatuses, so the DataContext is the status
        // name string.
        private void CompletionStatusMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm
                && sender is System.Windows.Controls.MenuItem { DataContext: string status })
            {
                vm.SetCompletionStatusCommand.Execute(status);
            }
        }

        // Selects a random game from whatever the current view shows
        // (respects the active search/filter/sort).
        private void SelectRandomGame_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.MainViewModel vm)
                return;

            // GamesView, when grouped, enumerates CollectionViewGroup wrappers —
            // OfType<Game> would come up empty. Enumerate Games and apply the
            // same filter predicate the view uses, so grouping can't break random.
            var visible = vm.Games
                .Where(g => vm.GamesView.Filter is null || vm.GamesView.Filter(g))
                .ToList();
            if (visible.Count == 0)
            {
                return;
            }

            vm.SelectedGame = visible[Random.Shared.Next(visible.Count)];
        }

        // Edit game: opens the dedicated edit window (Playnite-style). No more
        // inline editing — the details panel fields are read-only.
        private void EditGame_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel mainVm || mainVm.SelectedGame is not { } game)
            {
                return;
            }

            var editViewModel = new ViewModels.GameEditViewModel(
                game,
                App.Services.GetRequiredService<Bridge.Core.Contracts.IGameRepository>(),
                App.Services.GetRequiredService<Bridge.Core.Contracts.IRepository<Bridge.Core.Entities.Genre>>(),
                App.Services.GetRequiredService<Bridge.Core.Contracts.IRepository<Bridge.Core.Entities.Company>>(),
                App.Services.GetRequiredService<Bridge.Core.Contracts.IRepository<Bridge.Core.Entities.Platform>>());

            var window = new GameEditWindow(editViewModel, game.BackgroundImage) { Owner = this };
            if (window.ShowDialog() == true)
            {
                mainVm.RefreshGameDisplay(game);
            }
        }

        // Window construction stays in code-behind, matching DEVELOPMENT.md's
        // own "Credits / About Dialog" pattern — not every dialog needs a
        // MainViewModel command just to open it.
        private void ConfigureEmulator_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = App.Services.GetRequiredService<EmulationSettingsViewModel>();
            var window = new EmulationSettingsWindow(viewModel) { Owner = this };
            window.ShowDialog();
        }

        private void IgdbSettings_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = App.Services.GetRequiredService<IgdbSettingsViewModel>();
            var window = new IgdbSettingsWindow(viewModel) { Owner = this };
            window.ShowDialog();
        }

        private void AddGame_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel mainVm)
            {
                return;
            }

            var game = new Bridge.Core.Entities.Game();
            var editViewModel = new ViewModels.GameEditViewModel(
                game,
                App.Services.GetRequiredService<Bridge.Core.Contracts.IGameRepository>(),
                App.Services.GetRequiredService<Bridge.Core.Contracts.IRepository<Bridge.Core.Entities.Genre>>(),
                App.Services.GetRequiredService<Bridge.Core.Contracts.IRepository<Bridge.Core.Entities.Company>>(),
                App.Services.GetRequiredService<Bridge.Core.Contracts.IRepository<Bridge.Core.Entities.Platform>>(),
                isNew: true);

            var window = new GameEditWindow(editViewModel, game.BackgroundImage ?? mainVm.SelectedGame?.BackgroundImage) { Owner = this };
            if (window.ShowDialog() == true)
            {
                mainVm.AddGameToLibrary(game);
            }
        }

        private async void ScanInstalled_Click(object sender, RoutedEventArgs e)
        {
            var background = (DataContext as MainViewModel)?.SelectedGame?.BackgroundImage;
            var window = new ScanInstalledWindow(background) { Owner = this };
            if (window.ShowDialog() != true)
            {
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                foreach (var game in window.CreatedGames)
                {
                    viewModel.AddGameToLibrary(game);
                }

                // Pull metadata right away for the games that were just added —
                // Steam first (a manual copy of a Steam game gets its full store
                // metadata), then IGDB for the rest.
                if (window.CreatedGames.Count > 0)
                {
                    await viewModel.DownloadMetadataForAddedGamesAsync(window.CreatedGames);
                }
            }
        }

        private async void ScanRom_Click(object sender, RoutedEventArgs e)
        {
            var background = (DataContext as MainViewModel)?.SelectedGame?.BackgroundImage;
            var window = new ScanRomWindow(background) { Owner = this };
            if (window.ShowDialog() != true)
            {
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.ScanRomFolderAsync(window.RomFolder);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Covers view: the Info hover button opens the compact inline panel
        // (details + description, no images) on the right, like Playnite's grid
        // side panel. SelectedGame is set so the resolved DevelopersText/
        // PublishersText/PlatformsText refresh before the panel binds.
        private void GameInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement { Tag: Game game }
                && DataContext is MainViewModel viewModel)
            {
                viewModel.SelectedGame = game;
                CompactInfoPanel.Visibility = System.Windows.Visibility.Visible;

                // The panel shrinks the covers area and the wrap reflows, so a
                // cover near the end of a row can end up out of view — bring the
                // selected one back into the viewport (after the layout settles).
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() => CoversList.ScrollIntoView(game)));
            }
        }
    }
}
