# Bridge - Project Plan

> **Status:** In development — MVP core loop complete (Fases 1–8). Fase 9 consolidation and audit batches are largely done; remaining gaps are tracked in the phase table below.
>
> **Last updated:** 2026-08-22 (achievements + RetroAchievements on main)

## Project Overview

Bridge is an original game library manager: a local catalog that unifies games from external libraries (Steam, Epic, and future sources), manually added games, and emulated ROMs into a single, fast, self-contained app. The project was originally inspired by [Playnite](https://playnite.link/) when it was first conceived; Bridge drops the plugin system and multi-frontend split to stay a small, verifiable core.

## Current State

### Phase 1 (Core & Persistence) — Done for MVP
Domain entities, local storage, and the app's composition root are built, wired together, and verified by launching the real `Bridge.exe`.

### Phase 2 (Minimal UI & Editing) — Done for MVP
WPF shell showing list + detail, manual add/edit, launching and playtime tracking, and basic statistics all work end-to-end.

### Phase 3 (Metadata, Emulation & Polish) — Metadata, Emulation, and visual polish done; Steam Store metadata added
Metadata download from IGDB (ADR-10) and Steam Store (HTTP anonymous, no login) both work; emulator configuration and ROM scanning work end-to-end. Steam games auto-import on startup and auto-download metadata from the Steam Store. Visual polish (Fase 7) and packaging (Fase 8) are complete — the UI is now WPF-UI 4.3.0 (Mica backdrop, title bar, sidebar navigation, statistics overlay) and the app ships as a single self-contained ~155 MB `.exe`. Consolidation (Fase 9) and audit batches are largely complete — see [Development Phases](#development-phases) for remaining gaps.

---

## Problem Statement

Game library managers are often built around a plugin ecosystem and a full desktop/fullscreen split, which is a lot of moving parts for a personal, single-maintainer tool. Bridge is a deliberately smaller take on the same problem space: a single app that keeps the parts that matter functionally (import, metadata, emulation, virtualized views, local persistence) and drops the parts that exist purely to support third-party extensibility.

---

## Solution Overview

A modular-monolith WPF application (no runtime plugins) split into internal-only modules — `Core`, `Storage`, `Import`, `Metadata`, `Emulation`, `App` — each with a single responsibility and no UI/domain mixing. Modularity here is for development and testability, not runtime extensibility. The app migrated from plain WPF to WPF UI (4.3.0) in Fase 7, once the functional core was stable. Packaged as a self-contained single-file `.exe` (same distribution model as this template's other projects).

---

## Technical Decisions

| Aspect | Decision |
|--------|----------|
| Runtime | .NET 10 |
| UI Framework | WPF-UI 4.3.0 — migrated from plain WPF in Fase 7, see [ADR-3](ARCHITECTURE.md#adr-3-wpf-ui-430-for-the-visual-overhaul-supersedes-the-original-plain-wpf-first-adr) |
| Architecture | Modular monolith, internal-only module boundaries, no plugin system in v1 |
| Local storage engine | **SQLite via EF Core** — decided and implemented in `Bridge.Storage`, see [ARCHITECTURE.md ADR-4](ARCHITECTURE.md#adr-4-local-storage-engine--sqlite-vs-litedb) |
| Library import strategy | Standard importer per source (`GetGames()` + dedupe by external GameId) to start; per-source custom importers only if a source genuinely needs one |
| Packaging | Single-file, self-contained `.exe` (`win-x64`), same release pipeline pattern as the template default |

---

## Scope: Current vs Future

### Unreleased (main)
- **Achievements** — Steam, Epic, and RetroAchievements ROM progress in the detail panel; RetroArch rcheevos unlock-on-play; No-Intro DAT ROM naming on scan

### Current Version (0.8.0)
- **SteamGridDB artwork** — optional community icons, covers, and hero banners in the game editor (API key setup, picker with preview)
- **Change art** — context menu shortcut to the Media tab
- **Hero banner modes** — Default / Black / Custom with preservation across Steam sync
- **Detail metadata filters** — filter the library from clickable detail fields
- **Translucent UI** — optional blurred background and semi-transparent sidebar
- **Redesigned artwork pickers** — responsive web search and SteamGridDB grids with side preview

### Previous (0.7.0)
- **How Long to Beat** — completion-time estimates from howlongtobeat.com, synced on metadata download and startup/refresh; segmented playtime progress bar in the Details hero stats bar
- **Detail section layout** — Settings and context menu to place Details left or right of Overview (full Details view and Covers compact panel)
- **Covers UI polish** — compact panel with screenshot strip at top (no tabs), icon-only Play/Info on cover hover, selection ring only when selected

### Previous (0.6.0)
- **Refresh Library** — logo menu command re-runs startup sync: Steam/Epic import, configured folder rescans, missing-metadata download (`RefreshLibraryCoreAsync`)
- **RetroArch cheats** — libretro-database fetch/cache, `CheatsWindow`, optional auto-apply on launch, ROM-only context menu item
- **Compressed ROM archives** — `.zip`/`.7z` scan and launch via RetroArch-style `archive#entry` paths
- **Faster RetroArch exit detection** — process `WaitForExitAsync` instead of idle polling

### Previous (0.5.0)
The MVP defined in the foundation notes:
- Open the app, create/save/load/edit games, delete
- Add games manually
- **Detect and import installed Steam games automatically on startup** — `Bridge.Import`/`SteamLibraryImporter`, see [ARCHITECTURE.md ADR-11](ARCHITECTURE.md#adr-11-steam-library-detection--local-files-only-hand-rolled-vdf-parser-bridgeimport-created-for-real). Verified against a real Steam installation (29 real games, 2 library folders) before writing synthetic tests. Since 2026-08-14 the import also brings **real Steam playtime** from `userdata\*\config\localconfig.vdf` via `SteamLocalPlaytimeResolver` (zero-config, local-only — see ADR-11)
- **Steam Store metadata on import** — newly imported Steam games auto-download metadata from the official Steam store (name, description, release date, cover/background art, critic/community scores, developers, publishers, genres, platforms, features, links — all via public HTTP endpoints, no login, no API key). Manual metadata search tries IGDB first, then falls back to Steam Store for non-Steam games.
- **Steam icons in the library list** — each Steam game shows the square 32x32 clienticon Steam caches locally (`appcache\librarycache\{appid}`), falling back to the `header.jpg` URL when no cached icon exists
- List + detail views with basic selection
- **Launch Steam games with an auto-resolved play action** — Steam-imported games play via `steam://rungameid/{appid}` without any per-game setup, with directory-based playtime tracking (see Fase 3 in the phase table below)
- Basic statistics (totals, installed/not installed, favorites, total playtime)
- **Zero-setup ROM support** — recursively scans folders, detects supported systems from ROM extensions (`RomPlatformCatalog`), imports each ROM as a game with a managed "Bridge RetroArch" play action, enriches it through the IGDB metadata pipeline, and installs/updates Bridge-managed RetroArch + the required core on first play. The Play button reads **Download** (then **Downloading…**) until the frontend/core is installed. See the Managed Emulation section in DEVELOPMENT.md
- Local image caching for covers/icons
- **WPF-UI visual overhaul (Fase 7)** — `FluentWindow` with Mica backdrop, 3-zone title bar (search, view-mode toggles, overflow menu), 52px icon sidebar (Library / Favorites / Sources / Statistics / Settings), themed List/Covers/Table views, and a full-width Statistics overlay
- **Scan installed games automatically** — `ScanInstalledWindow` + `InstalledGameDetector` detect games installed on the PC from Start Menu shortcuts / a chosen folder / a browsed executable (the "Scan Automatically / Add Game Installed" pattern) and import them as manual games
- **Epic Games support** — `Bridge.Import/Epic/` detects installed Epic games from the launcher's local files (`LauncherInstalled.dat` + `.item` manifests), filters Unreal Engine/DLC/plugins, launches via `com.epicgames.launcher://` (directory tracking), and shows the installed exe's icon. See ADR-13.
- **Own Cloudflare Worker as the IGDB backend** — `Bridge.Infra/igdb-proxy-worker/` + `BridgeIgdbProvider` give IGDB metadata with zero user configuration (credentials as Worker Secrets server-side). A legacy public IGDB proxy and a user-configured IGDB key are fallbacks. The Worker also returns IGDB's real screenshots (mapped to `Game.Screenshots` at `t_1080p`), so Epic/manual games get the Table-view screenshot gallery like Steam games. See ADR-13.
- **Web image search in the editor** — `ImageSearchWindow` + `WebImageSearchService` let the user pick Icon/Cover/Background art from web image-search results (field-aware layout and preview; plus a local-file browse fallback)
- **SteamGridDB artwork (optional)** — `SteamGridDbClient` + `SteamGridDbPickerWindow` for community icon/cover/hero art when the user configures a free API key (`SteamGridDbSettingsStore`)
- **Configurable themes** — `ThemeManager` + `ThemeColorWindow`: 9 accent presets plus a custom color picker, applied at runtime and persisted to `theme.json`
- **Rich descriptions** — Steam descriptions are stored and rendered as `DescriptionBlocks` (text / heading / subheading / list / image blocks), not a single raw string
- **Self-updating** — `AppUpdateService` checks GitHub Releases (`ZavalaSebas/Bridge`) against the assembly version: silently at startup and on demand via **Check for updates…** in the app menu, then downloads the new `Bridge.exe` and applies the safe swap (running exe → `.old`, downloaded → current, restart) with an update handshake that keeps `.old` as a rollback copy until the new exe proves it starts (`ConfirmUpdateApplied`), restores it via `RollbackToPrevious` if startup fails, and backs up `bridge.db` → `bridge.db.bak-update` before each update. "Not now" on the confirm dialog keeps the update pending in the title bar (download button next to random game) until applied. Only in the published single-file build (`CanSelfUpdate`); security-bound to GitHub hosts over HTTPS with a 256 MB cap. See the "Version Management" section in DEVELOPMENT.md
- **Settings hub (v0.4.0)** — sidebar **Settings** opens a unified preferences overlay: integrations, appearance (theme, English/Spanish, system tray), library backup & restore, updates (check, beta channel, start with Windows), and About
- **English / Spanish UI** — `language.txt` + `Strings.es.resx`; restart on language change
- **Library backup & restore** — portable `.zip` of database, preferences, and artwork cache; staged restore on restart
- **System tray icon** — close minimizes to tray by default; double-click or context menu to reopen
- **Start with Windows** — optional Run-key registration (published exe only)
- **First-run setup wizard (v0.5.0)** — profile (name + avatar), Steam/Epic detection, external games folder, ROM folder; skips existing users
- **What's New on update (v0.5.0)** — summarized release notes from embedded `CHANGELOG.md` after each app update
- **Watched scan folders (v0.5.0)** — persisted ROM and installed-game folders auto-import new files on startup and when they appear (`WatchedScanFolderService`)
- **User profile (v0.5.0)** — display name + avatar in Statistics; editable in Settings → Profile
- **ROMs sidebar (v0.5.0)** — filters the library to games with ROMs
- **Detail panel position / keep selection (v0.5.0)** — dock detail panel left or right; optional keep selection when switching List/Covers/Table

### Future Versions — Backlog
- Additional library sources beyond Steam and Epic (GOG, itch.io, ...)
- Full metadata provider pipeline (`SkipExistingValues` semantics, result caching — multi-source field resolution already exists)
- Full emulation subsystem: multiple emulator profiles, scanner exclusions. (Bridge-managed RetroArch covers 15 systems today via `RomPlatformCatalog`; No-Intro DAT matching and RetroAchievements hash lookup exist for ROM titles and achievements; third-party emulators still need manual configuration.)
- Bulk/multi-game editing
- Fullscreen mode (explicitly deferred, not ruled out)
- Plugin system (explicitly deferred, not ruled out — the internal module boundaries are drawn so this remains possible later without a rewrite)

---

## Project Structure

```
Bridge/
├── Bridge.slnx
├── Bridge/              # WPF host app — created, and no longer just a scaffold:
│                        #   ViewModels/ (MainViewModel + partials, EmulatorSetupViewModel, EmulationSettingsViewModel, IgdbSettingsViewModel)
│                        #   Views/ (LibraryDetailView, SettingsOverlayView, …)
│                        #   Services/ (GameLauncher, MetadataSyncService, AppUpdateService, …)
│                        #   Settings/ (IgdbSettingsStore)
│                        #   Statistics/ (LibraryStatistics, GameSortComparer, GameGroupResolver)
│                        #   Converters/ (MetadataConverters — image, release date, playtime, group, short date)
│                        #   Styles/ (Theme.xaml — dark token palette), Fonts/ (InterVariable.ttf)
│                        #   Windows: MainWindow (CompactInfoPanel detail/info panel inline —
│                        #           not a separate window), GameEditWindow (in "New Game" mode),
│                        #           ScanRomWindow, EmulatorSetupWindow, EmulationSettingsWindow, IgdbSettingsWindow
├── Bridge.Core/         # Domain entities, contracts — created
├── Bridge.Storage/      # EF Core DbContext, repositories — created
├── Bridge.Import/       # created — SteamLibraryImporter, SteamLocalIconResolver, SteamLocalPlaytimeResolver, SteamPlayActions, VdfParser, SteamPaths
├── Bridge.Metadata/     # created — IgdbMetadataProvider/IgdbAuthClient/IgdbSettings
├── Bridge.Emulation/    # created — RomScanner, RomPlatformCatalog, RetroArchService, EmulationPaths
└── Bridge.Tests/        # 273 tests (269 unit + 4 integration), all passing (dotnet test Bridge.slnx)
│                        #   ViewModels/ (GameEditViewModelTests)
│                        #   Services/ (AppUpdateServiceTests, RomScannerTests, InstalledGameDetectorTests)
│                        #   Metadata/ (IgdbAuthClientTests, IgdbMetadataProviderTests,
│                        #               BridgeIgdbProviderTests, SteamDescriptionBlocksTests,
│                        #               SteamSearchRegexTests)
```

Flat layout — every project sits directly under the repo root, no `src/`/`tests/` wrapper folders.

**Module-boundary history (Code = Truth):** `Bridge.Metadata` and `Bridge.Import` were built as real separate projects during Fases 5 and 3 respectively. `Bridge.Emulation` was initially folded into `Bridge/Services/` while the vertical slice shipped; the 2026-08-18 consolidation batch extracted it into its own project. `GameLauncher` stays in `Bridge/Services` because it coordinates launch/playtime tracking across all game types, not just ROMs.

---

## Development Phases

> Numbered as in the original planning notes (`PROJECT_FOUNDATION.md`, §26) for traceability.

**Milestones:**

| Milestone | Description | Status |
|-----------|-------------|--------|
| Fase 0 — Base definition | Lock MVP scope, folder structure, storage engine (SQLite vs LiteDB), image storage format | Mostly done — scope/structure locked, storage engine decided ([ADR-4](ARCHITECTURE.md#adr-4-local-storage-engine--sqlite-vs-litedb)); image storage format still open |
| Fase 1 — Core & persistence | `Game`, `GameMetadata`, `Emulator`, `GameSource` entities **plus `GameAction` and `GameRom`** (`Game.GameActions`/`Game.Roms` are core fields, not an afterthought); repository; CRUD survives app restart | Done for MVP — `Bridge.Core`, `Bridge.Storage`, and the `Bridge` app's composition root (`Config.cs`, DI, real EF migrations via `MigrateToLatest()`) exist and were verified by launching `Bridge.exe` and confirming the real `bridge.db` gets the correct schema; the deliverable is covered by the storage tests (`Storage/GameRepositoryTests.cs`, `Storage/RepositoryTests.cs`) |
| Fase 2 — Minimal UI | MainWindow, MainViewModel, `LibraryDetailView`; list loads, detail responds to selection | Functionally done for MVP and expanded — `MainViewModel` loads `Games` from `IGameRepository`; the library + detail zone lives in `Bridge/Views/LibraryDetailView.xaml` (extracted from `MainWindow.xaml` in the consolidation batch). Verified by launching `Bridge.exe` (empty and seeded) and confirming the window comes up (`Responding: True`, correct title) — not yet visually screenshotted (no GUI-capture tool available in this environment). **Global actions moved into the title bar overflow menu** (Add Game, Import Steam Library, Exit; Scan ROMs, Configure Emulator, IGDB Settings) so the main page only shows the library list + detail panel — `GameEditWindow` (in "New Game" mode) feeds `AddGameToLibrary`; `ScanRomWindow` is a small prompt dialog that feeds the public `ScanRomFolder` method, keeping the main window clean (the Fase 7 visual pass kept this arrangement, restyled as WPF-UI). **Three view modes now** (`ViewMode` enum, switched via title bar icon toggles; a ComboBox before Fase 7): List (original list+detail), Covers (cover wall with hover Play/Info via a `CompactInfoPanel` — an inline panel in `MainWindow`, not a separate window), and Table (`ViewMode.Table` — flat `ListView`/`GridView` with Name/Release Date/Genre/Last Played/Time Played/Library columns). The left `ListBox` collapses in Covers/Table; all views share `GamesView`, so search/filter/sort/group apply in every mode. On startup, MainViewModel.SelectInitialGame selects the most recently played game (LastActivity), falling back to the first game when nothing has been played yet |
| Fase 3 — Manual edit & import, **play & track** | Create/edit/delete, favorite/hidden flags, manual metadata; **launch a game via its `GameAction` and track playtime** (poll-based process/directory monitoring) — this is its own explicit deliverable, not a side effect of "playtime updates" | Create/edit/delete + favorite/hidden work end-to-end (`GameEditWindow` in "New Game" mode → `AddGameToLibrary`; `SaveGameCommand`/`DeleteGameCommand`). **Play & track works end-to-end now**: `Bridge/Services/GameLauncher.cs` launches a `GameAction` and polls for exit — the same *behavioral approach* uses poll-based tracking (no `Process.Exited` event, a `Task.Delay` loop). Verified against a real 3-second child process: session length measured accurately, `PlayCount`/`LastActivity`/`PlaytimeSeconds` updated correctly, `GameStarted`/`GameStopped` events fire and are consumed by `MainViewModel` (`PlayGameCommand`, `StatusMessage`). UI has a "▶ Play" button. **Automatic Steam play action added** — Steam games get a runtime `steam://rungameid/{appid}` URL action: a Steam-imported game with no configured `GameAction` gets a runtime `steam://rungameid/{appid}` URL action, launched via `steam.exe -silent` (never the local exe — Steamworks DRM), tracked by `TrackingMode.Directory` (watch processes whose binary lives under `InstallDirectory`, since the launched process is `steam.exe`, not the game). Resolution logic lives in `Bridge.Import/Steam/SteamPlayActions.cs` (pure, unit-tested without Steam installed); the launcher only checks whether Steam's registry path exists. The UI's "Set Play Action" field was **removed** — Steam games resolve the action automatically, and the field only made sense for manual non-Steam games. **Deliberately scoped down**: `GameActionType.Url` is only wired for the auto-resolved Steam case (not as a general user-configured action), `Script` isn't supported, and there's no process-tree walking (`OriginalProcess`-like behavior for non-directory actions) — but **process-tree tracking was added (2026-08-14)** for File/Emulator actions (launcher-spawns-game-and-exits, e.g. Genshin): `ProcessTreeSnapshot` (Toolhelp32) + `ProcessTreeExpander` + `TrackProcessTreeAsync` keep the launched process *and its descendants* in the tree each poll, so the launcher exiting no longer ends the session. The Table hero's **Play button becomes Stop** while a game runs (Game.IsRunning raises INPC) and GameLauncher.Stop kills the processes (directory by path+name for elevated launchers, tree by PID); Stop cancels the tracking so the button always reverts, and a launch-aware idle grace (20s during the first 30s, 5s after) absorbs launcher spawn gaps without slowing close detection. The startup metadata sync no longer resets IsRunning (that crash-reset moved to LoadGames), so launching during the sync keeps the button in Stop. Also not done: manual metadata beyond Name/Description |
| Fase 4 — Basic statistics | Totals, installed/not installed, favorites/hidden, total playtime, top played | Done for MVP and expanded — `Bridge/Statistics/LibraryStatistics.cs` computes everything on the fly from the current `Games` list (no persisted stats entity — on-the-fly computation from the in-memory library). Verified against known test data (exact expected counts/totals). Now surfaced as a **Statistics overlay** in `MainWindow` — a sidebar button shows a full-width dashboard that hides the detail panel while open (before Fase 7 it was a tab in a right-panel `TabControl`) showing counts with percentages, total/average play time, total install size, completion status, and the Top Play Time list — recomputed after every Add/Delete/Save/GameStopped. The list itself now supports **search, filter presets (All/Favorite/Most Played/Recently Played), sorting** (22 fields via `GameSortComparer`, unit-tested in `Statistics/GameSortComparerTests.cs`) **and grouping** (21 fields via `GameGroupResolver`, unit-tested in `Statistics/GameGroupResolverTests.cs`) |
| Fase 5 — Metadata | Download name/description/images from IGDB (ADR-10) and Steam Store (HTTP anonymous); auto-download metadata for newly imported Steam games; multi-provider fallback chain (IGDB → Steam Store); cache results, respect existing values | **Unblocked and built with 2 providers** — user confirmed IGDB. `Bridge.Metadata` has `IgdbMetadataProvider` (verified against 9 tests with fake HTTP handler) and `SteamMetadataProvider` (calls `store.steampowered.com/api/appdetails` + `appreviews`, maps 12+ fields including Name/Description/ReleaseDate/CoverImage/BackgroundImage/**Screenshots**/CriticScore/CommunityScore/Developers/Publishers/Genres/Platforms/Features/Links, no login required). `MainViewModel` has `DownloadMetadataCommand` with multi-provider fallback and auto-metadata (`DownloadMissingSteamMetadataAsync` runs on startup for new Steam games). `IGameMetadataProvider` interface in `Bridge.Core.Contracts` for future provider additions. UI revamped to show cover image, release date, critic/community scores, background image, install directory, Developers/Publishers/Platforms, Features, Categories, Tags, Series, Age Rating, Region and Version (resolved to their reference entities via `ApplyMetadataReferences` in `MainViewModel`, labels collapse when empty; Steam platform slugs map to readable names). **Library icons added** — the list shows Steam's square 32x32 clienticon from the local `appcache\librarycache\{appid}` via `SteamLocalIconResolver` (the API's `clienticon` field is gone), falling back to the `header.jpg` URL (verified rendering by window capture: icons show correctly in the list). **Screenshot gallery added (2026-08-13)** — `SteamMetadataProvider` also collects `data.screenshots[].path_full` into `GameMetadata.Screenshots`; the Table view renders them via `Bridge/Controls/ScreenshotGallery` (see Fase 7). **IGDB screenshots added (2026-08-14)** — the own IGDB Worker now returns `screenshots` and `BridgeIgdbProvider` maps them to the same `GameMetadata.Screenshots` (at `t_1080p`), so Epic/manual games get the gallery too. **Not done**: caching results (re-downloads every time), `SkipExistingValues` (always overwrites), local image download (covers stored as raw URLs, not cached to disk) **Update (2026-08-13)**: zero-config IGDB via Bridge's own Cloudflare Worker (BridgeIgdbProvider, first in the chain) with legacy public IGDB proxy / user IGDB key / Steam-by-name as fallbacks - see ADR-13 |
| Fase 6 — Emulation | Detect emulators, scan ROMs, match & create/update entries | **Expanded (2026-08-17): Bridge-managed RetroArch** — `Bridge.Emulation/RetroArchService.cs` installs and maintains Bridge's own RetroArch (downloads the official `.7z` from Libretro's buildbot, extracts with SharpCompress, swaps atomically) and installs cores on demand; `Bridge.Emulation/RomPlatformCatalog.cs` is the curated platform→core table that recognizes 15 systems from ROM extensions. `RomScanner` scans **recursively**, filters companion files, and creates the managed "Bridge RetroArch" `GameAction`; `GameLauncher` (in `Bridge/Services`) launches with `-L {CorePath} {RomPath}`. The Play button shows Download/Downloading/Play/Stop based on install + run state, and scanning a folder auto-selects the first imported ROM. **Expanded (2026-08-22): No-Intro DAT matching** on scan (`RomDatMatcher`, clrmamepro DATs under AppData) for library titles; **RetroAchievements** progress in the detail panel and **rcheevos** unlock-on-play via managed RetroArch. **Still future scope**: multiple emulator profiles, scanner exclusions. **Third-party emulators** still need manual configuration (`EmulatorSetupWindow`); the managed catalog only covers Bridge's own RetroArch. **Not done**: emulator *detection* of already-installed third-party emulators |
| Fase 7 — Visual polish | WPF UI adoption, themes, Mica/Acrylic, light animation | **Done** — full WPF-UI 4.3.0 overhaul (see [ADR-3](ARCHITECTURE.md#adr-3-wpf-ui-430-for-the-visual-overhaul-supersedes-the-original-plain-wpf-first-adr)): `FluentWindow` with Mica backdrop, 3-zone `TitleBar` (search, view-mode toggles, overflow menu), 52px icon sidebar (Library / Favorites / Sources / Statistics / Settings), themed List/Covers/Table views (dark tokens in `Bridge/Styles/Theme.xaml`, Inter Variable font, `#007ACC` UI accent / `#10B981` Play accent), and a full-width Statistics overlay plus a Settings shortcuts hub. Measured post-overhaul baseline: ~2.6s cold start / ~180MB RAM (see DEVELOPMENT.md). The deferred `ApplicationThemeManager.Apply` experiment made cold start worse and was reverted. **Post-overhaul additions**: the hero background renders "cover-by-width" (`FadeImage.CoverByWidth` — always fills the window's width, vertical excess clipped, no side bars at any ratio/size) with a smooth cross-fade between games, and the Table view gained a **cinematic screenshot gallery** (`Bridge/Controls/ScreenshotGallery`) fed by `Game.Screenshots` (Steam `path_full` URLs, and IGDB `t_1080p` for Epic/manual games since 2026-08-14) — frosted-backdrop main image, drag-to-scroll thumbnail strip, counter, arrows, keyboard nav, full-window dark overlay and 4s auto-advance. **Covers info panel redesign (2026-08-14)**: the `CompactInfoPanel` hero fades into the panel background (no hard edge), the title is larger with the favorite star inline after it (follows the title's wrap lines, persists on click, same gold/pop as the Table hero star), Play/More/Edit moved onto the hero, a theme-colored square close button in the top-right, and links wrap into two columns. The gallery gained a `CompactMode` (thumbnail strip only, smaller tiles, tap → fullscreen overlay, no auto-advance) shown at the top of the info panel. The covers hover Play button and the info panel's Play both reuse the Table hero's animated Play/Stop template (the panel's binds to `SelectedGame.IsRunning` since its DataContext is the `MainViewModel`). Covers cards and the Table hero cover clip their images to rounded corners via `RoundedRectClipConverter` (Border.ClipToBounds ignores CornerRadius), the top row no longer clips on hover (card scales from `RenderTransformOrigin="0.5,0"`), and filter/sort/group menu items can no longer be visually unticked by clicking the active entry (`ReassertMenuChecks`) |
| Fase 8 — Packaging | Single-file self-contained publish, startup/RAM tuning, path/asset validation | Done for MVP — real finding: the documented `dotnet publish` command alone produced `Bridge.exe` **plus 6 sidecar native DLLs** (WPF's own native interop libs + native SQLite), not a true single file. Fixed by adding `IncludeNativeLibrariesForSelfExtract=true` and `DebugType=none` (via a new `Directory.Build.props`, applies repo-wide) to `Bridge.csproj` — re-verified: publish output is now exactly one `Bridge.exe` (~155 MB, no sidecars; 148 MB before the Fase 7 overhaul added WPF-UI/Inter). Tried `PublishReadyToRun=true` for startup speed on the pre-overhaul build — **made it slower** (2671ms vs ~2000ms average without it) and bigger (176MB vs 148MB), empirically not worth it, not enabled. Measured 3 real cold-start runs of the published exe from an isolated folder (not the dev `bin/` output): ~2s to visible window, ~140-147MB RAM at rest pre-overhaul; the post-overhaul baseline is ~2.6s / ~180MB (see DEVELOPMENT.md). Not done: no numeric startup/RAM budget was ever defined to tune against — these are baseline measurements, not validated against a target |
| Fase 9 — Consolidation | Full flow review, close functional gaps, polish docs and structure | Largely done — consolidation batch (2026-08-18), four audit passes, `Bridge.Emulation` extraction, i18n, status-bar progress; remaining gaps tracked per phase above |

**Deliverables:** see the per-phase Objective/Entregables breakdown in `PROJECT_FOUNDATION.md` §26.1–26.10 — that document remains the detailed reference; this table is the tracking surface.

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Taking on too much complexity too early | Medium | High | Stick to the MVP scope in Fase 0–4 before touching metadata/emulation |
| Mixing UI and domain logic | Medium | High | Enforce module boundaries (`Core`/`Storage` never reference `App`) |
| Losing control of source-of-origin identifiers during import | Low | Medium | Dedupe by external GameId + source, dedupe by `(ExternalId, SourceId)` per ADR-6 |
| Skipping metadata/image caching | Low | Medium | Cache is part of the Fase 5 deliverable, not optional |
| Over-generalizing the emulation subsystem | Medium | Medium | Start with one emulator/profile end-to-end before generalizing |
| Introducing a plugin system before the core is stable | Low | High | Explicitly out of scope until Fase 9 is complete and reassessed |
| Not validating in phases (building ahead without testing each phase) | Medium | Medium | Each phase in the table above has an explicit validation step in `PROJECT_FOUNDATION.md` §26 — don't start the next phase until the current one's validation passes |
| ~~SQLite vs LiteDB left undecided past Fase 0~~ — **Resolved 2026-08-05**, SQLite via EF Core, implemented and verified in `Bridge.Storage` | — | — | Closed, see [ARCHITECTURE.md ADR-4](ARCHITECTURE.md#adr-4-local-storage-engine--sqlite-vs-litedb) |
| ~~`SQLitePCLRaw.lib.e_sqlite3` (transitive dep of `Microsoft.EntityFrameworkCore.Sqlite`) has a known high-severity advisory (`GHSA-2m69-gcr7-jv3q`, CVE-2025-6965) at the version currently resolved~~ — **Resolved 2026-08-09**, `Bridge.Storage` now references `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 directly, which replaces the vulnerable `lib.e_sqlite3` (SQLite 3.49.1) with `SourceGear.sqlite3` (SQLite 3.50.4); verified by the EF Core round-trip tests | — | — | Closed — see [Dependencies](#dependencies) |

---

## Dependencies

| Dependency | Version | Purpose | Notes |
|-----------|---------|---------|-------|
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.11 | Persistence — `Bridge.Storage` | Decided, see [ADR-4](ARCHITECTURE.md#adr-4-local-storage-engine--sqlite-vs-litedb) |
| Microsoft.EntityFrameworkCore.Design | 10.0.11 | EF Core migration tooling (`dotnet ef migrations`) | Design-time only (`PrivateAssets=all`) — powers `Bridge.Storage/Migrations/`, see DEVELOPMENT.md "Schema Migrations" |
| dotnet-ef (global tool) | 10.0.11 | `dotnet ef migrations add ...` | Dev-only; paired with the Design package for schema migrations |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.5 | Native SQLite binary (SourceGear.sqlite3) | Explicit pin in `Bridge.Storage`; see [Dependencies](#dependencies) risk register |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM boilerplate (`ObservableObject`, `RelayCommand`) | Matches `DEVELOPMENT.md` MVVM conventions |
| Microsoft.Extensions.DependencyInjection | 10.0.11 | DI container | Matches `DEVELOPMENT.md` DI conventions |
| ~~Microsoft.Extensions.Logging~~ | — | **Not a dependency** — no `ILogger<T>` in the codebase; logging goes through `App.LogException` to `errors.log` | Code-verified — do not re-add without reason |
| WPF-UI | 4.3.0 | Modern WPF controls/theming — `FluentWindow`, `TitleBar`, `SymbolIcon`, theme dictionaries | In use since Fase 7, see [ADR-3](ARCHITECTURE.md#adr-3-wpf-ui-430-for-the-visual-overhaul-supersedes-the-original-plain-wpf-first-adr) |
| SharpCompress | 0.50.4 | 7z/zip extraction for the managed RetroArch install (`ArchiveFactory.WriteToDirectory` for solid 7z) | Pinned for RetroArch install; see the Managed Emulation section in DEVELOPMENT.md |

---

## Success Criteria

- Starts fast and uses little memory
- Saves data locally and reliably (survives restarts)
- Imports libraries and ROMs correctly
- List and detail views are smooth, even with virtualization
- Statistics stay consistent with the underlying data
- Caches metadata and images (no redundant re-downloads)
- Stays easy to maintain — no unnecessary complexity carried over from heavier library-manager designs
- Leaves room for future visual and (eventually) plugin evolution without a rewrite

---

## Timeline

No fixed calendar dates — this is a solo, milestone-driven project. Progress is tracked by the Fase 0–9 table above; a phase is "done" only when its validation step (per `PROJECT_FOUNDATION.md` §26) passes, not when the code merely compiles.

---

*This document is a living plan. Update as the project evolves.*
