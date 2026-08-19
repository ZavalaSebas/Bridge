using System.Windows;
using System.Windows.Controls;
using Bridge.Services;
using Bridge.ViewModels;
using Bridge.Core.Enums;

namespace Bridge;

public partial class MainWindow
{
    private void SetViewModeList_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        SwitchView(vm, ViewMode.List);
    }

    private void SetViewModeCovers_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        SwitchView(vm, ViewMode.Covers);
    }

    private void SetViewModeTable_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        SwitchView(vm, ViewMode.Table);
    }

    // Switching views: save the outgoing view's scroll offset, swap to the
    // new view, then restore that view's saved offset. Persisting each view's
    // position means coming back to Details/Covers/List/Table always lands
    // where you left it, instead of resetting to the top (and instead of the
    // selection re-assertion approach, which re-rendered the covers and
    // flickered).
    private void SwitchView(MainViewModel vm, ViewMode newMode)
    {
        var oldMode = vm.ViewMode;
        if (oldMode == newMode)
            return;

        SaveScrollPosition(oldMode);

        if (!KeepSelectionAcrossViewsSettingsStore.Load())
            vm.SelectedGame = null;

        vm.ViewMode = newMode;
        LibraryDetail.ApplyViewModeLayout(newMode);

        RestoreScrollPosition(newMode);
    }

    // Captures the current scroll offset of a view and persists it, so the
    // position survives switching away (and closing the app).
    private void SaveScrollPosition(ViewMode mode)
    {
        var offset = LibraryDetail.GetScrollOffset(mode);
        if (offset is null)
            return;

        ScrollPositionSettingsStore.Save(mode.ToString(), offset.Value);
    }

    // Restores a view's saved scroll offset. Runs synchronously from Loaded
    // (before the window's first paint), so opening back into a view lands on
    // the saved position without a visible jump from the top.
    private void RestoreScrollPosition(ViewMode mode)
    {
        var offset = ScrollPositionSettingsStore.Load(mode.ToString());
        if (offset <= 0)
            return;

        LibraryDetail.SetScrollOffset(mode, offset);
    }

    // Applies the Table view's saved Name-column width before the first
    // render, so opening straight into Table doesn't visibly resize the
    // column from its XAML default. Runs in Loaded, which fires before the
    // window is first painted.
    private void RestoreTableNameWidth(ViewMode mode)
    {
        if (mode != ViewMode.Table)
            return;

        var width = ScrollPositionSettingsStore.LoadTableNameWidth();
        LibraryDetail.RestoreTableNameWidth(width);
    }

    // Persists the Table view's current Name-column width, so the last used
    // width is what the next open restores. Called on close; the auto-fill
    // resize also persists as it adjusts.
    private void SaveTableNameWidth() => LibraryDetail.SaveTableNameWidth();

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
            || vm.SelectedGame is null
            || vm.ViewMode != ViewMode.Covers)
        {
            return;
        }

        LibraryDetail.ScrollSelectedCoverIntoView();
    }

    // Applies the per-view layout the click handlers used to hard-code:
    // List keeps the full detail panel, Grid/Table collapse it. Extracted so
    // startup can restore the saved view with the same visual state.
    private void ApplyViewModeLayout() =>
        LibraryDetail.ApplyViewModeLayout(
            DataContext is MainViewModel vm ? vm.ViewMode : ViewMode.List);
}
