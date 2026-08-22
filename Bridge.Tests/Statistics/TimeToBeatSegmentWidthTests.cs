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

    [Fact]
    public void ComputeProgressWidth_FillsFirstSegmentProportionally()
    {
        const double trackWidth = 260;
        var segmentWidths = TimeToBeatHelper.ComputeSegmentWidths(Hours(10), Hours(15), Hours(25), trackWidth);

        var progress = TimeToBeatHelper.ComputeProgressWidth(
            Hours(5),
            Hours(10),
            Hours(15),
            Hours(25),
            trackWidth);

        Assert.Equal(segmentWidths[0] * 0.5, progress, 1);
    }

    [Fact]
    public void ComputeProgressWidth_SpansCompletedSegments()
    {
        const double trackWidth = 260;
        var segmentWidths = TimeToBeatHelper.ComputeSegmentWidths(Hours(10), Hours(15), Hours(25), trackWidth);

        var progress = TimeToBeatHelper.ComputeProgressWidth(
            Hours(12),
            Hours(10),
            Hours(15),
            Hours(25),
            trackWidth);

        Assert.Equal(segmentWidths[0] + segmentWidths[1] * (2.0 / 15.0), progress, 1);
    }

    [Fact]
    public void ComputeProgressWidth_CapsAtTrackWidthWhenPlaytimeExceedsTotal()
    {
        const double trackWidth = 260;

        var progress = TimeToBeatHelper.ComputeProgressWidth(
            Hours(100),
            Hours(10),
            Hours(15),
            Hours(25),
            trackWidth);

        Assert.Equal(trackWidth, progress, 1);
    }
}
