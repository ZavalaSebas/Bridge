using System.Globalization;
using System.Windows.Data;

namespace Bridge.Converters;

/// <summary>
/// Multiplies a length (e.g. a container's ActualHeight) by a factor given as
/// the converter parameter ("0.75") — used to size an element as a fraction of
/// its container while keeping it centered, e.g. the gallery's main image that
/// floats above its frosted backdrop instead of filling it.
/// </summary>
public class FractionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double length = value is double d ? d : 0;
        double factor = parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
            ? f
            : 1.0;
        return length * factor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
