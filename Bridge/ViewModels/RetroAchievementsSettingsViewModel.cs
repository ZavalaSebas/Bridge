using Bridge.Metadata;
using Bridge.Resources;
using Bridge.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class RetroAchievementsSettingsViewModel : ObservableObject
{
    private readonly RetroAchievementsSettings _settings;
    private readonly string _initialPassword;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _webApiKey;

    [ObservableProperty]
    private string _password;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event Action? Saved;

    public RetroAchievementsSettingsViewModel(RetroAchievementsSettings settings)
    {
        _settings = settings;
        _username = settings.Username;
        _webApiKey = settings.WebApiKey;
        _password = settings.Password;
        _initialPassword = settings.Password;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Username = Username.Trim();
        _settings.WebApiKey = WebApiKey.Trim();

        var password = Password.Trim();
        if (!string.Equals(password, _initialPassword, StringComparison.Ordinal))
        {
            _settings.ConnectToken = string.Empty;
        }

        _settings.Password = password;

        try
        {
            RetroAchievementsSettingsStore.Save(_settings);
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
