using Bridge.Core.Entities;
using Bridge.Resources;

namespace Bridge.Statistics;

/// <summary>Computed library stats from the in-memory game list — nothing persisted.</summary>
public class LibraryStatistics
{
    private const int BoardPreviewCount = 6;

    public int TotalCount { get; init; }
    public int InstalledCount { get; init; }
    public int NotInstalledCount { get; init; }
    public int FavoriteCount { get; init; }
    public int HiddenCount { get; init; }
    public int PlayedCount { get; init; }
    public int NotPlayedCount { get; init; }
    public int BacklogCount { get; init; }
    public int InProgressCount { get; init; }
    public int CompletedCount { get; init; }
    public int AbandonedCount { get; init; }
    public int OtherCompletionCount { get; init; }
    public ulong TotalPlaytimeSeconds { get; init; }
    public ulong AveragePlaytimeSeconds { get; init; }
    public ulong TotalInstallSizeBytes { get; init; }
    public IReadOnlyList<Game> TopPlayed { get; init; } = [];
    public IReadOnlyList<BoardColumn> CompletionBoardColumns { get; init; } = [];
    public IReadOnlyList<TimelineEntry> TimelineEntries { get; init; } = [];

    public double InstalledPercent => Percent(InstalledCount);
    public double NotInstalledPercent => Percent(NotInstalledCount);
    public double HiddenPercent => Percent(HiddenCount);
    public double FavoritePercent => Percent(FavoriteCount);
    public double PlayedPercent => Percent(PlayedCount);
    public double NotPlayedPercent => Percent(NotPlayedCount);

    public string TotalPlaytimeDisplay => PlaytimeFormatter.FormatSeconds(TotalPlaytimeSeconds);
    public string AveragePlaytimeDisplay => PlaytimeFormatter.FormatSeconds(AveragePlaytimeSeconds);
    public string TotalInstallSizeDisplay => PlaytimeFormatter.FormatBytes(TotalInstallSizeBytes);

    public static LibraryStatistics Compute(
        IEnumerable<Game> games,
        IReadOnlyDictionary<Guid, string>? completionStatusNames = null,
        int topCount = 5,
        int historyCount = 12)
    {
        var list = games.ToList();
        var totalPlaytime = list.Aggregate(0UL, (acc, g) => acc + g.PlaytimeSeconds);
        var totalInstallSize = list.Aggregate(0UL, (acc, g) => acc + (g.InstallSizeBytes ?? 0));
        var statuses = completionStatusNames ?? new Dictionary<Guid, string>();

        var boardColumns = BuildBoardColumns(list, statuses);
        var timelineEntries = BuildTimeline(list, statuses, historyCount);

        return new LibraryStatistics
        {
            TotalCount = list.Count,
            InstalledCount = list.Count(g => g.IsInstalled),
            NotInstalledCount = list.Count(g => !g.IsInstalled),
            FavoriteCount = list.Count(g => g.Favorite),
            HiddenCount = list.Count(g => g.Hidden),
            PlayedCount = list.Count(g => g.PlaytimeSeconds > 0),
            NotPlayedCount = list.Count(g => g.PlaytimeSeconds == 0),
            BacklogCount = boardColumns.FirstOrDefault(c => c.Key == BoardColumnKeys.Backlog)?.Count ?? 0,
            InProgressCount = boardColumns.FirstOrDefault(c => c.Key == BoardColumnKeys.InProgress)?.Count ?? 0,
            CompletedCount = boardColumns.FirstOrDefault(c => c.Key == BoardColumnKeys.Completed)?.Count ?? 0,
            AbandonedCount = boardColumns.FirstOrDefault(c => c.Key == BoardColumnKeys.Abandoned)?.Count ?? 0,
            OtherCompletionCount = boardColumns.FirstOrDefault(c => c.Key == BoardColumnKeys.Other)?.Count ?? 0,
            TotalPlaytimeSeconds = totalPlaytime,
            AveragePlaytimeSeconds = list.Count > 0 ? totalPlaytime / (ulong)list.Count : 0,
            TotalInstallSizeBytes = totalInstallSize,
            TopPlayed = list.OrderByDescending(g => g.PlaytimeSeconds).Take(topCount).ToList(),
            CompletionBoardColumns = boardColumns,
            TimelineEntries = timelineEntries
        };
    }

    private static IReadOnlyList<BoardColumn> BuildBoardColumns(
        IReadOnlyList<Game> games,
        IReadOnlyDictionary<Guid, string> completionStatusNames)
    {
        var columns = new[]
        {
            new BoardColumnDefinition(BoardColumnKeys.Backlog, Strings.BacklogBoard, Strings.BacklogBoardHint, CompletionBucket.Backlog),
            new BoardColumnDefinition(BoardColumnKeys.InProgress, Strings.InProgressBoard, Strings.InProgressBoardHint, CompletionBucket.InProgress),
            new BoardColumnDefinition(BoardColumnKeys.Completed, Strings.CompletedBoard, Strings.CompletedBoardHint, CompletionBucket.Completed),
            new BoardColumnDefinition(BoardColumnKeys.Abandoned, Strings.AbandonedBoard, Strings.AbandonedBoardHint, CompletionBucket.Abandoned),
            new BoardColumnDefinition(BoardColumnKeys.Other, Strings.OtherStatusBoard, Strings.OtherStatusBoardHint, CompletionBucket.Other)
        };

        return columns.Select(definition =>
        {
            var matchingGames = games
                .Where(game => ClassifyCompletion(GetCompletionStatusName(game, completionStatusNames)) == definition.Bucket)
                .OrderByDescending(GetBoardSortTimestamp)
                .ThenBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .Take(BoardPreviewCount)
                .ToList();

            var total = games.Count(game => ClassifyCompletion(GetCompletionStatusName(game, completionStatusNames)) == definition.Bucket);
            return new BoardColumn
            {
                Key = definition.Key,
                Title = definition.Title,
                Subtitle = definition.Subtitle,
                Count = total,
                Percent = Percent(games.Count, total),
                Games = matchingGames
            };
        }).ToList();
    }

    private static IReadOnlyList<TimelineEntry> BuildTimeline(
        IReadOnlyList<Game> games,
        IReadOnlyDictionary<Guid, string> completionStatusNames,
        int historyCount)
    {
        var entries = new List<TimelineEntry>();

        foreach (var game in games)
        {
            foreach (var session in game.PlaySessions ?? Enumerable.Empty<GamePlaySession>())
            {
                entries.Add(new TimelineEntry
                {
                    Kind = TimelineEntryKind.Session,
                    Game = game,
                    Timestamp = session.EndedAt,
                    BadgeText = Strings.Played,
                    Title = game.Name,
                    Detail = Strings.Format(
                        nameof(Strings.StatisticsSessionTimelineFormat),
                        session.StartedAt.ToString("g"),
                        PlaytimeFormatter.FormatSecondsCompact(session.DurationSeconds)),
                    TimestampText = session.EndedAt.ToString("g")
                });
            }

            var statusName = GetCompletionStatusName(game, completionStatusNames);
            if (ClassifyCompletion(statusName) == CompletionBucket.Completed &&
                (game.CompletedAt ?? game.LastActivity ?? game.Modified) is { } completedAt)
            {
                entries.Add(new TimelineEntry
                {
                    Kind = TimelineEntryKind.Completed,
                    Game = game,
                    Timestamp = completedAt,
                    BadgeText = Strings.CompletionStatusCompleted,
                    Title = game.Name,
                    Detail = Strings.Format(
                        nameof(Strings.StatisticsCompletedTimelineFormat),
                        completedAt.ToString("g")),
                    TimestampText = completedAt.ToString("g")
                });
            }
        }

        return entries
            .OrderByDescending(entry => entry.Timestamp)
            .ThenBy(entry => entry.Game.Name, StringComparer.OrdinalIgnoreCase)
            .Take(historyCount)
            .ToList();
    }

    private static DateTime GetBoardSortTimestamp(Game game) =>
        game.CompletedAt ?? game.LastActivity ?? game.Added ?? game.Modified ?? DateTime.MinValue;

    private static string GetCompletionStatusName(Game game, IReadOnlyDictionary<Guid, string> completionStatusNames)
    {
        if (game.CompletionStatusId == Guid.Empty)
            return string.Empty;

        return completionStatusNames.TryGetValue(game.CompletionStatusId, out var name)
            ? name
            : string.Empty;
    }

    private static CompletionBucket ClassifyCompletion(string statusName)
    {
        var normalized = Normalize(statusName);
        if (normalized.Length == 0 ||
            normalized == Normalize(Strings.CompletionStatusNotPlayed) ||
            normalized == Normalize(Strings.CompletionStatusPlanToPlay))
        {
            return CompletionBucket.Backlog;
        }

        if (normalized == Normalize(Strings.CompletionStatusPlaying) ||
            normalized == Normalize(Strings.CompletionStatusOnHold))
        {
            return CompletionBucket.InProgress;
        }

        if (normalized == Normalize(Strings.CompletionStatusCompleted) ||
            normalized == Normalize(Strings.CompletionStatusBeaten) ||
            normalized == Normalize(Strings.Played))
        {
            return CompletionBucket.Completed;
        }

        if (normalized == Normalize(Strings.CompletionStatusAbandoned))
            return CompletionBucket.Abandoned;

        return CompletionBucket.Other;
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant();

    private double Percent(int count) =>
        TotalCount > 0 ? count * 100.0 / TotalCount : 0;

    private static double Percent(int total, int count) =>
        total > 0 ? count * 100.0 / total : 0;

    private sealed record BoardColumnDefinition(string Key, string Title, string Subtitle, CompletionBucket Bucket);

    private static class BoardColumnKeys
    {
        public const string Backlog = "backlog";
        public const string InProgress = "in-progress";
        public const string Completed = "completed";
        public const string Abandoned = "abandoned";
        public const string Other = "other";
    }

    private enum CompletionBucket
    {
        Backlog,
        InProgress,
        Completed,
        Abandoned,
        Other
    }

    public sealed class BoardColumn
    {
        public string Key { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public int Count { get; init; }
        public double Percent { get; init; }
        public IReadOnlyList<Game> Games { get; init; } = [];
        public string CountText => Strings.Format(nameof(Strings.CompletionCountFormat), Count, Percent);
    }

    public enum TimelineEntryKind
    {
        Session,
        Completed
    }

    public sealed class TimelineEntry
    {
        public TimelineEntryKind Kind { get; init; }
        public Game Game { get; init; } = new();
        public DateTime Timestamp { get; init; }
        public string BadgeText { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string TimestampText { get; init; } = string.Empty;
        public bool IsCompletion => Kind == TimelineEntryKind.Completed;
    }
}
