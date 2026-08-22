using Bridge.Metadata;
using Bridge.Resources;
using Bridge.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class SteamGridDbSettingsViewModel : ObservableObject
{
    private readonly SteamGridDbSettings _settings;

    [ObservableProperty]
    private string _apiKey;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event Action? Saved;

    public SteamGridDbSettingsViewModel(SteamGridDbSettings settings)
    {
        _settings = settings;
        _apiKey = settings.ApiKey;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.ApiKey = ApiKey.Trim();

        try
        {
            SteamGridDbSettingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Format(nameof(Strings.SaveFailedFormat), ex.Message);
            return;
        }

        StatusMessage = Strings.Saved;
        Saved?.Invoke();
    }
}
