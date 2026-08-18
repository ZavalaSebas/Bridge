using System.Windows;
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
            LibraryDetail.CompactInfoPanel.Visibility = System.Windows.Visibility.Visible;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => LibraryDetail.CoversList.ScrollIntoView(infoGame)));
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Enter &&
            System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.None &&
            SearchBox.IsKeyboardFocusWithin is false &&
            vm.SelectedGame is { } selectedGame)
        {
            if (selectedGame.IsRunning)
            {
                if (vm.StopGameCommand.CanExecute(null))
                {
                    vm.StopGameCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (vm.PlayGameCommand.CanExecute(null))
            {
                vm.PlayGameCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
