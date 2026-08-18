using Bridge.Emulation;
using Bridge.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

/// <summary>Settings surface for Bridge-managed RetroArch, not a general emulator editor.</summary>
public partial class EmulationSettingsViewModel(RetroArchService retroArch) : ObservableObject
{
    [ObservableProperty]
    private string _statusMessage = Strings.LoadingEmulationStatus;

    public async Task LoadAsync() => StatusMessage = await retroArch.GetStatusAsync();

    [RelayCommand]
    private async Task UpdateAsync()
    {
        try
        {
            await retroArch.UpdateInstalledAsync(new Progress<EmulatorProgress>(message => StatusMessage = message.Message));
            StatusMessage = await retroArch.GetStatusAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Format(nameof(Strings.CouldNotUpdateRetroArchFormat), ex.Message);
        }
    }
}
