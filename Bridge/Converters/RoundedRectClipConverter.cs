using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Bridge.Converters;

/// <summary>
/// Produces a RectangleGeometry with rounded corners for the given width and
/// height (two values fed via a MultiBinding, e.g. the card's ActualWidth and
/// ActualHeight). Border.ClipToBounds clips to a rectangle and ignores
/// CornerRadius, so rounded corners on an image need an explicit Clip instead.
/// </summary>
public class RoundedRectClipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double width || values[1] is not double height
            || width <= 0 || height <= 0)
        {
            return DependencyProperty.UnsetValue;
        }

        var radius = parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
            ? r
            : 8.0;
        var geometry = new RectangleGeometry(
            new Rect(0, 0, width, height),
            radius, radius);
        geometry.Freeze();
        return geometry;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
