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

    private static string FormatMinutes(ulong minutes) =>
        minutes == 1
            ? Strings.PlaytimeOneMinute
            : Strings.Format(nameof(Strings.PlaytimeMinutesFormat), minutes);

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
