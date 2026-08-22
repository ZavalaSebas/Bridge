using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Bridge.Converters;

/// Score color: green >= 75, yellow >= 50, red below. Null score keeps inherited text color.
public class ScoreToBrushConverter : IValueConverter
{
    private static readonly Brush Good = Freeze(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly Brush Mixed = Freeze(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush Negative = Freeze(Color.FromRgb(0xEF, 0x44, 0x44));

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int? score = value switch
        {
            int i => i,
            _ => null
        };

        if (score is not { } s)
        {
            return null!;
        }

        return s >= 75 ? Good : s >= 50 ? Mixed : Negative;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// Maps a 0–100 score to a bar width (converter parameter = max pixels, default 52).
public class ScoreToBarWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int score || score <= 0)
            return 0.0;

        var max = 52.0;
        if (parameter is string text && double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            max = parsed;
        else if (parameter is double d)
            max = d;

        return Math.Clamp(score / 100.0 * max, 0, max);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// Shows the hero scores capsule when critic or community score is present.
public class GameHasScoresVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Bridge.Core.Entities.Game game)
            return Visibility.Collapsed;

        return game.CriticScore.HasValue || game.CommunityScore.HasValue
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
