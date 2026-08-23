using System.Collections.ObjectModel;
using Bridge.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    private readonly FreeGamesService? _freeGamesService;

    [ObservableProperty]
    private bool _hasUnseenFreeGames;

    [ObservableProperty]
    private int _unseenFreeGamesCount;

    [ObservableProperty]
    private bool _isNotificationsPopupOpen;

    public ObservableCollection<FreeGameNotification> FreeGames { get; } = new();

    public async Task RefreshFreeGamesAsync()
    {
        if (_freeGamesService == null) return;
        try
        {
            var games = await _freeGamesService.GetFreeGamesAsync();
            FreeGames.Clear();
            foreach (var g in games.Take(10))
                FreeGames.Add(g);

            var seen = FreeGamesSeenStore.Load();
            var unseen = FreeGames.Count(g => !seen.Contains(g.Id));
            UnseenFreeGamesCount = unseen;
            HasUnseenFreeGames = unseen > 0;
        }
        catch { }
    }

    [RelayCommand]
    private void MarkFreeGamesSeen()
    {
        var ids = FreeGames.Select(g => g.Id);
        FreeGamesSeenStore.MarkSeen(ids);
        HasUnseenFreeGames = false;
        UnseenFreeGamesCount = 0;
    }

    [RelayCommand]
    private void ToggleNotificationsPopup()
    {
        IsNotificationsPopupOpen = !IsNotificationsPopupOpen;
        if (IsNotificationsPopupOpen && HasUnseenFreeGames)
            MarkFreeGamesSeen();
    }

    [RelayCommand]
    private void OpenFreeGame(FreeGameNotification? game)
    {
        if (game == null) return;
        var url = !string.IsNullOrWhiteSpace(game.OpenGiveawayUrl) ? game.OpenGiveawayUrl : game.GamerpowerUrl;
        if (string.IsNullOrWhiteSpace(url)) return;

        // Mark this one as seen
        FreeGamesSeenStore.MarkSeen(new[] { game.Id });
        var seen = FreeGamesSeenStore.Load();
        var unseen = FreeGames.Count(g => !seen.Contains(g.Id));
        UnseenFreeGamesCount = unseen;
        HasUnseenFreeGames = unseen > 0;

        // Try launcher-specific URL first
        try
        {
            if (game.IsEpic)
            {
                // Epic launcher will handle https://store.epicgames.com URLs if installed, but we try protocol
                // Fallback to https
                SafeLauncher.TryOpenUrl(url);
                return;
            }
            if (game.IsSteam)
            {
                // Try steam://store/ for direct launcher open if we can extract appid from url? GamerPower url doesn't contain appid, so fallback to https
                SafeLauncher.TryOpenUrl(url);
                return;
            }
            SafeLauncher.TryOpenUrl(url);
        }
        catch { SafeLauncher.TryOpenUrl(url); }
    }
}
