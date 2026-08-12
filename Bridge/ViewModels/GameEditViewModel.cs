using System.Collections.ObjectModel;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bridge.ViewModels;

/// <summary>
/// Edit form for a single game (Playnite's GameEditWindow but trimmed: Bridge
/// edits one game at a time, so there are no per-field "save this change"
/// checkboxes — just an editable copy applied on Save). Reference fields
/// (genres/devs/publishers/platforms) are multi-select checkbox lists; images
/// accept a local path or URL.
/// </summary>
public partial class GameEditViewModel : ObservableObject
{
    private readonly Game _game;
    private readonly IGameRepository _gameRepository;
    private readonly IRepository<Genre> _genreRepository;
    private readonly IRepository<Company> _companyRepository;
    private readonly IRepository<Platform> _platformRepository;

    /// <summary>True when the window opened for a brand-new manual game (so Save
    /// inserts instead of updating and the caller adds it to the library).</summary>
    public bool IsNewGame { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _sortingName;

    [ObservableProperty]
    private string _releaseDateText;

    [ObservableProperty]
    private string _criticScoreText;

    [ObservableProperty]
    private string _communityScoreText;

    [ObservableProperty]
    private bool _favorite;

    [ObservableProperty]
    private bool _hidden;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private string _installDirectory;

    [ObservableProperty]
    private string _icon;

    [ObservableProperty]
    private string _coverImage;

    [ObservableProperty]
    private string _backgroundImage;

    public ObservableCollection<SelectableItem> Genres { get; }
    public ObservableCollection<SelectableItem> Developers { get; }
    public ObservableCollection<SelectableItem> Publishers { get; }
    public ObservableCollection<SelectableItem> Platforms { get; }

    public GameEditViewModel(
        Game game,
        IGameRepository gameRepository,
        IRepository<Genre> genreRepository,
        IRepository<Company> companyRepository,
        IRepository<Platform> platformRepository,
        bool isNew = false)
    {
        _game = game;
        _gameRepository = gameRepository;
        _genreRepository = genreRepository;
        _companyRepository = companyRepository;
        _platformRepository = platformRepository;
        IsNewGame = isNew;

        Name = game.Name;
        SortingName = game.SortingName;
        ReleaseDateText = FormatReleaseDate(game.ReleaseDate);
        CriticScoreText = game.CriticScore?.ToString() ?? string.Empty;
        CommunityScoreText = game.CommunityScore?.ToString() ?? string.Empty;
        Favorite = game.Favorite;
        Hidden = game.Hidden;
        Description = game.Description;
        InstallDirectory = game.InstallDirectory;
        Icon = game.Icon;
        CoverImage = game.CoverImage;
        BackgroundImage = game.BackgroundImage;

        Genres = ToSelectable(genreRepository.GetAll(), game.GenreIds);
        Developers = ToSelectable(companyRepository.GetAll(), game.DeveloperIds);
        Publishers = ToSelectable(companyRepository.GetAll(), game.PublisherIds);
        Platforms = ToSelectable(platformRepository.GetAll(), game.PlatformIds);
    }

    private static ObservableCollection<SelectableItem> ToSelectable(IEnumerable<DatabaseObject> all, IReadOnlyCollection<Guid> selected)
        => new(all.Select(x => new SelectableItem(x.Id, x.Name, selected.Contains(x.Id))).OrderBy(x => x.Name));

    // Create-on-the-fly: persist a new reference entity (genre/company/platform)
    // and add it to its checkbox list, selected. Returns false when the name is
    // empty — the caller keeps focus on the input.
    public bool CreateNewGenre(string name)
    {
        var entity = _genreRepository.GetOrCreateByName(name);
        if (Genres.Any(x => x.Id == entity.Id))
            return true;
        Genres.Add(new SelectableItem(entity.Id, entity.Name, isSelected: true));
        return true;
    }

    public bool CreateNewDeveloper(string name) => AddCompany(name, Developers);

    public bool CreateNewPublisher(string name) => AddCompany(name, Publishers);

    private bool AddCompany(string name, ObservableCollection<SelectableItem> list)
    {
        var company = _companyRepository.GetOrCreateByName(name);
        if (list.Any(x => x.Id == company.Id))
            return true;
        list.Add(new SelectableItem(company.Id, company.Name, isSelected: true));
        return true;
    }

    public bool CreateNewPlatform(string name)
    {
        var entity = _platformRepository.GetOrCreateByName(name);
        if (Platforms.Any(x => x.Id == entity.Id))
            return true;
        Platforms.Add(new SelectableItem(entity.Id, entity.Name, isSelected: true));
        return true;
    }

    public bool Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return false;
        }

        _game.Name = Name.Trim();
        _game.SortingName = SortingName.Trim();
        _game.ReleaseDate = ParseReleaseDate(ReleaseDateText);
        _game.CriticScore = ParseNullableInt(CriticScoreText);
        _game.CommunityScore = ParseNullableInt(CommunityScoreText);
        _game.Favorite = Favorite;
        _game.Hidden = Hidden;
        _game.Description = Description;
        _game.InstallDirectory = InstallDirectory;
        _game.Icon = Icon;
        _game.CoverImage = CoverImage;
        _game.BackgroundImage = BackgroundImage;
        _game.GenreIds = Genres.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        _game.DeveloperIds = Developers.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        _game.PublisherIds = Publishers.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        _game.PlatformIds = Platforms.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        _game.Modified = DateTime.Now;

        if (IsNewGame)
        {
            _game.Added = DateTime.Now;
            _gameRepository.Add(_game);
        }
        else
        {
            _gameRepository.Update(_game);
        }

        return true;
    }

    private static string FormatReleaseDate(ReleaseDate? date)
    {
        if (date is not { } d)
        {
            return string.Empty;
        }

        return d.Day is { } day
            ? $"{d.Year:0000}-{d.Month:00}-{day:00}"
            : d.Month is { } month
                ? $"{d.Year:0000}-{month:00}"
                : $"{d.Year:0000}";
    }

    private static ReleaseDate? ParseReleaseDate(string text)
    {
        var parts = text.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], out var year))
        {
            return null;
        }

        int? month = null;
        int? day = null;
        if (parts.Length > 1 && int.TryParse(parts[1], out var m)) month = m;
        if (parts.Length > 2 && int.TryParse(parts[2], out var dd)) day = dd;
        return new ReleaseDate(year, month, day);
    }

    private static int? ParseNullableInt(string? text)
        => int.TryParse(text?.Trim(), out var value) ? value : null;
}

/// <summary>Checkbox entry for a reference entity (genre/developer/etc.).</summary>
public class SelectableItem
{
    public SelectableItem(Guid id, string name, bool isSelected = false)
    {
        Id = id;
        Name = name;
        IsSelected = isSelected;
    }

    public Guid Id { get; }
    public string Name { get; }
    public bool IsSelected { get; set; }
}
