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

        // Select a game as soon as the (fast, local) imports and folder scans have
        // populated the library, so a first run doesn't wait for the slow metadata
        // sync below to pick something. Only when nothing is chosen yet.
        if (SelectedGame is null)
            SelectedGame = SelectInitialGame(Games);

        await DownloadMissingSteamMetadataAsync(steamSourceId);
        var bridgeSourceId = InstalledGameImportService.EnsureBridgeSource(_sourceRepository);
        await DownloadMissingBridgeMetadataAsync(bridgeSourceId);
        await DownloadMissingMetadataByNameAsync([epicSourceId]);
        await ReidentifyRomGamesFromDatAsync();
        await DownloadMissingRomMetadataAsync();
        await DownloadMissingHowLongToBeatAsync();
        await DownloadMissingLogosAsync();
        RefreshAllEmulatorDownloadStates();

        // Clear the last "Downloading metadata…" text once the whole sync is done —
        // EndStatusProgress only hides the bar, and a batch that matched nothing
        // leaves its message set. Skip when a manual Refresh is running: it sets its
        // own completion message right after this returns.
        if (!_refreshInProgress)
            StatusMessage = string.Empty;
    }
}
