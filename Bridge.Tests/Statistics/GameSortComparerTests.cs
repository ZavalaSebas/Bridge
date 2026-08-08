using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Statistics;

namespace Bridge.Tests.Statistics;

public class GameSortComparerTests
{
    private static List<Game> Sort(List<Game> games, GameSortField field, bool descending = false)
    {
        var comparer = new GameSortComparer(field, descending);
        var sorted = games.ToList();
        sorted.Sort(comparer);
        return sorted;
    }

    [Fact]
    public void Compare_ByName_Ascending_CaseInsensitive()
    {
        var games = new List<Game>
        {
            new() { Name = "zeta" },
            new() { Name = "Alpha" },
            new() { Name = "beta" }
        };

        var sorted = Sort(games, GameSortField.Name);

        Assert.Equal(["Alpha", "beta", "zeta"], sorted.Select(g => g.Name));
    }

    [Fact]
    public void Compare_ByName_Descending_Reverses()
    {
        var games = new List<Game>
        {
            new() { Name = "alpha" },
            new() { Name = "beta" }
        };

        var sorted = Sort(games, GameSortField.Name, descending: true);

        Assert.Equal(["beta", "alpha"], sorted.Select(g => g.Name));
    }

    [Fact]
    public void Compare_ByPlaytime_Descending()
    {
        var games = new List<Game>
        {
            new() { Name = "Low", PlaytimeSeconds = 100 },
            new() { Name = "High", PlaytimeSeconds = 900 },
            new() { Name = "Mid", PlaytimeSeconds = 500 }
        };

        var sorted = Sort(games, GameSortField.PlaytimeSeconds, descending: true);

        Assert.Equal(["High", "Mid", "Low"], sorted.Select(g => g.Name));
    }

    [Fact]
    public void Compare_ByLastActivity_NullsGoLast()
    {
        var games = new List<Game>
        {
            new() { Name = "Played", LastActivity = new DateTime(2025, 1, 1) },
            new() { Name = "Never", LastActivity = null }
        };

        var sorted = Sort(games, GameSortField.RecentActivity, descending: true);

        Assert.Equal(["Played", "Never"], sorted.Select(g => g.Name));
    }

    [Fact]
    public void Compare_ByLastActivity_Ascending_NullsStillGoLast()
    {
        var games = new List<Game>
        {
            new() { Name = "Old", LastActivity = new DateTime(2020, 1, 1) },
            new() { Name = "Never", LastActivity = null }
        };

        var sorted = Sort(games, GameSortField.RecentActivity);

        Assert.Equal(["Old", "Never"], sorted.Select(g => g.Name));
    }

    [Fact]
    public void Compare_ByFavorite_FavoritesFirst()
    {
        var games = new List<Game>
        {
            new() { Name = "No", Favorite = false },
            new() { Name = "Yes", Favorite = true }
        };

        var sorted = Sort(games, GameSortField.Favorite, descending: true);

        Assert.Equal(["Yes", "No"], sorted.Select(g => g.Name));
    }

    [Fact]
    public void Compare_ByDeveloper_ResolvesNamesFromLookup()
    {
        var bethesda = Guid.NewGuid();
        var cdpr = Guid.NewGuid();
        var comparer = new GameSortComparer(
            GameSortField.Developer,
            descending: false,
            companyNames: new Dictionary<Guid, string>
            {
                [bethesda] = "Bethesda",
                [cdpr] = "CD Projekt Red"
            });

        var games = new List<Game>
        {
            new() { Name = "B", DeveloperIds = [bethesda] },
            new() { Name = "C", DeveloperIds = [cdpr] }
        };
        games.Sort(comparer);

        Assert.Equal(["B", "C"], games.Select(g => g.Name));
    }

    [Fact]
    public void Compare_BySource_UsesSourceName()
    {
        var steam = Guid.NewGuid();
        var epic = Guid.NewGuid();
        var comparer = new GameSortComparer(
            GameSortField.Source,
            descending: false,
            sourceNames: new Dictionary<Guid, string>
            {
                [steam] = "Steam",
                [epic] = "Epic"
            });

        var games = new List<Game>
        {
            new() { Name = "SteamGame", SourceId = steam },
            new() { Name = "EpicGame", SourceId = epic }
        };
        games.Sort(comparer);

        Assert.Equal(["EpicGame", "SteamGame"], games.Select(g => g.Name));
    }
}
