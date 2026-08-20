using System.Globalization;
using Bridge.Resources;

namespace Bridge.Statistics;

/// <summary>Shared playtime phrasing for converters, statistics, and overlays.</summary>
public static class PlaytimeFormatter
{
    public static string FormatSeconds(ulong seconds) => seconds switch
    {
        0 => Strings.PlaytimeNotPlayed,
        < 60 => seconds == 1
            ? Strings.PlaytimeOneSecond
            : Strings.Format(nameof(Strings.PlaytimeSecondsFormat), seconds),
        < 3600 => FormatMinutes(seconds / 60),
        _ => Strings.Format(nameof(Strings.PlaytimeHoursFormat), FormatDecimal(seconds / 3600.0))
    };

    /// <summary>Compact duration for progress labels — never uses the "Not played" phrase.</summary>
    public static string FormatSecondsCompact(ulong seconds) => seconds switch
    {
        0 => Strings.TimeToBeatZeroDuration,
        < 60 => seconds == 1
            ? Strings.PlaytimeOneSecond
            : Strings.Format(nameof(Strings.PlaytimeSecondsFormat), seconds),
        < 3600 => FormatMinutes(seconds / 60),
        _ => Strings.Format(nameof(Strings.PlaytimeHoursFormat), FormatDecimal(seconds / 3600.0))
    };

    private static string FormatMinutes(ulong minutes) =>
        minutes == 1
            ? Strings.PlaytimeOneMinute
            : Strings.Format(nameof(Strings.PlaytimeMinutesFormat), minutes);

    /// <summary>HLTB-style label, e.g. 21h 14m.</summary>
    public static string FormatHoursMinutes(ulong seconds)
    {
        if (seconds == 0)
            return string.Empty;

        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;

        if (hours > 0 && minutes > 0)
            return $"{hours}h {minutes}m";

        if (hours > 0)
            return $"{hours}h";

        return $"{minutes}m";
    }

    public static string FormatBytes(ulong bytes) => bytes switch
    {
        0 => Strings.SizeZeroBytes,
        < 1024 => Strings.Format(nameof(Strings.SizeBytesFormat), bytes),
        < 1024 * 1024 => Strings.Format(nameof(Strings.SizeKilobytesFormat), FormatDecimal(bytes / 1024.0)),
        < 1024UL * 1024 * 1024 => Strings.Format(nameof(Strings.SizeMegabytesFormat), FormatDecimal(bytes / 1048576.0)),
        _ => Strings.Format(nameof(Strings.SizeGigabytesFormat), FormatDecimal(bytes / 1073741824.0))
    };

    private static string FormatDecimal(double value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);
}
