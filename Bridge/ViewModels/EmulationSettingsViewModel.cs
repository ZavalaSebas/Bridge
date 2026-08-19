using Bridge.Emulation;
using Bridge.Resources;
using Bridge.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

/// <summary>Settings surface for Bridge-managed RetroArch, not a general emulator editor.</summary>
public partial class EmulationSettingsViewModel(RetroArchService retroArch) : ObservableObject
{
    [ObservableProperty]
    private string _statusMessage = Strings.LoadingEmulationStatus;

    [ObservableProperty]
    private bool _autoApplyCheatsOnLaunch = AutoApplyCheatsSettingsStore.Load();

    public Task LoadAsync()
    {
        AutoApplyCheatsOnLaunch = AutoApplyCheatsSettingsStore.Load();
        StatusMessage = FormatStatus();
        return Task.CompletedTask;
    }

    partial void OnAutoApplyCheatsOnLaunchChanged(bool value) =>
        AutoApplyCheatsSettingsStore.Save(value);

    [RelayCommand]
    private async Task UpdateAsync()
    {
        try
        {
            await retroArch.UpdateInstalledAsync(new Progress<EmulatorProgress>(message => StatusMessage = message.Message));
            StatusMessage = FormatStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Format(nameof(Strings.CouldNotUpdateRetroArchFormat), ex.Message);
        }
    }

    private string FormatStatus() =>
        retroArch.IsFrontendInstalled
            ? Strings.Format(nameof(Strings.RetroArchInstalledWithCoresFormat), retroArch.InstalledCoreCount)
            : Strings.RetroArchNotInstalled;
}
