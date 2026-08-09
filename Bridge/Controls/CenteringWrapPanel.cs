using System;
using System.Windows;
using System.Windows.Controls;

namespace Bridge.Controls;

/// <summary>
/// WrapPanel that centers every line, so a partial last row of covers sits
/// centered instead of hugging the left edge — and when the content is narrower
/// than the viewport the whole block centers too. Item margins are respected so
/// the existing card spacing keeps working.
/// </summary>
public class CenteringWrapPanel : Panel
{
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
        double x = 0;
        double y = 0;
        double lineHeight = 0;

        foreach (UIElement child in Children)
        {
            child.Measure(availableSize);

            var width = GetOuterWidth(child);
            var height = GetOuterHeight(child);

            if (x + width > availableSize.Width && x > 0)
            {
                y += lineHeight;
                x = 0;
                lineHeight = 0;
            }

            x += width;
            lineHeight = Math.Max(lineHeight, height);
        }

        y += lineHeight;
        return new Size(availableSize.Width, y);
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
