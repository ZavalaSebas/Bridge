using System.Globalization;
using System.Windows.Data;
using Bridge.Resources;

namespace Bridge.Converters;

/// <summary>
/// Returns <see cref="Strings.NotInstalled"/> (or a custom fallback) when the value is null or empty.
/// </summary>
public sealed class NullFallbackConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
        {
            return parameter as string ?? Strings.NotInstalled;
        }

        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
