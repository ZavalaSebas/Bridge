using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace Bridge.Converters;

public sealed class DetailFilterContainsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string name)
            return false;

        if (values[1] is not IEnumerable filters)
            return false;

        foreach (var item in filters)
        {
            if (item is string filter &&
                string.Equals(filter, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
