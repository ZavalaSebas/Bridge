using System.IO;
using Bridge.Core.Entities;
using Bridge.Core.Enums;

namespace Bridge.Statistics;

/// <summary>
/// Pure group-key resolver for the library list. Given a Game and a group
/// field, returns the string key that defines which group the game belongs to
/// ("" or null means "no group"). Reference entities (Developer/Publisher/
/// Platform/Genre/Library) resolve through nameByGuid dictionaries so the
/// resolver stays testable without a database. Value-based fields use buckets
/// (playtime/install size/scores) or the raw date's year — kept deliberately
/// coarse for the MVP.
/// </summary>
public class GameGroupResolver
{
    private readonly IReadOnlyDictionary<Guid, string> _companyNames;
    private readonly IReadOnlyDictionary<Guid, string> _platformNames;
    private readonly IReadOnlyDictionary<Guid, string> _genreNames;
    private readonly IReadOnlyDictionary<Guid, string> _sourceNames;
    private readonly IReadOnlyDictionary<Guid, string> _completionStatusNames;

    public GameGroupResolver(
        IReadOnlyDictionary<Guid, string>? companyNames = null,
        IReadOnlyDictionary<Guid, string>? platformNames = null,
        IReadOnlyDictionary<Guid, string>? genreNames = null,
        IReadOnlyDictionary<Guid, string>? sourceNames = null,
        IReadOnlyDictionary<Guid, string>? completionStatusNames = null)
    {
        _companyNames = companyNames ?? new Dictionary<Guid, string>();
        _platformNames = platformNames ?? new Dictionary<Guid, string>();
        _genreNames = genreNames ?? new Dictionary<Guid, string>();
        _sourceNames = sourceNames ?? new Dictionary<Guid, string>();
        _completionStatusNames = completionStatusNames ?? new Dictionary<Guid, string>();
    }

    public string GetGroupKey(Game game, GameGroupField field) => field switch
    {
        GameGroupField.Name => FirstLetter(game.Name),
        GameGroupField.Library => ResolveName(game.SourceId, _sourceNames, "Manual"),
        GameGroupField.Developer => FirstName(game.DeveloperIds, _companyNames),
        GameGroupField.Publisher => FirstName(game.PublisherIds, _companyNames),
        GameGroupField.Platform => FirstName(game.PlatformIds, _platformNames),
        GameGroupField.Genre => FirstName(game.GenreIds, _genreNames),
        GameGroupField.IsInstalled => game.IsInstalled ? "Installed" : "Not installed",
        GameGroupField.CompletionStatus => game.CompletionStatusId == Guid.Empty
            ? "None"
            : ResolveName(game.CompletionStatusId, _completionStatusNames, "Unknown"),
        GameGroupField.PlaytimeSeconds => PlaytimeBucket(game.PlaytimeSeconds),
        GameGroupField.PlayCount => PlayCountBucket(game.PlayCount),
        GameGroupField.InstallSizeBytes => InstallSizeBucket(game.IsInstalled, game.InstallSizeBytes),
        GameGroupField.InstallDrive => DriveLetter(game.InstallDirectory),
        GameGroupField.LastPlayed => DateBucket(game.LastActivity),
        GameGroupField.RecentActivity => DateBucket(game.LastActivity),
        GameGroupField.ReleaseYear => game.ReleaseDate?.Year.ToString() ?? "Unknown",
        GameGroupField.Added => DateBucket(game.Added),
        GameGroupField.Modified => DateBucket(game.Modified),
        GameGroupField.CommunityScore => ScoreBucket(game.CommunityScore),
        GameGroupField.CriticScore => ScoreBucket(game.CriticScore),
        GameGroupField.UserScore => ScoreBucket(game.UserScore),
        _ => string.Empty
    };

    private static string FirstLetter(string name) =>
        name.Length > 0 ? name[..1].ToUpperInvariant() : "Unknown";

    private static string ResolveName(Guid id, IReadOnlyDictionary<Guid, string> names, string fallback) =>
        names.TryGetValue(id, out var name) && name.Length > 0 ? name : fallback;

    private static string FirstName(IEnumerable<Guid> ids, IReadOnlyDictionary<Guid, string> names)
    {
        foreach (var id in ids)
            if (names.TryGetValue(id, out var name) && name.Length > 0)
                return name;
        return "Unknown";
    }

    private static string PlaytimeBucket(ulong seconds) => seconds switch
    {
        0 => "Not played",
        < 3600 => "Less than 1 hour",
        < 3600 * 10 => "1 - 10 hours",
        < 3600 * 100 => "10 - 100 hours",
        _ => "100+ hours"
    };

    private static string PlayCountBucket(ulong count) => count switch
    {
        0 => "Never played",
        1 => "Once",
        _ => "Multiple times"
    };

    private static string InstallSizeBucket(bool installed, ulong? bytes) => bytes switch
    {
        null when !installed => "Not installed",
        null => "Unknown size",
        < 1024UL * 1024 * 1024 => "Less than 1 GB",
        < 10UL * 1024 * 1024 * 1024 => "1 - 10 GB",
        < 100UL * 1024 * 1024 * 1024 => "10 - 100 GB",
        _ => "100+ GB"
    };

    private static string DriveLetter(string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
            return "Unknown";
        var root = Path.GetPathRoot(installDirectory);
        return string.IsNullOrEmpty(root) ? "Unknown" : root;
    }

    // Coarse buckets for MVP grouping: Never / last 7 days / last 30 days /
    // last year / older. Kept simple on purpose — revisit when real usage data
    // exists.
    private static string DateBucket(DateTime? date)
    {
        if (!date.HasValue)
            return "Never";
        var age = DateTime.Now - date.Value;
        return age switch
        {
            { TotalDays: < 1 } => "Today",
            { TotalDays: < 7 } => "Last 7 days",
            { TotalDays: < 30 } => "Last 30 days",
            { TotalDays: < 365 } => "Last year",
            _ => "Older"
        };
    }

    private static string ScoreBucket(int? score) => score switch
    {
        null => "No score",
        < 50 => "0 - 49",
        < 70 => "50 - 69",
        < 90 => "70 - 89",
        _ => "90 - 100"
    };
}
