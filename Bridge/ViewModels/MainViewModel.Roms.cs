using Bridge.Core.Entities;
using Bridge.Resources;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// ROM games grouped into per-platform shelves for the Nintendo Switch-style
    /// Roms view. Pure projection over <see cref="MainViewModel.Games"/> — the
    /// same platform-name resolution the library's "group by platform" uses
    /// (<c>PlatformIds</c> via the shared name lookup), so a ROM lands on the
    /// same shelf it would group under elsewhere.
    /// </summary>
    public IReadOnlyList<RomPlatformShelf> RomShelves => BuildRomShelves();

    public bool RomsHasGames => Games.Any(g => !g.Hidden && g.Roms.Count > 0);

    public void RefreshRoms()
    {
        OnPropertyChanged(nameof(RomShelves));
        OnPropertyChanged(nameof(RomsHasGames));
    }

    private IReadOnlyList<RomPlatformShelf> BuildRomShelves()
    {
        var romGames = Games.Where(g => !g.Hidden && g.Roms.Count > 0).ToList();
        if (romGames.Count == 0)
            return Array.Empty<RomPlatformShelf>();

        return romGames
            .GroupBy(ResolveShelfPlatformName)
            .Select(group => new RomPlatformShelf(
                group.Key,
                group
                    .OrderBy(SortKey, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()))
            .OrderBy(shelf => shelf.PlatformName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private string ResolveShelfPlatformName(Game game)
    {
        var names = _platformNames;
        if (names is not null)
        {
            foreach (var id in game.PlatformIds)
            {
                if (names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name))
                    return name;
            }
        }

        return Strings.Unknown;
    }

    private static string SortKey(Game game) =>
        string.IsNullOrWhiteSpace(game.SortingName) ? game.Name : game.SortingName;
}

/// <summary>One platform's row of ROM games in the Roms view.</summary>
public sealed record RomPlatformShelf(string PlatformName, IReadOnlyList<Game> Games);
