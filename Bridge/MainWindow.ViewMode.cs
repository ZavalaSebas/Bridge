using System.Windows;
using System.Windows.Controls;
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
            || LibraryDetail.TableList.View is not GridView gridView
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
        if (LibraryDetail.TableList.View is not GridView gridView
            || gridView.Columns.Count < 1)
            return;

        ScrollPositionSettingsStore.SaveTableNameWidth(gridView.Columns[0].Width);
    }

    private double? GetScrollOffset(Bridge.Core.Enums.ViewMode mode)
    {
        return mode switch
        {
            Bridge.Core.Enums.ViewMode.Covers => GetScrollViewer(LibraryDetail.CoversList)?.VerticalOffset,
            Bridge.Core.Enums.ViewMode.List => GetScrollViewer(LibraryDetail.GamesList)?.VerticalOffset,
            Bridge.Core.Enums.ViewMode.Table => GetScrollViewer(LibraryDetail.TableList)?.VerticalOffset,
            _ => null
        };
    }

    private void SetScrollOffset(Bridge.Core.Enums.ViewMode mode, double offset)
    {
        if (mode == Bridge.Core.Enums.ViewMode.Covers)
            GetScrollViewer(LibraryDetail.CoversList)?.ScrollToVerticalOffset(offset);
        else if (mode == Bridge.Core.Enums.ViewMode.List)
            GetScrollViewer(LibraryDetail.GamesList)?.ScrollToVerticalOffset(offset);
        else if (mode == Bridge.Core.Enums.ViewMode.Table)
            GetScrollViewer(LibraryDetail.TableList)?.ScrollToVerticalOffset(offset);
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
                LibraryDetail.CompactInfoPanel.Visibility = Visibility.Collapsed;
                break;
            case Bridge.Core.Enums.ViewMode.Covers:
                LibraryDetail.CompactInfoPanel.Visibility = Visibility.Collapsed;
                HideDetailPanel();
                break;
            case Bridge.Core.Enums.ViewMode.Table:
                LibraryDetail.CompactInfoPanel.Visibility = Visibility.Collapsed;
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

        LibraryDetail.CoversList.ScrollIntoView(game);
    }

    // The Details view keeps the full detail panel on the right; the covers
    // (Grid) and the List (Table) views run full-screen without it.
    private void ShowFullWidthDetail()
    {
        LibraryDetail.ViewsColumn.Width = new GridLength(360);
        LibraryDetail.DetailColumn.MinWidth = 320;
        LibraryDetail.DetailColumn.Width = new GridLength(1, GridUnitType.Star);
        LibraryDetail.DetailSeparator.Visibility = Visibility.Visible;
        LibraryDetail.DetailSplitter.Visibility = Visibility.Visible;
    }

    private void HideDetailPanel()
    {
        LibraryDetail.ViewsColumn.Width = new GridLength(1, GridUnitType.Star);
        LibraryDetail.DetailColumn.MinWidth = 0;
        LibraryDetail.DetailColumn.Width = new GridLength(0);
        LibraryDetail.DetailSeparator.Visibility = Visibility.Collapsed;
        LibraryDetail.DetailSplitter.Visibility = Visibility.Collapsed;
    }
}
