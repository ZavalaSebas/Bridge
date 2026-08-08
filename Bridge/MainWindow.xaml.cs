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

        // View-only behavior (Fase 1 shell): the hamburger collapses/expands the
        // sidebar to an icon rail. Kept in code-behind because it's pure layout
        // state, not app logic — same category as GridSplitter handling.
        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            bool collapsed = SidebarColumn.Width.Value <= 56;
            SidebarColumn.Width = new GridLength(collapsed ? 280 : 56);
            SidebarTitle.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            foreach (var item in NavList.ItemContainerGenerator.Items)
            {
                if (NavList.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container
                    && FindVisualChild<System.Windows.Controls.TextBlock>(container) is { } label
                    && label.Name == "NavLabel")
                {
                    label.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        // Fase 2 (List): collapses/expands the detail panel to the right of the
        // list. Width 0 = collapsed; the GridSplitter still lets the user drag it
        // back open (its neighbor column has MinWidth 0).
        private void ToggleDetailPanel_Click(object sender, RoutedEventArgs e)
        {
            bool collapsed = DetailColumn.Width.Value <= 0;
            DetailColumn.Width = new GridLength(collapsed ? 380 : 0);
        }

        // Fase 4 (Table): the ListView rows bind to GameDetailRow (not Game
        // directly), so SelectedItem needs an explicit handler to keep
        // SelectedGame in sync with the current table selection.
        private void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ListView { SelectedItem: GameDetailRow row }
                && DataContext is ViewModels.MainViewModel vm)
            {
                vm.SelectedGame = row.Game;
            }
        }

        // Fase 4 (Table): GridView doesn't support star sizing on columns, so
        // the Name column width is recalculated here on every ListView resize
        // event. The remaining space after all fixed-width columns (cols 1-6)
        // and the system scrollbar is given to column 0 (Name).
        //
        // Column Widths (use Width, NOT ActualWidth — ActualWidth varies
        // during layout and causes Name to oscillate between overshoot and
        // collapse). The assignment is deferred to Loaded priority so the
        // selection trigger's BorderThickness layout finishes before the
        // column resize; without this deferral the two layout passes race
        // within the same frame and produce a gray ghost band + blurry text.
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

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                if (FindVisualChild<T>(child) is { } nested)
                {
                    return nested;
                }
            }

            return null;
        }

        // Opens the overflow menu anchored to the top-bar button. ContextMenu is
        // not part of the visual tree, so it must be opened explicitly.
        private void OverflowMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element && element.ContextMenu is { } menu)
            {
                menu.PlacementTarget = element;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
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