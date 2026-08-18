using Bridge.Core.Entities;
using Bridge.Core.Import;
using Bridge.Core.Utilities;
using Bridge.Emulation;
using Bridge.Metadata;
using Bridge.Resources;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
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

        StatusMessage = Strings.Format(nameof(Strings.DownloadingMetadataForGameFormat), game.Name);

        BeginStatusProgress(indeterminate: true);
        try
        {
            var result = await _metadataSync.SearchForManualDownloadAsync(
                gameName,
                romImport,
                game.SourceId != GameSource.ManualId ? game.ExternalId : null);

            if (result is null)
            {
                StatusMessage = IsNetworkAvailable()
                    ? Strings.Format(nameof(Strings.NoMetadataFoundFormat), gameName)
                    : Strings.NoInternetMetadataDeferred;
                return;
            }

            var (metadata, providerName) = result.Value;

            if (providerName == _steamMetadataProvider.Name)
                await _metadataSync.EnrichSteamLinksFromIgdbAsync(gameName, metadata);

            ApplyMetadata(game, metadata);
            ApplyMetadataReferences(game, metadata);
            ApplySteamLocalArtwork(game);

            _gameRepository.Update(game);
            RefreshListDisplay(game);
            StatusMessage = Strings.Format(nameof(Strings.MetadataAppliedToGameFormat), game.Name, providerName);
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
        var candidates = games.Where(g => string.IsNullOrWhiteSpace(g.Description)).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        if (!IsNetworkAvailable())
        {
            StatusMessage = Strings.Format(nameof(Strings.NoInternetMetadataDeferredForGamesFormat), candidates.Count);
            return;
        }

        StatusMessage = Strings.Format(nameof(Strings.DownloadingMetadataForGamesFormat), candidates.Count);

        var completed = 0;
        var total = candidates.Count;
        BeginStatusProgress(indeterminate: total <= 1);
        ReportBatchProgress(0, total);
        try
        {
            using var throttle = new SemaphoreSlim(4);
            var results = await Task.WhenAll(candidates.Select(game =>
                Task.Run(async () =>
                {
                    await throttle.WaitAsync();
                    try
                    {
                        var searchName = romImport ? RomScanner.ToSearchName(game.Name) : game.Name;
                        var found = await _metadataSync.SearchForAddedGameAsync(searchName, romImport);
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

            StatusMessage = applied > 0
                ? Strings.Format(nameof(Strings.MetadataAppliedBatchFormat), applied, candidates.Count)
                : Strings.Format(nameof(Strings.NoMetadataFoundForAddedGamesFormat), candidates.Count);
        }
        finally
        {
            EndStatusProgress();
        }
    }

    private static void ApplyMetadata(Game game, GameMetadata metadata, bool overwrite = true)
    {
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
            (overwrite || string.IsNullOrWhiteSpace(game.BackgroundImage)))
        {
            var background = UrlValidator.SanitizePersistedUrl(metadata.BackgroundImage);
            if (!string.IsNullOrWhiteSpace(background))
                game.BackgroundImage = background;
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

        if (metadata.UserScore.HasValue)
            game.UserScore = metadata.UserScore;

        if (!string.IsNullOrWhiteSpace(metadata.Version))
            game.Version = metadata.Version;

        // Merge links instead of replacing: Playnite shows the library links
        // (Steam store, community, ...) together with the social ones a
        // metadata provider adds (YouTube, Reddit, ...). Dedupe by URL.
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

        if (!IsNetworkAvailable())
        {
            StatusMessage = allSteam.Count > 0
                ? Strings.Format(nameof(Strings.NoInternetMetadataDeferredForGamesFormat), allSteam.Count)
                : Strings.NoInternetMetadataDeferred;
            return;
        }

        // Two distinct needs, handled differently so we don't re-download a
        // game's full Steam metadata on every open just to fetch a missing link:
        //  - Missing a description → fetch the full Steam metadata (+ IGDB links).
        //  - Has a description but no IGDB social links → only call our IGDB
        //    Worker for the links; no Steam re-download.
        var needMetadata = allSteam
            .Where(g => string.IsNullOrWhiteSpace(g.Description))
            .ToList();
        var needLinksOnly = allSteam
            .Where(g => !string.IsNullOrWhiteSpace(g.Description) &&
                        !g.Links.Any(l => !IsSteamLink(l.Name)))
            .ToList();

        int applied = 0;
        var totalWork = needMetadata.Count + needLinksOnly.Count;
        var completed = 0;
        if (totalWork > 0)
        {
            BeginStatusProgress(indeterminate: totalWork <= 1);
            ReportBatchProgress(0, totalWork);
        }

        try
        {
            if (needMetadata.Count > 0)
            {
                StatusMessage = Strings.Format(nameof(Strings.DownloadingMetadataForGamesFormat), needMetadata.Count);

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
        }
        finally
        {
            if (totalWork > 0)
                EndStatusProgress();
        }

        StatusMessage = applied > 0
            ? Strings.Format(nameof(Strings.MetadataSyncCompleteFormat), applied, needMetadata.Count + needLinksOnly.Count)
            : needMetadata.Count + needLinksOnly.Count > 0 && !IsNetworkAvailable()
                ? Strings.MetadataSyncNoInternet
                : Strings.Format(nameof(Strings.MetadataSyncNoUpdatesFormat), needMetadata.Count + needLinksOnly.Count);
    }

    // Downloads metadata for games from sources without an appid-based lookup
    // (Epic, manual non-Steam games): search each provider chain by display name
    // and apply the best match. Same bounded-parallelism pattern as the Steam
    // sync, minus the appid shortcut. The chain starts with our own IGDB Worker,
    // which resolves Epic-only games correctly (unlike Steam-by-name).
    private async Task DownloadMissingMetadataByNameAsync(IReadOnlyList<Guid> sourceIds)
    {
        var candidates = Games
            .Where(g => sourceIds.Contains(g.SourceId) && string.IsNullOrWhiteSpace(g.Description))
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        if (!IsNetworkAvailable())
        {
            StatusMessage = Strings.Format(nameof(Strings.NoInternetMetadataDeferredForGamesFormat), candidates.Count);
            return;
        }

        StatusMessage = Strings.Format(nameof(Strings.DownloadingMetadataForGamesFormat), candidates.Count);

        var completed = 0;
        var total = candidates.Count;
        BeginStatusProgress(indeterminate: total <= 1);
        ReportBatchProgress(0, total);
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

                    _gameRepository.Update(live);
                    RefreshListDisplay(live);
                    applied++;
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                }
            }

            StatusMessage = applied > 0
                ? Strings.Format(nameof(Strings.MetadataSyncCompleteFormat), applied, candidates.Count)
                : !IsNetworkAvailable()
                    ? Strings.MetadataSyncNoInternet
                    : Strings.Format(nameof(Strings.MetadataSyncNoUpdatesFormat), candidates.Count);
        }
        finally
        {
            EndStatusProgress();
        }
    }
}
