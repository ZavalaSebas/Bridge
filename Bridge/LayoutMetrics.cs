using System.Windows;

namespace Bridge;

internal static class LayoutMetrics
{
    public const double ListColumnWidth = 260;

    /// <summary>Detail tab content height — matches <c>Bridge.DetailTabControl</c> row minus top margin.</summary>
    public const double DetailTabContentHeight = 448;

    public static readonly GridLength ListColumnGridLength = new(ListColumnWidth);
}
