using Bridge.Core.Entities;
using Bridge.Resources;
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

    [Fact]
    public void Compute_BuildsCompletionBoardAndTimeline()
    {
        var completedStatusId = Guid.NewGuid();
        var inProgressStatusId = Guid.NewGuid();

        var games = new List<Game>
        {
            new()
            {
                Name = "Backlog",
                Added = new DateTime(2026, 1, 5)
            },
            new()
            {
                Name = "Completed",
                CompletionStatusId = completedStatusId,
                CompletedAt = new DateTime(2026, 8, 21, 19, 30, 0),
                PlaySessions =
                [
                    new GamePlaySession
                    {
                        StartedAt = new DateTime(2026, 8, 21, 18, 0, 0),
                        EndedAt = new DateTime(2026, 8, 21, 19, 30, 0),
                        DurationSeconds = 5400
                    }
                ]
            },
            new()
            {
                Name = "Active",
                CompletionStatusId = inProgressStatusId,
                PlaySessions =
                [
                    new GamePlaySession
                    {
                        StartedAt = new DateTime(2026, 8, 22, 20, 0, 0),
                        EndedAt = new DateTime(2026, 8, 22, 21, 15, 0),
                        DurationSeconds = 4500
                    }
                ]
            }
        };

        var statuses = new Dictionary<Guid, string>
        {
            [completedStatusId] = Strings.CompletionStatusCompleted,
            [inProgressStatusId] = Strings.CompletionStatusPlaying
        };

        var stats = LibraryStatistics.Compute(games, statuses, topCount: 5, historyCount: 10);

        Assert.Equal(1, stats.BacklogCount);
        Assert.Equal(1, stats.InProgressCount);
        Assert.Equal(1, stats.CompletedCount);
        Assert.Contains(stats.CompletionBoardColumns, column => column.Key == "completed" && column.Count == 1);
        Assert.Contains(stats.TimelineEntries, entry => entry.Kind == LibraryStatistics.TimelineEntryKind.Completed && entry.Game.Name == "Completed");
        Assert.Contains(stats.TimelineEntries, entry => entry.Kind == LibraryStatistics.TimelineEntryKind.Session && entry.Game.Name == "Active");
    }
}
