# Bridge — Project Guide

This document serves as a guide to this specific project AND as a reference for the architecture, workflow, and decisions made during planning.

---

## Documentation Philosophy

> **IMPORTANT: This document is a living memory of the project. Treat it as such.**

### Why This Matters

This document is designed to serve two purposes simultaneously:

1. **Technical documentation** for developers contributing to the project
2. **Context preservation** for future sessions — whether by the original developer or an AI agent continuing the work

When making changes, consider:
- Will someone reading this in 2 years understand why this decision was made?
- Would an AI agent reading this have enough context to continue the work without asking clarifying questions?
- Is the historical context preserved for future reference?

### Documentation Principles

| Principle | Application |
|-----------|-------------|
| **Precision over speed** | One factual error makes the whole document lose credibility |
| **Minimal change** | Fix only objective inconsistencies; don't reorganize out of preference |
| **Preserve historical context** | If something changed, mark the previous state as "was/used to" |
| **Code = Truth** | If the docs say one thing and the code another, the docs are wrong — fix the docs |
| **Tested** | If it's not documented, it doesn't exist for a new contributor |

---

## Why Bridge?

Bridge is an original game library manager: one local, self-contained catalog for games from external libraries, manual entries, and emulated ROMs. It keeps what makes a good library manager functionally useful (incremental import, local metadata/image caching, virtualized list views, emulation support) and drops what exists purely to support runtime extensibility — no plugin architecture, no dual desktop/fullscreen frontends. [Playnite](https://playnite.link/) is an *inspiration*: its observed behavior informs how Bridge's features should feel, but Bridge is designed independently — different module layout, different persistence, different UI, no shared code. See [`PROJECT_FOUNDATION.md`](PROJECT_FOUNDATION.md) for the behavioral notes used during development, and [`ARCHITECTURE.md`](ARCHITECTURE.md) for the specific decisions (ADR-1 through ADR-12) that follow from this direction.

---

## Architecture Overview

```
App (Bridge)                                        │ WPF
│  MainWindow, ViewModels, Views, Controls, DI root
├─────────────────────────────────────────────────┤
Import / Metadata / Emulation                       │ Use-case modules
│  Bridge.Import, Bridge.Metadata, Bridge.Emulation │  (Bridge.Emulation has no
│                                                    │   project — RomScanner and
│                                                    │   the emulator-launch half of
│                                                    │   GameLauncher live in
│                                                    │   Bridge/Services, see PLAN.md)
├─────────────────────────────────────────────────┤
Storage                                              │ Persistence
│  Bridge.Storage — repositories, DB context, file/image cache
├─────────────────────────────────────────────────┤
Core                                                 │ Domain
│  Bridge.Core — entities, rules, filters, statistics, contracts
└─────────────────────────────────────────────────┘
```

`App` depends on everything below it; `Storage` depends on `Core`; `Import` and `Metadata` depend on `Core` only (neither references `Bridge.Storage` — importers and metadata providers are pure, persistence comes from the caller); `Core` depends on nothing else in the solution. There is no `Bridge.Emulation` project — emulator/ROM logic lives in `Bridge/Services` (see Project Structure). All of this is enforced by project references, not convention — see [ARCHITECTURE.md ADR-5](ARCHITECTURE.md#adr-5-internal-modularity-only--no-runtime-module-boundaries).

### Key Design Decisions

| Decision | Choice | Why |
|----------|--------|-----|
| Plugin system | None in v1 | See [ADR-1](ARCHITECTURE.md#adr-1-no-plugin-system-in-v1) |
| Frontend | Single desktop app, no fullscreen mode | See [ADR-2](ARCHITECTURE.md#adr-2-single-application-no-separate-fullscreen-frontend-in-v1) |
| Theming | WPF-UI 4.3.0 (migrated from plain WPF in Fase 7), single built-in dark theme | See [ADR-3](ARCHITECTURE.md#adr-3-wpf-ui-430-for-the-visual-overhaul-supersedes-the-original-plain-wpf-first-adr) |
| Storage engine | SQLite via EF Core — decided and implemented in `Bridge.Storage` | See [ADR-4](ARCHITECTURE.md#adr-4-local-storage-engine--sqlite-vs-litedb) |
| Module boundaries | Internal-only, compile-time enforced | See [ADR-5](ARCHITECTURE.md#adr-5-internal-modularity-only--no-runtime-module-boundaries) |

---

## Project Structure

```
Bridge/
├── Bridge.slnx
├── Bridge/              # WPF host app — created, real ViewModels/Services/Statistics/Settings now, not just scaffold
├── Bridge.Core/         # Domain entities and repository contracts — created (see below)
├── Bridge.Storage/      # EF Core DbContext + repository implementations — created (see below)
├── Bridge.Metadata/     # created — IgdbMetadataProvider, SteamMetadataProvider (see below)
├── Bridge.Import/       # created — SteamLibraryImporter, SteamLocalIconResolver, SteamLocalPlaytimeResolver, SteamPlayActions, VdfParser, SteamPaths (see below)
├── Bridge.Emulation/    # not created — RomScanner/emulator-launch logic lives in Bridge/Services instead
└── Bridge.Tests/        # created — 102 tests, all passing (see below)
```

Flat layout — every project sits directly under the repo root, no `src/`/`tests/` wrapper folders.

> **Status:** `Bridge.slnx`, `Bridge/Bridge.csproj`, `Bridge.Core/Bridge.Core.csproj`, `Bridge.Storage/Bridge.Storage.csproj`, `Bridge.Metadata/Bridge.Metadata.csproj`, `Bridge.Import/Bridge.Import.csproj`, and `Bridge.Tests/Bridge.Tests.csproj` all exist and build/test clean. **Module-boundary note** (see `PLAN.md` > Project Structure for the fuller version): `Bridge.Emulation` was never actually created as a separate project — that logic lives inside `Bridge` (the app project) instead, a real deviation from the original plan, flagged there for a deliberate decision before Fase 9, not silently accepted. `Bridge.Metadata` and `Bridge.Import` were built as real separate projects.

### `Bridge.Core` — what's in it

```
Bridge.Core/
├── Entities/
│   ├── DatabaseObject.cs      # base: Guid Id, string Name
│   ├── Game.cs                # the central entity, plus the ReleaseDate struct
│   ├── GameAction.cs
│   ├── GameRom.cs
│   ├── Link.cs
│   ├── DescriptionBlock.cs     # ordered description chunks (paragraph/heading/subheading/list text + images), stored as JSON on Game
│   ├── Emulator.cs            # Emulator + EmulatorProfile (EmulatorProfile.CorePath is set only for Bridge-managed RetroArch profiles)
│   ├── GameScannerConfig.cs
│   ├── Company.cs             # one entity, no Developer/Publisher subclasses — see ADR-7
│   ├── GameSource.cs          # replaces Playnite's PluginId — see ADR-6
│   ├── CompletionStatus.cs
│   ├── Region.cs
│   ├── Platform.cs
│   └── ReferenceEntities.cs   # Genre, Category, Tag, Series, AgeRating, GameFeature
├── Enums/
│   ├── GameActionType.cs, TrackingMode.cs, ScannerPlayActionMode.cs, CompletionStatusKind.cs
│   ├── LibraryFilterPreset.cs   # All/Favorite/Installed/NotPlayed/RecentlyPlayed filter predicates
│   ├── GameSortField.cs         # 22 sortable fields, Description attribute = display label
│   ├── GameGroupField.cs        # 21 groupable fields, Description attribute = display label
│   ├── NavigationSection.cs     # sidebar sections: Library/Favorites/Sources/Statistics/Settings
│   └── ViewMode.cs              # List / Covers / Details main-content views
├── Import/
│   └── GameMetadata.cs        # importer-facing DTO — no MetadataProperty, see ADR-8
└── Contracts/
    ├── IRepository.cs         # generic CRUD + GetOrCreateByName
    ├── IGameRepository.cs     # FindByExternalId
    └── IGameMetadataProvider.cs # SearchAsync + Name — enables multi-provider fallback chain
```

Every entity's field shape is traced directly to `PROJECT_FOUNDATION.md` §28's verified reference — where Bridge's shape deliberately diverges from Playnite's (no `PluginId`, one `Company` type, no `MetadataProperty`, single `EmulatorProfile` shape), the reasoning is recorded in [ARCHITECTURE.md](ARCHITECTURE.md) ADR-6 through ADR-9, not just in code comments. `dotnet build Bridge.Core/Bridge.Core.csproj` compiles clean (0 warnings, 0 errors) as of this writing.

### `Bridge.Storage` — what's in it

```
Bridge.Storage/
├── BridgeDbContext.cs          # EF Core DbContext — one DbSet per top-level entity (GameAction/GameRom/Link/DescriptionBlock have no DbSet: they're JSON columns on Game), OnModelCreating wires up JSON converters
├── Converters/
│   └── JsonValueConverter.cs   # generic EF value converter: List<T>/ReleaseDate <-> JSON text column
└── Repositories/
    ├── Repository.cs           # generic IRepository<T> impl — covers every reference entity
    └── GameRepository.cs       # IGameRepository impl, adds FindByExternalId
```

Uses `Microsoft.EntityFrameworkCore.Sqlite` (see [ADR-4](ARCHITECTURE.md#adr-4-local-storage-engine--sqlite-vs-litedb)). Every `List<T>`/`ReleaseDate` property on an entity is stored as a JSON text column rather than a separate EF owned-entity table — a deliberate simplicity tradeoff, see the comment at the top of `JsonValueConverter.cs` for why, and `BridgeDbContext.OnModelCreating` for exactly which properties use it. Schema changes use **real EF migrations** (`Bridge.Storage/Migrations/`, applied at startup by `BridgeDbMigrator.MigrateToLatest` — see the Updater/Migrations section below).

Verified at runtime (not just compiled) against a real SQLite database file: create → save a `Game` with populated `GameActions`/`Roms`/`Links`/`GenreIds`/`ReleaseDate` → close the context → reopen a fresh context pointing at the same file → every field reads back correctly, including the dedup lookup by `(ExternalId, SourceId)`.

**Wired up and verified end-to-end (2026-08-05).** `Bridge/Config.cs` defines `AppDataPath`/`DatabasePath`; `Bridge/App.xaml.cs`'s `OnStartup` builds the DI container (`Microsoft.Extensions.DependencyInjection`, see `ConfigureServices` — `BridgeDbContext` and every repository are registered `Singleton`, matching this doc's own Lifetime Guidelines for WPF), creates `%LOCALAPPDATA%\Bridge\` if missing, and calls `BridgeDbMigrator.MigrateToLatest()` (migrates to the latest schema, baselining pre-migrations DBs — see the Updater/Migrations section below). Verified by actually launching the built `Bridge.exe` (not just `dotnet build`) and confirming the real `bridge.db` file gets the correct 14 tables. There's a global `DispatcherUnhandledException` handler showing a `MessageBox` and writing the exception to `%LOCALAPPDATA%\Bridge\logs\errors.log` via `App.LogException` — minimal, deliberately not a full `ILogger` setup (see Logging).

### `Bridge.Metadata` — what's in it

```
Bridge.Metadata/
├── IgdbSettings.cs          # Client ID/Secret DTO — never hardcoded, see Bridge/Settings/IgdbSettingsStore.cs
├── IgdbAuthClient.cs        # Twitch OAuth2 client-credentials flow, caches the token
├── IgdbGame.cs              # raw IGDB /v4/games response shape (only the fields this MVP uses)
├── IgdbMetadataProvider.cs  # SearchAsync(name) -> GameMetadata?, implements IGameMetadataProvider
├── SteamStoreModels.cs      # DTOs for store.steampowered.com/api/appdetails, /appreviews
└── SteamMetadataProvider.cs # HTTP-anonymous Steam Store metadata (appid direct + name search), implements IGameMetadataProvider
```

See [ARCHITECTURE.md ADR-10](ARCHITECTURE.md#adr-10-igdb-as-a-text-metadata-source-primary-not-sole--see-adr-12) for why IGDB, and [ADR-12](ARCHITECTURE.md#adr-12-steam-store-metadata-as-a-secondary-http-anonymous-metadata-source) for the Steam Store metadata provider. `IGameMetadataProvider` (`Bridge.Core.Contracts`) enables the multi-provider fallback chain: Steam-imported games use appid-direct lookup (guaranteed), non-Steam games try IGDB first then Steam search. **`MetadataSyncService`** (`Bridge/Services/MetadataSyncService.cs`) centralizes that chain so `MainViewModel` does not duplicate provider ordering in four places. On startup, `MainViewModel.DownloadMissingSteamMetadataAsync` fire-and-forget fetches metadata for all newly imported Steam games. Cover/background URLs are stored as-is on the Game, and rendered through an in-memory cache of decoded (frozen) bitmaps — `Bridge/Converters/RemoteImageCache.cs`, bounded at 512 entries (LRU trim, not a full clear), with SSRF checks on download URLs, exposing `Preload`/`Get`/`Subscribe` — plus `Bridge/Converters/CachedImage.cs`, an attached property that subscribes so an `Image` re-renders the moment its artwork lands (the virtualized-list blank-icon fix). `SteamMetadataProvider` also fills `GameMetadata.Screenshots` (the `data.screenshots[].path_full` URLs, query string stripped) — persisted to `Game.Screenshots` as a JSON list column and rendered by the screenshot gallery in the Details view (see below). Since 2026-08-14 the own IGDB Worker does the same for non-Steam games: `BridgeIgdbProvider` maps IGDB's `screenshots` field (upgraded to `t_1080p`, the same 1920×1080 as Steam's `path_full`) into `GameMetadata.Screenshots`, so Epic/manual games get the same gallery from the same JSON column.

### `Bridge.Import` — what's in it

```
Bridge.Import/
└── Steam/
    ├── SteamLibraryImporter.cs     # GetInstalledGames() — registry → libraryfolders.vdf → appmanifest*.acf
    ├── SteamLocalIconResolver.cs   # local art from appcache\librarycache\{appid}\ — icon (32x32 clienticon), cover, hero background
    ├── SteamLocalPlaytimeResolver.cs # real Steam playtime from userdata\{steamid}\config\localconfig.vdf
    ├── SteamPlayActions.cs         # runtime play action resolution for Steam games (steam://rungameid/{appid})
    ├── VdfParser.cs                # hand-rolled recursive-descent VDF parser (ADR-11)
    └── SteamPaths.cs               # reads HKCU\Software\Valve\Steam\SteamPath from Windows registry
```

See [ARCHITECTURE.md ADR-11](ARCHITECTURE.md#adr-11-steam-library-detection--local-files-only-hand-rolled-vdf-parser-bridgeimport-created-for-real). Detection is 100% local — no Steam Web API, no API key, no network call. See also `PROJECT_FOUNDATION.md` §28.26 for the behavioral notes on Playnite's SteamLibrary extension, and §28.27.A for the full upstream pipeline (online owned games, playtime via Web API, Family Sharing, etc. — the online playtime path requires an account login and is documented for future reference; the *local* playtime path is what Bridge implements). `SteamPlayActions.CreatePlayAction` builds the `steam://rungameid/{appid}` URL action that Steam games use at launch (the same behavior Playnite's `SteamPlayController` exhibits, §28.26): resolved at runtime, launched via `steam.exe -silent` because Steamworks DRM makes launching the local `.exe` fail. Pure logic, unit-tested in `Bridge.Tests/Import/SteamPlayActionsTests.cs` without needing Steam installed.

`SteamLocalPlaytimeResolver.GetPlaytimes()` reads Steam's locally-recorded playtime from `userdata\{steamid}\config\localconfig.vdf` — under `UserLocalConfigStore > Software > Valve > Steam > apps > {appid}`, Steam writes `Playtime` (minutes) and `LastPlayed` (unix timestamp) for every game the account has run. The resolver converts minutes→seconds (the units `Game.PlaytimeSeconds` uses) and `LastPlayed`→`LastActivity`; multiple `userdata` accounts on the same install are merged per game by taking the largest playtime and the most recent activity. This is the zero-config source of a Steam game's real playtime (Playnite's Web API alternative requires connecting an account — §28.27.A). `SteamLibraryImporter.GetInstalledGames()` fills it into the imported metadata, and `MainViewModel.ImportLibraryCoreAsync` applies it: new games take Steam's playtime, existing games merge it without ever shrinking what Bridge already tracked (the two overlap, so max can't double-count), and `LastActivity` only moves forward. Unit-tested in `Bridge.Tests/Import/SteamLocalPlaytimeResolverTests.cs` against the real file shape.

`SteamLocalIconResolver.TryGetLocalIconPath(appId, steamInstallPath = null)` resolves the square 32x32 icon Steam itself caches for the library (the `clienticon` — the exact artwork Playnite shows). Steam stopped returning `clienticon` from the web API, so Bridge reads the file Steam writes to `appcache\librarycache\{appid}\{40-hex}.jpg` (default install path comes from `SteamPaths`); returns `null` when Steam isn't installed or that app has no cached icon, so callers fall back to the `header.jpg` URL from metadata. `TryGetLocalCoverPath` and `TryGetLocalBackgroundPath` resolve the rest of the local art the same way — `library_600x900.jpg` (vertical cover) and `library_hero.jpg` (widescreen hero). Verified against the real cache on this machine (628 apps with a cached 32x32 icon; covers/heroes present alongside). `MainViewModel.ApplySteamLocalArtwork` prefers all three on load, during Steam import (before the row binds, so a fresh install shows complete art immediately) and after every metadata download — local art wins over the web URLs, so the library renders from disk with no download.

### Library list: search, presets, sorting, grouping

The left list uses `GamesView` (a `ListCollectionView` over `Games`) instead of the raw collection, so all concerns stay in one place:

- **Search** — `SearchText` observable bound to a text box; `GameMatchesSearch` filters on a case-insensitive name substring.
- **Presets** — `FilterPreset` (`LibraryFilterPreset`: All/Favorite/Installed/NotPlayed/RecentlyPlayed) adds a **pure predicate** (`GameMatchesSearch`) that decides which games show. Presets never touch the sort or grouping — those are independent concerns, so switching a filter and back to All always keeps the sort you had. Combinable with the search box.
- **Sorting** — `SortField` (`GameSortField`, 22 fields) + `SortDescending` drive a `GameSortComparer` assigned to `CustomSort`. Reference fields (Developer/Publisher/Platform/Genre/Source) compare by display name resolved through `BuildNameLookup` dictionaries built from the repositories, so the comparer stays pure and testable. Empty/unset values always sort last regardless of direction (matches the "Not played at the bottom" expectation — Playnite's `StatisticsViewModel`-style handling). The sort menu shows the active field with a checkmark and a direction toggle entry.
- **Grouping** — `GroupField` (`GameGroupField`, 21 fields, "Don't group" off) adds a `PropertyGroupDescription` (with a null property name so the item itself flows through a `GameGroupConverter`) to `GroupDescriptions`. `GameGroupResolver` is pure and unit-tested: buckets for playtime/install size/scores, drive letter, release year, coarse date buckets ("Never/Last 7 days/..."), reference names via lookups. Group headers render via the `ListBox.GroupStyle`. In the **Covers** view, `GroupStyle.Panel` must also be set to `CenteringWrapPanel` — WPF's default group panel is a vertical `StackPanel`, which ignores the ListBox's root `ItemsPanel` and stacks every cover in one column inside each group.

The sort/group field enums live in `Bridge.Core.Enums`; the comparer/resolver live in the app project under `Bridge/Statistics` (namespace `Bridge.Statistics`). They avoid WPF types but ship inside the WPF app, and they're unit-testable only because `Bridge.Tests` targets `net10.0-windows` (it references `Bridge`). The XAML binds them through `GameGroupConverter`/`GameSortComparer` (see `Bridge/Converters/MetadataConverters.cs`).

### Shell architecture

The window uses WPF-UI's `FluentWindow` with Mica backdrop (`WindowBackdropType="Mica"`), laid out as 3 rows:

1. **Top Panel** (`ui:TitleBar`, 56px): Logo button (opens the main menu: **Add Game** → Import from Steam / Add Manually... / Scan ROMs... / Scan Automatically..., **Support** → Ko-fi / GitHub Sponsors, **Sidebar** → Show Sidebar / Position (Left/Right/Top/Bottom), **Theme** → 9 accent presets + Custom..., **Settings** → IGDB... / Configure Emulator..., **About Bridge...**, Exit — there's no Statistics item, that's the sidebar's job) + Search `TextBox` (460px, binds `SearchText`) + Filter/Sort/Group icon buttons (each opens a `ContextMenu` with options, see `MainWindow.xaml.cs` handlers) + 3 view mode toggle buttons (List/Covers/Details, segmented control) + a Random-game placeholder button.

2. **Content Area** (`*`): a `DockPanel` with the **Sidebar** (52px icon rail, **Library**/**Statistics** buttons via the `NavigationSection` enum in `Bridge.Core.Enums`, collapsible and re-positionable through its right-click menu) + 1px separator, then an inner `Grid` of 3 columns — `ViewsColumn` (360px, `MinWidth=200`, the List/Covers/Details views) + `Auto` (a 1px `DetailSeparator` + `GridSplitter`) + `DetailColumn` (`*`, `MinWidth=320`). When Statistics is active, the detail panel hides and the Statistics dashboard overlay spans all 3 columns.

3. **Status strip** (`Auto`, 28px): `StatisticsSummary` on the left, `StatusMessage` next to it.

View modes switch via 3 toggle buttons in the Top Panel:
- **List** (`ViewMode.List`): `ListBox` with 28×28 icons + white game names (grouped via `GamesView`). Double-click launches the game.
- **Covers** (`ViewMode.Grid`): `ListBox` whose `ItemsPanel` is `controls:CenteringWrapPanel` (a custom wrap panel that also centers each row) with 200×300px cards (base size, driven through the `ZoomToSize` converter) with hover intent (scale 1.03x + drop shadow + overlay fade, 120ms `CubicEase EaseOut`). The panel invalidates measure on `SizeChanged` so covers re-wrap when the views column expands from the List-view width (360px) to the full Grid layout. When grouping is on, `GroupStyle.Panel` uses the same `CenteringWrapPanel` (see Grouping above).
- **Details** (`ViewMode.Table`): `ListView`/`GridView` with 6 columns — Name, Release, Genre, Last Played, Time Played, Library. Name column dynamically fills remaining space via `SizeChanged` handler.

Detail panel (right): `CoverImage` 200px + title/scores/checkboxes + Play (uses `SystemAccentColorPrimaryBrush`, `#007ACC`, `CornerRadius="2"`) / More (ContextMenu with Save/Download Metadata/Delete) / Edit buttons. Below: Details (label/value pairs in accent blue) + Description (read-only here — editable via the Edit window, `GameEditWindow`). Background image (`SelectedGame.BackgroundImage`) fills the panel at 60% opacity with dark overlay.

Below the Details/Description row, when the selected game has screenshots (`SelectedGame.Screenshots.Count > 0`), the **screenshot gallery** renders (`Bridge/Controls/ScreenshotGallery.xaml(.cs)`): a large main image floating over a frosted backdrop of the same screenshot, with previous/next arrows, a counter (`1 / N`), a drag-to-scroll thumbnail strip (no scrollbar — `Preview*` mouse events pan the `ScrollViewer`), and click-to-expand into a full-window dark overlay with its own thumbnail strip, arrows and counter. The carousel auto-advances every 4s (paused while the mouse is over the main image or the overlay is open; only runs with 2+ screenshots). The main image sizes to 72% height / 82% width of its container via `Converters/FractionConverter`, keeping the "image inside image" look proportional at any window size. The gallery only appears in the **Details** view (`ViewMode.List`), matching the detail panel that hosts it. Screenshots come from either provider — Steam (`path_full`) or the IGDB Worker (`t_1080p`, non-Steam games) — they all land in the same `Game.Screenshots` JSON column. A `CompactMode` property (used by the covers info panel) hides the header and main image, shows only a smaller thumbnail strip (110×62 tiles), and opens the fullscreen overlay straight from a thumbnail tap; auto-advance never runs in that mode.

The **covers info panel** (`CompactInfoPanel`) shows its own compact layout: the hero art fades into the panel background via a strong vertical gradient (ending in the panel's `#CC1B2132` so there's no hard edge), a larger game title (22px bold, drop shadow) with the favorite star inline after it (`InlineUIContainer` in the title `TextBlock`, so the star follows the title's wrapping lines and never gets cut), the Play/More/Edit row sitting on the hero under the title, and a theme-colored square close button in the top-right. The link list wraps to two columns (`WrapPanel` + per-item `MaxWidth`). The Play button is the same animated Play/Stop template as the Details hero — there its `IsRunning` DataTriggers bind to `SelectedGame.IsRunning` because the panel's DataContext is the `MainViewModel`, not the game.

> **Known issue — hero cross-fade blur edge pops when switching tall→short backgrounds (open, 2026-08-14).**
> In the Details view, switching from a tall game background (height ≥ the 510px hero, e.g. a 16:9 screenshot used by Genshin) to a short one (height < 510) shows the short frame's bottom fade *snapping in* once the cross-fade completes, rather than revealing smoothly. What works today: applying the per-frame `OpacityMask` to **both** frames up front in `ShowImage` (`ApplyShortFrameFade()`, `FadeImage.xaml.cs`), and **not** clearing the outgoing frame's Source for the hero (`CoverByWidth`), since the hero sizes both frames itself — clearing it collapses the Grid mid-transition and makes the blur edge jump. The cover over the hero is separate and correct (`Stretch=Uniform` + fixed 170px height, outgoing Source cleared deferred). What still pops: the moment the tall frame reaches opacity 0, the short frame's own fade (anchored to the hero's 0.35 offset) becomes fully visible at once. Candidates to try next, in order: (1) animate the fade anchor of the *outgoing* tall frame downward to meet the incoming frame's edge over `TransitionDuration` (a `DoubleAnimation` on the gradient stop), so both edges converge instead of one being revealed; (2) only ever fade the *visible* frame and keep the other fully opaque until the swap (no per-frame mask on the incoming short frame until the previous is gone), accepting a brief unfaded short frame; (3) cross-fade the shared hero mask anchor itself when the incoming frame is shorter. Do not regress the cover fix (Genshin black bars) or the `CoverByWidth` hero sizing while testing.

### Selection / Hover pattern (unified)

All list-based views (List, Details, Statistics Top Played) use the same visual treatment on selection:
- `IsMouseOver`: `Background="{StaticResource Bridge.Card.Hover}"` (#2C3550)
- `IsSelected`: `Background="Transparent"` (suppresses `SystemColors.HighlightBrush` from default control template), `BorderThickness="3,0,0,0"` + `BorderBrush="{StaticResource Bridge.SystemAccentBrush}"` (#007ACC left accent bar)

The Details table row (`ListViewItem`) highlights with `Bridge.Accent.Hover` (#1F4E79).

Covers uses a similar pattern through a custom `ControlTemplate` with a `SelectionRing` `Border` and `DataTrigger` on `TemplatedParent.IsSelected`.

### Typography & Theme

All styling is defined in `Bridge/Styles/Theme.xaml` (indigo-tinted dark palette #151A28/#1B2132/#232B40/#2C3550, Inter Variable font via `pack://application:,,,/Fonts/#Inter`, spacing and corner radius tokens, motion durations). The dictionary overrides WPF-UI's semantic tokens in Color+Brush pairs — see ADR-3 in ARCHITECTURE.md for the full decision record.

### DataTrigger + DataContext convention (detail panel / hero zone)

**Rule:** Any `Style.DataTrigger` that checks `SelectedGame` from within a subtree that overrides `DataContext` to `{Binding SelectedGame}` **must use `ElementName=Root`**, never a bare `{Binding SelectedGame}`.

**Why:** When `DataContext` is overridden to the Game object, `{Binding SelectedGame}` resolves against the Game (property doesn't exist → null → trigger fires incorrectly). This has caused the same bug twice (Fase 2 ScrollViewer, Fase 7 hero Grid) — both times the entire detail panel rendered invisible because the visibility trigger collapsed everything.

**Correct pattern:**
```xml
<DataTrigger Binding="{Binding DataContext.SelectedGame, ElementName=Root}" Value="{x:Null}">
    <Setter Property="Visibility" Value="Collapsed" />
</DataTrigger>
```

---

## Version Management

**Single source of truth**: `<Version>` in `Bridge/Bridge.csproj`

```xml
<Version>0.1.0</Version>
<AssemblyVersion>$(Version).0</AssemblyVersion>
```

- `AssemblyVersion` derives from `$(Version)` so assembly version is correct (e.g., `0.1.0.0`)
- **Updater (implemented)** — `Bridge/Services/AppUpdateService.cs` checks `https://api.github.com/repos/{owner}/{repo}/releases?per_page=100` (User-Agent `Bridge/{version}`, `Accept: application/vnd.github+json`), picks the newest release that matches the **update channel**, compares its `tag_name` numeric prefix against `Config.AssemblyVersion`, and when remote is newer finds the `Bridge.exe` asset (`browser_download_url`). The channel lives in `UpdateChannelSettingsStore` (`update-channel.txt` under AppDataPath): **Stable** (default) skips GitHub prereleases, **Beta** accepts them, toggled in the About window. Prerelease tags keep their suffix in the repo but are compared by numeric prefix only (`v0.3.0-beta1` never beats an installed `0.3.0` stable, so a stable user can't be downgraded onto a beta); the `prerelease` flag is what distinguishes channels. Registered as a singleton in `App.xaml.cs` alongside the shared `HttpClient` (whose `Timeout` is now `Config.RequestTimeoutSeconds = 10`). The check runs automatically (silently) at startup and on demand from **Check for updates…** in the app menu; "Not now" on the confirm dialog keeps the update pending and shows the download button next to the random-game button (tooltip + `ApplyPendingUpdateCommand`). On the stable channel, when a newer prerelease exists but no newer stable, the up-to-date message points the user to the beta toggle.

  The most critical part is the **safe executable swap** — never overwrite the running `.exe` directly:

  ```csharp
  var currentExe = Environment.ProcessPath;
  var tempExe = Path.Combine(Path.GetTempPath(), $"Bridge_update_{Guid.NewGuid()}.exe");
  var oldExe = currentExe + ".old";

  await DownloadFileAsync(downloadUrl, tempExe, progress);
  BackupDatabase();              // copy bridge.db → bridge.db.bak-update (recovery net)
  File.Delete(oldExe);           // discard any stale .old
  File.Move(currentExe, oldExe); // rename running exe → .old
  File.Move(tempExe, currentExe); // rename downloaded → current location
  File.WriteAllText(currentExe + ".update-pending", version); // arm the handshake

  Process.Start(new ProcessStartInfo { FileName = currentExe, UseShellExecute = true });
  Environment.Exit(0);           // terminate running instance so new exe takes over
  ```

  On the next launch the **update handshake** (replacing the old one-shot `CleanupOldExe`)
  decides what the new exe owns:

  - `AppUpdateService.HandleUpdateHandshake()` runs at the very top of
    `App.OnStartup`, before any window/DB/DI work. Three files next to the exe
    drive it: `<exe>.update-pending` (armed by `ApplyUpdateAsync` after the swap),
    `<exe>.old` (the previously working exe), and `<exe>.failed` (a broken new
    exe a rollback moved aside). When pending + old are both present, the old
    exe is **kept** as the rollback copy — it is not deleted yet.
  - If startup succeeds (the window shows), `AppUpdateService.ConfirmUpdateApplied()`
    deletes the `.old` and the pending marker: the new exe has proven it runs.
  - If startup **fails** (an exception from `OnStartup`'s try block or the
    dispatcher handler — bad XAML, DB/DI failure), `AppUpdateService.RollbackToPrevious()`
    moves the broken new exe to `<exe>.failed`, restores the previous exe from
    `.old`, clears the pending marker and relaunches it. The `.failed` leftover
    is cleaned up by the next `HandleUpdateHandshake`. The user ends up on the
    version that worked instead of a broken build.

  `BackupDatabase()` copies `bridge.db` to `bridge.db.bak-update` (next to the DB,
  overwritten each update) before the swap — a manual-recovery safety net for the
  day a schema migration goes wrong, **not** auto-restored.

  Security constraints: downloads only over HTTPS, only from GitHub's hosts
  (`api.github.com`, `github.com`, `*.githubusercontent.com` — with redirects
  manually followed against the allowlist), capped at 256 MB, with a 15-minute
  timeout and byte-count progress reported to the status bar. `CanSelfUpdate`
  returns false in dev (`dotnet run`/the build output) so updates never try to
  replace a non-published exe. Covered by `Bridge.Tests/Services/AppUpdateServiceTests.cs`.

**To bump the version**: edit `<Version>` in the csproj, commit with a descriptive message, push to `main`.

### Schema Migrations

> The auto-updater replaces the exe; the **schema** of `bridge.db` is updated by
> the app itself at startup, applying EF Core migrations on top of the user's
> existing AppData DB — so a release can change the DB structure without the
> user re-downloading or losing data. This is how the updater "updates
> everything, not just the exe": the exe swap ships new code **and** new
> migrations, and the migrations run on the user's next launch.

**Real EF migrations** replaced the old `EnsureCreated()` + raw-SQL `EnsureColumn`
mini-migrations:

- **Migrations live in `Bridge.Storage/Migrations/`** — generated with
  `dotnet ef migrations add <Name> --project Bridge.Storage --startup-project Bridge.Storage`
  (a `BridgeDbContextDesignTimeFactory` lets the tools build the context without
  launching the WPF app; it uses `BRIDGE_DEV_DB` when set, otherwise a temp file —
  **not** the real `%LOCALAPPDATA%\Bridge\bridge.db`, so `dotnet ef` never touches
  user data).
- **Applied at startup** by `BridgeDbMigrator.MigrateToLatest()` in
  `Bridge/App.xaml.cs` (where `EnsureCreated` + the three `EnsureColumn` calls
  used to be). `Database.Migrate()` applies every pending migration in order.
- **Pre-migrations DBs are baselined, not rebuilt.** A DB created by the old
  `EnsureCreated` era has all 14 tables but no `__EFMigrationsHistory` table,
  so a bare `Migrate()` would fail trying to recreate tables that already
  exist. `MigrateToLatest` detects that case (tables present, no history) and
  baselines it: it creates the history table and records `InitialCreate` as
  already applied, so the user's existing schema **and data** survive untouched
  and only future migrations apply on top. Verified against the real
  `%LOCALAPPDATA%\Bridge\bridge.db` (46 games, 20 genres) and covered by
  `Bridge.Tests/Storage/MigrationTests.cs` (fresh-DB and pre-migration baseline).
- **`AddUniqueIndexes` (2026-08-18)** — unique indexes on reference-entity names
  (`Genres`, `Platforms`, `Companies`, …) and on `(ExternalId, SourceId)` for
  `Games`. Migration fails if duplicate rows already exist; clean up duplicates
  before upgrading. Storage unit tests now call `MigrateToLatest()` instead of
  `EnsureCreated()` so they exercise the same path as production.

**To change the schema** (add/remove a column or table, etc.):

```powershell
# 1. Edit the entity in Bridge.Core and/or BridgeDbContext.OnModelCreating
dotnet ef migrations add <Name> --project Bridge.Storage --startup-project Bridge.Storage
# 2. Review Bridge.Storage/Migrations/<Timestamp>_<Name>.cs (the generated SQL)
```

The migration ships with the release; users on any older version get the new
schema automatically on their next launch — no re-download, no data loss. SQLite
only supports a subset of `ALTER TABLE` natively; EF Core generates table-rebuild
migrations for anything else, which is fine but heavier (a large `Games` table is
copied). Prefer additive changes (new columns/tables) where possible; a column
**drop or rename** rebuilds the table.

### Welcome Sentinel

> **Not implemented** — pattern reference only. `Config.WelcomeSentinelFile`, `Config.ShouldShowWelcome()` and `WelcomeWindow` do not exist in the codebase; nothing on this page creates `welcome_sentinel.txt`.

Show a welcome dialog on first launch or after a version change:

```csharp
// In Config.cs
public const string WelcomeSentinelFile = "welcome_sentinel.txt";
public static readonly string AppDataPath =
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bridge");

// Sentinel check
public static bool ShouldShowWelcome()
{
    var flagPath = Path.Combine(Config.AppDataPath, Config.WelcomeSentinelFile);
    if (!File.Exists(flagPath)) return true;
    return File.ReadAllText(flagPath) != Config.AssemblyVersion;
}

// After showing welcome (e.g. in MainWindow startup):
if (ShouldShowWelcome())
{
    var welcome = new WelcomeWindow { Owner = this };
    welcome.ShowDialog();
}
```

When the user dismisses the dialog with "Don't show again", write the current version to the sentinel file so it won't show again until the version changes.

### Constants Pattern (`Config.cs`)

> **Implemented** — the real `Bridge/Config.cs` holds `AppName`, `AppDataPath`, `DatabasePath`, the emulator paths, `GitHubReleasesUrl`, `RequestTimeoutSeconds`, `UpdateAssetName`, and `AssemblyVersion` (read from the executing assembly).

**Prefer keeping constants centralized** in a dedicated `Config.cs` file rather than scattered across classes.

```csharp
public static class Config
{
    public const string AppName = "Bridge";
    public const string GitHubReleasesUrl = "https://api.github.com/repos/ZavalaSebas/Bridge/releases?per_page=100";
    public const int RequestTimeoutSeconds = 10;
    // ... etc
}
```

---

## Semantic Versioning (SemVer)

> **Always follow SemVer for version numbers.**

Format: `MAJOR.MINOR.PATCH`

```
MAJOR.MINOR.PATCH
  │     │     │
  │     │     └── Fixes, bugs, security patches
  │     └──────── New features (backwards compatible)
  └────────────── Breaking changes (incompatible with previous)
```

### When to bump

| Change Type | Bump | Example |
|-------------|------|---------|
| Fix bug | PATCH | `1.0.0` → `1.0.1` |
| New feature | MINOR | `1.0.1` → `1.1.0` |
| Breaking change | MAJOR | `1.0.0` → `2.0.0` |
| Pre-release | Suffix | `1.0.0-beta.1` |

### Rules

1. **Start at 0.x.y** — while in development, MAJOR is 0
2. **Once 1.0.0** — public API is stable
3. **Never reuse versions** — if you delete a release, don't reuse that version number
4. **Update CHANGELOG.md** — document what changed in each version

---

## Packaging (Fase 8 findings — read before touching publish settings)

`Bridge.csproj` bakes in `PublishSingleFile`, `SelfContained`, `RuntimeIdentifier=win-x64`, and `IncludeNativeLibrariesForSelfExtract`. `Directory.Build.props` (repo root, applies to every project) sets `DebugType=none` for Release. Together these are what make `dotnet publish Bridge/Bridge.csproj -c Release -o ./publish` produce a genuine single file. **Do not remove `IncludeNativeLibrariesForSelfExtract` or the `Directory.Build.props` DebugType setting** without re-verifying the publish output only contains `Bridge.exe` — both were added because of two concrete, empirically-found problems, not as defensive boilerplate:

1. **Without `IncludeNativeLibrariesForSelfExtract`**: the documented publish command (just `PublishSingleFile`+`SelfContained`+`-r win-x64`) produced `Bridge.exe` **plus 6 sidecar files** — `D3DCompiler_47_cor3.dll`, `PenImc_cor3.dll`, `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll` (WPF's own native interop libraries) and `e_sqlite3.dll` (native SQLite driver). This is a documented .NET/WPF limitation, not a Bridge bug — verified by actually running the publish and looking at the output folder, not assumed from docs.
2. **Without the `Directory.Build.props` `DebugType=none`**: setting `DebugType=none` only on `Bridge.csproj` itself left `Bridge.Core.pdb`/`Bridge.Storage.pdb`/`Bridge.Metadata.pdb` (from the *referenced* projects) sitting next to `Bridge.exe` — also verified empirically, not assumed.

**`PublishReadyToRun=true` was tried and rejected** — measured slower startup (2671ms vs. a ~2000ms average without it) and a larger file (176MB vs 148MB). Don't re-enable it without a fresh measurement; there's no guarantee this app's startup profile won't change as more of it gets built.

**Measured baseline** (3 runs, published exe launched from an isolated folder — not the dev `bin/` output): ~2.6 seconds from process start to visible window (2548-2614ms across runs), ~180MB RAM at rest (WorkingSet). The pre-overhaul baseline was ~2s / ~147MB — the +~600ms / +~33MB regression is attributable to WPF-UI theme dictionaries, Inter Variable font (879KB), Mica DWM compositing, and per-card DropShadowEffect. A deferral experiment (`Loaded` event) didn't help. No numeric startup/RAM target was ever set — these are documented as the current baseline, not a pass/fail measurement. If startup time becomes a real complaint, try trimming `App.OnStartup` work before window show.

---

## Release Process (CI/CD)

On push or PR to `main` (and on manual `workflow_dispatch`), `.github/workflows/release.yml` runs:

1. **Check version change** — compares `<Version>` in HEAD vs HEAD~1
2. **Build** — `dotnet restore Bridge.slnx` then `dotnet build Bridge.slnx -c Release --no-restore`
3. **Test** — `dotnet test Bridge.slnx -c Release --verbosity normal` (no `--no-build`: each job runs on its own fresh runner, so there's no build output to reuse and `--no-build` would always fail — see the comment in `release.yml`)
4. **CodeQL** — security scanning
5. **NuGet Audit** — `dotnet nuget audit` (vulnerability check, `continue-on-error`)
6. **Release** (only if version changed):
   - `dotnet publish` as self-contained single-file
   - Generate body from commit message
   - Create tag + release with `.exe`

### Critical workflow details

- `fetch-depth: 0` — required so `git show HEAD~1:path` can access the parent commit
- `permissions: contents: write` — required for release creation
- Csproj path: `Bridge/Bridge.csproj`
- Release body comes from the **commit body** — write it with `### Added/Fixed/Changed` sections

### Publishing a beta/prerelease

To ship a test build to the **Beta** channel only: bump `<Version>` to the
numeric version the beta targets (e.g. `0.3.0`), commit with a body
describing the changes, and on GitHub create the release with **"Set as a
pre-release"** checked and a tag like `v0.3.0-beta1` (any `-suffix` is fine —
Bridge compares the numeric prefix, so `v0.3.0-beta1` won't offer itself to a
user already on `0.3.0`). The `Bridge.exe` asset must use the same name as
stable releases. Stable users never see it; users who enabled **Receive beta
builds** in About will.

### Additional Quality Gates (optional)

Beyond the standard CI pipeline, these checks can be added as the project grows:

| Gate | Tool | When to Add |
|------|------|-------------|
| Formatting enforcement | `dotnet format` | Team with shared style |
| Linting | Roslyn analyzers (StyleCop) | Team or strict consistency |
| Coverage threshold | `coverlet` + gate | Before 1.0.0 |
| API compatibility check | `ApiChange` | Pre-1.0.0 stabilizes |

These are **not enabled by default** — add them only when the team size or project scope justifies the overhead.

---

## Release Checklist

### Pre-release

- [ ] All features for this version are complete
- [ ] All tests pass locally: `dotnet test Bridge.slnx -c Release`
- [ ] No compiler warnings (or warnings documented)
- [ ] Code reviewed (if working with others)

### Version Bump

- [ ] Update `<Version>` in `Bridge/Bridge.csproj`
- [ ] Update `CHANGELOG.md` with new version and changes
- [ ] Commit with subject `bump vX.Y.Z — <short summary>` and body with `### Added / Fixed / Changed` sections (the commit body becomes the GitHub Release body)

### Commit & Push

- [ ] `git status` — no unexpected changes
- [ ] `git diff` — review all changes
- [ ] `git log --oneline -3` — verify commit history
- [ ] `git push origin main`

### Post-release

- [ ] Verify GitHub Actions workflow completed
- [ ] Check release page on GitHub
- [ ] Test downloaded `.exe` works
- [ ] Update documentation if needed

### Hotfix

When a critical bug is found in a released version and cannot wait for the next regular release:

1. **Identify the tag** of the broken release — `git tag --list 'v*' | sort -V`
2. **Create branch** from that tag: `git switch -c hotfix/v1.1.1 v1.1.0` — branch name `hotfix/v1.1.1` on the left, existing tag `v1.1.0` (the broken release) on the right
3. **Fix the bug** — apply only the minimal changes needed; no unrelated refactors
4. **Bump `<Version>`** in `Bridge/Bridge.csproj` — increment PATCH only (e.g. `1.0.0` → `1.0.1`)
5. **Update `CHANGELOG.md`** — add entry under new version with `### Fixed`
6. **Commit** with subject `bump vX.Y.Z — <short summary>` and body describing the fix
7. **Push branch**: `git push origin hotfix/v1.1.1`
8. **Open PR** to `main` with a clear title referencing the hotfix version
9. **Merge PR** — CI runs on merge to `main` and creates the release automatically
10. **Verify** — release created on GitHub and the `.exe` works

---

## Documentation Sync Map

When you make a change, consult this table to know which documents to update:

| If you changed… | Update these document(s) | What to update specifically |
|---|---|---|
| **A user-facing feature** (added, modified, or removed) | `README.md` (Features section), `CHANGELOG.md` | README: add/update the feature name with a one-line description; CHANGELOG: new entry under `[Unreleased]` with `### Added` / `### Changed` / `### Removed` |
| **A breaking API change** | `CHANGELOG.md`, `DEVELOPMENT.md` (Version Management → SemVer) | CHANGELOG: `### Changed` with migration instructions; DEVELOPMENT.md: verify the change justifies a MAJOR bump per SemVer rules |
| **An architecture decision or the "why" behind a pattern** | `ARCHITECTURE.md` (Key Design Decisions) or the equivalent section in `DEVELOPMENT.md` if the project has not split documents | Add a row to the table with the decision, rationale, and consequences |
| **A discovered limitation or unfixable bug** | `DEVELOPMENT.md` (Known Limitations table) | Add a row with the limitation, its root cause, and the recommended workaround |
| **The build, test, or release process** (CI workflow, scripts, tooling) | `DEVELOPMENT.md` (Release Process / CI-CD), `.github/workflows/release.yml` (the source of truth) | Update the prose description to match the actual workflow — if the YAML changed, the docs must reflect it |
| **A new NuGet dependency** | `ARCHITECTURE.md` (Key Design Decisions) — only if the choice is architecturally significant | Add a table row explaining why this library was chosen over alternatives (batteries-included vs lightweight, license compatibility, etc.) |
| **The project folder structure** (new project, new top-level folder) | `DEVELOPMENT.md` (Project Structure) | Update the ASCII tree to match the new layout |
| **The environment requirements** (SDK version, OS, IDE) | `DEVELOPMENT.md` (Development Environment Setup) | Update the Requirements table with the new version or tool |
| **An error-handling pattern** (custom exception, global handler change) | `DEVELOPMENT.md` (Error Handling) | Add the new exception class or update the requirements / examples |
| **The contribution workflow itself** (PR process, branch naming, review policy) | `CONTRIBUTING.md` (Workflow) | Update the numbered steps, commit format, or branch naming conventions |

> This table has no row for "bootstrapping a new project from a template" — that flow belonged to a checklist that was deleted once this project's setup finished, and it doesn't apply downstream.

**Rule:** Before marking a code change as complete, review this table and decide whether any document needs updating accordingly. Document updates are part of the change, not an afterthought — include them before closing the work.

---

## Documentation Audit Checklist

Over time, documentation drifts from reality. Run this audit periodically (or when something feels "off" in the docs) to bring it back in sync:

1. **Read all documents in full** — not skimming, every word. You cannot spot inconsistencies in a document you haven't fully read.
2. **Compare every claim against the actual code/state** — do not assume anything is still true. If it says "the config lives in Config.cs", verify that Config.cs still exists and has that constant.
3. **Classify each finding** as one of:
   - **Inconsistency** — the docs say X, the code does Y (contradiction)
   - **Outdated** — the docs describe something that no longer exists or has changed
   - **Redundant** — the same information appears in multiple places with risk of future drift
4. **Present findings in a table** with one row per issue: location, finding type, description, and proposed action
5. **Present findings for review** before touching any file — do not "just fix" inconsistencies without a shared understanding of what needs to change and why
6. **Stick to the agreed scope** — no scope creep during a documentation pass. If new inconsistencies appear during the fix, log them separately and address in a follow-up pass.
7. **Final cross-check**: after applying changes, verify that documents do not contradict each other. Strip any "planned"/"future"/"roadmap" language from active documents — track future ideas in GitHub Issues if needed, not in a living roadmap document that will drift again.

---

## Tests

Run locally with: `dotnet test Bridge.slnx -c Release` — 102 tests, all passing as of this writing.

`Bridge.Tests` (`net10.0-windows` — not plain `net10.0`, because it references `Bridge`, a WPF project, and a plain-`net10.0` project can't reference a `net10.0-windows` one) covers:
- `Storage/GameRepositoryTests.cs` — full field round-trip through real SQLite (not `:memory:` — the JSON-converter/EF mapping is exactly what needs verifying, and in-memory providers skip that code path), the `(ExternalId, SourceId)` dedup lookup, in-place `GameActions` mutation + `Update()`. Schema via `MigrateToLatest()`.
- `Storage/RepositoryTests.cs` — `GetOrCreateByName` dedup, including case-insensitivity and retry on unique-index races. Schema via `MigrateToLatest()`.
- `Core/PathContainmentTests.cs` — `PathContainment.IsPathUnderDirectory` (normal paths, trailing separators, `..` rejection).
- `Services/SafeLauncherTests.cs` — URL allowlist (`UrlValidator`) and uninstall-string parsing/rejection.
- `Import/SteamLibraryImporterTests.cs` — library folder detection, StateFlags filtering, re-scan update behavior (test content shaped exactly like real Steam files, verified against a real install, `PROJECT_FOUNDATION.md` §28.26).
- `Import/VdfParserTests.cs` — the hand-rolled VDF tokenizer/parser: key/value pairs, nested blocks, escape sequences, unquoted-junk tolerance.
- `Import/SteamLocalIconResolverTests.cs` — `TryGetLocalIconPath` picks the 40-hex clienticon file over `header.jpg`, returns null when there's no cached icon / non-numeric appid / missing Steam install.
- `Import/SteamLocalPlaytimeResolverTests.cs` — reads real `localconfig.vdf` shape: minutes→seconds conversion, LastPlayed→LastActivity, zero-playtime skipped, missing LastPlayed stays null, multi-account merge (max playtime / latest activity), missing files and malformed config return null.
- `Import/SteamPlayActionsTests.cs` — the automatic Steam play action resolution (pure logic in `SteamPlayActions`, testable without Steam installed): URL action + Directory tracking for a numeric appid, null for custom games / non-numeric ExternalId.
- `Services/RomScannerTests.cs` — extension recognition via `RomPlatformCatalog`, dedup-by-existing-ROM-path, missing-directory error, recursive scan into subfolders, companion-file filtering (`.sav`/`.srm`), `SanitizeName` + `ToSearchName` name normalization, and the managed "Bridge RetroArch" `GameAction` creation.
- `Services/AppUpdateServiceTests.cs` — `CheckForUpdateAsync` against the fake `HttpMessageHandler`: a newer remote release → `UpdateAvailable` with the right version + `Bridge.exe` download URL; a matching version → `UpToDate`; a release with no `Bridge.exe` asset → `Failed`. The swap/restart itself is not covered (it replaces the running exe — see the deliberately-not-covered note below).
- `Statistics/LibraryStatisticsTests.cs` — pure computation, no I/O.
- `Statistics/GameSortComparerTests.cs` — `GameSortComparer` sort-by-field logic: ascending/descending by name, playtime, last activity, favorite, plus reference resolution (Developer, Source) through the name lookups and the "empty values sort last in both directions" rule.
- `Statistics/GameGroupResolverTests.cs` — `GameGroupResolver` group-key logic: first-letter Name groups, reference-name resolution (Developer/Library), install/completion buckets, playtime/install-size buckets, drive letter, release year, and the empty key for "Don't group".
- `Metadata/IgdbAuthClientTests.cs`, `Metadata/IgdbMetadataProviderTests.cs` — the whole IGDB flow (OAuth token fetch + caching, request format, response mapping, error paths) against a fake `HttpMessageHandler` (`Metadata/FakeHttpMessageHandler.cs`) — not real IGDB credentials, see [ARCHITECTURE.md ADR-10](ARCHITECTURE.md#adr-10-igdb-as-a-text-metadata-source-primary-not-sole--see-adr-12) for why that's a real, flagged limitation and not silently glossed over.
- `Metadata/SteamDescriptionBlocksTests.cs` — `SteamMetadataProvider.ParseDescriptionBlocks` (via reflection over the non-public static): paragraphs/headings/subheadings/lists/images kept in order and mapped to `DescriptionBlockKind`, inline markup stripped but text kept.
- `Metadata/SteamSearchRegexTests.cs` — the Steam Store search-result regex: `data-ds-appid` captured even when `data-ds-packageid` comes first, bundle links without an appid don't match.
- `Services/InstalledGameDetectorTests.cs` — `InstalledGameDetector.ScanFolder` against a temp folder: `.exe`/`.bat` found, unrelated extensions ignored, installers/helpers (setup/unins/vc_redist/UnityCrashHandler/python) filtered out, recursive scan, missing folder throws.
- `ViewModels/GameEditViewModelTests.cs` — `GameEditViewModel.Save` against a real SQLite DB: empty-name rejection, name trim + persist, new-game `Added` set, existing-game `Added` preserved, `SortingName` persisted.
- `Services/ProcessTreeExpanderTests.cs` — the pure process-tree expansion: launcher spawning a game keeps both alive; the launcher exiting after spawning leaves the game (and only the game) tracking; all processes exiting empties the tree; multi-hop launchers (launcher → updater → game) join across polls; a launcher that never spawns anything prunes away with no phantom session.
- `Storage/GameRepositoryTests.cs` (`GetAll_ResetsTransientRuntimeFlags_ButPreservesIsRunning`) — `GetAll` resets the transient install/launch flags but preserves `IsRunning`, because it's a live launcher flag that the background metadata sync must not clobber (the stale-`IsRunning` crash reset happens once in `MainViewModel.LoadGames`).

**Deliberately not covered by automated tests**: `GameLauncher`'s actual process-launching (spawning real OS processes in every CI run is slow and flaky — that's why it was verified manually via scratch scripts during development instead, see the session history in git log / PR descriptions once those exist). The Steam play action *resolution* is covered (`SteamPlayActionsTests`) since it's pure logic, but the real `steam.exe -silent "steam://..."` invocation, directory-based process watching, and the process-tree *snapshot* (`ProcessTreeSnapshot`, the Toolhelp32 P/Invoke boundary) are not — the tree *expansion* is covered (`ProcessTreeExpanderTests`) and the snapshot was verified end-to-end with a real launcher→child process pair. Directory tracking matches processes by install-directory path *and* by executable name (`GetExecutableNames`) — the name fallback exists because Genshin's HoYoPlay launcher (`HYP.exe`) runs elevated and its `MainModule.FileName` is unreadable. An already-launched session uses a **launch-aware idle grace**: 20s while the session is under 30s old (a gap right after Play is the launcher still spawning the game, e.g. across Genshin's UAC relaunch), dropping to 5s once the game has been running so the close is detected quickly. All verified live against the running HYP processes and the observed UAC flow. If this ever needs automated coverage, introduce a `Process.Start` abstraction to mock against rather than launching real processes in `Bridge.Tests`. Likewise, the visual controls (the hero `FadeImage` cover-by-width sizing and the `ScreenshotGallery` carousel/drag/fullscreen behaviors) are WPF XAML/code-behind and are verified by launching the app, not by unit tests.

**Real bug found and fixed while writing these tests**: `Microsoft.Data.Sqlite` pools connections by default, so `File.Delete` on a temp `.db` file right after disposing its `DbContext` intermittently throws `IOException` — the connection isn't actually released yet. Fixed by adding `Pooling=False` to the test-only connection strings. This wasn't just a scratch-script annoyance — it would have made these exact tests flaky in CI if left in.

> **Note**: Tests run in CI on every push (see `.github/workflows/release.yml`, `test` job). If a test fails, the build is blocked.

### Test conventions

- One `[Fact]` per test method (no `[Theory]` unless data-driven)
- No test dependencies — each test is independent
- Arrange → Act → Assert pattern
- Test class name = Service/Class name + "Tests" (e.g., `MyServiceTests`)
- Namespace mirrors source: `Bridge.Tests.Services.MyServiceTests`

---

## External launch and path safety

User-triggered URLs and uninstall commands must go through the shared helpers — do not call `Process.Start` with raw strings from the DB or registry.

| Helper | Location | Use |
|--------|----------|-----|
| `UrlValidator.IsSafeToOpen` | `Bridge.Core/Utilities/UrlValidator.cs` | HTTPS + `steam://` + Epic launcher scheme allowlist; blocks `javascript:`, `file:`, private IPs (SSRF) |
| `SafeLauncher.TryOpenUrl` | `Bridge/Services/SafeLauncher.cs` | Opens validated URLs (About, links panel, uninstall fallback browser) |
| `SafeLauncher.TryRunUninstallCommand` | same | Parses registry `UninstallString`, rejects `cmd`/PowerShell chains, requires executable on disk |
| `PathContainment.IsPathUnderDirectory` | `Bridge.Core/Utilities/PathContainment.cs` | Library importers and `GameLauncher.KillProcessesInDirectory` — paths must stay under the library root |

Metadata image downloads use the same URL rules in `RemoteImageCache`. IGDB credentials are stored with DPAPI (`IgdbSettingsStore`); the Twitch token request sends the client secret in the POST body, not the query string.

---

## Logging

The project does **not** use `Microsoft.Extensions.Logging` / `ILogger` — that was aspirational. The real logging is a single entry point: `App.LogException(Exception)` in `Bridge/App.xaml.cs`, which appends a timestamped full-exception dump to `%LOCALAPPDATA%\Bridge\logs\errors.log` (creating the `logs` folder on demand). It's called from the global `DispatcherUnhandledException` handler (before the `MessageBox`) and from the catch blocks of fire-and-forget paths that never reach the dispatcher, so failures aren't silently swallowed.

### Requirements

- **Route exceptions through `App.LogException`** — the one place unhandled errors reach disk
- **Never swallow exceptions silently** — code that catches and continues (e.g. `RemoteImageCache`'s decode catch) must still call `App.LogException`
- **No `Debug.WriteLine`** — `App.LogException` is the only logging surface
- **Logging must never crash the app** — `App.LogException` swallows its own failures by design

---

## Error Handling

### Requirements

1. **Never swallow exceptions silently** — always log or notify the user
2. **Global exception handler** in `App.xaml.cs` for unhandled exceptions
3. **Use custom exceptions** when they add context
4. **User-facing errors** should update StatusMessage or show a dialog

### Custom Exceptions

Define domain-specific exceptions in code when a custom type adds context:

```csharp
public class BridgeException : Exception
{
    public BridgeException(string message) : base(message) { }
    public BridgeException(string message, Exception inner) : base(message, inner) { }
}
```

---

## Bug Investigation Process

When investigating a bug, the goal is to find the *actual* cause — not the first explanation that sounds convincing. Follow this process to avoid wasting time on plausible but wrong theories:

1. **Formulate a specific, testable hypothesis** — not "it might be X", but "if X is the cause, then when I do Y, Z should happen".
2. **Test that hypothesis with real evidence** (logs, temporary instrumentation, actual execution) — never accept a hypothesis because it "sounds logical" or because the code "looks similar" to a working reference.
3. **If the hypothesis is ruled out by evidence, say so explicitly and move on** — do not leave it as a "possible cause" without resolution.
4. **When an explanation is accepted, confirm it with a direct test before applying the fix** — do not fix based on theory alone.
5. **After the fix, add a regression test** that would have caught the original bug.
6. **Document the finding before closing** — do not let the real cause live only in the conversation history. Log it in the appropriate place depending on the type of finding:
   - **Actual bug that was fixed**: entry in `CHANGELOG.md` under `### Fixed` with a brief description of the *root cause*, not just the symptom.
   - **Investigation revealed a known limitation** that cannot be resolved now: `DEVELOPMENT.md` (Known Limitations table), with root cause and workaround.
   - **Investigation ruled out a hypothesis worth recording** (e.g., "not a threading issue — confirmed with test X so nobody has to rediscover that"): a short note in `ARCHITECTURE.md` or `DEVELOPMENT.md` as appropriate.
7. **Clean up any temporary instrumentation/logging** used during the investigation before closing.

---

## Dependency Injection

Use `Microsoft.Extensions.DependencyInjection` for service management.

### Registration

```csharp
// App.xaml.cs
var services = new ServiceCollection();
services.AddSingleton<IMyService, MyService>();
services.AddTransient<MainViewModel>();
// ... etc
ServiceProvider = services.BuildServiceProvider();
```

### Lifetime Guidelines

| Lifetime | Use for |
|----------|---------|
| `Singleton` | Services that hold state, external connections |
| `Transient` | ViewModels, lightweight stateless services |
| `Scoped` | Rarely used in WPF |

---

## MVVM Pattern

This project follows the **Model-View-ViewModel** pattern for UI separation.

### Components

| Component | Responsibility |
|-----------|----------------|
| **Model** | Data and business logic (no UI) |
| **View** | XAML + code-behind (visible UI only) |
| **ViewModel** | UI state + commands, exposes data to the View |

### ViewModel Requirements

- Inherit from `ObservableObject` or implement `INotifyPropertyChanged`
- Expose data via properties — never fields
- Expose actions via `ICommand` properties
- No direct reference to Views

```csharp
public class MainViewModel : ObservableObject
{
    private string _status;

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public ICommand SaveCommand => new RelayCommand(Save);

    private void Save()
    {
        Status = "Saved";
    }
}
```

### Commands

Use `RelayCommand` (from CommunityToolkit.Mvvm) for simple commands:

```csharp
public ICommand SaveCommand => new RelayCommand(Save);
public ICommand AsyncCommand => new AsyncRelayCommand(LoadDataAsync);
```

### View-ViewModel Wiring

In `App.xaml.cs` or a DI container, create the ViewModel and assign it as the View's `DataContext`:

```csharp
var mainVm = ServiceProvider.GetRequiredService<MainViewModel>();
var mainWindow = new MainWindow { DataContext = mainVm };
mainWindow.Show();
```

### WPF Bindings

Bind ViewModel properties to XAML using `{Binding}`:

```xml
<TextBlock Text="{Binding Status}" />
<Button Content="Save" Command="{Binding SaveCommand}" />
```

---

## Async/Await Patterns

All I/O operations should be async.

### Requirements

- Use `async Task` return types
- **Never use `.Result` or `.Wait()`** — blocks the UI thread
- Use `CancellationToken` for cancellable operations
- Use `IProgress<T>` for progress reporting

### Example

```csharp
public async Task<List<Item>> GetItemsAsync(CancellationToken cancellationToken)
{
    _logger.LogInformation("Fetching items");

    var response = await _httpClient.GetAsync(_url, cancellationToken);
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    return JsonSerializer.Deserialize<List<Item>>(content);
}
```

---

## Configuration

All configuration goes in `Config.cs`:

```csharp
public static class Config
{
    // URLs
    public const string ApiUrl = "https://api.example.com";
    public const string UserAgent = "Bridge/0.1.0";

    // Timeouts
    public const int RequestTimeoutSeconds = 10;

    // Paths
    public static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Bridge");
}
```

### Rules

- **No magic numbers** — use named constants
- **No hardcoded secrets** — use environment variables or user input
- **Urls configurable** — makes testing easier

---

## Key Files Quick Reference

| File | Purpose |
|------|---------|
| `PROJECT_FOUNDATION.md` | Source analysis and rewrite rationale this project is based on — the detailed reference behind `PLAN.md`/`ARCHITECTURE.md` |
| `PLAN.md` | Phase tracking, scope, risks, open decisions |
| `ARCHITECTURE.md` | ADRs — the "why" behind structural decisions |
| `Bridge.Core/Entities/Game.cs` | The central domain entity — start here to understand the data model |
| `Bridge.Core/Import/GameMetadata.cs` | What an importer produces before it becomes a `Game` |
| `Bridge.Core/Contracts/IGameRepository.cs` | The persistence contract `Bridge.Storage` implements |
| `Bridge/App.xaml.cs` | Composition root — DI setup, theme dictionaries, logging |
| `Bridge/Config.cs` | App constants (AppName, paths) |
| `Bridge/Services/SafeLauncher.cs` | Validated external URLs and uninstall commands — see External launch and path safety |
| `Bridge/Services/MetadataSyncService.cs` | Centralized metadata provider fallback chains |
| `Bridge.Core/Utilities/PathContainment.cs` | Path-under-root checks for importers and process killing |
| `Bridge.Core/Utilities/UrlValidator.cs` | HTTPS/steam/epic allowlist + SSRF guard for user-facing URLs |
| `Bridge.Storage/BridgeDbContext.cs` | EF Core + SQLite context (schema via EF migrations — `Bridge.Storage/Migrations/`, applied by `BridgeDbMigrator.MigrateToLatest`) |
| `Bridge.Metadata/BridgeIgdbProvider.cs` | IGDB metadata via Bridge's own Cloudflare Worker (zero-config) — first in the metadata chain |
| `Bridge.Metadata/PlayniteIgdbProvider.cs` | IGDB via Playnite's public proxy — fallback if the Worker is unreachable |
| `Bridge.Metadata/IgdbMetadataProvider.cs` | IGDB with a user-configured Twitch key (optional) |
| `Bridge.Import/Epic/` | Epic Games detection (`EpicLibraryImporter`, `EpicPaths`) — installed games from local launcher files, launch via `com.epicgames.launcher://` |
| `Bridge.Infra/igdb-proxy-worker/` | The Cloudflare Worker backend that holds the IGDB key (see its README) |
| `Bridge/Services/RetroArchService.cs` | Bridge-managed RetroArch: resolves the latest release from GitHub, downloads the `.7z` from Libretro's buildbot, extracts with SharpCompress (solid-7z via `WriteToDirectory`), swaps atomically, and installs cores on demand — see "Managed Emulation" below |
| `Bridge/Services/RomPlatformCatalog.cs` | Curated platform→core table (15 systems → Libretro core DLL + ROM extensions) that drives ROM recognition |
| `Bridge/Services/RomScanner.cs` | Recursive ROM scan by extension, companion-file filtering, managed "Bridge RetroArch" `GameAction`, `SanitizeName`/`ToSearchName` |
| `Bridge/Services/AppUpdateService.cs` | Self-updater: checks GitHub Releases against `Config.AssemblyVersion`, downloads the `Bridge.exe` asset with host-allowlist + size-cap + progress, applies the safe swap (running exe → `.old`, downloaded → current, restart, startup cleanup) — see "Version Management" above |

## IGDB Metadata Infrastructure

Bridge gets IGDB metadata with **zero user configuration** via its own
Cloudflare Worker (`Bridge.Infra/igdb-proxy-worker/`). See
[ADR-13](ARCHITECTURE.md#adr-13-own-cloudflare-worker-as-the-igdb-metadata-backend)
for the rationale.

**Provider chain (first hit wins):**
1. `BridgeIgdbProvider` — Bridge's own Worker (`https://bridge-igdb.<account>.workers.dev/metadata`). Credentials live as Worker Secrets in Cloudflare, never in code.
2. `PlayniteIgdbProvider` — Playnite's public proxy (fallback if our Worker is down).
3. `IgdbMetadataProvider` — the user's own Twitch key, if configured.
4. Steam by name — last resort (can match a wrong game, especially for games only on Epic).

**Deploying/updating the Worker** (see `Bridge.Infra/igdb-proxy-worker/README.md`):
```bash
cd Bridge.Infra/igdb-proxy-worker
npm install
npx wrangler login
npx wrangler secret put TWITCH_CLIENT_ID
npx wrangler secret put TWITCH_CLIENT_SECRET
npx wrangler deploy
```

**Security note:** the Worker secrets are the project owner's own Twitch app
credentials. Never commit them; the `.gitignore` excludes `.dev.vars`/`.env`/`.wrangler`.

The Worker tries two IGDB queries in order (see its README): a literal
`where name ~ "..."` match first, then IGDB's fuzzy `search "..."` only if the
literal query returns nothing — so exact titles stay exact while ROM names with
accents/hyphens ("Pokemon - Emerald Version" → "Pokémon Emerald Version") still
resolve. Bridge also short-circuits all four metadata flows with a clear "No
internet connection" status message when `NetworkInterface.GetIsNetworkAvailable()`
is false, instead of reporting a misleading "No metadata found".

---

## Managed Emulation (Bridge-managed RetroArch)

Bridge installs and maintains its **own** RetroArch so ROMs launch with zero
setup — no emulator configuration required. The user-configured
`EmulatorSetupWindow` path still exists for third-party emulators; this section
is the managed RetroArch flow.

**Install pipeline (`RetroArchService`):**
1. **Resolve** — the newest stable release version is read from the GitHub
   `libretro/RetroArch` latest-release tag (stable Windows builds moved off
   GitHub to the Libretro buildbot as `.7z` archives).
2. **Download** — `stable/{version}/windows/x86_64/RetroArch.7z` from
   `buildbot.libretro.com` (~200 MB), streamed with per-chunk progress into
   `Config.EmulatorDownloadPath` under a neutral `.archive` name. The download's
   own `HttpClient` uses a 15-minute timeout (the 100s default would abort a
   slow transfer); only HTTPS + `buildbot.libretro.com`/`api.github.com` hosts
   are allowed, with a 512 MB safety ceiling.
3. **Extract** — `SharpCompress.ArchiveFactory.WriteToDirectory` streams the
   solid 7z in one forward pass (~25s for the 14.8k-file build) on a pool thread
   (`Task.Run`), so the UI never freezes. `ReaderFactory` does **not** support
   7z, and per-entry extraction of a solid archive is O(n²) — `WriteToDirectory`
   is the correct tool. SharpCompress is pinned at **0.48.0** for
   CVE-2026-44788 / GHSA-6c8g-7p36-r338 (directory-traversal via directory
   entries in `WriteToDirectory`).
4. **Swap** — the extracted `retroarch.exe` is located, the install directory is
   atomically replaced (move target → `.old`, move staging → target), and the
   resolved version is written to `Config.RetroArchVersionPath`
   (`emulators\retroarch.version`) as the change signal — the buildbot publishes
   no digest, so "same version string = current, no re-download". Orphaned
   staging/`.old`/partial `.archive` leftovers from a crash are cleaned up at
   service construction.

**Cores:** on first play of a ROM, the platform's core (e.g. `mgba_libretro.dll`)
is downloaded from the buildbot **nightly** as `{core}_libretro.dll.zip` (the
`.dll` is part of the zip filename — `CoreFileName + ".zip"`), extracted to
`emulators\retroarch\cores\`, and the managed `EmulatorProfile` is built with
`-L {CorePath} {RomPath}`. A core already on disk is left alone on Play;
"Check for updates" (`UpdateInstalledAsync`) refreshes frontend + all cores
explicitly.

**Robustness:** a `SemaphoreSlim` serializes all install work (two quick Plays
can't extract over each other; re-check under the gate prevents double installs).
If the network drops mid-download, an already-installed frontend/core is kept
and the status bar says so instead of throwing. The Play button shows
**Download** / **Downloading…** / **Play** / **Stop** based on install + run
state (`MainViewModel.PlayButtonText`/`PlayButtonSymbol`/`PlayButtonIsStop`).

**Config paths** (`Bridge/Config.cs`): `EmulatorInstallPath`
(`AppData\Bridge\emulators\retroarch`), `EmulatorDownloadPath`
(`AppData\Bridge\emulator-downloads`), `RetroArchVersionPath`
(`AppData\Bridge\emulators\retroarch.version`) — all separate from the game
database so deleting an emulator install never risks library data.

---

## Known Limitations

| Limitation | Reason | Workaround |
|------------|--------|------------|
| No plugin system | Deliberate scope decision, see [ADR-1](ARCHITECTURE.md#adr-1-no-plugin-system-in-v1) | Add a library source or metadata provider by editing `Bridge.Import`/`Bridge.Metadata` directly and releasing |
| No fullscreen/controller mode | Deliberate scope decision, see [ADR-2](ARCHITECTURE.md#adr-2-single-application-no-separate-fullscreen-frontend-in-v1) | Use the desktop app; revisit if there's real demand |
| `%LOCALAPPDATA%\Bridge\` collided with an unrelated older "Bridge" project's real app data on this machine (`bridge.db`, `settings.json`, `ImageCache/`, last modified 2026-08-04) | Both projects independently chose the app name "Bridge" | The old folder was renamed (not deleted) to `%LOCALAPPDATA%\Bridge_OLD_BACKUP_1785967008\` on 2026-08-05 before this project's `bridge.db` was first created, so no data was lost. If you're reading this on a different machine, or that backup folder is gone, this row no longer applies — safe to delete |
| SQLite lacks native ALTER for some schema changes (column drop/rename, etc.) | EF Core generates a table-rebuild migration for operations SQLite can't do in place — fine, but heavier (a large `Games` table gets copied) | Prefer additive changes (new columns/tables). Baseline assumes a pre-migration DB already matches `InitialCreate`; the `EnsureColumn` era ran at every startup since those columns were added, so every real DB has them |
| ROM matching is filename/extension-based only | No CRC/serial/DAT matching against emulation databases yet (future scope — see `PLAN.md` Backlog) | Rename ROMs to recognizable titles; the scanner still enriches them through the IGDB metadata pipeline |
| Only Bridge-managed RetroArch gets a curated catalog | `RomPlatformCatalog` covers 15 systems; third-party emulators still need manual configuration (`EmulatorSetupWindow`) — see [ADR-9](ARCHITECTURE.md#adr-9-single-emulatorprofile-shape-no-built-in-emulator-catalog-yet) | Configure the emulator manually, or add its core to the catalog |

---

## Architecture Decision Records (ADR)

For significant architectural decisions, document the context, decision, and consequences.

### Creating New ADRs

Refer to [`ARCHITECTURE.md`](ARCHITECTURE.md) for the ADR format and instructions on adding new records.

---

## Workflow Rules

**These are strict rules that must always be followed:**

1. **Never commit without explicit confirmation** — a commit should represent a coherent, verified unit of work. Run `dotnet build -c Release` and `dotnet test -c Release`, review the diff, and confirm the change is ready before staging. Whether that confirmation comes from yourself, a teammate, or whoever is directing the work, do not commit without it.
2. **Never push without explicit confirmation** — pushing to main triggers CI and creates a GitHub release automatically. Unlike a local commit, a push is visible to everyone and harder to undo cleanly once the pipeline ran. Do not push without explicit confirmation that the change is ready, whether that confirmation comes from yourself, a teammate, or whoever is directing the work.
3. **Verify build and tests locally before pushing** — run `dotnet build -c Release` and `dotnet test -c Release`.
4. **Multiple commits are fine for progress**, but group them meaningfully when pushing.
5. **Commit messages matter** — subject line ≤72 chars, body describes exactly what was done and why. For version bumps, the body becomes the release notes.
6. **Force push only for cleanup** — when squashing test commits or fixing history. Never force push over someone else's work.

### Good commit structure:

```
feat: add new feature

### Added
- Feature description

### Fixed
- Bug fix description

### Changed
- Breaking change description
```

### Git Hooks

> **Git hooks are NOT versioned automatically.** The `.git/hooks/` folder is local and not shared. If the project defines hooks for pre-commit checks, they must be installed manually:

```bash
# Option A: version hooks in hooks/ (create the folder if it doesn't exist)
git config core.hooksPath hooks/

# Option B: copy manually
cp hooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

No pre-commit validation is active by default — each developer decides whether to install it. Rule **3 (verify local build/test)** always applies, whether or not hooks are installed.

---

## New Feature Process

Before writing code for a new feature, follow this process:

1. **Inventory options and scope first** — what are the possible approaches? What is in scope vs explicitly out of scope? Do not commit to a solution before understanding the landscape.
2. **Explicit design first** — cover background states that are easy to skip:
   - UI interaction flow (happy path + edge cases)
   - Loading and error states
   - Cancellation behavior (if applicable)
   - What happens in boundary cases (empty data, network failure, concurrent access)
3. **Write the design down before writing code** — scope, approach, and edge cases should be explicit and reviewable before implementation starts.
4. **Review the actual code at each step** — read the diff, run the tests, verify edge cases were actually handled. Do not accept a summary of "it's done" as verification.
5. **Tests cover the background states from the design** — not just the happy path. Every state documented in step 2 should have a corresponding test.
6. **Cross-check**: does this change require documentation updates? Consult the [Documentation Sync Map](#documentation-sync-map) before closing.

---

## Development Environment Setup

### Requirements

| Requirement | Version | Notes |
|------------|---------|-------|
| OS | Windows 10/11 | Required |
| .NET SDK | 10 | `dotnet --version` to verify |
| IDE | VS 2022 17.10+ / VS Code / Rider | .NET 10 support |
| Git | Latest | Version control |

### First Steps

```bash
# 1. Clone the repo
git clone https://github.com/ZavalaSebas/Bridge.git
cd Bridge

# 2. Restore packages
dotnet restore

# 3. Build
dotnet build Bridge.slnx -c Release

# 4. Run tests
dotnet test Bridge.slnx -c Release

# 5. Run the app
dotnet run --project Bridge/Bridge.csproj
```

---

## Branding & Sponsorship

> **Partially implemented.** There's a **Support** submenu in the logo menu (Ko-fi + GitHub Sponsors, heart icon) that opens the links in the browser, and an **About Bridge...** item opening `Bridge/AboutWindow.xaml` (version, repo/license links, its own Support section). What's still missing: `Config.SponsorUrl` / `OpenSponsorCommand` don't exist — the URLs are hard-coded in `MainWindow.xaml`/`AboutWindow.xaml` (including the placeholder `https://ko-fi.com/YOUR_KOFI`). The status-bar heart and `CreditsWindow` snippets below remain templates for that remaining gap.

### Heart Icon in Status Bar

Add a sponsor link in the status bar with a heart icon:

```xml
<!-- MainWindow.xaml -->
<StatusBar Grid.Row="2">
    <StatusBarItem>
        <Button Command="{Binding OpenSponsorCommand}"
                Background="Transparent"
                BorderThickness="0"
                Cursor="Hand"
                ToolTip="Support the project">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="♥" Foreground="#E74C3C" FontSize="14" Margin="0,0,6,0" />
                <TextBlock Text="Sponsor on GitHub" FontSize="12" />
            </StackPanel>
        </Button>
    </StatusBarItem>
    <StatusBarItem HorizontalAlignment="Right">
        <TextBlock Text="Made with care by ZavalaSebas" FontSize="11" Foreground="Gray" />
    </StatusBarItem>
</StatusBar>
```

```csharp
// ViewModel
public ICommand OpenSponsorCommand => new RelayCommand(OpenSponsor);

private void OpenSponsor()
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Config.SponsorUrl,
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to open sponsor link");
    }
}
```

```csharp
// Config.cs
public const string SponsorUrl = "https://github.com/sponsors/ZavalaSebas";
```

---

### Credits / About Dialog

Show a credits window with the app name, version, author credit, and legal disclaimer:

```xml
<!-- Views/CreditsWindow.xaml -->
<Window x:Class="Bridge.Views.CreditsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="About Bridge"
        ResizeMode="NoResize"
        WindowStartupLocation="CenterOwner"
        Width="420" Height="320"
        ShowInTaskbar="False">

    <Grid Margin="24">
        <StackPanel VerticalAlignment="Center">
            <TextBlock Text="Bridge"
                       FontSize="22" FontWeight="SemiBold"
                       TextAlignment="Center" />
            <TextBlock Text="Version 0.1.0"
                       TextAlignment="Center"
                       Foreground="Gray"
                       Margin="0,4,0,20" />

            <Separator Margin="0,0,0,20" />

            <TextBlock Text="Made with care by ZavalaSebas"
                       TextAlignment="Center"
                       FontSize="14"
                       Margin="0,0,0,20" />

            <TextBlock TextWrapping="Wrap"
                       TextAlignment="Center"
                       FontSize="11"
                       Foreground="Gray">
This software is provided &quot;as is&quot;, without warranty of any kind, express or implied. Use at your own risk.

See the
<Hyperlink NavigateUri="https://github.com/ZavalaSebas/Bridge/blob/main/LICENSE"
          RequestNavigate="LicenseLink_Click">LICENSE</Hyperlink>
file for details.
            </TextBlock>

            <Button Content="Close"
                    Width="80"
                    Margin="0,24,0,0"
                    IsCancel="True"
                    IsDefault="True" />
        </StackPanel>
    </Grid>
</Window>
```

```csharp
// Views/CreditsWindow.xaml.cs
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

namespace Bridge.Views;

public partial class CreditsWindow : Window
{
    public CreditsWindow()
    {
        InitializeComponent();
    }

    private void LicenseLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink link)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link.NavigateUri.ToString(),
                UseShellExecute = true
            });
        }
    }
}
```

```csharp
// Trigger from MainWindow
private void ShowCredits_Click(object sender, RoutedEventArgs e)
{
    var credits = new CreditsWindow { Owner = this };
    credits.ShowDialog();
}
```

---

## Keyboard Navigation

WPF supports keyboard navigation out of the box, but must be designed intentionally.

### Tab Order

WPF follows the XAML declaration order by default — if your layout matches the visual/logical order, no explicit `TabIndex` is needed. Only add explicit `TabIndex` when the logical order differs from the visual order:

```xml
<!-- Natural order — no TabIndex needed -->
<StackPanel>
    <TextBox />
    <Button Content="Next" />
    <ComboBox />
</StackPanel>

<!-- Explicit order needed when visual layout doesn't match logical -->
<Grid>
    <TextBox TabIndex="2" />
    <Button TabIndex="0" Content="First" />
    <ComboBox TabIndex="1" />
</Grid>
```

### Focus Indicators

WPFUI (the project's UI framework) provides visible focus indicators by default on all controls. **Do not disable or remove them** without providing an accessible replacement. The default focus rectangle is sufficient for keyboard users to see where they are.

### Keyboard Shortcuts

Define application-level shortcuts in `Window.InputBindings`:

```xml
<Window.InputBindings>
    <KeyBinding Key="S" Modifiers="Ctrl" Command="{Binding SaveCommand}" />
    <KeyBinding Key="F5" Command="{Binding RefreshCommand}" />
</Window.InputBindings>
```

For menu items, use `InputGestureText`:

```xml
<MenuItem Header="_Save" Command="{Binding SaveCommand}" InputGestureText="Ctrl+S" />
```

### Dialogs

Modal dialogs should return focus to the parent window when closed. Set `Owner` before showing:

```csharp
var dialog = new SettingsWindow { Owner = this };
dialog.ShowDialog();
```

### Alt+Key Navigation

For menu accesskeys (underlined letters), prefix the letter with underscore in the `Header`:

```xml
<MenuItem Header="_File">
<MenuItem Header="_Edit">
```

Press `Alt` to show accesskeys, then press the letter to activate.

---

Built with care by ZavalaSebas
