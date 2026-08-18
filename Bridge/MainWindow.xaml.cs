using System.Windows;
using Bridge.Services;
using Bridge.ViewModels;
using Wpf.Ui.Controls;
namespace Bridge
{
    public partial class MainWindow : FluentWindow
    {
        private string _sidebarPosition = "Left";

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
                if (DataContext is MainViewModel vm)
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
                if (DataContext is MainViewModel vm)
                    SaveScrollPosition(vm.ViewMode);
            };

            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }
    }
}
