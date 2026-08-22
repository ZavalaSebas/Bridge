using System.Windows;
using System.Windows.Controls;
using Bridge.Converters;

namespace Bridge.Services;

public static class ArtworkPreviewHelper
{
    public static void ApplyFrame(Border frame, int sourceWidth, int sourceHeight, ScrollViewer host)
    {
        host.UpdateLayout();
        var maxW = ResolveMaxWidth(host);
        var maxH = ResolveMaxHeight(host);
        var (width, height) = ArtworkFit.FitToViewport(sourceWidth, sourceHeight, maxW, maxH);
        frame.Width = width;
        frame.Height = height;
        frame.MinHeight = 0;
    }

    public static void ClearFrame(Border frame)
    {
        frame.ClearValue(FrameworkElement.WidthProperty);
        frame.ClearValue(FrameworkElement.HeightProperty);
        frame.MinHeight = 120;
    }

    private static double ResolveMaxWidth(ScrollViewer host)
    {
        if (host.ActualWidth > 40)
            return host.ActualWidth - 2;

        if (host.ViewportWidth > 40)
            return host.ViewportWidth - 2;

        return 352;
    }

    private static double ResolveMaxHeight(ScrollViewer host)
    {
        if (host.ActualHeight > 80)
            return host.ActualHeight - 2;

        if (host.ViewportHeight > 80)
            return host.ViewportHeight - 2;

        return 520;
    }
}
