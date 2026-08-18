using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Bridge.Resources;

namespace Bridge.Converters;

/// <summary>
/// Maps a completion status name to its accent color for the hero badge:
/// Completed/Played/Beaten → green, On Hold → amber, Abandoned → red,
/// Plan to Play/Playing → blue, everything else → neutral gray. Returns null
/// for an empty string so the badge stays collapsed.
/// </summary>
public class CompletionStatusToColorConverter : IValueConverter
{
    private static readonly Brush Completed = Freeze(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly Brush OnHold = Freeze(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush Abandoned = Freeze(Color.FromRgb(0xEF, 0x44, 0x44));
    private static readonly Brush Planned = Freeze(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly Brush Neutral = Freeze(Color.FromRgb(0x9A, 0x9A, 0x9A));

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string status || string.IsNullOrWhiteSpace(status))
        {
            return null!;
        }

        return status switch
        {
            var s when s == Strings.CompletionStatusCompleted
                || s == Strings.Played
                || s == Strings.CompletionStatusBeaten => Completed,
            var s when s == Strings.CompletionStatusOnHold => OnHold,
            var s when s == Strings.CompletionStatusAbandoned => Abandoned,
            var s when s == Strings.CompletionStatusPlanToPlay
                || s == Strings.CompletionStatusPlaying => Planned,
            _ => Neutral
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
