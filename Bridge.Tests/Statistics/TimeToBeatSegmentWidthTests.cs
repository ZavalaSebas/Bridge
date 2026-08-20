using Bridge.Statistics;

namespace Bridge.Tests.Statistics;

public class TimeToBeatSegmentWidthTests
{
    private static ulong Hours(double hours) => (ulong)(hours * 3600);

    [Fact]
    public void ComputeSegmentWidths_SumsToTrackWidth()
    {
        var widths = TimeToBeatHelper.ComputeSegmentWidths(Hours(21), Hours(37), Hours(61), 260);

        Assert.Equal(260, widths.Sum(), 1);
    }

    [Fact]
    public void ComputeSegmentWidths_EnforcesMinimumForFirstSegment()
    {
        var widths = TimeToBeatHelper.ComputeSegmentWidths(Hours(5), Hours(20), Hours(100), 260);

        Assert.True(widths[0] >= 54);
        Assert.True(widths[2] <= 260 * 0.46 + 1);
        Assert.Equal(260, widths.Sum(), 1);
    }

    [Fact]
    public void ComputeSegmentWidths_BlendsTowardUniformity()
    {
        var pureMain = 260.0 * (5.0 / 125.0);
        var widths = TimeToBeatHelper.ComputeSegmentWidths(Hours(5), Hours(20), Hours(100), 260);

        Assert.True(widths[0] > pureMain);
        Assert.True(widths[2] < 260 * 0.8);
    }
}
