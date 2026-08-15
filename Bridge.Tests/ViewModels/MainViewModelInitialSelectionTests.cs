using Bridge.Core.Entities;
using Bridge.ViewModels;

namespace Bridge.Tests.ViewModels;

// MainViewModel.SelectInitialGame is a pure startup-selection helper (the
// real MainViewModel needs a full DI graph to construct, so the pure part is
// extracted and tested directly).
public class MainViewModelInitialSelectionTests
{
    [Fact]
    public void SelectInitialGame_NoLastActivity_ReturnsFirstGame()
    {
        var games = new List<Game>
        {
            new() { Name = "Alpha", LastActivity = null },
            new() { Name = "Beta", LastActivity = null },
        };

        var selected = MainViewModel.SelectInitialGame(games);

        Assert.Equal("Alpha", selected?.Name);
    }

    [Fact]
    public void SelectInitialGame_MostRecentlyPlayedWins()
    {
        var games = new List<Game>
        {
            new() { Name = "Alpha", LastActivity = new DateTime(2026, 1, 1) },
            new() { Name = "Beta", LastActivity = new DateTime(2026, 3, 15) },
            new() { Name = "Gamma", LastActivity = new DateTime(2026, 2, 10) },
        };

        var selected = MainViewModel.SelectInitialGame(games);

        Assert.Equal("Beta", selected?.Name);
    }

    [Fact]
    public void SelectInitialGame_MixedWithNeverPlayed_ReturnsMostRecent()
    {
        var games = new List<Game>
        {
            new() { Name = "Alpha", LastActivity = new DateTime(2026, 1, 1) },
            new() { Name = "Never", LastActivity = null },
            new() { Name = "Gamma", LastActivity = new DateTime(2026, 5, 5) },
        };

        var selected = MainViewModel.SelectInitialGame(games);

        Assert.Equal("Gamma", selected?.Name);
    }

    [Fact]
    public void SelectInitialGame_EmptyList_ReturnsNull()
    {
        var selected = MainViewModel.SelectInitialGame(new List<Game>());

        Assert.Null(selected);
    }
}
