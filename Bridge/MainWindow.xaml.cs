using System.Windows;
using System.Windows.Threading;
using Bridge.Services;
using Bridge.ViewModels;
using Wpf.Ui.Controls;
namespace Bridge
{
    public partial class MainWindow : FluentWindow
    {
        private string _sidebarPosition = "Left";
        private bool _forceExit;

        public MainWindow()
        {
            InitializeComponent();

            // Restore the saved view's layout once the DataContext is assigned
            // (App.xaml.cs sets it after construction): List keeps the detail
            // panel, Grid/Table collapse it.
            Loaded += (_, _) =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                {
                    ThemeManager.ApplyAppearanceSettings();
                });

                ApplyViewModeLayout();
                LibraryDetail.WarmupDetailContent();

                // Restore this view's saved scroll position on open, so Bridge
                // comes back to where you were instead of the top. Also scrolls
                // to the selected game on a fresh library (no saved position yet).
                if (DataContext is MainViewModel vm)
                {
                    RestoreTableNameWidth(vm.ViewMode);
                    RestoreScrollPosition(vm.ViewMode);
                    if (ScrollPositionSettingsStore.Load(vm.ViewMode.ToString()) <= 0)
                        ScrollToSelectedGame();

                    // Defer first-run dialogs until after Show() returns and the
                    // splash is closed — showing a modal during Show()'s Loaded
                    // stack left the main window disabled or non-draggable.
                    Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                    {
                        _ = RunFirstLaunchDialogsAsync(vm);
                    });
                }
                else
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                    {
                        WhatsNewService.ShowIfNeeded(this);
                    });
                }
            };

            // Persist the Table view's Name-column width and the current view's
            // scroll position on close, so the next open (straight into the same
            // view) restores exactly where you left it instead of jumping.
            Closing += (_, e) =>
            {
                PersistViewStateBeforeHide();

                if (!_forceExit && App.TrayIcon.TryMinimizeToTray())
                    e.Cancel = true;
            };

            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        internal void PersistViewStateBeforeHide()
        {
            SaveTableNameWidth();
            if (DataContext is MainViewModel vm)
                SaveScrollPosition(vm.ViewMode);
        }

        internal void RequestExit()
        {
            _forceExit = true;
            App.TrayIcon.Dispose();
            Close();
        }

        private void MainTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // TitleBar.Header does not stretch, so the center column only exists
            // if the header grid has an explicit width that leaves the caption
            // buttons free (minimize / maximize / close).
            const double captionButtonsWidth = 138;
            TitleBarHeaderGrid.Width = Math.Max(0, MainTitleBar.ActualWidth - captionButtonsWidth);
        }

        private async Task RunFirstLaunchDialogsAsync(MainViewModel viewModel)
        {
            await SetupWizardService.ShowIfNeededAsync(this, viewModel);
            WhatsNewService.ShowIfNeeded(this);
            IsEnabled = true;
            Activate();
            Focus();
        }
    }
}
