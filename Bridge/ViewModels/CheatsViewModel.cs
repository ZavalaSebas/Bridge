using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Emulation;
using Bridge.Resources;
using Bridge.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;

namespace Bridge.ViewModels;

public partial class CheatsViewModel : ObservableObject
{
    public const string ProjectAttributionUrl = "https://github.com/libretro/libretro-database";

    private readonly RetroArchCheatService _cheatService;
    private readonly IRepository<Platform> _platformRepository;
    private readonly RetroArchService _retroArch;
    private readonly IDialogService _dialogService;
    private Game? _game;
    private RomPlatformDefinition? _platformDefinition;

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private string _platformName = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasCheats;

    [ObservableProperty]
    private ObservableCollection<CheatItemViewModel> _cheats = [];

    [ObservableProperty]
    private string? _sourceFileUrl;

    public CheatsViewModel(
        RetroArchCheatService cheatService,
        IRepository<Platform> platformRepository,
        RetroArchService retroArch,
        IDialogService dialogService)
    {
        _cheatService = cheatService;
        _platformRepository = platformRepository;
        _retroArch = retroArch;
        _dialogService = dialogService;
    }

    public void SetGame(Game game)
    {
        _game = game;
        GameName = game.Name;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_game is null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var platform = _game.PlatformIds.Select(_platformRepository.Get).FirstOrDefault(item => item is not null);
            PlatformName = platform?.Name ?? Strings.Unknown;
            _platformDefinition = platform is null ? null : RomPlatformCatalog.FindByPlatformName(platform.Name);

            if (!_retroArch.IsManagedRom(_game))
            {
                StatusMessage = Strings.CheatsRequireManagedRom;
                return;
            }

            if (_platformDefinition is null || !_platformDefinition.SupportsCheats)
            {
                StatusMessage = Strings.CheatsPlatformNotSupported;
                return;
            }

            var result = await _cheatService.LoadCheatsAsync(_game, _platformDefinition, ct);
            ApplyResult(result);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyResult(CheatsResult result)
    {
        SourceFileUrl = result.SourceFileUrl;

        switch (result.Outcome)
        {
            case CheatFetchOutcome.Success when result.Cheats.Count == 0:
            case CheatFetchOutcome.NotFound:
                HasCheats = false;
                StatusMessage = Strings.CheatsNotFoundForGame;
                break;

            case CheatFetchOutcome.Success:
                HasCheats = true;
                StatusMessage = string.Empty;
                Cheats = new ObservableCollection<CheatItemViewModel>(
                    result.Cheats.Select(c => new CheatItemViewModel(c.Index, c.Description, c.Enabled)));
                break;

            case CheatFetchOutcome.PlatformNotSupported:
                HasCheats = false;
                StatusMessage = Strings.CheatsPlatformNotSupported;
                break;

            case CheatFetchOutcome.FetchFailed:
                HasCheats = false;
                StatusMessage = result.ErrorMessage ?? Strings.CheatsFetchFailed;
                break;

            case CheatFetchOutcome.Corrupted:
                HasCheats = false;
                StatusMessage = result.ErrorMessage ?? Strings.CheatsFileCorrupted;
                break;
        }
    }

    [RelayCommand]
    private async Task ToggleCheatAsync(CheatItemViewModel? item)
    {
        if (_game is null || _platformDefinition is null || item is null)
        {
            return;
        }

        try
        {
            await _cheatService.SetCheatEnabledAsync(_game, _platformDefinition, item.Index, item.Enabled);
        }
        catch (IOException ex)
        {
            item.Enabled = !item.Enabled;
            _dialogService.ShowWarning(
                Strings.Format(nameof(Strings.CouldNotSaveCheatFormat), ex.Message),
                Strings.CouldNotSaveCheatTitle);
        }
    }
}

public partial class CheatItemViewModel : ObservableObject
{
    public int Index { get; }
    public string Description { get; }

    [ObservableProperty]
    private bool _enabled;

    public CheatItemViewModel(int index, string description, bool enabled)
    {
        Index = index;
        Description = description;
        _enabled = enabled;
    }
}
