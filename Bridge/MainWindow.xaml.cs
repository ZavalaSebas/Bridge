using System.Windows;
using System.Windows.Controls;
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

        // Collapses/expands the detail panel. Width 0 = collapsed; the
        // GridSplitter still lets the user drag it back open.
        private void ToggleDetailPanel_Click(object sender, RoutedEventArgs e)
        {
            bool collapsed = DetailColumn.Width.Value <= 0;
            DetailColumn.Width = new GridLength(collapsed ? 1 : 0, GridUnitType.Star);
        }

        // Toggles sort direction (Ascending/Descending).
        private void ToggleSortDirection_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.SortDescending = !vm.SortDescending;
        }

        // View mode toggle buttons in the top panel.
        private void SetViewModeList_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.ViewMode = Bridge.Core.Enums.ViewMode.List;
        }

        private void SetViewModeGrid_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.ViewMode = Bridge.Core.Enums.ViewMode.Grid;
        }

        private void SetViewModeTable_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.ViewMode = Bridge.Core.Enums.ViewMode.Table;
        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Settings;
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

        // Double-click on a list item launches the game.
        private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox
                && listBox.SelectedItem is Game game
                && DataContext is ViewModels.MainViewModel vm)
            {
                vm.PlayGameCommand.Execute(game);
            }
        }

        // Top panel: filter preset context menu handler.
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

        // Top panel: sort field context menu handler.
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

        // Top panel: group field context menu handler.
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

        // Stub: select random game from current view.
        private void SelectRandomGame_Click(object sender, RoutedEventArgs e) { }

        // Stub: toggle explorer panel.
        private void ToggleExplorerPanel_Click(object sender, RoutedEventArgs e) { }

        // Stub: toggle filter panel.
        private void ToggleFilterPanel_Click(object sender, RoutedEventArgs e) { }

        // Edit game: focuses the name TextBox (or opens a full editor in future).
        private void EditGame_Click(object sender, RoutedEventArgs e) { }

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

        // Opens the compact info window (all details + description, no images)
        // from a cover's hover button. Uses the hovered game, not the list
        // selection — SelectedGame is set so the resolved DevelopersText/
        // PublishersText/PlatformsText refresh before the window binds.
        private void GameInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement { Tag: Game game })
            {
                var window = new GameInfoWindow { Owner = this, DataContext = DataContext };
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.SelectedGame = game;
                }
                window.ShowDialog();
            }
        }
    }
}