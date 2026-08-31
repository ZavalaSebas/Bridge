using System.IO;
using System.Net.NetworkInformation;
using Bridge.Core.Entities;
using Bridge.Core.Import;
using Bridge.Core.Utilities;
using Bridge.Emulation;
using Bridge.Emulation.Dat;
using Bridge.Import.Epic;
using Bridge.Import.Steam;
using Bridge.Resources;
using Bridge.Services;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    public async Task ScanRomFolderAsync(string? romFolder, bool silent = false, bool persistWatchedFolder = true)
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
            if (persistWatchedFolder)
            {
                RomScanFolderSettingsStore.Save(folder);
                _watchedScanFolders.RestartWatchers();
            }

            // Build the already-imported set on the UI thread, then enumerate + hash
            // + DAT-match off the UI thread so a large folder never freezes the
            // window. The scanner only reads this pre-built set, never the live
            // collection, so there's nothing to race from the worker thread.
            var alreadyImported = Games
                .SelectMany(g => g.Roms)
                .Select(r => RomArchivePath.Normalize(r.Path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var found = await Task.Run(() => _romScanner.Scan(folder, alreadyImported));

            var romSource = _sourceRepository.GetOrCreateByName("ROM");
            var added = new List<Game>();
            // Track the CRCs added in THIS scan: pending games aren't in Games yet
            // (they're bound after the batch insert), so FindRomGameByCrc alone
            // wouldn't catch two just-found ROMs that share a CRC.
            var addedCrcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Mutate the collection inside a SYNCHRONOUS suspend region (no await
            // between set and restore) so it nests cleanly even if a watched-folder
            // scan overlaps. Persist the batch BEFORE adding rows to the UI so a
            // failed insert can't leave games showing that aren't in the database.
            _suspendDetailedRows++;
            try
            {
                foreach (var game in found)
                {
                    var rom = game.Roms[0];
                    var existing = FindRomGameByCrc(rom.Crc);
                    if (existing is not null)
                    {
                        UpdateRomPathFromRescan(existing, rom.Path);
                        continue;
                    }

                    // Skip a duplicate CRC already collected earlier in this same scan
                    // (a real CRC only — empty means "unmatched", which never dedups).
                    if (!string.IsNullOrWhiteSpace(rom.Crc) && !addedCrcs.Add(rom.Crc))
                    {
                        continue;
                    }

                    var extension = RomArchivePath.GetRomExtension(rom.Path);
                    if (RomPlatformCatalog.TryGetByExtension(extension, out var platform))
                    {
                        game.PlatformIds.Add(_platformRepository.GetOrCreateByName(platform!.PlatformName).Id);
                    }
                    game.SourceId = romSource.Id;
                    game.ExternalId = RomArchivePath.Normalize(rom.Path);
                    added.Add(game);
                }

                // One transaction for the whole batch, then bind the rows.
                if (added.Count > 0)
                {
                    _gameRepository.AddMany(added);
                    foreach (var game in added)
                    {
                        AddGameSorted(game);
                    }
                }
            }
            finally
            {
                EndDetailRowSuspension();
            }

            // DAT re-identification runs its own synchronous suspend region after.
            await ReidentifyRomGamesFromDatAsync();
            await OrganizeRomsInFolderAsync(folder, force: false);

            SyncRomInstallStates();
            RefreshStatistics();
            RefreshAllEmulatorDownloadStates();
            UpdatePlayButtonState();
            if (added.Count > 0)
            {
                SetStatus(Strings.Format(nameof(Strings.ScanCompleteFormat), added.Count, folder), StatusMessageKind.Normal);
                if (!silent)
                {
                    SelectedGame = added[0];
                }

                await DownloadMetadataForAddedGamesAsync(added, romImport: true);
            }
            else if (!silent)
            {
                SetStatus(Strings.Format(nameof(Strings.ScanCompleteFormat), 0, folder), StatusMessageKind.Normal);
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex);
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
            _suspendDetailedRows++;
            try
            {
                foreach (var game in result.Added)
                {
                    AddGameSorted(game);
                }
            }
            finally
            {
                EndDetailRowSuspension();
            }

            InvalidateReferenceCaches();
            RefreshStatistics();
            RefreshAllEmulatorDownloadStates();
            if (result.Added.Count > 0)
            {
                SetStatus(Strings.Format(nameof(Strings.InstalledScanCompleteFormat), result.Added.Count, scanFolder), StatusMessageKind.Normal);
                if (!silent)
                {
                    SelectedGame = result.Added[0];
                }

                await DownloadMetadataForAddedGamesAsync(result.Added);
            }
            else if (!silent)
            {
                SetStatus(Strings.Format(nameof(Strings.InstalledScanCompleteFormat), 0, scanFolder), StatusMessageKind.Normal);
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex);
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
            applyLocalArtwork: game => ApplySteamLocalArtwork(game));
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
        var found = await Task.Run(enumerate);
        int added = 0, updated = 0;

        _suspendDetailedRows++;
        try
        {
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

            // Games of this source that were previously installed but are no longer
            // enumerated are now uninstalled (e.g. Steam appmanifest removed after
            // uninstall). This keeps Space War (480) and similar titles from staying
            // stuck as "Play" after an external uninstall.
            var foundIds = new HashSet<string>(found.Select(m => m.ExternalId), StringComparer.OrdinalIgnoreCase);
            foreach (var game in Games.Where(g => g.SourceId == sourceId).ToList())
            {
                if (!foundIds.Contains(game.ExternalId) && game.IsInstalled)
                {
                    game.IsInstalled = false;
                    _gameRepository.Update(game);
                    RefreshListDisplay(game);
                    updated++;
                }
            }

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
            EndDetailRowSuspension();
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

    private Game? FindRomGameByCrc(string? crcHex)
    {
        if (string.IsNullOrWhiteSpace(crcHex))
            return null;

        return Games.FirstOrDefault(game =>
            game.Roms.Any(rom => string.Equals(rom.Crc, crcHex, StringComparison.OrdinalIgnoreCase)));
    }

    private void UpdateRomPathFromRescan(Game existing, string newPath)
    {
        var rom = existing.Roms[0];
        var normalized = RomArchivePath.Normalize(newPath);
        if (string.Equals(rom.Path, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        rom.Path = normalized;
        existing.ExternalId = normalized;
        _gameRepository.Update(existing);
        RefreshListDisplay(existing);
    }

    private async Task ReidentifyRomGamesFromDatAsync()
    {
        var romGames = Games.Where(g => g.Roms.Count > 0).ToList();
        if (romGames.Count == 0)
        {
            return;
        }

        // Snapshot the data the matcher needs on the UI thread, then resolve the DAT
        // matches off the UI thread. The lookup reuses the CRC already stored on each
        // ROM, so no file is re-read/re-hashed unless its CRC is still unknown — this
        // is what turns a per-refresh whole-library re-hash into cheap dictionary hits.
        var inputs = romGames
            .Select(game => (game, path: game.Roms[0].Path, crc: game.Roms[0].Crc))
            .ToList();

        var resolved = await Task.Run(() => inputs
            .Select(input =>
            {
                var platform = RomDatMatcher.ResolvePlatformName(input.path);
                var matched = _romDatMatcher.TryMatch(input.path, input.crc, out var match);
                return (input.game, platform, match, matched);
            })
            .ToList());

        // Apply the resolved changes on the UI thread, one repository update per game
        // that actually changed. Each RefreshListDisplay marks the rows dirty, so the
        // suspension's outermost scope rebuilds once when it unwinds.
        _suspendDetailedRows++;
        try
        {
            foreach (var (game, platform, match, matched) in resolved)
            {
                var rom = game.Roms[0];
                var changed = false;

                if (platform is not null && !string.Equals(rom.DatPlatform, platform, StringComparison.Ordinal))
                {
                    rom.DatPlatform = platform;
                    changed = true;
                }

                if (matched)
                {
                    if (!string.Equals(rom.Crc, match!.Crc, StringComparison.OrdinalIgnoreCase))
                    {
                        rom.Crc = match.Crc;
                        changed = true;
                    }

                    if (!string.Equals(rom.DatRegion, match.Region, StringComparison.Ordinal))
                    {
                        rom.DatRegion = match.Region;
                        changed = true;
                    }

                    if (!string.Equals(rom.Name, match.Name, StringComparison.Ordinal))
                    {
                        rom.Name = match.Name;
                        changed = true;
                    }

                    if (!string.Equals(game.Name, match.Name, StringComparison.Ordinal))
                    {
                        game.Name = match.Name;
                        changed = true;
                    }
                }
                else if (rom.DatRegion is not null)
                {
                    rom.DatRegion = null;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                _gameRepository.Update(game);
                RefreshListDisplay(game);
            }
        }
        finally
        {
            EndDetailRowSuspension();
        }
    }

    public async Task<RomOrganizeResult> OrganizeRomsNowAsync()
    {
        var folder = RomScanFolderSettingsStore.Load();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return new RomOrganizeResult([], 0, 0, 0);

        return await OrganizeRomsInFolderAsync(folder, force: true);
    }

    private async Task<RomOrganizeResult> OrganizeRomsInFolderAsync(string folder, bool force)
    {
        if (!force && !RomOrganizeSettingsStore.Load())
            return new RomOrganizeResult([], 0, 0, 0);

        var root = Path.GetFullPath(folder);
        var targets = new List<RomOrganizeTarget>();
        foreach (var game in Games.ToList())
        {
            if (game.Roms.Count == 0)
                continue;

            var rom = game.Roms[0];
            if (string.IsNullOrWhiteSpace(rom.Path))
                continue;

            var diskPath = RomArchivePath.TrySplit(rom.Path, out var archivePath, out _)
                ? archivePath
                : rom.Path;
            if (!PathContainment.IsUnderRoot(diskPath, root))
                continue;

            targets.Add(new RomOrganizeTarget(
                rom.Path,
                string.IsNullOrWhiteSpace(rom.Name) ? game.Name : rom.Name,
                rom.DatPlatform ?? RomDatMatcher.ResolvePlatformName(rom.Path),
                Skip: game.IsRunning));
        }

        if (targets.Count == 0)
            return new RomOrganizeResult([], 0, 0, 0);

        _watchedScanFolders.SuspendRomWatcher();
        RomOrganizeResult result;
        try
        {
            result = await Task.Run(() => RomOrganizeService.Organize(targets, root));
        }
        finally
        {
            _watchedScanFolders.ResumeRomWatcher();
        }

        if (result.Changes.Count == 0)
            return result;

        _suspendDetailedRows++;
        try
        {
            var byOriginal = result.Changes.ToDictionary(
                change => change.OriginalRomPath,
                StringComparer.OrdinalIgnoreCase);
            foreach (var game in Games.ToList())
            {
                if (game.Roms.Count == 0)
                    continue;

                var rom = game.Roms[0];
                if (!byOriginal.TryGetValue(rom.Path, out var change))
                    continue;

                rom.Path = change.NewRomPath;
                game.ExternalId = RomArchivePath.Normalize(change.NewRomPath);
                _gameRepository.Update(game);
                RefreshListDisplay(game);
            }
        }
        finally
        {
            EndDetailRowSuspension();
        }

        return result;
    }
}
