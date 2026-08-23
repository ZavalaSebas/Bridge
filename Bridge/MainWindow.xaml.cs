using System.Windows;
using System.Windows.Input;
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
        private WindowState _previousWindowState = WindowState.Normal;
        private MainViewModel? _subscribedVm;

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;

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

            // If DataContext was set before Loaded (App.xaml.cs), wire now
            if (DataContext is MainViewModel vm0) SubscribeToViewModel(vm0);

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

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm) UnsubscribeFromViewModel(oldVm);
            if (e.NewValue is MainViewModel newVm) SubscribeToViewModel(newVm);
        }

        // Positions the notifications popup under the bell button (the bell lives
        // inside TitleBar.Header's namescope, so XAML ElementName can't reach it).
        // Placement=Relative + PlacementTarget=this makes the popup follow the
        // window when it's moved/resized — offsets are window-relative.
        private void PositionNotificationsPopup()
        {
            if (NotificationsPopup.Child is not FrameworkElement content)
                return;

            content.Measure(new Size(360, double.PositiveInfinity));
            var width = content.DesiredSize.Width > 0 ? content.DesiredSize.Width : 360;

            // Bell button's position relative to the window (both live in the same visual tree)
            var transform = BellButton.TransformToVisual(this);
            var bellPos = transform.Transform(new Point(0, 0));

            NotificationsPopup.PlacementTarget = this;
            NotificationsPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            NotificationsPopup.HorizontalOffset = bellPos.X + BellButton.ActualWidth - width;
            NotificationsPopup.VerticalOffset = bellPos.Y + BellButton.ActualHeight + 8;
        }

        private void SubscribeToViewModel(MainViewModel vm)
        {
            if (_subscribedVm == vm) return;
            if (_subscribedVm is not null) UnsubscribeFromViewModel(_subscribedVm);
            _subscribedVm = vm;
            vm.MinimizeWindowRequested += OnMinimizeRequested;
            vm.RestoreWindowRequested += OnRestoreRequested;
            vm.PropertyChanged += OnNotificationsVmPropertyChanged;
        }

        private void UnsubscribeFromViewModel(MainViewModel vm)
        {
            vm.MinimizeWindowRequested -= OnMinimizeRequested;
            vm.RestoreWindowRequested -= OnRestoreRequested;
            vm.PropertyChanged -= OnNotificationsVmPropertyChanged;
            if (_subscribedVm == vm) _subscribedVm = null;
        }

        private void OnNotificationsVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsNotificationsPopupOpen))
            {
                if (_subscribedVm?.IsNotificationsPopupOpen == true)
                    PositionNotificationsPopup();
            }
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            if (!_subscribedVm?.IsNotificationsPopupOpen ?? false)
                return;
            if (_subscribedVm is null || !NotificationsPopup.IsOpen)
                return;

            // Close when clicking anywhere outside the popup and the bell button
            var pos = e.GetPosition(this);
            var popupScreenPos = PointFromScreen(new Point(NotificationsPopup.HorizontalOffset, NotificationsPopup.VerticalOffset));
            var popupRect = new Rect(popupScreenPos, new Size(NotificationsPopup.ActualWidth > 0 ? 360 : 360, 480));
            var bellPos = e.GetPosition(BellButton);

            var inPopup = popupRect.Contains(pos);
            var inBell = bellPos.X >= 0 && bellPos.X <= BellButton.ActualWidth && bellPos.Y >= 0 && bellPos.Y <= BellButton.ActualHeight;

            if (!inPopup && !inBell && _subscribedVm != null)
                _subscribedVm.IsNotificationsPopupOpen = false;
        }

        private void OnMinimizeRequested()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!MinimizeOnGameLaunchSettingsStore.Load()) return;
                _previousWindowState = WindowState;
                WindowState = WindowState.Minimized;
            });
        }

        private void OnRestoreRequested()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!MinimizeOnGameLaunchSettingsStore.Load()) return;
                if (WindowState == WindowState.Minimized)
                    WindowState = _previousWindowState == WindowState.Minimized ? WindowState.Normal : _previousWindowState;
                Activate();
                Focus();
            });
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

