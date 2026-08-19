using System.Windows;
using Bridge.ViewModels;

namespace Bridge.Services;

public sealed class SetupWizardResult
{
    public required UserProfile Profile { get; init; }
    public string? RomFolder { get; init; }
    public string? ExternalGamesFolder { get; init; }
}

public static class SetupWizardService
{
    public static async Task ShowIfNeededAsync(Window owner, MainViewModel viewModel)
    {
        if (!ShouldShowSetup())
            return;

        var window = new SetupWizardWindow { Owner = owner };
        var accepted = window.ShowDialog() == true;
        owner.IsEnabled = true;
        owner.Activate();

        if (!accepted || window.Result is not { } result)
            return;

        UserProfileSettingsStore.Save(result.Profile);
        viewModel.ApplyUserProfile(result.Profile);

        if (!string.IsNullOrWhiteSpace(result.RomFolder))
            await viewModel.ScanRomFolderAsync(result.RomFolder, silent: true);

        if (!string.IsNullOrWhiteSpace(result.ExternalGamesFolder))
            await viewModel.ScanInstalledFolderAsync(result.ExternalGamesFolder, silent: true);

        SetupCompleteSettingsStore.MarkComplete();
        viewModel.RestartWatchedScanFolders();
    }

    internal static bool ShouldShowSetup()
    {
        if (SetupCompleteSettingsStore.IsComplete())
            return false;

        // Upgrades from builds before the setup wizard: the app has already run.
        if (WhatsNewSettingsStore.Load() is not null)
        {
            SetupCompleteSettingsStore.MarkComplete();
            return false;
        }

        if (RomScanFolderSettingsStore.Load() is not null ||
            InstalledScanFolderSettingsStore.Load() is not null)
        {
            SetupCompleteSettingsStore.MarkComplete();
            return false;
        }

        return true;
    }
}
