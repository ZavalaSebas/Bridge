using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Bridge.Services;

namespace Bridge.Converters;

public sealed class UserProfileAvatarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not UserProfile profile)
            return Binding.DoNothing;

        var size = 128;
        if (parameter is string sizeText && int.TryParse(sizeText, out var parsed) && parsed > 0)
            size = parsed;

        return UserProfileAvatarHelper.GetAvatarImage(profile, size);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
