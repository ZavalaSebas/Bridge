using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using Bridge.Core.Entities;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace Bridge
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ToggleSortDirection_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.SortDescending = !vm.SortDescending;
        }

        private void SetViewModeList_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.ViewMode = Bridge.Core.Enums.ViewMode.List;
                ShowFullWidthDetail();
            }
        }

        private void SetViewModeGrid_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.ViewMode = Bridge.Core.Enums.ViewMode.Grid;
                CompactInfoPanel.Visibility = System.Windows.Visibility.Collapsed;
                HideDetailPanel();
            }
        }

        private void SetViewModeTable_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.ViewMode = Bridge.Core.Enums.ViewMode.Table;
                HideDetailPanel();
            }
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

            if (available < 100) available = 100;

            var nameColumn = gridView.Columns[0];
            if (Math.Abs(nameColumn.Width - available) > 0.5)
            {
                double capture = available;
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() =>
                    {
                        nameColumn.Width = capture;
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
            var window = new AboutWindow { Owner = this };
            window.ShowDialog();
        }

        private string _sidebarPosition = "Left";

        // View > Sidebar: show/hide the sidebar (and its divider).
        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem { IsChecked: bool shown })
            {
                SidebarHost.Visibility = shown ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                SidebarSeparator.Visibility = shown ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
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
                var sidebar = menu.Items.OfType<System.Windows.Controls.MenuItem>()
                    .FirstOrDefault(i => i.Header?.ToString() == "Sidebar");
                if (sidebar is not null)
                {
                    SyncSidebarMenu(sidebar);
                }
            }
        }

        private void SyncSidebarMenu(System.Windows.Controls.ItemsControl menu)
        {
            foreach (var child in menu.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                if (child.Header?.ToString() == "Show Sidebar")
                {
                    child.IsChecked = SidebarHost.Visibility == System.Windows.Visibility.Visible;
                }
                else if (child.Header?.ToString() == "Position")
                {
                    foreach (var position in child.Items.OfType<System.Windows.Controls.MenuItem>())
                    {
                        position.IsChecked = position.Tag?.ToString() == _sidebarPosition;
                    }
                }
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
                    "MostPlayed" => Bridge.Core.Enums.LibraryFilterPreset.MostPlayed,
                    "RecentlyPlayed" => Bridge.Core.Enums.LibraryFilterPreset.RecentlyPlayed,
                    _ => Bridge.Core.Enums.LibraryFilterPreset.All
                };
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
                    "RecentActivity" => Bridge.Core.Enums.GameSortField.LastPlayed,
                    "ReleaseDate" => Bridge.Core.Enums.GameSortField.ReleaseDate,
                    "Developer" => Bridge.Core.Enums.GameSortField.Developer,
                    "Publisher" => Bridge.Core.Enums.GameSortField.Publisher,
                    "Source" => Bridge.Core.Enums.GameSortField.Source,
                    "CriticScore" => Bridge.Core.Enums.GameSortField.CriticScore,
                    _ => Bridge.Core.Enums.GameSortField.Name
                };
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
                    "ReleaseYear" => Bridge.Core.Enums.GameGroupField.ReleaseYear,
                    _ => Bridge.Core.Enums.GameGroupField.None
                };
            }
        }

        // Selects a random game from whatever the current view shows
        // (respects the active search/filter/sort).
        private void SelectRandomGame_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.MainViewModel vm)
                return;

            var visible = vm.GamesView.OfType<Bridge.Core.Entities.Game>().ToList();
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

            var window = new GameEditWindow(editViewModel) { Owner = this };
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
            var viewModel = App.Services.GetRequiredService<EmulatorSetupViewModel>();
            var window = new EmulatorSetupWindow(viewModel) { Owner = this };
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
            var window = new AddGameWindow { Owner = this };
            if (window.ShowDialog() != true)
            {
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.AddGameCommand.Execute(window.GameName);
            }
        }

        private void ScanRom_Click(object sender, RoutedEventArgs e)
        {
            var window = new ScanRomWindow { Owner = this };
            if (window.ShowDialog() != true)
            {
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ScanRomFolderCommand.Execute(window.RomFolder);
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