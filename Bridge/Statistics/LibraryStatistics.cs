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

    public string TotalPlaytimeDisplay => FormatDuration(TotalPlaytimeSeconds);
    public string AveragePlaytimeDisplay => FormatDuration(AveragePlaytimeSeconds);
    public string TotalInstallSizeDisplay => FormatBytes(TotalInstallSizeBytes);

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

    private static string FormatDuration(ulong seconds) => seconds switch
    {
        0 => "Not played",
        < 60 => $"{seconds} seconds",
        < 3600 => $"{seconds / 60} minutes",
        _ => $"{seconds / 3600.0:0.#} hours"
    };

    private static string FormatBytes(ulong bytes) => bytes switch
    {
        0 => "0 B",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024UL * 1024 * 1024 => $"{bytes / 1048576.0:0.#} MB",
        _ => $"{bytes / 1073741824.0:0.#} GB"
    };
}
