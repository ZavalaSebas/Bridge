using Bridge.Core.Entities;
using Bridge.Statistics;

namespace Bridge.Tests.Statistics;

public class LibraryStatisticsTests
{
    [Fact]
    public void Compute_CountsEverythingCorrectly()
    {
        var games = new List<Game>
        {
            new() { Name = "A", IsInstalled = true, Favorite = true, PlaytimeSeconds = 3600 },
            new() { Name = "B", IsInstalled = false, Hidden = true, PlaytimeSeconds = 1800 },
            new() { Name = "C", IsInstalled = true, PlaytimeSeconds = 0 }
        };

        var stats = LibraryStatistics.Compute(games);

        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(2, stats.InstalledCount);
        Assert.Equal(1, stats.NotInstalledCount);
        Assert.Equal(1, stats.FavoriteCount);
        Assert.Equal(1, stats.HiddenCount);
        Assert.Equal(5400UL, stats.TotalPlaytimeSeconds);
        Assert.Equal(1800UL, stats.AveragePlaytimeSeconds);
    }

    [Fact]
    public void Compute_TopPlayed_IsOrderedDescendingByPlaytime()
    {
        var games = new List<Game>
        {
            new() { Name = "Low", PlaytimeSeconds = 100 },
            new() { Name = "High", PlaytimeSeconds = 900 },
            new() { Name = "Mid", PlaytimeSeconds = 500 }
        };

        var stats = LibraryStatistics.Compute(games);

        Assert.Equal(["High", "Mid", "Low"], stats.TopPlayed.Select(g => g.Name));
    }

    [Fact]
    public void Compute_EmptyLibrary_DoesNotThrow_AndAveragesToZero()
    {
        var stats = LibraryStatistics.Compute([]);

        Assert.Equal(0, stats.TotalCount);
        Assert.Equal(0UL, stats.AveragePlaytimeSeconds);
        Assert.Empty(stats.TopPlayed);
    }
}
