using System.Globalization;
using System.Windows.Data;

namespace Bridge.Converters;

/// <summary>
/// Scales a base size (given as the converter parameter, e.g. "200") by the
/// covers-zoom value, so a cover card grows/shrinks with the zoom slider and the
/// wrapping grid reflows to fit more/fewer columns.
/// </summary>
public class ZoomToSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double zoom = value is double z ? z : 1.0;
        double baseSize = parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
            ? b
            : 100;
        return baseSize * zoom;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
