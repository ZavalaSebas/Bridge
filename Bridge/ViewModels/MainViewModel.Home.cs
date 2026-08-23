using System.Collections.ObjectModel;
using Bridge.Core.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    // Home carousel index
    [ObservableProperty]
    private int _homeCarouselIndex;

    public Game? HomeFeaturedGame => HomeFeaturedGames.Count == 0 ? null : HomeFeaturedGames[HomeCarouselIndex % HomeFeaturedGames.Count];

    public IReadOnlyList<Game> HomeFeaturedGames => BuildHomeFeatured(7);

    public IReadOnlyList<Game> HomeWhatToPlayNext => BuildWhatToPlayNext(12);

    public IReadOnlyList<Game> HomeContinuePlaying => BuildContinuePlaying(12);

    public IReadOnlyList<Game> HomeRecentlyPlayed => BuildRecentlyPlayed(12);

    public IReadOnlyList<Game> HomeRecentlyAdded => BuildRecentlyAdded(12);

    public IReadOnlyList<Game> HomeFavorites => BuildFavorites(12);

    // Right panel
    public IReadOnlyList<Game> HomeRightContinue => BuildContinuePlaying(5);

    public IReadOnlyList<Game> HomeRightRecentlyPlayed => BuildRecentlyPlayed(8);

    public IReadOnlyList<RecentAchievementRow> HomeRecentAchievements => BuildRecentAchievements(8);

    public bool HomeHasGames => Games.Count > 0;

    // Quick view popup (overlay in Home, not a separate Window) for never-played games
    [ObservableProperty]
    private Game? _quickViewGame;

    private IReadOnlyList<Game> _quickViewSource = Array.Empty<Game>();
    private int _quickViewIndex = -1;

    public void ShowQuickView(Game game, IReadOnlyList<Game> source)
    {
        _quickViewSource = source;
        _quickViewIndex = source.ToList().IndexOf(game);
        if (_quickViewIndex < 0) _quickViewIndex = 0;
        QuickViewGame = game;
    }

    public void CloseQuickView() => QuickViewGame = null;

    [RelayCommand]
    private void QuickViewNext()
    {
        if (_quickViewSource.Count == 0 || QuickViewGame is null) return;
        _quickViewIndex = (_quickViewIndex + 1) % _quickViewSource.Count;
        QuickViewGame = _quickViewSource[_quickViewIndex];
    }

    [RelayCommand]
    private void QuickViewPrev()
    {
        if (_quickViewSource.Count == 0 || QuickViewGame is null) return;
        _quickViewIndex = (_quickViewIndex - 1 + _quickViewSource.Count) % _quickViewSource.Count;
        QuickViewGame = _quickViewSource[_quickViewIndex];
    }

    [RelayCommand]
    private void QuickViewPlay()
    {
        if (QuickViewGame is null) return;
        var game = QuickViewGame;
        SelectedGame = game;
        NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
        CloseQuickView();
        if (game.IsRunning)
            StopGameCommand.Execute(game);
        else
            PlayGameCommand.Execute(game);
    }

    [RelayCommand]
    private void QuickViewOpenDetails()
    {
        if (QuickViewGame is null) return;
        SelectedGame = QuickViewGame;
        NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
        CloseQuickView();
    }

    public void RefreshHome()
    {
        OnPropertyChanged(nameof(HomeFeaturedGames));
        OnPropertyChanged(nameof(HomeFeaturedGame));
        OnPropertyChanged(nameof(HomeWhatToPlayNext));
        OnPropertyChanged(nameof(HomeContinuePlaying));
        OnPropertyChanged(nameof(HomeRecentlyPlayed));
        OnPropertyChanged(nameof(HomeRecentlyAdded));
        OnPropertyChanged(nameof(HomeFavorites));
        OnPropertyChanged(nameof(HomeRightContinue));
        OnPropertyChanged(nameof(HomeRightRecentlyPlayed));
        OnPropertyChanged(nameof(HomeRecentAchievements));
        OnPropertyChanged(nameof(HomeHasGames));
        if (HomeCarouselIndex >= HomeFeaturedGames.Count)
            HomeCarouselIndex = 0;
    }

    partial void OnHomeCarouselIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HomeFeaturedGame));
    }

    private IReadOnlyList<Game> BuildHomeFeatured(int count)
    {
        if (Games.Count == 0) return Array.Empty<Game>();
        // Prefer games with artwork, favorites first, then recently played, then recently added
        var withArt = Games.Where(g => !g.Hidden && !string.IsNullOrWhiteSpace(g.BackgroundImage) || !string.IsNullOrWhiteSpace(g.CoverImage)).ToList();
        var source = withArt.Count >= count ? withArt : Games.Where(g => !g.Hidden).ToList();
        return source
            .OrderByDescending(g => g.Favorite)
            .ThenByDescending(g => g.LastActivity ?? DateTime.MinValue)
            .ThenByDescending(g => g.Added ?? DateTime.MinValue)
            .Take(count)
            .ToList();
    }

    private IReadOnlyList<Game> BuildWhatToPlayNext(int count)
    {
        if (Games.Count == 0) return Array.Empty<Game>();
        var candidates = Games.Where(g => !g.Hidden && g.PlaytimeSeconds == 0).ToList();
        if (candidates.Count == 0)
            candidates = Games.Where(g => !g.Hidden).ToList();
        // Prefer installed, then high community/critic score, then recently added
        return candidates
            .OrderByDescending(g => g.IsInstalled)
            .ThenByDescending(g => (g.CriticScore ?? 0) + (g.CommunityScore ?? 0))
            .ThenByDescending(g => g.Added ?? DateTime.MinValue)
            .Take(count)
            .ToList();
    }

    private IReadOnlyList<Game> BuildContinuePlaying(int count)
    {
        if (Games.Count == 0) return Array.Empty<Game>();
        return Games.Where(g => !g.Hidden && g.LastActivity.HasValue)
            .OrderByDescending(g => g.LastActivity)
            .Take(count)
            .ToList();
    }

    private IReadOnlyList<Game> BuildRecentlyPlayed(int count)
    {
        if (Games.Count == 0) return Array.Empty<Game>();
        return Games.Where(g => !g.Hidden && g.PlaytimeSeconds > 0 && g.LastActivity.HasValue)
            .OrderByDescending(g => g.LastActivity)
            .Take(count)
            .ToList();
    }

    private IReadOnlyList<Game> BuildRecentlyAdded(int count)
    {
        if (Games.Count == 0) return Array.Empty<Game>();
        return Games.Where(g => !g.Hidden)
            .OrderByDescending(g => g.Added ?? DateTime.MinValue)
            .Take(count)
            .ToList();
    }

    private IReadOnlyList<Game> BuildFavorites(int count)
    {
        if (Games.Count == 0) return Array.Empty<Game>();
        return Games.Where(g => g.Favorite && !g.Hidden)
            .OrderByDescending(g => g.LastActivity ?? g.Added ?? DateTime.MinValue)
            .Take(count)
            .ToList();
    }

    private IReadOnlyList<RecentAchievementRow> BuildRecentAchievements(int count)
    {
        var rows = new List<RecentAchievementRow>();
        if (Games.Count == 0) return rows;
        // Collect cached achievements
        foreach (var game in Games.Where(g => !g.Hidden).OrderByDescending(g => g.LastActivity ?? DateTime.MinValue).Take(20))
        {
            if (!_gameAchievementsService.TryGetCached(game, out var snapshot) || snapshot is null) continue;
            foreach (var ach in snapshot.Achievements.Where(a => a.IsUnlocked && a.UnlockedAt.HasValue))
            {
                rows.Add(new RecentAchievementRow(game, ach));
            }
        }
        return rows.OrderByDescending(r => r.Achievement.UnlockedAt).Take(count).ToList();
    }

    [RelayCommand]
    private void HomeNextCarousel()
    {
        if (HomeFeaturedGames.Count == 0) return;
        HomeCarouselIndex = (HomeCarouselIndex + 1) % HomeFeaturedGames.Count;
    }

    [RelayCommand]
    private void HomePrevCarousel()
    {
        if (HomeFeaturedGames.Count == 0) return;
        HomeCarouselIndex = (HomeCarouselIndex - 1 + HomeFeaturedGames.Count) % HomeFeaturedGames.Count;
    }

    [RelayCommand]
    private void HomeSelectFeatured()
    {
        if (HomeFeaturedGame is not null)
        {
            SelectedGame = HomeFeaturedGame;
            NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
        }
    }

    [RelayCommand]
    private void HomePlayFeatured()
    {
        if (HomeFeaturedGame is null) return;
        var game = HomeFeaturedGame;
        SelectedGame = game;
        NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
        if (game.IsRunning)
            StopGameCommand.Execute(game);
        else
            PlayGameCommand.Execute(game);
    }
}

public sealed record RecentAchievementRow(Game Game, GameAchievement Achievement);
