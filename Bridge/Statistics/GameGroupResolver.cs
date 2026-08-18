using System.IO;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Resources;

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
        GameGroupField.Library => ResolveName(game.SourceId, _sourceNames, Strings.Manual),
        GameGroupField.Developer => FirstName(game.DeveloperIds, _companyNames),
        GameGroupField.Publisher => FirstName(game.PublisherIds, _companyNames),
        GameGroupField.Platform => FirstName(game.PlatformIds, _platformNames),
        GameGroupField.Genre => FirstName(game.GenreIds, _genreNames),
        GameGroupField.IsInstalled => game.IsInstalled ? Strings.Installed : Strings.NotInstalled,
        GameGroupField.CompletionStatus => game.CompletionStatusId == Guid.Empty
            ? Strings.None
            : ResolveName(game.CompletionStatusId, _completionStatusNames, Strings.Unknown),
        GameGroupField.PlaytimeSeconds => PlaytimeBucket(game.PlaytimeSeconds),
        GameGroupField.PlayCount => PlayCountBucket(game.PlayCount),
        GameGroupField.InstallSizeBytes => InstallSizeBucket(game.IsInstalled, game.InstallSizeBytes),
        GameGroupField.InstallDrive => DriveLetter(game.InstallDirectory),
        GameGroupField.LastPlayed => DateBucket(game.LastActivity),
        GameGroupField.RecentActivity => DateBucket(game.LastActivity),
        GameGroupField.ReleaseYear => game.ReleaseDate?.Year.ToString() ?? Strings.Unknown,
        GameGroupField.Added => DateBucket(game.Added),
        GameGroupField.Modified => DateBucket(game.Modified),
        GameGroupField.CommunityScore => ScoreBucket(game.CommunityScore),
        GameGroupField.CriticScore => ScoreBucket(game.CriticScore),
        GameGroupField.UserScore => ScoreBucket(game.UserScore),
        _ => string.Empty
    };

    private static string FirstLetter(string name) =>
        name.Length > 0 ? name[..1].ToUpperInvariant() : Strings.Unknown;

    private static string ResolveName(Guid id, IReadOnlyDictionary<Guid, string> names, string fallback) =>
        names.TryGetValue(id, out var name) && name.Length > 0 ? name : fallback;

    private static string FirstName(IEnumerable<Guid> ids, IReadOnlyDictionary<Guid, string> names)
    {
        foreach (var id in ids)
            if (names.TryGetValue(id, out var name) && name.Length > 0)
                return name;
        return Strings.Unknown;
    }

    private static string PlaytimeBucket(ulong seconds) => seconds switch
    {
        0 => Strings.PlaytimeNotPlayed,
        < 3600 => Strings.GroupLessThanOneHour,
        < 3600 * 10 => Strings.GroupOneToTenHours,
        < 3600 * 100 => Strings.GroupTenToHundredHours,
        _ => Strings.GroupHundredPlusHours
    };

    private static string PlayCountBucket(ulong count) => count switch
    {
        0 => Strings.GroupNeverPlayed,
        1 => Strings.GroupPlayedOnce,
        _ => Strings.GroupPlayedMultiple
    };

    private static string InstallSizeBucket(bool installed, ulong? bytes) => bytes switch
    {
        null when !installed => Strings.NotInstalled,
        null => Strings.GroupUnknownSize,
        < 1024UL * 1024 * 1024 => Strings.GroupLessThanOneGb,
        < 10UL * 1024 * 1024 * 1024 => Strings.GroupOneToTenGb,
        < 100UL * 1024 * 1024 * 1024 => Strings.GroupTenToHundredGb,
        _ => Strings.GroupHundredPlusGb
    };

    private static string DriveLetter(string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
            return Strings.Unknown;
        var root = Path.GetPathRoot(installDirectory);
        return string.IsNullOrEmpty(root) ? Strings.Unknown : root;
    }

    private static string DateBucket(DateTime? date)
    {
        if (!date.HasValue)
            return Strings.GroupNever;
        var age = DateTime.Now - date.Value;
        return age switch
        {
            { TotalDays: < 1 } => Strings.GroupToday,
            { TotalDays: < 7 } => Strings.GroupLastSevenDays,
            { TotalDays: < 30 } => Strings.GroupLastThirtyDays,
            { TotalDays: < 365 } => Strings.GroupLastYear,
            _ => Strings.GroupOlder
        };
    }

    private static string ScoreBucket(int? score) => score switch
    {
        null => Strings.GroupNoScore,
        < 50 => Strings.GroupScore0To49,
        < 70 => Strings.GroupScore50To69,
        < 90 => Strings.GroupScore70To89,
        _ => Strings.GroupScore90To100
    };
}
