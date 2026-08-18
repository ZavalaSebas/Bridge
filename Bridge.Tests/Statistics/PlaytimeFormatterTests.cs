using Bridge.Resources;
using Bridge.Statistics;

namespace Bridge.Tests.Statistics;

public class PlaytimeFormatterTests
{
    [Fact]
    public void FormatSeconds_NotPlayed_ReturnsLocalizedLabel() =>
        Assert.Equal(Strings.PlaytimeNotPlayed, PlaytimeFormatter.FormatSeconds(0));

    [Fact]
    public void FormatSeconds_OneSecond_UsesSingularLabel() =>
        Assert.Equal(Strings.PlaytimeOneSecond, PlaytimeFormatter.FormatSeconds(1));

    [Fact]
    public void FormatSeconds_MultipleSeconds_UsesFormat() =>
        Assert.Equal(Strings.Format(nameof(Strings.PlaytimeSecondsFormat), 45UL), PlaytimeFormatter.FormatSeconds(45));

    [Fact]
    public void FormatSeconds_Hours_UsesHourFormat() =>
        Assert.Equal(Strings.Format(nameof(Strings.PlaytimeHoursFormat), "2.5"), PlaytimeFormatter.FormatSeconds(9000));

    [Fact]
    public void FormatBytes_Kilobytes_RoundsToOneDecimal()
    {
        var formatted = PlaytimeFormatter.FormatBytes(1536);
        Assert.Equal(Strings.Format(nameof(Strings.SizeKilobytesFormat), "1.5"), formatted);
    }

    [Fact]
    public void FormatBytes_Zero_ReturnsLocalizedZero() =>
        Assert.Equal(Strings.SizeZeroBytes, PlaytimeFormatter.FormatBytes(0));
}
