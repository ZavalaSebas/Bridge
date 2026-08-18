using System.Globalization;
using System.Windows.Data;
using Bridge.Resources;

namespace Bridge.Converters;

/// <summary>Formats an integer game count using <see cref="Strings.GamesCountFormat"/>.</summary>
public class GamesCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count
            ? Strings.Format(nameof(Strings.GamesCountFormat), count)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
