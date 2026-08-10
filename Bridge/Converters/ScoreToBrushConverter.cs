using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Bridge.Converters;

/// <summary>
/// Colors a 0-100 score like Playnite's rating brushes: green (good, >=75),
/// yellow (mixed, >=50) or red (negative). Returns null for an empty score so
/// the text keeps its inherited color.
/// </summary>
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
