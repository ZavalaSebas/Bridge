using System.Globalization;
using System.Windows.Data;

namespace Bridge.Converters;

public sealed class DetailFilterActiveConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string name)
            return false;

        var filter = values[1] as string;
        return !string.IsNullOrWhiteSpace(filter) &&
               string.Equals(filter, name, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
