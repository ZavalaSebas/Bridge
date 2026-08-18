using Bridge.Core.Entities;

namespace Bridge.Statistics;

/// <summary>Computed library stats from the in-memory game list — nothing persisted.</summary>
public class LibraryStatistics
{
    public int TotalCount { get; init; }
    public int InstalledCount { get; init; }
    public int NotInstalledCount { get; init; }
    public int FavoriteCount { get; init; }
    public int HiddenCount { get; init; }
    public int PlayedCount { get; init; }
    public int NotPlayedCount { get; init; }
    public ulong TotalPlaytimeSeconds { get; init; }
    public ulong AveragePlaytimeSeconds { get; init; }
    public ulong TotalInstallSizeBytes { get; init; }
    public IReadOnlyList<Game> TopPlayed { get; init; } = [];

    public double InstalledPercent => Percent(InstalledCount);
    public double NotInstalledPercent => Percent(NotInstalledCount);
    public double HiddenPercent => Percent(HiddenCount);
    public double FavoritePercent => Percent(FavoriteCount);
    public double PlayedPercent => Percent(PlayedCount);
    public double NotPlayedPercent => Percent(NotPlayedCount);

    public string TotalPlaytimeDisplay => PlaytimeFormatter.FormatSeconds(TotalPlaytimeSeconds);
    public string AveragePlaytimeDisplay => PlaytimeFormatter.FormatSeconds(AveragePlaytimeSeconds);
    public string TotalInstallSizeDisplay => PlaytimeFormatter.FormatBytes(TotalInstallSizeBytes);

    public static LibraryStatistics Compute(IEnumerable<Game> games, int topCount = 5)
    {
        var list = games.ToList();
        var totalPlaytime = list.Aggregate(0UL, (acc, g) => acc + g.PlaytimeSeconds);
        var totalInstallSize = list.Aggregate(0UL, (acc, g) => acc + (g.InstallSizeBytes ?? 0));

        return new LibraryStatistics
        {
            TotalCount = list.Count,
            InstalledCount = list.Count(g => g.IsInstalled),
            NotInstalledCount = list.Count(g => !g.IsInstalled),
            FavoriteCount = list.Count(g => g.Favorite),
            HiddenCount = list.Count(g => g.Hidden),
            PlayedCount = list.Count(g => g.PlaytimeSeconds > 0),
            NotPlayedCount = list.Count(g => g.PlaytimeSeconds == 0),
            TotalPlaytimeSeconds = totalPlaytime,
            AveragePlaytimeSeconds = list.Count > 0 ? totalPlaytime / (ulong)list.Count : 0,
            TotalInstallSizeBytes = totalInstallSize,
            TopPlayed = list.OrderByDescending(g => g.PlaytimeSeconds).Take(topCount).ToList()
        };
    }

    private double Percent(int count) =>
        TotalCount > 0 ? count * 100.0 / TotalCount : 0;

}
