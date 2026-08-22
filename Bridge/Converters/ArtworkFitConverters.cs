using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Bridge.Converters;

/// <summary>Fits artwork width/height into a max box preserving aspect ratio (no letterboxing).</summary>
public static class ArtworkFit
{
    public const double UnboundedHeight = 10000;

    public static (double Width, double Height) Fit(double sourceWidth, double sourceHeight, double maxWidth, double maxHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return FallbackSize(maxWidth, maxHeight);

        var scale = Math.Min(maxWidth / sourceWidth, maxHeight / sourceHeight);
        return (sourceWidth * scale, sourceHeight * scale);
    }

    public static (int Columns, bool Square) ParseTileLayout(object? parameter)
    {
        if (parameter is not string text)
            return (1, false);

        if (text.EndsWith("s", StringComparison.Ordinal) &&
            int.TryParse(text[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var squareColumns))
        {
            return (Math.Max(1, squareColumns), true);
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var columns)
            ? (Math.Max(1, columns), false)
            : (1, false);
    }

    /// <param name="columns">Target column count in the results panel.</param>
    public static double ComputeTileMaxEdge(double containerWidth, int columns)
    {
        columns = Math.Max(1, columns);
        if (containerWidth <= 40)
        {
            return columns switch
            {
                1 => 420,
                2 => 170,
                _ => 120
            };
        }

        const double listPadding = 8;
        const double tileMargin = 4;
        var available = containerWidth - listPadding;
        var gapTotal = tileMargin * Math.Max(0, columns - 1);
        return Math.Max(72, (available - gapTotal) / columns);
    }

    public static (double MaxWidth, double MaxHeight) ResolveTileLimits(object[] values, object? parameter)
    {
        var (columns, square) = ParseTileLayout(parameter);
        var containerWidth = values.Length > 2 ? ToDouble(values, 2) : 0;
        var maxW = ComputeTileMaxEdge(containerWidth, columns);
        var maxH = square ? maxW : UnboundedHeight;
        return (maxW, maxH);
    }

    public static (double Width, double Height) FitToViewport(
        double sourceWidth,
        double sourceHeight,
        double maxWidth,
        double maxHeight)
    {
        if (maxWidth <= 0 || maxHeight <= 0)
            return (320, 240);

        if (sourceWidth <= 0 || sourceHeight <= 0)
            return (maxWidth, maxHeight);

        return Fit(sourceWidth, sourceHeight, maxWidth, maxHeight);
    }

    internal static double ToDouble(object[] values, int index)
    {
        if (values.Length <= index || values[index] is null)
            return 0;

        return values[index] switch
        {
            int i => i,
            long l => l,
            double d => d,
            float f => f,
            _ => double.TryParse(values[index]!.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0
        };
    }

    private static (double Width, double Height) FallbackSize(double maxWidth, double maxHeight)
    {
        if (maxHeight >= UnboundedHeight - 1)
            return (maxWidth, maxWidth * 1.5);

        return (maxWidth, maxHeight);
    }
}

public sealed class ArtworkTileWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var (maxW, maxH) = ArtworkFit.ResolveTileLimits(values, parameter);
        var srcW = ArtworkFit.ToDouble(values, 0);
        var srcH = ArtworkFit.ToDouble(values, 1);
        return ArtworkFit.Fit(srcW, srcH, maxW, maxH).Width;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ArtworkTileHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var (maxW, maxH) = ArtworkFit.ResolveTileLimits(values, parameter);
        var srcW = ArtworkFit.ToDouble(values, 0);
        var srcH = ArtworkFit.ToDouble(values, 1);
        return ArtworkFit.Fit(srcW, srcH, maxW, maxH).Height;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
