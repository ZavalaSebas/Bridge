using Bridge.Resources;
using Bridge.Services;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    private bool _refreshInProgress;

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        if (_refreshInProgress || IsEmulationBusy)
        {
            return;
        }

        _refreshInProgress = true;
        BeginStatusProgress(indeterminate: true);
        StatusMessage = Strings.RefreshLibraryInProgress;
        try
        {
            _ = PreloadArtworkAsync();
            await RefreshLibraryCoreAsync();
            InvalidateReferenceCaches();
            RebuildDetailedRows();
            RefreshStatistics();
            _ = PreloadArtworkAsync();
            StatusMessage = Strings.RefreshLibraryComplete;
        }
        catch (Exception ex)
        {
            SetStatus(Strings.Format(nameof(Strings.RefreshLibraryFailedFormat), ex.Message), StatusMessageKind.Error);
        }
        finally
        {
            _refreshInProgress = false;
            EndStatusProgress();
        }
    }

    private async Task RefreshLibraryCoreAsync()
    {
        var steamSourceId = _sourceRepository.GetOrCreateByName("Steam").Id;
        var epicSourceId = _sourceRepository.GetOrCreateByName("Epic").Id;
        await ImportSteamLibraryCoreAsync(steamSourceId);
        await ImportEpicLibraryCoreAsync(epicSourceId);

        var romFolder = RomScanFolderSettingsStore.Load();
        if (!string.IsNullOrWhiteSpace(romFolder))
        {
            await ScanRomFolderAsync(romFolder, silent: true);
        }

        var installedFolder = InstalledScanFolderSettingsStore.Load();
        if (!string.IsNullOrWhiteSpace(installedFolder))
        {
            await ScanInstalledFolderAsync(installedFolder, silent: true);
        }

        await DownloadMissingSteamMetadataAsync(steamSourceId);
        await DownloadMissingMetadataByNameAsync([epicSourceId]);
        await DownloadMissingHowLongToBeatAsync();
        RefreshAllEmulatorDownloadStates();
    }
}
