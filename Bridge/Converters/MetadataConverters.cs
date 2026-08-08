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
