using System.Windows;
using System.Windows.Threading;
using Bridge.Services;
using Bridge.ViewModels;
using Wpf.Ui.Controls;

namespace Bridge
{
    public partial class MainWindow : FluentWindow
    {
        private readonly DispatcherTimer _favoriteHideTimer;
        private bool _suppressTableResize;
        private string _sidebarPosition = "Left";

        public MainWindow()
        {
            InitializeComponent();
            WireMainWindowResourceHandlers();

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

            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }
    }
}
