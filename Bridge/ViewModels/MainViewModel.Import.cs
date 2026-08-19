using System.IO;
using System.Net.NetworkInformation;
using Bridge.Core.Entities;
using Bridge.Core.Import;
using Bridge.Emulation;
using Bridge.Import.Epic;
using Bridge.Import.Steam;
using Bridge.Resources;
using Bridge.Services;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    public async Task ScanRomFolderAsync(string? romFolder, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(romFolder))
        {
            return;
        }

        var folder = romFolder.Trim();
        if (!Directory.Exists(folder))
        {
            if (!silent)
            {
                SetStatus(Strings.Format(nameof(Strings.ScanFailedFormat), Strings.SelectFolderToScan), StatusMessageKind.Error);
            }

            return;
        }

        try
        {
            RomScanFolderSettingsStore.Save(folder);
            _watchedScanFolders.RestartWatchers();

            var found = _romScanner.Scan(folder, Games);
            var romSource = _sourceRepository.GetOrCreateByName("ROM");
            foreach (var game in found)
            {
                var extension = RomArchivePath.GetRomExtension(game.Roms[0].Path);
                if (RomPlatformCatalog.TryGetByExtension(extension, out var platform))
                {
                    game.PlatformIds.Add(_platformRepository.GetOrCreateByName(platform!.PlatformName).Id);
                }
                game.SourceId = romSource.Id;
                game.ExternalId = RomArchivePath.Normalize(game.Roms[0].Path);
                _gameRepository.Add(game);
                AddGameSorted(game);
            }

            RefreshStatistics();
            RefreshAllEmulatorDownloadStates();
            if (found.Count > 0)
            {
                StatusMessage = Strings.Format(nameof(Strings.ScanCompleteFormat), found.Count, folder);
                if (!silent)
                {
                    SelectedGame = found[0];
                }

                await DownloadMetadataForAddedGamesAsync(found, romImport: true);
            }
            else if (!silent)
            {
                StatusMessage = Strings.Format(nameof(Strings.ScanCompleteFormat), 0, folder);
            }
        }
        catch (Exception ex)
        {
            SetStatus(Strings.Format(nameof(Strings.ScanFailedFormat), ex.Message), StatusMessageKind.Error);
        }
    }

    public async Task ScanInstalledFolderAsync(string? folder, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        var scanFolder = folder.Trim();
        if (!Directory.Exists(scanFolder))
        {
            if (!silent)
            {
                SetStatus(Strings.Format(nameof(Strings.ScanFailedFormat), Strings.SelectFolderToScanGames), StatusMessageKind.Error);
            }

            return;
        }

        try
        {
            InstalledScanFolderSettingsStore.Save(scanFolder);
            _watchedScanFolders.RestartWatchers();

            var result = await Task.Run(() => _installedGameImport.ImportNewFromFolder(scanFolder));
            foreach (var game in result.Added)
            {
                AddGameToLibrary(game);
            }

            RefreshStatistics();
            RefreshAllEmulatorDownloadStates();
            if (result.Added.Count > 0)
            {
                StatusMessage = Strings.Format(nameof(Strings.InstalledScanCompleteFormat), result.Added.Count, scanFolder);
                if (!silent)
                {
                    SelectedGame = result.Added[0];
                }

                await DownloadMetadataForAddedGamesAsync(result.Added);
            }
            else if (!silent)
            {
                StatusMessage = Strings.Format(nameof(Strings.InstalledScanCompleteFormat), 0, scanFolder);
            }
        }
        catch (Exception ex)
        {
            SetStatus(Strings.Format(nameof(Strings.ScanFailedFormat), ex.Message), StatusMessageKind.Error);
        }
    }

    public void RestartWatchedScanFolders() => _watchedScanFolders.RestartWatchers();

    [RelayCommand]
    private async Task ImportSteamLibrary()
    {
        var steamSource = _sourceRepository.GetOrCreateByName("Steam");
        await ImportSteamLibraryCoreAsync(steamSource.Id);
    }

    [RelayCommand]
    private async Task ImportEpicLibrary()
    {
        var epicSource = _sourceRepository.GetOrCreateByName("Epic");
        await ImportEpicLibraryCoreAsync(epicSource.Id);
    }

    private async Task ImportSteamLibraryCoreAsync(Guid steamSourceId)
    {
        // Steam not installed is a normal condition (the import is optional) —
        // skip quietly instead of surfacing a scary message.
        if (string.IsNullOrEmpty(SteamPaths.GetInstallationPath()))
        {
            StatusMessage = Strings.SteamNotDetectedSkippedImport;
            return;
        }

        await ImportLibraryCoreAsync(
            steamSourceId,
            "Steam",
            () => _steamImporter.GetInstalledGames(),
            applyLocalArtwork: ApplySteamLocalArtwork);
    }

    private async Task ImportEpicLibraryCoreAsync(Guid epicSourceId)
    {
        if (!EpicPaths.IsInstalled)
        {
            StatusMessage = Strings.EpicNotDetectedSkippedImport;
            return;
        }

        await ImportLibraryCoreAsync(
            epicSourceId,
            "Epic",
            () => _epicImporter.GetInstalledGames(),
            applyLocalArtwork: null);
    }

    // Shared bulk import: enumerates games (on a pool thread — pure file I/O),
    // inserts new ones and syncs install state on existing ones, all on the UI
    // thread. Suspend the per-change rebuild so a large library doesn't trigger
    // O(n²) table rebuilds.
    private async Task ImportLibraryCoreAsync(
        Guid sourceId,
        string sourceName,
        Func<List<GameMetadata>> enumerate,
        Action<Game>? applyLocalArtwork)
    {
        _suspendDetailedRows = true;
        try
        {
            var found = await Task.Run(enumerate);
            int added = 0, updated = 0;

            foreach (var metadata in found)
            {
                // Yield periodically so a large library doesn't freeze the UI
                // while the window is already interactive.
                if ((added + updated) > 0 && (added + updated) % 25 == 0)
                {
                    await Task.Yield();
                }

                var existing = _gameRepository.FindByExternalId(metadata.ExternalId, sourceId);
                if (existing is null)
                {
                    var game = new Game
                    {
                        Name = metadata.Name,
                        ExternalId = metadata.ExternalId,
                        SourceId = sourceId,
                        InstallDirectory = metadata.InstallDirectory,
                        InstallSizeBytes = metadata.InstallSizeBytes,
                        Icon = metadata.Icon ?? string.Empty,
                        IsInstalled = metadata.IsInstalled,
                        Added = DateTime.Now,
                        GameActions = metadata.GameActions,
                        Links = metadata.Links,
                        PlaytimeSeconds = metadata.PlaytimeSeconds,
                        LastActivity = metadata.LastActivity
                    };
                    // Resolve locally-cached artwork (Steam) BEFORE the row binds
                    // so the library shows complete art the moment it's added.
                    applyLocalArtwork?.Invoke(game);
                    _gameRepository.Add(game);
                    AddGameSorted(game);
                    added++;
                }
                else
                {
                    // FindByExternalId returns a detached snapshot — mutate the live
                    // instance bound to the UI so import + metadata sync see the same object.
                    var live = Games.FirstOrDefault(g => g.Id == existing.Id);
                    if (live is null)
                    {
                        continue;
                    }

                    // Re-import only syncs install state — leave user/metadata fields alone.
                    live.IsInstalled = metadata.IsInstalled;
                    live.InstallDirectory = metadata.InstallDirectory;
                    live.InstallSizeBytes = metadata.InstallSizeBytes;
                    // Steam's locally-recorded playtime fills in the real number
                    // without ever shrinking what Bridge already tracked (the two
                    // overlap, so taking the max can't double-count), and
                    // LastActivity only moves forward.
                    live.PlaytimeSeconds = Math.Max(live.PlaytimeSeconds, metadata.PlaytimeSeconds);
                    if (metadata.LastActivity is { } steamPlayed &&
                        (live.LastActivity is null || steamPlayed > live.LastActivity))
                    {
                        live.LastActivity = steamPlayed;
                    }
                    // Fill a missing icon from the source (Epic exe icon, Steam
                    // local art) without overwriting one the user set. A local
                    // file icon (the Epic exe) always wins over a remote URL.
                    var srcIcon = metadata.Icon;
                    if (!string.IsNullOrWhiteSpace(srcIcon) &&
                        (string.IsNullOrWhiteSpace(live.Icon) || Path.IsPathRooted(srcIcon)))
                    {
                        live.Icon = srcIcon;
                    }
                    applyLocalArtwork?.Invoke(live);
                    _gameRepository.Update(live);
                    RefreshListDisplay(live);
                    updated++;
                }
            }

            RebuildDetailedRows();
            RefreshStatistics();
            InvalidateReferenceCaches();
            RefreshAllEmulatorDownloadStates();
            StatusMessage = Strings.Format(nameof(Strings.ImportResultFormat), sourceName, added, updated);
        }
        catch (Exception ex)
        {
            SetStatus(Strings.Format(nameof(Strings.ImportFailedFormat), sourceName, ex.Message), StatusMessageKind.Error);
        }
        finally
        {
            _suspendDetailedRows = false;
        }
    }

    // True when the OS reports at least one active network interface. Metadata
    // and emulator downloads all need internet, so a negative answer short-circuits
    // them with a clear "you're offline" message instead of a confusing
    // "no metadata found" (the providers swallow HTTP failures to keep the
    // fallback chain moving, which hides the real cause). A positive answer is
    // NOT a guarantee of internet — it just lets the normal flow run and report
    // its own outcome.
    private static bool IsNetworkAvailable()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }
        catch
        {
            // The check itself failed (rare) — let the normal flow run and
            // report its own outcome rather than guessing.
            return true;
        }
    }
}
