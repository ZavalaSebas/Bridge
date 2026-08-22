using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Resources;
using Bridge.Statistics;

namespace Bridge.Converters;

[ValueConversion(typeof(Bridge.Core.Entities.ReleaseDate?), typeof(string))]
public class ReleaseDateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Bridge.Core.Entities.ReleaseDate releaseDate)
            return null;

        return releaseDate switch
        {
            { Month: null } => releaseDate.Year.ToString(),
            { Day: null } => $"{releaseDate.Year}-{releaseDate.Month:D2}",
            _ => $"{releaseDate.Year}-{releaseDate.Month:D2}-{releaseDate.Day:D2}"
        };
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

        return PlaytimeFormatter.FormatSeconds(seconds);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Feeds ListCollectionView.GroupDescriptions: turns a Game into its group key
/// via a GameGroupResolver (configured by the ViewModel with name lookups).
/// </summary>
public class GameGroupConverter : IValueConverter
{
    public required GameGroupResolver Resolver { get; init; }
    public required GameGroupField Field { get; init; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Game game ? Resolver.GetGroupKey(game, Field) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(DateTime?), typeof(string))]
public class ShortDateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime date ? date.ToString("d") : Strings.GroupNever;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Collapses the bound element when an int count is zero (used to hide the Links row when a game has no links).</summary>
public class NonZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Hides detail rows when the bound value is empty. Non-string values hide only when null.
/// </summary>
public class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return System.Windows.Visibility.Collapsed;

        if (value is string text)
            return string.IsNullOrWhiteSpace(text) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

        return System.Windows.Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows content when the bound string/value is empty; hides otherwise.</summary>
public class InverseEmptyToVisibilityConverter : IValueConverter
{
    private static readonly EmptyToVisibilityConverter Empty = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Empty.Convert(value, targetType, parameter, culture) is Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(string), typeof(Visibility))]
public class HeroBlackToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Bridge.Core.Entities.HeroBackground.IsBlack(value as string)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(string), typeof(Visibility))]
public class HeroDefaultToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Bridge.Core.Entities.HeroBackground.IsDefault(value as string)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(string), typeof(string))]
public class HeroArtSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Bridge.Core.Entities.HeroBackground.IsCustom(value as string) ? value : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(string), typeof(Visibility))]
public class HeroNonDefaultToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Bridge.Core.Entities.HeroBackground.IsDefault(value as string)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
