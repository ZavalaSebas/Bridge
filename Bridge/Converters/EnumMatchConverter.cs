using System.Globalization;
using System.Windows.Data;

namespace Bridge.Converters;

/// <summary>
/// True when the bound value's string form equals the converter parameter
/// (case-insensitive). Used to tick the active entry in the filter/sort/group
/// menus: bind IsChecked to the enum property and pass the menu Tag as the
/// parameter, e.g. FilterPreset + "Favorite".
/// </summary>
public class EnumMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return false;

        return string.Equals(
            value.ToString(),
            parameter.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
