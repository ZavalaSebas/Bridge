using System.Collections;
using Bridge.Core.Entities;
using Bridge.Core.Enums;

namespace Bridge.Statistics;

/// <summary>Sorts games for ListCollectionView.CustomSort. Empty values always sort last.</summary>
public class GameSortComparer : IComparer<Game>, IComparer
{
    private readonly GameSortField _field;
    private readonly bool _descending;
    private readonly IReadOnlyDictionary<Guid, string> _companyNames;
    private readonly IReadOnlyDictionary<Guid, string> _platformNames;
    private readonly IReadOnlyDictionary<Guid, string> _genreNames;
    private readonly IReadOnlyDictionary<Guid, string> _sourceNames;

    public GameSortComparer(
        GameSortField field,
        bool descending,
        IReadOnlyDictionary<Guid, string>? companyNames = null,
        IReadOnlyDictionary<Guid, string>? platformNames = null,
        IReadOnlyDictionary<Guid, string>? genreNames = null,
        IReadOnlyDictionary<Guid, string>? sourceNames = null)
    {
        _field = field;
        _descending = descending;
        _companyNames = companyNames ?? new Dictionary<Guid, string>();
        _platformNames = platformNames ?? new Dictionary<Guid, string>();
        _genreNames = genreNames ?? new Dictionary<Guid, string>();
        _sourceNames = sourceNames ?? new Dictionary<Guid, string>();
    }

    int IComparer.Compare(object? x, object? y) => Compare(x as Game, y as Game);

    public int Compare(Game? x, Game? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return 1;
        if (y is null) return -1;

        // Result computed in "ascending with empty last" form; Desc() reverses
        // only when both sides have values.
        return _field switch
        {
            GameSortField.PlaytimeSeconds => Desc(x.PlaytimeSeconds.CompareTo(y.PlaytimeSeconds)),
            GameSortField.PlayCount => Desc(x.PlayCount.CompareTo(y.PlayCount)),
            GameSortField.LastPlayed => CompareNullable(x.LastActivity, y.LastActivity),
            GameSortField.RecentActivity => CompareNullable(x.LastActivity, y.LastActivity),
            GameSortField.Favorite => Desc(x.Favorite.CompareTo(y.Favorite)),
            GameSortField.Hidden => Desc(x.Hidden.CompareTo(y.Hidden)),
            GameSortField.InstallSizeBytes => CompareNullable(x.InstallSizeBytes, y.InstallSizeBytes),
            GameSortField.InstallDirectory => CompareString(x.InstallDirectory, y.InstallDirectory),
            GameSortField.IsInstalled => Desc(x.IsInstalled.CompareTo(y.IsInstalled)),
            GameSortField.ReleaseDate => CompareReleaseDate(x.ReleaseDate, y.ReleaseDate),
            GameSortField.Added => CompareNullable(x.Added, y.Added),
            GameSortField.Modified => CompareNullable(x.Modified, y.Modified),
            GameSortField.Version => CompareString(x.Version, y.Version),
            GameSortField.CommunityScore => CompareNullable(x.CommunityScore, y.CommunityScore),
            GameSortField.CriticScore => CompareNullable(x.CriticScore, y.CriticScore),
            GameSortField.UserScore => CompareNullable(x.UserScore, y.UserScore),
            GameSortField.Developer => CompareNames(x.DeveloperIds, y.DeveloperIds, _companyNames),
            GameSortField.Publisher => CompareNames(x.PublisherIds, y.PublisherIds, _companyNames),
            GameSortField.Platform => CompareNames(x.PlatformIds, y.PlatformIds, _platformNames),
            GameSortField.Genre => CompareNames(x.GenreIds, y.GenreIds, _genreNames),
            GameSortField.Source => CompareNames([x.SourceId], [y.SourceId], _sourceNames),
            _ => CompareString(x.Name, y.Name)
        };
    }

    private int Desc(int ascendingResult) => _descending ? -ascendingResult : ascendingResult;

    private int CompareNullable<T>(T? x, T? y) where T : struct, IComparable<T>
    {
        if (!x.HasValue && !y.HasValue) return 0;
        if (!x.HasValue) return 1;
        if (!y.HasValue) return -1;
        return Desc(x.Value.CompareTo(y.Value));
    }

    private int CompareString(string x, string y)
    {
        if (x.Length == 0 && y.Length == 0) return 0;
        if (x.Length == 0) return 1;
        if (y.Length == 0) return -1;
        return Desc(string.Compare(x, y, StringComparison.OrdinalIgnoreCase));
    }

    private int CompareReleaseDate(ReleaseDate? x, ReleaseDate? y)
    {
        if (!x.HasValue && !y.HasValue) return 0;
        if (!x.HasValue) return 1;
        if (!y.HasValue) return -1;

        int byYear = x.Value.Year.CompareTo(y.Value.Year);
        if (byYear != 0) return Desc(byYear);
        int xm = x.Value.Month ?? int.MaxValue, ym = y.Value.Month ?? int.MaxValue;
        int byMonth = xm.CompareTo(ym);
        if (byMonth != 0) return Desc(byMonth);
        int xd = x.Value.Day ?? int.MaxValue, yd = y.Value.Day ?? int.MaxValue;
        return Desc(xd.CompareTo(yd));
    }

    // First non-empty name of the referenced entities, else "" — keeps it
    // First matching name wins when a game has several reference ids.
    private int CompareNames(
        IEnumerable<Guid> xIds,
        IEnumerable<Guid> yIds,
        IReadOnlyDictionary<Guid, string> names)
    {
        string xName = FirstName(xIds, names);
        string yName = FirstName(yIds, names);
        return CompareString(xName, yName);
    }

    private static string FirstName(IEnumerable<Guid> ids, IReadOnlyDictionary<Guid, string> names)
    {
        foreach (var id in ids)
            if (names.TryGetValue(id, out var name) && name.Length > 0)
                return name;
        return string.Empty;
    }
}
