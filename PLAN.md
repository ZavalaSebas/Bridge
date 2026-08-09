# Bridge - Project Plan

> **Status:** In development — MVP core loop working end-to-end (Fases 1-8, including the Fase 7 WPF-UI visual overhaul) plus real Steam library detection; Fase 9 (consolidation) not started
>
> **Last updated:** 2026-08-08

## Project Overview

Bridge is an original game library manager: a local catalog that unifies games from external libraries (Steam, GOG, etc.), manually added games, and emulated ROMs into a single, fast, self-contained app. Its feature set is inspired by what makes tools like [Playnite](https://playnite.link/) useful (incremental import, local metadata/image caching, list virtualization, emulation support) without copying their architecture — Bridge drops the plugin system and the multi-frontend split to start from a small, verifiable core.

## Current State

### Phase 1 (Core & Persistence) — Done for MVP
Domain entities, local storage, and the app's composition root are built, wired together, and verified by launching the real `Bridge.exe`.

### Phase 2 (Minimal UI & Editing) — Done for MVP
WPF shell showing list + detail, manual add/edit, launching and playtime tracking, and basic statistics all work end-to-end.

### Phase 3 (Metadata, Emulation & Polish) — Metadata, Emulation, and visual polish done; Steam Store metadata added
Metadata download from IGDB (ADR-10) and Steam Store (HTTP anonymous, no login) both work; emulator configuration and ROM scanning work end-to-end. Steam games auto-import on startup and auto-download metadata from the Steam Store. Visual polish (Fase 7) and packaging (Fase 8) are complete — the UI is now WPF-UI 4.3.0 (Mica backdrop, title bar, sidebar navigation, statistics overlay) and the app ships as a single self-contained ~155 MB `.exe`. Consolidation (Fase 9) has not started — see [Development Phases](#development-phases) for the full per-fase breakdown and every known gap, not just this summary.

---

## Problem Statement

Game library managers are often built around a plugin ecosystem and a full desktop/fullscreen split, which is a lot of moving parts for a personal, single-maintainer tool. Bridge is a deliberately smaller take on the same problem space: a single app that keeps the parts that matter functionally (import, metadata, emulation, virtualized views, local persistence) and drops the parts that exist purely to support third-party extensibility. [Playnite](https://playnite.link/)'s observed behavior informs how Bridge's features should feel, but Bridge is its own design — different module layout, different persistence, different UI, no shared code.

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
| Library import strategy | Standard importer per source (`GetGames()` + dedupe by external GameId) to start; per-source custom importers only if a source genuinely needs one — the same conceptual split as Playnite's `ImportGames`, without committing to the plugin abstraction that carries it there |
| Packaging | Single-file, self-contained `.exe` (`win-x64`), same release pipeline pattern as the template default |

---

## Scope: Current vs Future

### Current Version (0.1.0) — In Progress
The MVP defined in the foundation notes:
- Open the app, create/save/load/edit games, delete
- Add games manually
- **Detect and import installed Steam games automatically on startup** — `Bridge.Import`/`SteamLibraryImporter`, see [ARCHITECTURE.md ADR-11](ARCHITECTURE.md#adr-11-steam-library-detection--local-files-only-hand-rolled-vdf-parser-bridgeimport-created-for-real). Verified against a real Steam installation (29 real games, 2 library folders) before writing synthetic tests
- **Steam Store metadata on import** — newly imported Steam games auto-download metadata from the official Steam store (name, description, release date, cover/background art, critic/community scores, developers, publishers, genres, platforms, features, links — all via public HTTP endpoints, no login, no API key). Manual metadata search tries IGDB first, then falls back to Steam Store for non-Steam games.
- **Steam icons in the library list** — each Steam game shows the square 32x32 clienticon Steam caches locally (`appcache\librarycache\{appid}`), falling back to the `header.jpg` URL when no cached icon exists — the same icon Playnite displays.
- List + detail views with basic selection
- **Launch Steam games with an auto-resolved play action** — Steam-imported games play via `steam://rungameid/{appid}` without any per-game setup, with directory-based playtime tracking (see Fase 3 in the phase table below)
- Basic statistics (totals, installed/not installed, favorites, total playtime)
- Import simple ROMs (single emulator, single folder) and match them to games
- Local image caching for covers/icons
- **WPF-UI visual overhaul (Fase 7)** — `FluentWindow` with Mica backdrop, 3-zone title bar (search, view-mode toggles, overflow menu), 44px icon sidebar (Library / Statistics), themed List/Covers/Details views, and a full-width Statistics overlay

### Future Versions — Backlog
- Epic Games (and other) library sources beyond Steam
- Full metadata provider pipeline (`SkipExistingValues` semantics, result caching — multi-source field resolution already exists)
- Full emulation subsystem: CRC/serial/partial-name ROM matching, multiple emulator profiles, scanner exclusions
- Bulk/multi-game editing
- Fullscreen mode (explicitly deferred, not ruled out)
- Plugin system (explicitly deferred, not ruled out — the internal module boundaries are drawn so this remains possible later without a rewrite)

---

## Project Structure

```
Bridge/
├── Bridge.slnx
├── Bridge/              # WPF host app — created, and no longer just a scaffold:
│                        #   ViewModels/ (MainViewModel, EmulatorSetupViewModel, IgdbSettingsViewModel)
│                        #   Services/ (GameLauncher, RomScanner)
│                        #   Settings/ (IgdbSettingsStore)
│                        #   Statistics/ (LibraryStatistics, GameSortComparer, GameGroupResolver)
│                        #   Converters/ (MetadataConverters — image, release date, playtime, group, short date)
│                        #   Styles/ (Theme.xaml — dark token palette), Fonts/ (InterVariable.ttf)
│                        #   Views: MainWindow, GameInfoWindow, AddGameWindow, ScanRomWindow,
│                        #           EmulatorSetupWindow, IgdbSettingsWindow
├── Bridge.Core/         # Domain entities, contracts — created
├── Bridge.Storage/      # EF Core DbContext, repositories — created
├── Bridge.Import/       # created — SteamLibraryImporter, SteamLocalIconResolver, SteamPlayActions, VdfParser, SteamPaths
├── Bridge.Metadata/     # created — IgdbMetadataProvider/IgdbAuthClient/IgdbSettings
├── Bridge.Emulation/    # not created — see note below
└── Bridge.Tests/        # created — 66 tests, all passing (dotnet test Bridge.slnx)
```

Flat layout — every project sits directly under the repo root, no `src/`/`tests/` wrapper folders.

**Deviation from this table worth flagging explicitly** (per this project's own "Código = Verdad" principle — the plan should say what's actually true): `RomScanner` (ROM folder scanning) and the emulator-launch half of `GameLauncher` ended up living in `Bridge/Services/`, not in a separate `Bridge.Emulation` project. Same for import logic (`AddGame`/`ScanRomFolder` in `MainViewModel` directly, no `Bridge.Import` project). This happened because building the working vertical slice (UI → service → storage) was prioritized over strictly following the module boundaries sketched when this table was first written, before any code existed. It is not necessarily wrong — [ADR-5](ARCHITECTURE.md#adr-5-internal-modularity-only--no-runtime-module-boundaries) already says these boundaries are for development organization, not runtime extensibility — but it's a real decision that was never explicitly made, just fell out of moving fast. Worth a deliberate look before Fase 9 (consolidation): either update this table to match reality (fold `Bridge.Import`/`Bridge.Emulation` into `Bridge` for good), or actually extract `Bridge/Services` into those separate projects. Don't let this table keep saying "not created" for something that, in spirit, already exists elsewhere.

**Update (Fase 5):** `Bridge.Metadata` was deliberately built as a real separate project this time, not folded into `Bridge` — a conscious correction after noticing the drift above, not a third repeat of it.

**Update (Steam import):** `Bridge.Import` now exists for real too (`SteamLibraryImporter`, `VdfParser`, `SteamPaths` — see [ADR-11](ARCHITECTURE.md#adr-11-steam-library-detection--local-files-only-hand-rolled-vdf-parser-bridgeimport-created-for-real)). Only `Bridge.Emulation` is still folded into `Bridge/Services/` (`RomScanner`, the emulator-launch half of `GameLauncher`) — one deviation left, not two.

---

## Development Phases

> Numbered as in the original planning notes (`PROJECT_FOUNDATION.md`, §26) to keep traceability back to that document. Playnite references throughout are behavioral *inspiration* (how a feature should feel), never a spec for Bridge's implementation.

**Milestones:**

| Milestone | Description | Status |
|-----------|-------------|--------|
| Fase 0 — Base definition | Lock MVP scope, folder structure, storage engine (SQLite vs LiteDB), image storage format | Mostly done — scope/structure locked, storage engine decided ([ADR-4](ARCHITECTURE.md#adr-4-local-storage-engine--sqlite-vs-litedb)); image storage format still open |
| Fase 1 — Core & persistence | `Game`, `GameMetadata`, `Emulator`, `LibrarySource` entities **plus `GameAction` and `GameRom`** (see `PROJECT_FOUNDATION.md` §28.8 — `Game.GameActions`/`Game.Roms` are core fields, not an afterthought); repository; CRUD survives app restart | In progress — `Bridge.Core`, `Bridge.Storage`, and the `Bridge` app's composition root (`Config.cs`, DI, `EnsureCreated()`) all exist and are verified by actually launching `Bridge.exe` and confirming the real `bridge.db` gets the correct schema. Not done: `Bridge.Import`/`Bridge.Metadata`/`Bridge.Emulation`, and any real UI |
| Fase 2 — Minimal UI | MainWindow, MainViewModel, LibraryView, GameDetailsView; list loads, detail responds to selection | Functionally done for MVP and expanded — `MainViewModel` loads `Games` from `IGameRepository`; `MainWindow.xaml` binds a `ListBox` (selection) + detail panel to it. Verified by launching `Bridge.exe` (empty and seeded) and confirming the window comes up (`Responding: True`, correct title) — not yet visually screenshotted (no GUI-capture tool available in this environment). No dedicated `LibraryView`/`GameDetailsView` split yet — both live inline in `MainWindow.xaml`, fine for MVP, split out if `MainWindow.xaml` grows unwieldy. **Global actions moved into the title bar overflow menu** (Add Game, Import Steam Library, Exit; Scan ROMs, Configure Emulator, IGDB Settings) so the main page only shows the library list + detail panel — `AddGameWindow`/`ScanRomWindow` are small prompt dialogs that feed `AddGameCommand`/`ScanRomFolderCommand`, keeping the main window clean (the Fase 7 visual pass kept this arrangement, restyled as WPF-UI). **Three view modes now** (`ViewMode` enum, switched via title bar icon toggles; a ComboBox before Fase 7): List (original list+detail), Covers (cover wall with hover Play/Info via a `GameInfoWindow` compact info window), and Details (flat `ListView`/`GridView` with Name/Release Date/Genre/Last Played/Time Played/Library columns). The left `ListBox` collapses in Covers/Details; all views share `GamesView`, so search/filter/sort/group apply in every mode |
| Fase 3 — Manual edit & import, **play & track** | Create/edit/delete, favorite/hidden flags, manual metadata; **launch a game via its `GameAction` and track playtime** (poll-based process/directory monitoring — see §28.9-28.10 for the behavioral notes) — this is its own explicit deliverable, not a side effect of "playtime updates" | Create/edit/delete + favorite/hidden work end-to-end (`AddGameCommand`/`SaveGameCommand`/`DeleteGameCommand`). **Play & track works end-to-end now**: `Bridge/Services/GameLauncher.cs` launches a `GameAction` and polls for exit — the same *behavioral approach* Playnite uses (§28.9-28.10 — no `Process.Exited` event, a `Task.Delay` loop). Verified against a real 3-second child process: session length measured accurately, `PlayCount`/`LastActivity`/`PlaytimeSeconds` updated correctly, `GameStarted`/`GameStopped` events fire and are consumed by `MainViewModel` (`PlayGameCommand`, `StatusMessage`). UI has a "▶ Play" button. **Automatic Steam play action added** — informed by how Playnite's `SteamPlayController` behaves (§28.26): a Steam-imported game with no configured `GameAction` gets a runtime `steam://rungameid/{appid}` URL action, launched via `steam.exe -silent` (never the local exe — Steamworks DRM), tracked by `TrackingMode.Directory` (watch processes whose binary lives under `InstallDirectory`, since the launched process is `steam.exe`, not the game). Resolution logic lives in `Bridge.Import/Steam/SteamPlayActions.cs` (pure, unit-tested without Steam installed); the launcher only checks whether Steam's registry path exists. The UI's "Set Play Action" field was **removed for now** — Steam games resolve the action automatically, and the field only made sense for manual non-Steam games (`SetPlayActionCommand`/`ExecutablePathInput` still exist in `MainViewModel`, unbound from the UI). **Deliberately scoped down**: `GameActionType.Url` is only wired for the auto-resolved Steam case (not as a general user-configured action), `Script` isn't supported, and there's no process-tree walking (`OriginalProcess`-like behavior for non-directory actions) — see the doc comment at the top of `GameLauncher.cs`. Also not done: manual metadata beyond Name/Description |
| Fase 4 — Basic statistics | Totals, installed/not installed, favorites/hidden, total playtime, top played | Done for MVP and expanded — `Bridge/Statistics/LibraryStatistics.cs` computes everything on the fly from the current `Games` list (no persisted stats entity — the same on-the-fly approach Playnite takes, §28.5). Verified against known test data (exact expected counts/totals). Now surfaced as a **Statistics overlay** in `MainWindow` — a sidebar button shows a full-width dashboard that hides the detail panel while open (before Fase 7 it was a tab in a right-panel `TabControl`) showing counts with percentages, total/average play time, total install size, completion status, and the Top Play Time list — recomputed after every Add/Delete/Save/GameStopped. The list itself now supports **search, filter presets (All/Favorite/Most Played/Recently Played), sorting** (22 fields via `GameSortComparer`, unit-tested in `Statistics/GameSortComparerTests.cs`) **and grouping** (21 fields via `GameGroupResolver`, unit-tested in `Statistics/GameGroupResolverTests.cs`) |
| Fase 5 — Metadata | Download name/description/images from IGDB (ADR-10) and Steam Store (HTTP anonymous); auto-download metadata for newly imported Steam games; multi-provider fallback chain (IGDB → Steam Store); cache results, respect existing values | **Unblocked and built with 2 providers** — user confirmed IGDB. `Bridge.Metadata` has `IgdbMetadataProvider` (verified against 9 tests with fake HTTP handler) and `SteamMetadataProvider` (calls `store.steampowered.com/api/appdetails` + `appreviews`, maps 12+ fields including Name/Description/ReleaseDate/CoverImage/BackgroundImage/CriticScore/CommunityScore/Developers/Publishers/Genres/Platforms/Features/Links, no login required). `MainViewModel` has `DownloadMetadataCommand` with multi-provider fallback and auto-metadata (`DownloadMissingSteamMetadataAsync` runs on startup for new Steam games). `IGameMetadataProvider` interface in `Bridge.Core.Contracts` for future provider additions. UI revamped to show cover image, release date, critic/community scores, background image, install directory, and Developers/Publishers/Platforms (resolved to `Company`/`Platform` entities via `ApplyMetadataReferences` in `MainViewModel`, labels collapse when empty; Steam platform slugs map to readable names). **Library icons added** — the list shows Steam's square 32x32 clienticon from the local `appcache\librarycache\{appid}` via `SteamLocalIconResolver` (the API's `clienticon` field is gone), falling back to the `header.jpg` URL (verified rendering by window capture: icons show correctly in the list). **Not done**: caching results (re-downloads every time), `SkipExistingValues` (always overwrites), local image download (covers stored as raw URLs, not cached to disk), `Features` downloaded but not yet displayed in the UI |
| Fase 6 — Emulation | Detect emulators, scan ROMs, match & create/update entries | Core mechanism done and verified — `Bridge/Services/RomScanner.cs` (filename+extension matching, no CRC/DAT — that's Future Scope per §28.4) scans a folder and creates `Game`+`GameRom`+`GameAction(Emulator)` entries; `GameLauncher` resolves `Emulator`/`EmulatorProfile` and launches with a `{RomPath}` substitution. **UI to configure an emulator now exists**: `EmulatorSetupWindow`/`EmulatorSetupViewModel` (opened via "Configure Emulator..." in `MainWindow`) — edits the single existing `Emulator` in place instead of duplicating it on repeat saves (verified). Verified end-to-end: scan found the right files, launch substituted the ROM path correctly, tracking worked, editing an emulator's settings twice left exactly one row in the DB. **Not done**: emulator *detection* (finding already-installed emulators automatically — §28.9's `EmulatorScanner`) — the user has to type in the install directory/executable by hand for now |
| Fase 7 — Visual polish | WPF UI adoption, themes, Mica/Acrylic, light animation | **Done** — full WPF-UI 4.3.0 overhaul (see [ADR-3](ARCHITECTURE.md#adr-3-wpf-ui-430-for-the-visual-overhaul-supersedes-the-original-plain-wpf-first-adr)): `FluentWindow` with Mica backdrop, 3-zone `TitleBar` (search, view-mode toggles, overflow menu), 44px icon sidebar (Library / Statistics), themed List/Covers/Details views (dark tokens in `Bridge/Styles/Theme.xaml`, Inter Variable font, `#007ACC` UI accent / `#10B981` Play accent), and a full-width Statistics overlay. `NavigationSection.Settings` exists in the enum but has no view yet. Measured post-overhaul baseline: ~2.6s cold start / ~180MB RAM (see DEVELOPMENT.md). The deferred `ApplicationThemeManager.Apply` experiment made cold start worse and was reverted |
| Fase 8 — Packaging | Single-file self-contained publish, startup/RAM tuning, path/asset validation | Done for MVP — real finding: the documented `dotnet publish` command alone produced `Bridge.exe` **plus 6 sidecar native DLLs** (WPF's own native interop libs + native SQLite), not a true single file. Fixed by adding `IncludeNativeLibrariesForSelfExtract=true` and `DebugType=none` (via a new `Directory.Build.props`, applies repo-wide) to `Bridge.csproj` — re-verified: publish output is now exactly one `Bridge.exe` (~155 MB, no sidecars; 148 MB before the Fase 7 overhaul added WPF-UI/Inter). Tried `PublishReadyToRun=true` for startup speed on the pre-overhaul build — **made it slower** (2671ms vs ~2000ms average without it) and bigger (176MB vs 148MB), empirically not worth it, not enabled. Measured 3 real cold-start runs of the published exe from an isolated folder (not the dev `bin/` output): ~2s to visible window, ~140-147MB RAM at rest pre-overhaul; the post-overhaul baseline is ~2.6s / ~180MB (see DEVELOPMENT.md). Not done: no numeric startup/RAM budget was ever defined to tune against — these are baseline measurements, not validated against a target |
| Fase 9 — Consolidation | Full flow review against `PROJECT_FOUNDATION.md`, close functional gaps | Not started |

**Deliverables:** see the per-phase Objective/Entregables breakdown in `PROJECT_FOUNDATION.md` §26.1–26.10 — that document remains the detailed reference; this table is the tracking surface.

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Copying too much of Playnite's complexity too early | Medium | High | Stick to the MVP scope in Fase 0–4 before touching metadata/emulation |
| Mixing UI and domain logic | Medium | High | Enforce module boundaries (`Core`/`Storage` never reference `App`) |
| Losing control of source-of-origin identifiers during import | Low | Medium | Dedupe by external GameId + source, following the same dedup concept Playnite uses (`GameId + PluginId`) |
| Skipping metadata/image caching | Low | Medium | Cache is part of the Fase 5 deliverable, not optional |
| Over-generalizing the emulation subsystem | Medium | Medium | Start with one emulator/profile end-to-end before generalizing |
| Introducing a plugin system before the core is stable | Low | High | Explicitly out of scope until Fase 9 is complete and reassessed |
| Not validating in phases (building ahead without testing each phase) | Medium | Medium | Each phase in the table above has an explicit validation step in `PROJECT_FOUNDATION.md` §26 — don't start the next phase until the current one's validation passes |
| ~~SQLite vs LiteDB left undecided past Fase 0~~ — **Resolved 2026-08-05**, SQLite via EF Core, implemented and verified in `Bridge.Storage` | — | — | Closed, see [ARCHITECTURE.md ADR-4](ARCHITECTURE.md#adr-4-local-storage-engine--sqlite-vs-litedb) |
| **New:** `SQLitePCLRaw.lib.e_sqlite3` (transitive dep of `Microsoft.EntityFrameworkCore.Sqlite`) has a known high-severity advisory (`GHSA-2m69-gcr7-jv3q`) at the version currently resolved | Low | Low | Dependabot (`dependabot.yml`) will flag updates automatically; not a code defect in Bridge, just a dependency to keep current |

---

## Dependencies

| Dependency | Version | Purpose | Notes |
|-----------|---------|---------|-------|
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.10 | Persistence — `Bridge.Storage` | Decided, see [ADR-4](ARCHITECTURE.md#adr-4-local-storage-engine--sqlite-vs-litedb) |
| CommunityToolkit.Mvvm | Latest stable | MVVM boilerplate (`ObservableObject`, `RelayCommand`) | Matches `DEVELOPMENT.md` MVVM conventions |
| Microsoft.Extensions.DependencyInjection | Latest stable | DI container | Matches `DEVELOPMENT.md` DI conventions |
| Microsoft.Extensions.Logging | Latest stable | `ILogger<T>` throughout services/ViewModels | Matches `DEVELOPMENT.md` logging conventions |
| WPF-UI | 4.3.0 | Modern WPF controls/theming — `FluentWindow`, `TitleBar`, `SymbolIcon`, theme dictionaries | In use since Fase 7, see [ADR-3](ARCHITECTURE.md#adr-3-wpf-ui-430-for-the-visual-overhaul-supersedes-the-original-plain-wpf-first-adr) |

---

## Success Criteria

- Starts fast and uses little memory
- Saves data locally and reliably (survives restarts)
- Imports libraries and ROMs correctly
- List and detail views are smooth, even with virtualization
- Statistics stay consistent with the underlying data
- Caches metadata and images (no redundant re-downloads)
- Stays easy to maintain — no unnecessary complexity carried over from Playnite
- Leaves room for future visual and (eventually) plugin evolution without a rewrite

---

## Timeline

No fixed calendar dates — this is a solo, milestone-driven project. Progress is tracked by the Fase 0–9 table above; a phase is "done" only when its validation step (per `PROJECT_FOUNDATION.md` §26) passes, not when the code merely compiles.

---

*This document is a living plan. Update as the project evolves.*
