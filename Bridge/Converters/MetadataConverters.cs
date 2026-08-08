using System.Globalization;
using System.Net.Http;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Bridge.Converters;

[ValueConversion(typeof(string), typeof(BitmapImage))]
public class ImageUrlConverter : IValueConverter
{
    private static readonly HttpClient HttpClient = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            return new BitmapImage(new Uri(url));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(Bridge.Core.Entities.ReleaseDate?), typeof(string))]
public class ReleaseDateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Bridge.Core.Entities.ReleaseDate releaseDate)
            return null;

        return releaseDate.Month.HasValue
            ? $"{releaseDate.Year}-{releaseDate.Month:D2}-{releaseDate.Day:D2}"
            : releaseDate.Year.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(ulong), typeof(string))]
public class PlaytimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ulong seconds)
            return null;

        return seconds switch
        {
            0 => "Not played",
            < 60 => $"{seconds} seconds",
            < 3600 => $"{seconds / 60} minutes",
            _ => $"{seconds / 3600.0:0.#} hours"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(Enum), typeof(string))]
public class EnumDescriptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Enum e ? EnumValues.GetDisplayName(e) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(int))]
public class BoolToIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 1 : 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i != 0;
}
