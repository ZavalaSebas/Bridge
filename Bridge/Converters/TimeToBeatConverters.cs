using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Bridge.Core.Entities;
using Bridge.Resources;
using Bridge.Statistics;

namespace Bridge.Converters;

public class TimeToBeatTargetConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Game game ? TimeToBeatHelper.GetProgressTarget(game) : 0UL;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TimeToBeatVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Game game && TimeToBeatHelper.GetProgressTarget(game) > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TimeToBeatProgressWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3)
            return 0.0;

        var playtime = TimeToBeatConverterHelpers.ReadUlong(values[0]);
        var scale = TimeToBeatConverterHelpers.ReadUlong(values[1]);
        var trackWidth = values[2] is double width ? width : 0;

        if (scale == 0 || trackWidth <= 0)
            return 0.0;

        var ratio = Math.Min(1.0, playtime / (double)scale);
        return trackWidth * ratio;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TimeToBeatUnfilledWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3)
            return 0.0;

        var playtime = TimeToBeatConverterHelpers.ReadUlong(values[0]);
        var scale = TimeToBeatConverterHelpers.ReadUlong(values[1]);
        var trackWidth = values[2] is double width ? width : 0;

        if (scale == 0 || trackWidth <= 0)
            return trackWidth;

        var filled = trackWidth * Math.Min(1.0, playtime / (double)scale);
        return Math.Max(0, trackWidth - filled);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TimeToBeatSegmentWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 4 || parameter is not string indexText || !int.TryParse(indexText, out var index))
            return 0.0;

        var main = TimeToBeatConverterHelpers.ReadUlong(values[0]);
        var extra = TimeToBeatConverterHelpers.ReadUlong(values[1]);
        var complete = TimeToBeatConverterHelpers.ReadUlong(values[2]);
        var trackWidth = values[3] is double width ? width : 0;
        if (trackWidth <= 0)
            return 0.0;

        return TimeToBeatHelper.GetSegmentWidth(index, main, extra, complete, trackWidth);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TimeToBeatHoursMinutesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ulong seconds && seconds > 0
            ? PlaytimeFormatter.FormatHoursMinutes(seconds)
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TimeToBeatCompactSummaryConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Game game)
            return string.Empty;

        var parts = new List<string>(3);
        if (game.TimeToBeatMainSeconds is > 0)
            parts.Add(PlaytimeFormatter.FormatHoursMinutes(game.TimeToBeatMainSeconds.Value));

        if (game.TimeToBeatExtraSeconds is > 0)
            parts.Add(PlaytimeFormatter.FormatHoursMinutes(game.TimeToBeatExtraSeconds.Value));

        if (game.TimeToBeatCompleteSeconds is > 0)
            parts.Add(PlaytimeFormatter.FormatHoursMinutes(game.TimeToBeatCompleteSeconds.Value));

        return string.Join(" · ", parts);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TimeToBeatToolTipConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Game game)
            return string.Empty;

        var lines = new List<string>(4);
        if (game.TimeToBeatMainSeconds is > 0)
        {
            lines.Add($"{Strings.TimeToBeatMainStory}: {PlaytimeFormatter.FormatHoursMinutes(game.TimeToBeatMainSeconds.Value)}");
        }

        if (game.TimeToBeatExtraSeconds is > 0)
        {
            lines.Add($"{Strings.TimeToBeatMainExtra}: {PlaytimeFormatter.FormatHoursMinutes(game.TimeToBeatExtraSeconds.Value)}");
        }

        if (game.TimeToBeatCompleteSeconds is > 0)
        {
            lines.Add($"{Strings.TimeToBeatCompletionist}: {PlaytimeFormatter.FormatHoursMinutes(game.TimeToBeatCompleteSeconds.Value)}");
        }

        if (lines.Count == 0)
            return string.Empty;

        var target = TimeToBeatHelper.GetProgressTarget(game);
        if (target > 0)
        {
            lines.Add(string.Empty);
            lines.Add(Strings.Format(
                nameof(Strings.TimeToBeatProgressFormat),
                PlaytimeFormatter.FormatSecondsCompact(game.PlaytimeSeconds),
                PlaytimeFormatter.FormatSecondsCompact(target)));
        }

        return string.Join(Environment.NewLine, lines);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal static class TimeToBeatConverterHelpers
{
    internal static ulong ReadUlong(object? value) => value switch
    {
        ulong u => u,
        long l when l > 0 => (ulong)l,
        int i when i > 0 => (ulong)i,
        _ => 0UL
    };
}

public class TimeToBeatProgressTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return string.Empty;

        var playtime = values[0] is ulong playedSeconds ? playedSeconds : 0UL;
        var target = values[1] is ulong estimateSeconds ? estimateSeconds : 0UL;
        if (target == 0)
            return string.Empty;

        return Strings.Format(
            nameof(Strings.TimeToBeatProgressFormat),
            PlaytimeFormatter.FormatSecondsCompact(playtime),
            PlaytimeFormatter.FormatSecondsCompact(target));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class PositiveULongToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return Visibility.Collapsed;

        try
        {
            var seconds = System.Convert.ToUInt64(value, culture);
            return seconds > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (FormatException)
        {
            return Visibility.Collapsed;
        }
        catch (OverflowException)
        {
            return Visibility.Collapsed;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
