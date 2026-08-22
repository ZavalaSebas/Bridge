using System.Windows;

namespace Bridge;

internal static class LayoutMetrics
{
    public const double ListColumnWidth = 260;

    public static readonly GridLength ListColumnGridLength = new(ListColumnWidth);
}
