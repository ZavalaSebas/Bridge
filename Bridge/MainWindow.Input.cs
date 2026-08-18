using System.Windows;
using Bridge.Core.Entities;
using Bridge.ViewModels;

namespace Bridge;

public partial class MainWindow
{
    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (e.Key == System.Windows.Input.Key.F &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            SearchBox.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.I &&
            System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.None &&
            SearchBox.IsKeyboardFocusWithin is false &&
            vm.ViewMode == Bridge.Core.Enums.ViewMode.Covers &&
            vm.SelectedGame is { } infoGame)
        {
            CompactInfoPanel.Visibility = System.Windows.Visibility.Visible;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => CoversList.ScrollIntoView(infoGame)));
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Enter &&
            System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.None &&
            SearchBox.IsKeyboardFocusWithin is false &&
            vm.SelectedGame is not null)
        {
            if (vm.PlayGameCommand.CanExecute(null))
            {
                vm.PlayGameCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox
            && listBox.SelectedItem is Game game
            && DataContext is MainViewModel vm)
        {
            vm.PlayGameCommand.Execute(game);
        }
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
