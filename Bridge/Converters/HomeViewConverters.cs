using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Bridge.Core.Entities;

namespace Bridge.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString() == "invert";
        bool b = value is bool bv && bv;
        if (invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString() == "invert";
        int count = 0;
        if (value is int i) count = i;
        else if (value is ICollection<object> c) count = c.Count;
        var visible = count > 0;
        if (invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString() == "invert";
        var isNull = value is null;
        var visible = invert ? !isNull : isNull;
        // Actually for Home we want visible when not null, so we use inverse logic with trigger
        // But keep generic: null => Collapsed unless invert
        if (value is null) return invert ? Visibility.Visible : Visibility.Collapsed;
        return invert ? Visibility.Collapsed : Visibility.Visible;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CarouselIsSelectedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        if (values[0] is Game a && values[1] is Game b)
            return a.Id == b.Id;
        return false;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class FallbackCoverConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var url = value as string;
        return string.IsNullOrWhiteSpace(url) ? "pack://application:,,,/Bridge;component/Assets/FallbackCover.png" : url;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class FallbackIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var url = value as string;
        return string.IsNullOrWhiteSpace(url) ? "pack://application:,,,/Bridge;component/Assets/FallbackIcon.png" : url;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PlaytimeShortConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ulong seconds)
        {
            if (seconds == 0) return "Not played";
            var hours = seconds / 3600;
            if (hours > 0) return $"{hours}h played";
            var minutes = seconds / 60;
            return $"{minutes}m played";
        }
        return "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
