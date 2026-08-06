using Bridge.Core.Entities;

namespace Bridge.Statistics;

/// <summary>
/// No persisted "GameStatistics" entity exists — same as Playnite's real
/// StatisticsViewModel (PROJECT_FOUNDATION.md §28.5, §28.6 finding 4): stats
/// are computed on the fly from the current games list, never stored.
/// </summary>
public class LibraryStatistics
{
    public int TotalCount { get; init; }
    public int InstalledCount { get; init; }
    public int NotInstalledCount { get; init; }
    public int FavoriteCount { get; init; }
    public int HiddenCount { get; init; }
    public ulong TotalPlaytimeSeconds { get; init; }
    public ulong AveragePlaytimeSeconds { get; init; }
    public IReadOnlyList<Game> TopPlayed { get; init; } = [];

    public static LibraryStatistics Compute(IEnumerable<Game> games, int topCount = 5)
    {
        var list = games.ToList();
        var totalPlaytime = list.Aggregate(0UL, (acc, g) => acc + g.PlaytimeSeconds);

        return new LibraryStatistics
        {
            TotalCount = list.Count,
            InstalledCount = list.Count(g => g.IsInstalled),
            NotInstalledCount = list.Count(g => !g.IsInstalled),
            FavoriteCount = list.Count(g => g.Favorite),
            HiddenCount = list.Count(g => g.Hidden),
            TotalPlaytimeSeconds = totalPlaytime,
            AveragePlaytimeSeconds = list.Count > 0 ? totalPlaytime / (ulong)list.Count : 0,
            TopPlayed = list.OrderByDescending(g => g.PlaytimeSeconds).Take(topCount).ToList()
        };
    }
}
