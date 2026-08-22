using System.Windows;
using System.Windows.Controls;
using Bridge.Core.Entities;
using Bridge.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge.Controls;

public partial class AchievementHeroSummary : UserControl
{
    public static readonly DependencyProperty SummaryTextProperty =
        DependencyProperty.Register(nameof(SummaryText), typeof(string), typeof(AchievementHeroSummary), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SummaryToolTipProperty =
        DependencyProperty.Register(nameof(SummaryToolTip), typeof(string), typeof(AchievementHeroSummary), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HasSummaryProperty =
        DependencyProperty.Register(nameof(HasSummary), typeof(bool), typeof(AchievementHeroSummary), new PropertyMetadata(false));

    private CancellationTokenSource? _loadCts;
    private GameAchievementsService? _service;
    private Guid _loadedGameId;

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        private set => SetValue(SummaryTextProperty, value);
    }

    public string SummaryToolTip
    {
        get => (string)GetValue(SummaryToolTipProperty);
        private set => SetValue(SummaryToolTipProperty, value);
    }

    public bool HasSummary
    {
        get => (bool)GetValue(HasSummaryProperty);
        private set => SetValue(HasSummaryProperty, value);
    }

    public AchievementHeroSummary()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _ = ReloadAsync();
        Unloaded += (_, _) => CancelLoad();
    }

    private void CancelLoad()
    {
        _loadCts?.Cancel();
        _loadCts = null;
    }

    private async Task ReloadAsync()
    {
        if (DataContext is not Game game)
        {
            ApplyEmptyState();
            return;
        }

        if (game.Id == _loadedGameId)
            return;

        _service ??= App.Services.GetRequiredService<GameAchievementsService>();
        if (!_service.SupportsAchievements(game))
        {
            ApplyEmptyState();
            return;
        }

        if (_service.IsEpicGame(game) && !_service.IsEpicLauncherSessionAvailable())
        {
            ApplyEmptyState();
            return;
        }

        if (_service.IsRomGame(game) && !_service.IsRetroAchievementsConfigured())
        {
            ApplyEmptyState();
            return;
        }

        if (_service.TryGetCached(game, out var cachedSnapshot))
        {
            ApplySnapshot(cachedSnapshot);
            return;
        }

        CancelLoad();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        GameAchievementsSnapshot? snapshot;
        try
        {
            snapshot = await _service.LoadForGameAsync(game, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested || !ReferenceEquals(DataContext, game))
            return;

        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(GameAchievementsSnapshot? snapshot)
    {
        if (DataContext is not Game game)
            return;

        _loadedGameId = game.Id;

        if (snapshot is null || snapshot.TotalCount == 0)
        {
            ApplyEmptyState();
            return;
        }

        HasSummary = true;
        SummaryText = AchievementSummaryFormatter.FormatHeroText(snapshot);
        SummaryToolTip = AchievementSummaryFormatter.FormatHeroToolTip(snapshot);
    }

    private void ApplyEmptyState()
    {
        if (DataContext is Game game)
            _loadedGameId = game.Id;

        HasSummary = false;
        SummaryText = string.Empty;
        SummaryToolTip = string.Empty;
    }
}
