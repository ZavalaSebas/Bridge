using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Bridge.Controls;

/// <summary>
/// Forwards mouse-wheel scrolling to the nearest parent <see cref="ScrollViewer"/>
/// when this viewer has no overflow or is already at its scroll edge.
/// </summary>
public static class ScrollViewerChaining
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ScrollViewerChaining),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer)
            return;

        if ((bool)e.NewValue)
            viewer.PreviewMouseWheel += OnPreviewMouseWheel;
        else
            viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.Handled)
            return;

        var parent = FindParentScrollViewer(scrollViewer);
        if (parent is null)
            return;

        var scrollableHeight = scrollViewer.ScrollableHeight;
        var offset = scrollViewer.VerticalOffset;
        var scrollingUp = e.Delta > 0;
        var atTop = offset <= 0;
        var atBottom = offset >= scrollableHeight;

        var shouldChain = scrollableHeight <= 0
            || (scrollingUp && atTop)
            || (!scrollingUp && atBottom);

        if (!shouldChain)
            return;

        parent.ScrollToVerticalOffset(
            Math.Clamp(parent.VerticalOffset - e.Delta, 0, parent.ScrollableHeight));
        e.Handled = true;
    }

    private static ScrollViewer? FindParentScrollViewer(DependencyObject child)
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is ScrollViewer viewer && !ReferenceEquals(viewer, child))
                return viewer;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
