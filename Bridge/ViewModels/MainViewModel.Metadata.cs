using Bridge.Core.Entities;
using Bridge.Core.Import;
using Bridge.Core.Utilities;
using Bridge.Emulation;
using Bridge.Metadata;
using Bridge.Resources;
using Bridge.Statistics;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    /// <summary>Elapsed time before retrying metadata sync for a game that was last attempted (success or fail).</summary>
    private static readonly TimeSpan METADATA_SYNC_TTL = TimeSpan.FromDays(30);

    private static bool NeedsMetadataRefresh(Game game, DateTime now) =>
        (string.IsNullOrWhiteSpace(game.Description) ||
         string.IsNullOrWhiteSpace(game.CoverImage) ||
         HeroBackground.IsDefault(game.BackgroundImage) ||
         string.IsNullOrWhiteSpace(game.LogoImage) ||
         game.Screenshots.Count == 0) &&
        (game.MetadataSyncedAt == null || now - game.MetadataSyncedAt > METADATA_SYNC_TTL);

    [RelayCommand]
    private async Task DownloadMetadataAsync()
    {
        if (SelectedGame is null)
            return;

        var game = SelectedGame;
        // ROMs get the same treatment as a scan import: IGDB chain first (with
        // the normalized name), Steam as the last resort — the strict Steam
        // title match only wastes time on titles IGDB knows as "Pokémon".
        var romImport = game.Roms.Count > 0;
        var gameName = romImport ? RomScanner.ToSearchName(game.Name) : game.Name;

        SetStatus(Strings.Format(nameof(Strings.DownloadingMetadataForGameFormat), game.Name), StatusMessageKind.Normal);

        BeginStatusProgress(indeterminate: true);
        try
        {
            var result = romImport
                ? await _metadataSync.SearchRomMetadataAsync(game.Name, GetPrimaryRomPath(game))
                : await _metadataSync.SearchForManualDownloadAsync(
                    gameName,
                    romImport: false,
                    !string.IsNullOrWhiteSpace(game.ExternalId) && uint.TryParse(game.ExternalId, out _)
                        ? game.ExternalId
                        : null);

            var metadataApplied = false;
            string? providerName = null;

            if (result is not null)
            {
                (var metadata, providerName) = result.Value;

                if (providerName == _steamMetadataProvider.Name)
                    await _metadataSync.EnrichSteamLinksFromIgdbAsync(gameName, metadata);

                ApplyMetadata(game, metadata);
                ApplyMetadataReferences(game, metadata);
                await TryEnrichArtworkFromSteamGridDbAsync(game, overwrite: true);
                ApplySteamLocalArtwork(game, overwrite: true);
                metadataApplied = true;
            }

            var hltbApplied = await _howLongToBeat.TryEnrichGameAsync(game, overwrite: true);

            if (metadataApplied || hltbApplied)
            {
                _gameRepository.Update(game);
                RefreshListDisplay(game);
            }

            // Seal sync markers to respect TTL: prevents perpetual re-download on every startup
            // (seals both on success and failure — if no metadata found, still mark as "attempted")
            _gameRepository.UpdateManyMetadataSyncMarkers(
                new[] { game }.ToList(),
                MetadataSyncMarker.Metadata);
            _gameRepository.UpdateManyMetadataSyncMarkers(
                new[] { game }.ToList(),
                MetadataSyncMarker.Links);

            if (!metadataApplied && !hltbApplied)
            {
                SetStatus(IsNetworkAvailable()
                    ? Strings.Format(nameof(Strings.NoMetadataFoundFormat), gameName)
                    : Strings.NoInternetMetadataDeferred, StatusMessageKind.Normal);
                return;
            }

            SetStatus(metadataApplied
                ? Strings.Format(nameof(Strings.MetadataAppliedToGameFormat), game.Name, providerName!)
                : Strings.Format(nameof(Strings.HowLongToBeatAppliedToGameFormat), game.Name), StatusMessageKind.Normal);
        }
        finally
        {
            EndStatusProgress();
        }
    }

    // Downloads metadata for games just added from "Scan Automatically". Unlike
    // the startup sync (which walks existing games), this runs on demand for a
    // handful of freshly-created manual games and prefers Steam by name first —
    // a manually-installed copy of a game that exists on Steam (Risk of Rain 2,
    // Fallout 3) gets the full Steam metadata (cover, hero, screenshots, store
    // links) instead of IGDB's, and only falls back to the IGDB chain when
    // Steam has no match. Matches the explicit "Download Metadata" action.
    //
    // For ROM imports (romImport: true) the order is reversed: ROMs are almost
    // never on Steam, and Steam's strict tokenized title match only wastes
    // time, so the IGDB chain runs first and Steam is the last-resort fallback.
    // The search name is normalized (RomScanner.ToSearchName) so IGDB's fuzzy
    // `search` matches titles like "Pokemon - Emerald Version" -> "Pokémon
    // Emerald Version" instead of missing them.
    public async Task DownloadMetadataForAddedGamesAsync(IReadOnlyList<Game> games, bool romImport = false)
    {
        var now = DateTime.Now;
        var candidates = games
            .Where(g => NeedsMetadataRefresh(g, now))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        SetStatus(Strings.Format(nameof(Strings.DownloadingMetadataForGamesFormat), candidates.Count), StatusMessageKind.Normal);

        var completed = 0;
        var total = candidates.Count;
        BeginStatusProgress(indeterminate: total <= 1);
        ReportBatchProgress(0, total);
        _suspendDetailedRows++;
        try
        {
            using var throttle = new SemaphoreSlim(4);
            var results = await Task.WhenAll(candidates.Select(game =>
                Task.Run(async () =>
                {
                    await throttle.WaitAsync();
                    try
                    {
                        var found = romImport
                            ? await _metadataSync.SearchRomMetadataAsync(game.Name, GetPrimaryRomPath(game))
                            : await _metadataSync.SearchForAddedGameAsync(game.Name, romImport: false);
                        return found is null
                            ? (game, metadata: (GameMetadata?)null, provider: (string?)null)
                            : (game, metadata: found.Value.Metadata, provider: found.Value.ProviderName);
                    }
                    finally
                    {
                        throttle.Release();
                        var done = Interlocked.Increment(ref completed);
                        RunOnUiThread(() => ReportBatchProgress(done, total));
                    }
                })));

            int applied = 0;
            foreach (var (game, metadata, providerName) in results)
            {
                if (metadata is null || providerName is null)
                    continue;

                try
                {
                    var live = TryGetLiveGame(game);
                    if (live is null)
                        continue;

                    if (providerName == _steamMetadataProvider.Name)
                        await _metadataSync.EnrichSteamLinksFromIgdbAsync(live.Name, metadata);

                    ApplyMetadata(live, metadata);
                    ApplyMetadataReferences(live, metadata);
                    await TryEnrichArtworkFromSteamGridDbAsync(live, overwrite: false);
                    ApplySteamLocalArtwork(live);

                    _gameRepository.Update(live);
                    RefreshListDisplay(live);
                    applied++;
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                }
            }

            // Batch seal metadata markers
            _gameRepository.UpdateManyMetadataSyncMarkers(candidates, MetadataSyncMarker.Metadata);

            if (applied > 0)
                SetStatus(Strings.Format(nameof(Strings.MetadataAppliedBatchFormat), applied, candidates.Count), StatusMessageKind.Normal);
            else
                SetStatus(Strings.Format(nameof(Strings.NoMetadataFoundForAddedGamesFormat), candidates.Count), StatusMessageKind.Normal);
        }
        finally
        {
            EndDetailRowSuspension();
            EndStatusProgress();
        }
    }

    private static void ApplyMetadata(Game game, GameMetadata metadata, bool overwrite = true)
    {
        // Never rename a ROM that didn't match the No-Intro DAT (hack/homebrew/bad dump):
        // the filename is the only reliable identity and an IGDB "Pokemon Black 2: DE"
        // coincidence would clobber the user's hack name. DAT-matched ROMs have a
        // populated DatRegion; hacks keep it null (see RomScanner.ProcessRom).
        var isDatMatchedRom = game.Roms.Count > 0 && game.Roms[0].DatRegion is not null;
        var renameFromMetadata = (isDatMatchedRom || game.Roms.Count == 0)
            && !string.IsNullOrWhiteSpace(metadata.Name)
            && (overwrite || string.IsNullOrWhiteSpace(game.Description));
        // For non-DAT ROMs, also never clobber the display name even if description is empty.
        if (!isDatMatchedRom && game.Roms.Count > 0)
            renameFromMetadata = false;

        if (!string.IsNullOrWhiteSpace(metadata.Description))
            game.Description = metadata.Description;

        if (metadata.DescriptionImages.Count > 0)
            game.DescriptionImages = metadata.DescriptionImages;

        if (metadata.DescriptionBlocks.Count > 0)
            game.DescriptionBlocks = metadata.DescriptionBlocks;

        if (metadata.ReleaseDate is { } releaseDate)
            game.ReleaseDate = releaseDate;

        // The manual "Download Metadata" action (overwrite: true) refreshes every
        // image, even ones already set. The startup sync (overwrite: false) only
        // fills images that are missing — these are re-downloaded rarely and
        // unlikely to change, so an existing image is kept rather than clobbered
        // on every app open.
        if (!string.IsNullOrWhiteSpace(metadata.CoverImage) &&
            (overwrite || string.IsNullOrWhiteSpace(game.CoverImage)))
        {
            var cover = UrlValidator.SanitizePersistedUrl(metadata.CoverImage);
            if (!string.IsNullOrWhiteSpace(cover))
                game.CoverImage = cover;
        }

        // Don't overwrite an existing icon: for Epic games the importer sets the
        // installed executable's icon (better than a cover thumbnail), and for
        // Steam games ApplySteamLocalArtwork resolved the local clienticon. A
        // metadata icon only fills in games that have none yet.
        if (string.IsNullOrWhiteSpace(game.Icon) && !string.IsNullOrWhiteSpace(metadata.Icon))
        {
            var icon = UrlValidator.SanitizePersistedUrl(metadata.Icon);
            if (!string.IsNullOrWhiteSpace(icon))
                game.Icon = icon;
        }

        if (!string.IsNullOrWhiteSpace(metadata.BackgroundImage) &&
            (overwrite || HeroBackground.IsDefault(game.BackgroundImage)))
        {
            var background = UrlValidator.SanitizePersistedUrl(metadata.BackgroundImage);
            if (!string.IsNullOrWhiteSpace(background))
                game.BackgroundImage = background;
        }

        if (!string.IsNullOrWhiteSpace(metadata.LogoImage) &&
            (overwrite || string.IsNullOrWhiteSpace(game.LogoImage)))
        {
            var logo = UrlValidator.SanitizePersistedUrl(metadata.LogoImage);
            if (!string.IsNullOrWhiteSpace(logo))
                game.LogoImage = logo;
        }

        if (metadata.Screenshots is { Count: > 0 } &&
            (overwrite || game.Screenshots.Count == 0))
        {
            game.Screenshots = metadata.Screenshots
                .Select(UrlValidator.SanitizePersistedUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url!)
                .ToList();
        }

        if (metadata.CriticScore.HasValue)
            game.CriticScore = metadata.CriticScore;

        if (metadata.CommunityScore.HasValue)
            game.CommunityScore = metadata.CommunityScore;

        if (GameSource.IsUserManaged(game.SourceId) &&
            string.IsNullOrWhiteSpace(game.ExternalId) &&
            uint.TryParse(metadata.ExternalId, out _))
        {
            game.ExternalId = metadata.ExternalId.Trim();
        }

        if (metadata.UserScore.HasValue)
            game.UserScore = metadata.UserScore;

        if (metadata.TimeToBeatMainSeconds is > 0 &&
            (overwrite || game.TimeToBeatMainSeconds is null or 0))
        {
            game.TimeToBeatMainSeconds = metadata.TimeToBeatMainSeconds;
        }

        if (metadata.TimeToBeatExtraSeconds is > 0 &&
            (overwrite || game.TimeToBeatExtraSeconds is null or 0))
        {
            game.TimeToBeatExtraSeconds = metadata.TimeToBeatExtraSeconds;
        }

        if (metadata.TimeToBeatCompleteSeconds is > 0 &&
            (overwrite || game.TimeToBeatCompleteSeconds is null or 0))
        {
            game.TimeToBeatCompleteSeconds = metadata.TimeToBeatCompleteSeconds;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Version))
            game.Version = metadata.Version;

        // Merge links instead of replacing — keep store links and add social ones. Dedupe by URL.
        if (metadata.Links is { Count: > 0 })
        {
            var known = new HashSet<string>(game.Links.Select(l => l.Url), StringComparer.OrdinalIgnoreCase);
            foreach (var link in metadata.Links.Where(l => !string.IsNullOrWhiteSpace(l.Url)))
            {
                var sanitized = Bridge.Core.Utilities.UrlValidator.SanitizePersistedUrl(link.Url);
                if (sanitized is null)
                    continue;

                if (known.Add(sanitized))
                    game.Links.Add(new Link { Name = link.Name, Url = sanitized });
            }
        }

        if (renameFromMetadata)
            game.Name = metadata.Name.Trim();
    }

    // Steam-sourced links (store/community/guides/news/wiki) are identified by
    // their domain; anything else (YouTube, Reddit, Wikipedia, official site,
    // social networks) is a non-Steam link — the IGDB enrichment target.
    private static bool IsSteamLink(string name) => name switch
    {
        "Community Hub" or "Discussions" or "Guides" or "News" or
        "Steam Store" or "PCGamingWiki" or "Achievements" or "Workshop" => true,
        _ => false
    };

    // Resolve metadata names into real reference-entity ids (Genre/Company/
    // Platform) via GetOrCreateByName — the same mechanism Bridge.Import uses
    // for Steam data (see ADR-7 for why Developer/Publisher share one Company
    // table). Appends to the existing id lists without duplicating ids.
    private void ApplyMetadataReferences(Game game, GameMetadata metadata)
    {
        if (metadata.Genres is { Count: > 0 })
        {
            foreach (var genreName in metadata.Genres)
            {
                var genre = _genreRepository.GetOrCreateByName(genreName);
                if (!game.GenreIds.Contains(genre.Id))
                    game.GenreIds.Add(genre.Id);
            }
        }

        if (metadata.Developers is { Count: > 0 })
        {
            foreach (var name in metadata.Developers)
            {
                var company = _companyRepository.GetOrCreateByName(name);
                if (!game.DeveloperIds.Contains(company.Id))
                    game.DeveloperIds.Add(company.Id);
            }
        }

        if (metadata.Publishers is { Count: > 0 })
        {
            foreach (var name in metadata.Publishers)
            {
                var company = _companyRepository.GetOrCreateByName(name);
                if (!game.PublisherIds.Contains(company.Id))
                    game.PublisherIds.Add(company.Id);
            }
        }

        if (metadata.Platforms is { Count: > 0 })
        {
            foreach (var name in metadata.Platforms)
            {
                var platform = _platformRepository.GetOrCreateByName(name);
                if (!game.PlatformIds.Contains(platform.Id))
                    game.PlatformIds.Add(platform.Id);
            }
        }

        if (metadata.Categories is { Count: > 0 })
        {
            foreach (var name in metadata.Categories)
            {
                var category = _categoryRepository.GetOrCreateByName(name);
                if (!game.CategoryIds.Contains(category.Id))
                    game.CategoryIds.Add(category.Id);
            }
        }

        if (metadata.Tags is { Count: > 0 })
        {
            foreach (var name in metadata.Tags)
            {
                var tag = _tagRepository.GetOrCreateByName(name);
                if (!game.TagIds.Contains(tag.Id))
                    game.TagIds.Add(tag.Id);
            }
        }

        if (metadata.Features is { Count: > 0 })
        {
            foreach (var name in metadata.Features)
            {
                var feature = _featureRepository.GetOrCreateByName(name);
                if (!game.FeatureIds.Contains(feature.Id))
                    game.FeatureIds.Add(feature.Id);
            }
        }

        if (metadata.Series is { Count: > 0 })
        {
            foreach (var name in metadata.Series)
            {
                var series = _seriesRepository.GetOrCreateByName(name);
                if (!game.SeriesIds.Contains(series.Id))
                    game.SeriesIds.Add(series.Id);
            }
        }

        if (metadata.AgeRatings is { Count: > 0 })
        {
            foreach (var name in metadata.AgeRatings)
            {
                var ageRating = _ageRatingRepository.GetOrCreateByName(name);
                if (!game.AgeRatingIds.Contains(ageRating.Id))
                    game.AgeRatingIds.Add(ageRating.Id);
            }
        }

        if (metadata.Regions is { Count: > 0 })
        {
            foreach (var name in metadata.Regions)
            {
                var region = _regionRepository.GetOrCreateByName(name);
                if (!game.RegionIds.Contains(region.Id))
                    game.RegionIds.Add(region.Id);
            }
        }

        InvalidateMetadataReferenceCaches();
        if (ReferenceEquals(SelectedGame, game))
            RefreshReferenceFields(game);
    }

    // Metadata providers can add genres/platforms/etc. — drop only those caches,
    // not completion-status names (unaffected by metadata and used by the hero
    // badge + details row).
    private void InvalidateMetadataReferenceCaches()
    {
        _companyNames = null;
        _platformNames = null;
        _genreNames = null;
        _categoryNames = null;
        _tagNames = null;
        _featureNames = null;
        _seriesNames = null;
        _ageRatingNames = null;
        _regionNames = null;
    }

    private Game? TryGetLiveGame(Game game) =>
        Games.FirstOrDefault(g => g.Id == game.Id);

    private async Task DownloadMissingSteamMetadataAsync(Guid steamSourceId)
    {
        // Use the in-memory library — _gameRepository.GetAll() returns detached
        // snapshots (AsNoTracking), and Games.Contains() is reference-based, so
        // a repository round-trip made every startup sync a no-op.
        var allSteam = Games
            .Where(g => g.SourceId == steamSourceId)
            .ToList();

        // Two distinct needs, handled differently so we don't re-download a
        // game's full Steam metadata on every open just to fetch a missing link:
        //  - Missing a description → fetch the full Steam metadata (+ IGDB links).
        //  - Has a description but no IGDB social links → only call our IGDB
        //    Worker for the links; no Steam re-download.
        var now = DateTime.Now;
        var needMetadata = allSteam
            .Where(g => NeedsMetadataRefresh(g, now))
            .ToList();
        var needLinksOnly = allSteam
            .Where(g => !string.IsNullOrWhiteSpace(g.Description) &&
                        !g.Links.Any(l => !IsSteamLink(l.Name)) &&
                        !NeedsMetadataRefresh(g, now))
            .ToList();

        int applied = 0;
        var totalWork = needMetadata.Count + needLinksOnly.Count;
        var completed = 0;
        if (totalWork > 0)
        {
            BeginStatusProgress(indeterminate: totalWork <= 1);
            ReportBatchProgress(0, totalWork);
        }

        _suspendDetailedRows++;
        try
        {
            if (needMetadata.Count > 0)
            {
                SetStatus(Strings.Format(nameof(Strings.DownloadingMetadataForGamesFormat), needMetadata.Count), StatusMessageKind.Normal);

                // Fetch the HTTP payloads with bounded parallelism (4 at a time): the
                // requests are the slow part, and firing all of them at once would trip
                // Steam's store throttling (429s → "partial" metadata). Task.Run puts
                // the work on pool threads so the HTTP continuations don't come back to
                // the UI thread; only reads (game.ExternalId) happen off the UI thread
                // here — entity mutation and the DbContext saves stay on the UI thread
                // in the loop below.
                using var throttle = new SemaphoreSlim(4);
                var results = await Task.WhenAll(needMetadata.Select(game =>
                    Task.Run(async () =>
                    {
                        await throttle.WaitAsync();
                        try
                        {
                            return (game, metadata: await _steamMetadataProvider.GetByAppIdAsync(game.ExternalId));
                        }
                        finally
                        {
                            throttle.Release();
                            if (totalWork > 0)
                            {
                                var done = Interlocked.Increment(ref completed);
                                RunOnUiThread(() => ReportBatchProgress(done, totalWork));
                            }
                        }
                    })));

                foreach (var (game, metadata) in results)
                {
                    if (metadata is null)
                        continue;

                    try
                    {
                        // This sync runs after the window is interactive — the game may
                        // have been deleted (or had its actions edited) while the awaits
                        // above were in flight. Only mutate/save what's still live.
                        var live = TryGetLiveGame(game);
                        if (live is null)
                            continue;

                        // Steam provides store/community links; our IGDB Worker adds the
                        // social links (YouTube, Reddit, ...) so the automatic sync is
                        // complete, not just the manual one.
                        await _metadataSync.EnrichSteamLinksFromIgdbAsync(live.Name, metadata);

                        ApplyMetadata(live, metadata, overwrite: false);
                        ApplyMetadataReferences(live, metadata);
                        await TryEnrichArtworkFromSteamGridDbAsync(live, overwrite: false);
                        ApplySteamLocalArtwork(live);

                        _gameRepository.Update(live);
                        RefreshListDisplay(live);
                        applied++;
                    }
                    catch (Exception ex)
                    {
                        // One bad game shouldn't abort the whole sync — log and continue.
                        App.LogException(ex);
                    }
                }
            }

            // Games that already have their description but never got the IGDB social
            // links (e.g. the Worker was unreachable on a previous run). Add just the
            // links — a light IGDB call, no Steam metadata re-download.
            foreach (var game in needLinksOnly)
            {
                try
                {
                    var live = TryGetLiveGame(game);
                    if (live is null)
                        continue;

                    var metadata = new GameMetadata();
                    await _metadataSync.EnrichSteamLinksFromIgdbAsync(live.Name, metadata);
                    if (metadata.Links.Count == 0)
                        continue;

                    ApplyMetadata(live, metadata, overwrite: false);
                    _gameRepository.Update(live);
                    RefreshListDisplay(live);
                    applied++;
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                }
                finally
                {
                    if (totalWork > 0)
                    {
                        completed++;
                        ReportBatchProgress(completed, totalWork);
                    }
                }
            }

            // Batch seal markers for all attempted syncs (success or fail)
            // Respects TTL to prevent perpetual re-downloads on every startup
            _gameRepository.UpdateManyMetadataSyncMarkers(needMetadata, MetadataSyncMarker.Metadata);
            if (needLinksOnly.Count > 0)
                _gameRepository.UpdateManyMetadataSyncMarkers(needLinksOnly, MetadataSyncMarker.Links);

        }
        finally
        {
            EndDetailRowSuspension();
            if (totalWork > 0)
                EndStatusProgress();
        }

        if (applied > 0)
            SetStatus(Strings.Format(nameof(Strings.MetadataSyncCompleteFormat), applied, needMetadata.Count + needLinksOnly.Count), StatusMessageKind.Normal);
        else if (needMetadata.Count + needLinksOnly.Count > 0 && !IsNetworkAvailable())
            SetStatus(Strings.MetadataSyncNoInternet, StatusMessageKind.Normal);
        else
            SetStatus(Strings.Format(nameof(Strings.MetadataSyncNoUpdatesFormat), needMetadata.Count + needLinksOnly.Count), StatusMessageKind.Normal);
    }

    // Downloads metadata for games from sources without an appid-based lookup
    // (Epic, manual non-Steam games): search each provider chain by display name
    // and apply the best match. Same bounded-parallelism pattern as the Steam
    // sync, minus the appid shortcut. The chain starts with our own IGDB Worker,
    // which resolves Epic-only games correctly (unlike Steam-by-name).
    private async Task DownloadMissingMetadataByNameAsync(IReadOnlyList<Guid> sourceIds)
    {
        var now = DateTime.Now;
        var candidates = Games
            .Where(g => sourceIds.Contains(g.SourceId) && NeedsMetadataRefresh(g, now))
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        SetStatus(Strings.Format(nameof(Strings.DownloadingMetadataForGamesFormat), candidates.Count), StatusMessageKind.Normal);

        var completed = 0;
        var total = candidates.Count;
        BeginStatusProgress(indeterminate: total <= 1);
        ReportBatchProgress(0, total);
        _suspendDetailedRows++;
        try
        {
            using var throttle = new SemaphoreSlim(4);
            var results = await Task.WhenAll(candidates.Select(game =>
                Task.Run(async () =>
                {
                    await throttle.WaitAsync();
                    try
                    {
                        var found = await _metadataSync.SearchByNameChainAsync(game.Name);
                        return found is null
                            ? (game, metadata: (GameMetadata?)null)
                            : (game, metadata: found.Value.Metadata);
                    }
                    finally
                    {
                        throttle.Release();
                        var done = Interlocked.Increment(ref completed);
                        RunOnUiThread(() => ReportBatchProgress(done, total));
                    }
                })));

            int applied = 0;
            foreach (var (game, metadata) in results)
            {
                if (metadata is null)
                    continue;

                try
                {
                    var live = TryGetLiveGame(game);
                    if (live is null)
                        continue;

                    ApplyMetadata(live, metadata, overwrite: false);
                    ApplyMetadataReferences(live, metadata);
                    await TryEnrichArtworkFromSteamGridDbAsync(live, overwrite: false);
                    ApplySteamLocalArtwork(live);

                    _gameRepository.Update(live);
                    RefreshListDisplay(live);
                    applied++;
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                }
            }

            // Batch seal metadata markers
            _gameRepository.UpdateManyMetadataSyncMarkers(candidates, MetadataSyncMarker.Metadata);

            if (applied > 0)
                SetStatus(Strings.Format(nameof(Strings.MetadataSyncCompleteFormat), applied, candidates.Count), StatusMessageKind.Normal);
            else if (!IsNetworkAvailable())
                SetStatus(Strings.MetadataSyncNoInternet, StatusMessageKind.Normal);
            else
                SetStatus(Strings.Format(nameof(Strings.MetadataSyncNoUpdatesFormat), candidates.Count), StatusMessageKind.Normal);
        }
        finally
        {
            EndDetailRowSuspension();
            EndStatusProgress();
        }
    }

    // ROMs are not tied to a store source id, so fill missing metadata on
    // startup/refresh by searching IGDB with normalized Spanish/English titles.
    private async Task DownloadMissingRomMetadataAsync()
    {
        var now = DateTime.Now;
        var candidates = Games
            .Where(g => g.Roms.Count > 0 && NeedsMetadataRefresh(g, now))
            .ToList();

        if (candidates.Count == 0)
            return;

        SetStatus(Strings.Format(nameof(Strings.DownloadingMetadataForGamesFormat), candidates.Count), StatusMessageKind.Normal);

        var completed = 0;
        var total = candidates.Count;
        BeginStatusProgress(indeterminate: total <= 1);
        ReportBatchProgress(0, total);
        _suspendDetailedRows++;
        try
        {
            using var throttle = new SemaphoreSlim(4);
            var results = await Task.WhenAll(candidates.Select(game =>
                Task.Run(async () =>
                {
                    await throttle.WaitAsync();
                    try
                    {
                        var found = await _metadataSync.SearchRomMetadataAsync(game.Name, GetPrimaryRomPath(game));
                        return found is null
                            ? (game, metadata: (GameMetadata?)null, provider: (string?)null)
                            : (game, metadata: found.Value.Metadata, provider: found.Value.ProviderName);
                    }
                    finally
                    {
                        throttle.Release();
                        var done = Interlocked.Increment(ref completed);
                        RunOnUiThread(() => ReportBatchProgress(done, total));
                    }
                })));

            int applied = 0;
            foreach (var (game, metadata, providerName) in results)
            {
                if (metadata is null || providerName is null)
                    continue;

                try
                {
                    var live = TryGetLiveGame(game);
                    if (live is null)
                        continue;

                    if (providerName == _steamMetadataProvider.Name)
                        await _metadataSync.EnrichSteamLinksFromIgdbAsync(live.Name, metadata);

                    ApplyMetadata(live, metadata, overwrite: false);
                    ApplyMetadataReferences(live, metadata);
                    await TryEnrichArtworkFromSteamGridDbAsync(live, overwrite: false);
                    ApplySteamLocalArtwork(live);

                    _gameRepository.Update(live);
                    RefreshListDisplay(live);
                    applied++;
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                }
            }

            // Batch seal metadata markers
            _gameRepository.UpdateManyMetadataSyncMarkers(candidates, MetadataSyncMarker.Metadata);

            if (applied > 0)
                SetStatus(Strings.Format(nameof(Strings.MetadataSyncCompleteFormat), applied, candidates.Count), StatusMessageKind.Normal);
        }
        finally
        {
            EndDetailRowSuspension();
            EndStatusProgress();
        }
    }

    // Downloads metadata for Bridge-managed external installs (Steam-first by
    // name, same as the post-scan import path).
    private async Task DownloadMissingBridgeMetadataAsync(Guid bridgeSourceId)
    {
        var now = DateTime.Now;
        var candidates = Games
            .Where(g => g.SourceId == bridgeSourceId && NeedsMetadataRefresh(g, now))
            .ToList();

        if (candidates.Count == 0)
            return;

        SetStatus(Strings.Format(nameof(Strings.DownloadingMetadataForGamesFormat), candidates.Count), StatusMessageKind.Normal);

        var completed = 0;
        var total = candidates.Count;
        BeginStatusProgress(indeterminate: total <= 1);
        ReportBatchProgress(0, total);
        _suspendDetailedRows++;
        try
        {
            using var throttle = new SemaphoreSlim(4);
            var results = await Task.WhenAll(candidates.Select(game =>
                Task.Run(async () =>
                {
                    await throttle.WaitAsync();
                    try
                    {
                        var found = await _metadataSync.SearchForAddedGameAsync(game.Name, romImport: false);
                        return found is null
                            ? (game, metadata: (GameMetadata?)null, provider: (string?)null)
                            : (game, metadata: found.Value.Metadata, provider: found.Value.ProviderName);
                    }
                    finally
                    {
                        throttle.Release();
                        var done = Interlocked.Increment(ref completed);
                        RunOnUiThread(() => ReportBatchProgress(done, total));
                    }
                })));

            int applied = 0;
            foreach (var (game, metadata, providerName) in results)
            {
                if (metadata is null || providerName is null)
                    continue;

                try
                {
                    var live = TryGetLiveGame(game);
                    if (live is null)
                        continue;

                    if (providerName == _steamMetadataProvider.Name)
                        await _metadataSync.EnrichSteamLinksFromIgdbAsync(live.Name, metadata);

                    ApplyMetadata(live, metadata, overwrite: false);
                    ApplyMetadataReferences(live, metadata);
                    await TryEnrichArtworkFromSteamGridDbAsync(live, overwrite: false);
                    ApplySteamLocalArtwork(live);

                    _gameRepository.Update(live);
                    RefreshListDisplay(live);
                    applied++;
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                }
            }

            // Batch seal metadata markers
            _gameRepository.UpdateManyMetadataSyncMarkers(candidates, MetadataSyncMarker.Metadata);

            if (applied > 0)
                SetStatus(Strings.Format(nameof(Strings.MetadataSyncCompleteFormat), applied, candidates.Count), StatusMessageKind.Normal);
        }
        finally
        {
            EndDetailRowSuspension();
            EndStatusProgress();
        }
    }

    private async Task DownloadMissingHowLongToBeatAsync()
    {
        var now = DateTime.Now;
        var candidates = Games
            .Where(g => (g.TimeToBeatMainSeconds is null or 0 ||
                         g.TimeToBeatExtraSeconds is null or 0 ||
                         g.TimeToBeatCompleteSeconds is null or 0) &&
                        (g.TimeToBeatSyncedAt == null || now - g.TimeToBeatSyncedAt > METADATA_SYNC_TTL))
            .ToList();

        if (candidates.Count == 0)
            return;

        using var throttle = new SemaphoreSlim(2);

        var updatedGames = new List<Game>();
        _suspendDetailedRows++;
        try
        {
            foreach (var batch in candidates.Chunk(8))
            {
                var batchResults = await Task.WhenAll(batch.Select(game => Task.Run(async () =>
                {
                    await throttle.WaitAsync();
                    try
                    {
                        var live = TryGetLiveGame(game);
                        if (live is null)
                            return (Game?)null;
                        if (!await _howLongToBeat.TryEnrichGameAsync(live))
                            return null;
                        return live;
                    }
                    catch (Exception ex)
                    {
                        App.LogException(ex);
                        return null;
                    }
                    finally
                    {
                        throttle.Release();
                    }
                })));
                var toUpdate = batchResults.Where(g => g is not null).Cast<Game>().ToList();
                if (toUpdate.Count > 0)
                {
                    RunOnUiThread(() =>
                    {
                        foreach (var live in toUpdate)
                        {
                            _gameRepository.Update(live);
                            RefreshListDisplay(live);
                        }
                    });
                    updatedGames.AddRange(toUpdate);
                }
            }

            // Batch seal TimeToBeat markers
            _gameRepository.UpdateManyMetadataSyncMarkers(candidates, MetadataSyncMarker.TimeToBeat);

        }
        finally
        {
            EndDetailRowSuspension();
        }
    }

    private async Task TryEnrichLogoAsync(Game game, bool overwrite = true)
    {
        if (_steamGridDbClient is null || !_steamGridDbClient.IsConfigured)
            return;
        if (!string.IsNullOrWhiteSpace(game.LogoImage) && !overwrite)
            return;
        try
        {
            var search = await _steamGridDbClient.SearchGamesAsync(game.Name);
            if (search.Count == 0)
                return;
            var logos = await _steamGridDbClient.GetAssetsAsync(search[0].Id, SteamGridDbAssetKind.Logo);
            if (logos.Count == 0)
                return;
            var logo = logos[0].Url;
            if (!string.IsNullOrWhiteSpace(logo) && (overwrite || string.IsNullOrWhiteSpace(game.LogoImage)))
            {
                var sanitized = UrlValidator.SanitizePersistedUrl(logo);
                if (!string.IsNullOrWhiteSpace(sanitized))
                    game.LogoImage = sanitized;
            }
        }
        catch
        {
            // Best effort — logo is optional, never fail metadata sync.
        }
    }

    private async Task DownloadMissingLogosAsync()
    {
        var candidates = Games.Where(g =>
            string.IsNullOrWhiteSpace(g.LogoImage) ||
            string.IsNullOrWhiteSpace(g.CoverImage) ||
            HeroBackground.IsDefault(g.BackgroundImage) ||
            string.IsNullOrWhiteSpace(g.Icon)).ToList();
        if (candidates.Count == 0 || _steamGridDbClient is null || !_steamGridDbClient.IsConfigured)
            return;

        SetStatus(Strings.Format(nameof(Strings.DownloadingMetadataForGamesFormat), candidates.Count), StatusMessageKind.Normal);
        var completed = 0;
        var total = candidates.Count;
        BeginStatusProgress(indeterminate: total <= 1);
        ReportBatchProgress(0, total);
        _suspendDetailedRows++;
        try
        {
            using var throttle = new SemaphoreSlim(4);
            var results = await Task.WhenAll(candidates.Select(game =>
                Task.Run(async () =>
                {
                    await throttle.WaitAsync();
                    try
                    {
                        var live = TryGetLiveGame(game);
                        if (live is null)
                            return (live, false);
                        var hadLogo = !string.IsNullOrWhiteSpace(live.LogoImage);
                        var hadCover = !string.IsNullOrWhiteSpace(live.CoverImage);
                        var hadHero = !HeroBackground.IsDefault(live.BackgroundImage);
                        var hadIcon = !string.IsNullOrWhiteSpace(live.Icon);
                        if (hadLogo && hadCover && hadHero && hadIcon)
                            return (live, false);
                        await TryEnrichArtworkFromSteamGridDbAsync(live, overwrite: false);
                        var hasNew = (!hadLogo && !string.IsNullOrWhiteSpace(live.LogoImage)) ||
                                     (!hadCover && !string.IsNullOrWhiteSpace(live.CoverImage)) ||
                                     (!hadHero && !HeroBackground.IsDefault(live.BackgroundImage)) ||
                                     (!hadIcon && !string.IsNullOrWhiteSpace(live.Icon));
                        return (live, hasNew);
                    }
                    catch { return (null, false); }
                    finally
                    {
                        throttle.Release();
                        var done = Interlocked.Increment(ref completed);
                        RunOnUiThread(() => ReportBatchProgress(done, total));
                    }
                })));

            int applied = 0;
            foreach (var (live, hasNew) in results)
            {
                if (live is null || !hasNew)
                    continue;
                try
                {
                    _gameRepository.Update(live);
                    RefreshListDisplay(live);
                    applied++;
                }
                catch (Exception ex) { App.LogException(ex); }
            }

            if (applied > 0)
                SetStatus(Strings.Format(nameof(Strings.MetadataAppliedBatchFormat), applied, candidates.Count), StatusMessageKind.Normal);
        }
        finally
        {
            EndDetailRowSuspension();
            EndStatusProgress();
        }
    }

    private async Task TryEnrichArtworkFromSteamGridDbAsync(Game game, bool overwrite = false)
    {
        if (_steamGridDbClient is null || !_steamGridDbClient.IsConfigured)
            return;
        var needsLogo = overwrite || string.IsNullOrWhiteSpace(game.LogoImage);
        var needsCover = overwrite || string.IsNullOrWhiteSpace(game.CoverImage);
        var needsHero = overwrite || HeroBackground.IsDefault(game.BackgroundImage);
        var needsIcon = overwrite || string.IsNullOrWhiteSpace(game.Icon);
        if (!needsLogo && !needsCover && !needsHero && !needsIcon)
            return;
        try
        {
            var search = await _steamGridDbClient.SearchGamesAsync(game.Name);
            if (search.Count == 0)
                return;
            var gameId = search[0].Id;
            if (needsLogo)
            {
                var logos = await _steamGridDbClient.GetAssetsAsync(gameId, SteamGridDbAssetKind.Logo);
                if (logos.Count > 0)
                {
                    var sanitized = UrlValidator.SanitizePersistedUrl(logos[0].Url);
                    if (!string.IsNullOrWhiteSpace(sanitized) && (overwrite || string.IsNullOrWhiteSpace(game.LogoImage)))
                        game.LogoImage = sanitized;
                }
            }
            if (needsCover)
            {
                var covers = await _steamGridDbClient.GetAssetsAsync(gameId, SteamGridDbAssetKind.Cover);
                if (covers.Count > 0)
                {
                    var sanitized = UrlValidator.SanitizePersistedUrl(covers[0].Url);
                    if (!string.IsNullOrWhiteSpace(sanitized) && (overwrite || string.IsNullOrWhiteSpace(game.CoverImage)))
                        game.CoverImage = sanitized;
                }
            }
            if (needsHero)
            {
                var heroes = await _steamGridDbClient.GetAssetsAsync(gameId, SteamGridDbAssetKind.Hero);
                if (heroes.Count > 0)
                {
                    var sanitized = UrlValidator.SanitizePersistedUrl(heroes[0].Url);
                    if (!string.IsNullOrWhiteSpace(sanitized) && (overwrite || HeroBackground.IsDefault(game.BackgroundImage)))
                        game.BackgroundImage = sanitized;
                }
            }
            if (needsIcon)
            {
                var icons = await _steamGridDbClient.GetAssetsAsync(gameId, SteamGridDbAssetKind.Icon);
                if (icons.Count > 0)
                {
                    var sanitized = UrlValidator.SanitizePersistedUrl(icons[0].Url);
                    if (!string.IsNullOrWhiteSpace(sanitized) && (overwrite || string.IsNullOrWhiteSpace(game.Icon)))
                        game.Icon = sanitized;
                }
            }
        }
        catch { }
    }

    private static string? GetPrimaryRomPath(Game game) =>
        game.Roms.FirstOrDefault()?.Path;
}
