using Bridge.Core.Entities;

namespace Bridge.Statistics;

public static class TimeToBeatHelper
{
    private const double UniformBlendWeight = 0.34;
    private const double MaxSegmentFraction = 0.44;
    private const double MinSegmentPixels = 58;

    public static ulong GetProgressTarget(Game game) => GetProgressScale(game);

    public static ulong GetProgressScale(Game game) =>
        game.TimeToBeatCompleteSeconds
        ?? game.TimeToBeatExtraSeconds
        ?? game.TimeToBeatMainSeconds
        ?? 0;

    public static double GetSegmentWidth(int index, ulong mainSeconds, ulong extraSeconds, ulong completeSeconds, double trackWidth)
    {
        var widths = ComputeSegmentWidths(mainSeconds, extraSeconds, completeSeconds, trackWidth);
        return index is >= 0 and <= 2 ? widths[index] : 0;
    }

    public static double[] ComputeSegmentWidths(ulong mainSeconds, ulong extraSeconds, ulong completeSeconds, double trackWidth)
    {
        var seconds = new[] { mainSeconds, extraSeconds, completeSeconds };
        var widths = new double[3];
        if (trackWidth <= 0)
            return widths;

        var visibleCount = seconds.Count(static s => s > 0);
        if (visibleCount == 0)
            return widths;

        var totalSeconds = seconds.Aggregate(0UL, static (sum, value) => sum + value);
        if (totalSeconds == 0)
            return widths;

        var equalShare = 1.0 / visibleCount;
        var weights = new double[3];
        for (var i = 0; i < 3; i++)
        {
            if (seconds[i] == 0)
                continue;

            var proportional = seconds[i] / (double)totalSeconds;
            weights[i] = UniformBlendWeight * equalShare + (1 - UniformBlendWeight) * proportional;
        }

        NormalizeWeights(weights);

        var minWidth = Math.Min(MinSegmentPixels, trackWidth / visibleCount * 0.9);
        var maxWidth = Math.Max(minWidth + 1, trackWidth * MaxSegmentFraction);

        for (var i = 0; i < 3; i++)
            widths[i] = seconds[i] > 0 ? weights[i] * trackWidth : 0;

        EnforceMinimumWidth(seconds, widths, trackWidth, minWidth);
        EnforceMaximumWidth(seconds, widths, trackWidth, maxWidth);
        NormalizeWidthsToTrack(seconds, widths, trackWidth);

        return widths;
    }

    private static void NormalizeWeights(double[] weights)
    {
        var sum = weights.Sum();
        if (sum <= 0)
            return;

        for (var i = 0; i < weights.Length; i++)
            weights[i] /= sum;
    }

    private static void EnforceMinimumWidth(ulong[] seconds, double[] widths, double trackWidth, double minWidth)
    {
        for (var pass = 0; pass < 6; pass++)
        {
            var deficit = 0.0;
            for (var i = 0; i < 3; i++)
            {
                if (seconds[i] == 0)
                    continue;

                if (widths[i] < minWidth)
                {
                    deficit += minWidth - widths[i];
                    widths[i] = minWidth;
                }
            }

            if (deficit <= 0.5)
                break;

            var shrinkable = 0.0;
            for (var i = 0; i < 3; i++)
            {
                if (seconds[i] > 0 && widths[i] > minWidth)
                    shrinkable += widths[i] - minWidth;
            }

            if (shrinkable <= 0)
                break;

            for (var i = 0; i < 3; i++)
            {
                if (seconds[i] > 0 && widths[i] > minWidth)
                    widths[i] -= deficit * (widths[i] - minWidth) / shrinkable;
            }
        }
    }

    private static void EnforceMaximumWidth(ulong[] seconds, double[] widths, double trackWidth, double maxWidth)
    {
        for (var pass = 0; pass < 6; pass++)
        {
            var excess = 0.0;
            for (var i = 0; i < 3; i++)
            {
                if (seconds[i] == 0)
                    continue;

                if (widths[i] > maxWidth)
                {
                    excess += widths[i] - maxWidth;
                    widths[i] = maxWidth;
                }
            }

            if (excess <= 0.5)
                break;

            var expandable = 0.0;
            for (var i = 0; i < 3; i++)
            {
                if (seconds[i] > 0 && widths[i] < maxWidth)
                    expandable += maxWidth - widths[i];
            }

            if (expandable <= 0)
                break;

            for (var i = 0; i < 3; i++)
            {
                if (seconds[i] > 0 && widths[i] < maxWidth)
                    widths[i] += excess * (maxWidth - widths[i]) / expandable;
            }
        }
    }

    private static void NormalizeWidthsToTrack(ulong[] seconds, double[] widths, double trackWidth)
    {
        var sum = 0.0;
        for (var i = 0; i < 3; i++)
        {
            if (seconds[i] == 0)
                widths[i] = 0;
            else
                sum += widths[i];
        }

        if (sum <= 0 || Math.Abs(sum - trackWidth) <= 0.5)
            return;

        for (var i = 0; i < 3; i++)
        {
            if (seconds[i] > 0)
                widths[i] *= trackWidth / sum;
        }
    }
}
