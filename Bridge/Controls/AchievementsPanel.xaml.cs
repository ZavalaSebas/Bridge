using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Bridge.Core.Entities;
using Bridge.Resources;
using Bridge.Services;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge.Controls;

public partial class AchievementsPanel : UserControl
{
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(AchievementsPanel), new PropertyMetadata(false));

    public static readonly DependencyProperty EmptyMessageProperty =
        DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(AchievementsPanel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ProgressTextProperty =
        DependencyProperty.Register(nameof(ProgressText), typeof(string), typeof(AchievementsPanel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CompletionPercentTextProperty =
        DependencyProperty.Register(nameof(CompletionPercentText), typeof(string), typeof(AchievementsPanel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RemainingTextProperty =
        DependencyProperty.Register(nameof(RemainingText), typeof(string), typeof(AchievementsPanel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ProgressValueProperty =
        DependencyProperty.Register(nameof(ProgressValue), typeof(double), typeof(AchievementsPanel), new PropertyMetadata(0d));

    public static readonly DependencyProperty ProgressMaximumProperty =
        DependencyProperty.Register(nameof(ProgressMaximum), typeof(double), typeof(AchievementsPanel), new PropertyMetadata(1d));

    public static readonly DependencyProperty HasAchievementsProperty =
        DependencyProperty.Register(nameof(HasAchievements), typeof(bool), typeof(AchievementsPanel), new PropertyMetadata(false));

    public static readonly DependencyProperty ShowsProgressProperty =
        DependencyProperty.Register(nameof(ShowsProgress), typeof(bool), typeof(AchievementsPanel), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowsConfigureRetroAchievementsButtonProperty =
        DependencyProperty.Register(
            nameof(ShowsConfigureRetroAchievementsButton),
            typeof(bool),
            typeof(AchievementsPanel),
            new PropertyMetadata(false));

    private CancellationTokenSource? _loadCts;
    private GameAchievementsService? _service;
    private Guid _loadedGameId;

    public ObservableCollection<GameAchievement> Achievements { get; } = [];

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        private set => SetValue(IsLoadingProperty, value);
    }

    public string EmptyMessage
    {
        get => (string)GetValue(EmptyMessageProperty);
        private set => SetValue(EmptyMessageProperty, value);
    }

    public string ProgressText
    {
        get => (string)GetValue(ProgressTextProperty);
        private set => SetValue(ProgressTextProperty, value);
    }

    public string CompletionPercentText
    {
        get => (string)GetValue(CompletionPercentTextProperty);
        private set => SetValue(CompletionPercentTextProperty, value);
    }

    public string RemainingText
    {
        get => (string)GetValue(RemainingTextProperty);
        private set => SetValue(RemainingTextProperty, value);
    }

    public double ProgressValue
    {
        get => (double)GetValue(ProgressValueProperty);
        private set => SetValue(ProgressValueProperty, value);
    }

    public double ProgressMaximum
    {
        get => (double)GetValue(ProgressMaximumProperty);
        private set => SetValue(ProgressMaximumProperty, value);
    }

    public bool HasAchievements
    {
        get => (bool)GetValue(HasAchievementsProperty);
        private set => SetValue(HasAchievementsProperty, value);
    }

    public bool ShowsProgress
    {
        get => (bool)GetValue(ShowsProgressProperty);
        private set => SetValue(ShowsProgressProperty, value);
    }

    public bool ShowsConfigureRetroAchievementsButton
    {
        get => (bool)GetValue(ShowsConfigureRetroAchievementsButtonProperty);
        private set => SetValue(ShowsConfigureRetroAchievementsButtonProperty, value);
    }

    public AchievementsPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _ = ReloadAsync();
        Unloaded += (_, _) => CancelLoad();

        var achievementsService = App.Services.GetRequiredService<GameAchievementsService>();
        achievementsService.RomSessionEnded += OnRomSessionEnded;
    }

    private void OnRomSessionEnded(Game game)
    {
        if (DataContext is not Game current || current.Id != game.Id)
            return;

        _loadedGameId = Guid.Empty;
        _ = ReloadAsync();
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
            ApplyEmptyState(Strings.AchievementsNotSupported, showConfigureButton: false);
            return;
        }

        if (game.Id == _loadedGameId && (HasAchievements || !string.IsNullOrEmpty(EmptyMessage)))
            return;

        _service ??= App.Services.GetRequiredService<GameAchievementsService>();
        if (!_service.SupportsAchievements(game))
        {
            ApplyEmptyState(Strings.AchievementsNotSupported, showConfigureButton: false);
            return;
        }

        if (_service.IsEpicGame(game) && !_service.IsEpicLauncherSessionAvailable())
        {
            ApplyEmptyState(Strings.AchievementsEpicLoginRequired, showConfigureButton: false);
            return;
        }

        if (_service.IsRomGame(game) && !_service.IsRetroAchievementsConfigured())
        {
            ApplyEmptyState(Strings.AchievementsRetroAchievementsRequired, showConfigureButton: true);
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

        IsLoading = true;
        EmptyMessage = string.Empty;
        ShowsConfigureRetroAchievementsButton = false;

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

        IsLoading = false;
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(GameAchievementsSnapshot? snapshot)
    {
        if (DataContext is not Game game)
            return;

        _loadedGameId = game.Id;
        IsLoading = false;
        ShowsConfigureRetroAchievementsButton = false;

        if (snapshot is null || snapshot.TotalCount == 0)
        {
            var message = _service?.IsRomGame(game) == true
                ? _service.GetRomEmptyMessage(game)
                : Strings.AchievementsNone;
            ApplyEmptyState(message, showConfigureButton: false);
            return;
        }

        Achievements.Clear();
        foreach (var achievement in snapshot.Achievements)
            Achievements.Add(achievement);

        HasAchievements = true;
        EmptyMessage = string.Empty;
        ShowsProgress = snapshot.TracksProgress;

        if (snapshot.TracksProgress)
        {
            ProgressValue = snapshot.UnlockedCount;
            ProgressMaximum = snapshot.TotalCount;
            ProgressText = Strings.Format(
                nameof(Strings.AchievementsProgressFormat),
                snapshot.UnlockedCount,
                snapshot.TotalCount);
            CompletionPercentText = Strings.Format(
                nameof(Strings.AchievementsCompletionPercentFormat),
                snapshot.CompletionPercent);
            RemainingText = snapshot.RemainingCount > 0
                ? Strings.Format(nameof(Strings.AchievementsRemainingFormat), snapshot.RemainingCount)
                : string.Empty;
        }
        else
        {
            ProgressValue = 0;
            ProgressMaximum = 1;
            ProgressText = Strings.Format(nameof(Strings.AchievementsCatalogCountFormat), snapshot.TotalCount);
            CompletionPercentText = string.Empty;
            RemainingText = Strings.AchievementsCatalogOnlyHint;
        }
    }

    private void ApplyEmptyState(string message, bool showConfigureButton)
    {
        if (DataContext is Game game)
            _loadedGameId = game.Id;

        IsLoading = false;
        HasAchievements = false;
        ShowsProgress = true;
        ShowsConfigureRetroAchievementsButton = showConfigureButton;
        ProgressText = string.Empty;
        CompletionPercentText = string.Empty;
        RemainingText = string.Empty;
        ProgressValue = 0;
        ProgressMaximum = 1;
        Achievements.Clear();
        EmptyMessage = message;
    }

    private void ConfigureRetroAchievements_Click(object sender, RoutedEventArgs e)
    {
        _service ??= App.Services.GetRequiredService<GameAchievementsService>();
        var owner = Window.GetWindow(this);
        var viewModel = App.Services.GetRequiredService<RetroAchievementsSettingsViewModel>();
        var saved = new RetroAchievementsSettingsWindow(viewModel) { Owner = owner }.ShowDialog() == true;
        if (!saved)
            return;

        _service.ClearRetroAchievementsCache();
        _loadedGameId = Guid.Empty;
        _ = ReloadAsync();
    }
}
