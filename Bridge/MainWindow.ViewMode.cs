using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Bridge.Core.Entities;
using Bridge.Services;
using Bridge.ViewModels;

namespace Bridge;

public partial class MainWindow
{
    private void SetViewModeList_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        SwitchView(vm, Bridge.Core.Enums.ViewMode.List);
    }

    private void SetViewModeCovers_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        SwitchView(vm, Bridge.Core.Enums.ViewMode.Covers);
    }

    private void SetViewModeTable_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        SwitchView(vm, Bridge.Core.Enums.ViewMode.Table);
    }

    // Switching views: save the outgoing view's scroll offset, swap to the
    // new view, then restore that view's saved offset. Persisting each view's
    // position means coming back to Details/Covers/List/Table always lands
    // where you left it, instead of resetting to the top (and instead of the
    // selection re-assertion approach, which re-rendered the covers and
    // flickered).
    private void SwitchView(MainViewModel vm, Bridge.Core.Enums.ViewMode newMode)
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
            || TableList.View is not GridView gridView
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
        if (TableList.View is not GridView gridView
            || gridView.Columns.Count < 1)
            return;

        ScrollPositionSettingsStore.SaveTableNameWidth(gridView.Columns[0].Width);
    }

    private double? GetScrollOffset(Bridge.Core.Enums.ViewMode mode)
    {
        return mode switch
        {
            Bridge.Core.Enums.ViewMode.Covers => GetScrollViewer(CoversList)?.VerticalOffset,
            Bridge.Core.Enums.ViewMode.List => GetScrollViewer(GamesList)?.VerticalOffset,
            Bridge.Core.Enums.ViewMode.Table => GetScrollViewer(TableList)?.VerticalOffset,
            _ => null
        };
    }

    private void SetScrollOffset(Bridge.Core.Enums.ViewMode mode, double offset)
    {
        if (mode == Bridge.Core.Enums.ViewMode.Covers)
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
        if (DataContext is not MainViewModel vm)
            return;

        switch (vm.ViewMode)
        {
            case Bridge.Core.Enums.ViewMode.List:
                ShowFullWidthDetail();
                CompactInfoPanel.Visibility = Visibility.Collapsed;
                break;
            case Bridge.Core.Enums.ViewMode.Covers:
                CompactInfoPanel.Visibility = Visibility.Collapsed;
                HideDetailPanel();
                break;
            case Bridge.Core.Enums.ViewMode.Table:
                CompactInfoPanel.Visibility = Visibility.Collapsed;
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
        if (DataContext is not MainViewModel vm
            || vm.SelectedGame is not { } game
            || vm.ViewMode != Bridge.Core.Enums.ViewMode.Covers)
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
        DetailSeparator.Visibility = Visibility.Visible;
        DetailSplitter.Visibility = Visibility.Visible;
    }

    private void HideDetailPanel()
    {
        ViewsColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailColumn.MinWidth = 0;
        DetailColumn.Width = new GridLength(0);
        DetailSeparator.Visibility = Visibility.Collapsed;
        DetailSplitter.Visibility = Visibility.Collapsed;
    }

    private void CloseCompactInfo_Click(object sender, RoutedEventArgs e)
    {
        CompactInfoPanel.Visibility = Visibility.Collapsed;
    }

    // Table view: rows bind to GameDetailRow (not Game directly), so
    // SelectedItem needs an explicit handler to sync SelectedGame.
    private void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView { SelectedItem: GameDetailRow row }
            && DataContext is MainViewModel vm)
        {
            vm.SelectedGame = row.Game;
        }
    }

    // Table view: dynamically adjusts Name column width to fill
    // remaining space after fixed-width columns. Uses Width (not
    // ActualWidth) for stable values. Deferred to Loaded priority
    // to avoid layout race conditions with selection triggers.
    private void TableList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not ListView listView
            || listView.View is not GridView gridView
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
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
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
}
