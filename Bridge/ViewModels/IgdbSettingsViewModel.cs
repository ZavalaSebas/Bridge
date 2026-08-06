using Bridge.Metadata;
using Bridge.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class IgdbSettingsViewModel : ObservableObject
{
    private readonly IgdbSettings _settings;

    [ObservableProperty]
    private string _clientId;

    [ObservableProperty]
    private string _clientSecret;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event Action? Saved;

    public IgdbSettingsViewModel(IgdbSettings settings)
    {
        _settings = settings;
        _clientId = settings.ClientId;
        _clientSecret = settings.ClientSecret;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.ClientId = ClientId.Trim();
        _settings.ClientSecret = ClientSecret.Trim();
        IgdbSettingsStore.Save(_settings);
        StatusMessage = "Saved.";
        Saved?.Invoke();
    }
}
