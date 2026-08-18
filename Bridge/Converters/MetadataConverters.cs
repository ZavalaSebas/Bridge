using System.Globalization;
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
/// Collapses the bound element when the value is null/empty/whitespace —
/// matches Playnite's details view, which hides rows that have no data.
/// Non-string values (scores, dates) are only hidden when null.
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
