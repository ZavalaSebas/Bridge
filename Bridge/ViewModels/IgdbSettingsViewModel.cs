using Bridge.Core.Contracts;
using Bridge.Core.Utilities;
using Bridge.Metadata;
using Bridge.Resources;
using Bridge.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class IgdbSettingsViewModel : ObservableObject
{
    private readonly IgdbSettings _settings;
    private readonly IGameRepository _gameRepository;

    [ObservableProperty]
    private string _clientId;

    [ObservableProperty]
    private string _clientSecret;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event Action? Saved;

    public IgdbSettingsViewModel(IgdbSettings settings, IGameRepository gameRepository)
    {
        _settings = settings;
        _gameRepository = gameRepository;
        _clientId = settings.ClientId;
        _clientSecret = settings.ClientSecret;
    }

    [RelayCommand]
    private void Save()
    {
        // Track if credentials changed to reset sync markers
        var credentialsChanged = ClientId.Trim() != _settings.ClientId || 
                                ClientSecret.Trim() != _settings.ClientSecret;

        _settings.ClientId = ClientId.Trim();
        _settings.ClientSecret = ClientSecret.Trim();

        try
        {
            IgdbSettingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Format(nameof(Strings.SaveFailedFormat), ex.Message);
            return;
        }

        // If IGDB credentials changed, reset metadata sync markers to retry with new config
        if (credentialsChanged)
        {
            var allGames = _gameRepository.GetAll();
            _gameRepository.UpdateManyMetadataSyncMarkers(allGames, MetadataSyncMarker.Metadata);
            _gameRepository.UpdateManyMetadataSyncMarkers(allGames, MetadataSyncMarker.Links);
        }

        StatusMessage = Strings.Saved;
        Saved?.Invoke();
    }
}
