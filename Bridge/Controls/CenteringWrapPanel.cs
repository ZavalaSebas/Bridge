using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Bridge.Controls;

/// <summary>
/// WrapPanel that centers every line, so a partial last row of covers sits
/// centered instead of hugging the left edge — and when the content is narrower
/// than the viewport the whole block centers too. Item margins are respected so
/// the existing card spacing keeps working.
/// </summary>
public class CenteringWrapPanel : Panel
{
    public CenteringWrapPanel()
    {
        // The covers list first lays out while the views column is still at its
        // List-view width (360px), so this panel wraps once for a narrow width
        // (one column). When it later receives its real, wider viewport — either
        // because the view switched to Grid or the window resized — its own
        // RenderSize changes, and re-measuring here re-wraps the cards into
        // multiple columns. Without this the panel kept the one-column layout
        // until a zoom change happened to invalidate it.
        SizeChanged += (_, _) => InvalidateMeasure();
    }

    private static double GetOuterWidth(UIElement child)
    {
        var margin = (child as FrameworkElement)?.Margin ?? new Thickness();
        return child.DesiredSize.Width + margin.Left + margin.Right;
    }

    private static double GetOuterHeight(UIElement child)
    {
        var margin = (child as FrameworkElement)?.Margin ?? new Thickness();
        return child.DesiredSize.Height + margin.Top + margin.Bottom;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var constraintWidth = ResolveConstraintWidth(availableSize.Width);
        var childConstraint = new Size(
            double.IsPositiveInfinity(constraintWidth) ? double.PositiveInfinity : constraintWidth,
            availableSize.Height);

        double x = 0;
        double y = 0;
        double lineHeight = 0;
        double maxLineWidth = 0;

        foreach (UIElement child in Children)
        {
            child.Measure(childConstraint);

            var width = GetOuterWidth(child);
            var height = GetOuterHeight(child);

            if (x + width > constraintWidth && x > 0)
            {
                maxLineWidth = Math.Max(maxLineWidth, x);
                y += lineHeight;
                x = 0;
                lineHeight = 0;
            }

            x += width;
            lineHeight = Math.Max(lineHeight, height);
        }

        maxLineWidth = Math.Max(maxLineWidth, x);
        y += lineHeight;

        var resultWidth = double.IsPositiveInfinity(availableSize.Width)
            ? (double.IsPositiveInfinity(constraintWidth) ? maxLineWidth : constraintWidth)
            : availableSize.Width;

        return new Size(resultWidth, y);
    }

    private double ResolveConstraintWidth(double availableWidth)
    {
        if (!double.IsPositiveInfinity(availableWidth))
            return Math.Max(0, availableWidth);

        var viewportWidth = GetScrollViewerViewportWidth();
        if (viewportWidth > 0)
            return viewportWidth;

        // Use an ancestor's width, but never this panel's own ActualWidth — that
        // creates a feedback loop where a one-column layout keeps shrinking.
        for (DependencyObject? current = VisualTreeHelper.GetParent(this); current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ScrollViewer)
                continue;

            if (current is FrameworkElement { ActualWidth: > 0 } element)
                return element.ActualWidth;
        }

        return double.PositiveInfinity;
    }

    private double GetScrollViewerViewportWidth()
    {
        for (DependencyObject? current = this; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ScrollViewer scrollViewer && scrollViewer.ViewportWidth > 0)
                return scrollViewer.ViewportWidth;
        }

        return 0;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        var index = 0;

        while (index < Children.Count)
        {
            var start = index;
            double lineWidth = 0;
            double lineHeight = 0;

            while (index < Children.Count)
            {
                var child = Children[index];
                var width = GetOuterWidth(child);
                if (lineWidth + width > finalSize.Width && lineWidth > 0)
                {
                    break;
                }

                lineWidth += width;
                lineHeight = Math.Max(lineHeight, GetOuterHeight(child));
                index++;
            }

            var x = Math.Max(0, (finalSize.Width - lineWidth) / 2);

            for (var i = start; i < index; i++)
            {
                var child = Children[i];
                var margin = (child as FrameworkElement)?.Margin ?? new Thickness();
                child.Arrange(new Rect(x + margin.Left, y + margin.Top, child.DesiredSize.Width, child.DesiredSize.Height));
                x += GetOuterWidth(child);
            }

            y += lineHeight;
        }

        return finalSize;
    }
}
