# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Cinematic screenshot gallery in the Details view** — `Bridge/Controls/ScreenshotGallery.cs` (+ `.xaml`) shows the game's full-resolution Steam screenshots (`Game.Screenshots`, populated by `SteamMetadataProvider` from `data.screenshots[].path_full`): a large main image floating over a frosted backdrop of the same screenshot, a drag-to-scroll thumbnail strip, counter, arrow buttons, keyboard navigation (←/→/Esc), click-to-expand into a full-window dark overlay with its own thumbnail strip, and auto-advance every 4s (paused on hover/fullscreen, only with 2+ screenshots). The main image scales proportionally to its container via the new `FractionConverter`. Only shown in the Details view; the column is a JSON list (mini-migration `EnsureColumn("Screenshots")`).
- **Hero background "cover-by-width" rendering** — `FadeImage.CoverByWidth` sizes the hero art to always fill the window's full width (height = width/aspect, vertical excess clipped by the parent's `ClipToBounds`), so Steam's `library_hero` and any other ratio show edge-to-edge with no side letterbox bars, whatever the window size.
- **Smooth hero cross-fade** — the `FadeImage` transition between games now keeps each frame at its own aspect-based size and fades the outgoing frame to 0 before removing it (no hard edge when switching between different-ratio artworks), with per-frame bottom fades only for frames shorter than the hero so the frosted blur edge stays consistent.
- **Epic Games support** — `Bridge.Import/Epic/` detects installed Epic games from the launcher's local files (`LauncherInstalled.dat` + `.item` manifests), filters out Unreal Engine/DLC/plugins, and launches via `com.epicgames.launcher://` (tracked by directory, like Steam). The icon is extracted from the installed game's `.exe` (Playnite's approach — Epic has no icon service). Epic games also get **automatic metadata by name search** (`DownloadMissingMetadataByNameAsync`, via the IGDB Worker chain) instead of the appid-based lookup Steam uses — matching Playnite's decision to avoid Steam-by-name for Epic-only titles.
- **Own Cloudflare Worker as the IGDB backend** — `Bridge.Infra/igdb-proxy-worker/` + `BridgeIgdbProvider` give Bridge IGDB metadata with zero user configuration, using the same architecture Playnite uses (credentials on a server, never in the app). The Worker is the first metadata provider; Playnite's public proxy, a user-configured IGDB key and Steam-by-name are fallbacks in that order. See ADR-13.
- **Playnite's public IGDB proxy as a fallback** (`PlayniteIgdbProvider`) — used only if our own Worker is unreachable.
- **Fixed context-menu commands in the More menu** — the menu lives in a Popup outside the visual tree, so `ElementName` bindings never resolved; commands now bind through the window's DataContext set in `MenuButton_Click`.

### Known issues
- **Hero cross-fade blur edge pops when switching tall→short backgrounds** — in the Details view, switching from a tall game background (height ≥ the 510px hero) to a short one shows the short frame's bottom fade snapping in once the cross-fade completes instead of revealing smoothly. Left as-is for now (pre-fading both frames up front + not clearing the hero's outgoing Source already fixes the worst of it); full detail and candidate fixes in `DEVELOPMENT.md` under the screenshot gallery section.

## [0.2.0] - 2026-08-12

### Added
- **Three view modes** — the main content area switches between **List** (list + collapsible detail panel), **Covers** (a cover wall where hovering a cover reveals **Play** and **Info** buttons over the artwork) and **Details** (a flat `ListView`/`GridView` with columns: Name + icon, Release Date, Genre, Last Played, Time Played, Library, and Play/Info actions). Switched with the title bar icon toggles (`ViewMode` enum; a ComboBox before the Fase 7 overhaul).
- **Compact info panel** (`CompactInfoPanel`, inline in the main window) — opened by the hover **Info** button (in Covers view) using the hovered game, not the list selection; shows the hero, icon, name, buttons, all details and description. `PlayGameCommand` now takes the game as an optional parameter so covers/rows can launch their own game.
- **Grouping in the library list** — group by 21 fields ("Don't group" + Name, Library, Developer, Publisher, Platform, Genre, Installation Status, Completion Status, Time Played, Play Count, Install Size, Install Drive, Last Played, Recent Activity, Release Year, Date Added, Date Modified, Community/Critic/User Score). Uses `ListCollectionView.GroupDescriptions` fed by a pure, unit-tested `GameGroupResolver` (buckets for playtime/install size/scores, reference names via lookups); the list shows group headers.
- **Search, filter presets and sorting in the library list** — a name search box (case-insensitive substring), filter presets (`All` / `Favorite` / `Most Played` / `Recently Played`, combinable with the search), and sort by field + direction (22 fields: Name, Time Played, Play Count, Last Played, Recent Activity, Favorite, Hidden, Install Size, Installation Folder, Installation Status, Release Date, Date Added, Date Modified, Version, Community/Critic/User Score, Developer, Publisher, Platform, Genre, Library). Sorting uses `ListCollectionView.CustomSort` with a pure, unit-tested `GameSortComparer`; reference entities sort by resolved display name. Empty/unset values always sort last, regardless of direction.
- **Statistics dashboard** — launched from the sidebar button as a full-width overlay that hides the detail panel while open (before the Fase 7 overhaul it was a tab in a right-panel `TabControl`). Replicates Playnite's Overview: library counts with percentages (All/Installed/Not installed/Hidden/Favorite), total/average play time, completion status (Not played/Played), and a Top Play Time list.
- **Automatic Steam play action** — a Steam-imported game with no configured `GameAction` launches via `steam://rungameid/{appid}` passed to `steam.exe -silent` (the same approach Playnite's Steam addon takes for Steamworks-DRM games, never the local `.exe`), tracked by directory (watch processes running from the game's `InstallDirectory`). Resolution logic is pure and unit-tested in `Bridge.Import/Steam/SteamPlayActions.cs`.
- **Developers/Publishers/Platforms shown in the detail panel** — metadata is now resolved to real `Company`/`Platform` entities and displayed under the install directory (labels collapse when empty). Steam platform slugs (`pc_windows`/`macintosh`/`pc_linux`) now map to readable names (`Windows`/`macOS`/`Linux`).
- **Local Steam artwork in the library** — icons, covers and hero backgrounds are read from Steam's local `appcache\librarycache\{appid}\` cache (32x32 clienticon, `library_600x900.jpg`, `library_hero.jpg`) the moment a game is imported/loaded, so a fresh install shows complete art for every Steam game without waiting on the web metadata — resolved by `Bridge.Import/Steam/SteamLocalIconResolver.cs` and applied by `MainViewModel.ApplySteamLocalArtwork` (local art wins over web URLs). Games without a cached file fall back to the `header.jpg`/cover URLs from metadata.
- **Scan Automatically (Add Game Installed)** — `ScanInstalledWindow` + `InstalledGameDetector` detect games installed on the PC that aren't in a library: start-menu shortcuts (`.lnk`), a recursive folder scan (`.exe`/`.bat`/`.lnk`), or a single browsed executable — with installers/redists/engine helpers filtered out and the exe icon shown in the picker.
- **Web image search in the game editor** — `ImageSearchWindow` + `WebImageSearchService` (DuckDuckGo image endpoint, no API key) let you search for and pick Icon/Cover/Background art directly in the editor's Media tab.
- **Formatted descriptions** — Steam descriptions are parsed into typed blocks (headings, subheadings, bullet lists, interleaved images) and rendered with their HTML structure instead of a flat text dump.
- **Runtime theme switching** — `ThemeManager` + `ThemeColorWindow`: 9 accent presets plus a custom picker; the whole palette is recalculated from the accent and persisted to `theme.json`.
- **Playnite-style manual game editor** — "Add Manually" opens the shared `GameEditWindow` in New-Game mode; it now has Sorting Name, per-field create-on-the-fly reference buttons (Genre/Developer/Publisher/Platform) and Browse/Search-web art pickers.

### Changed
- **Fase 7 — WPF-UI 4.3.0 visual overhaul.** The shell was rebuilt on WPF-UI: `FluentWindow` with Mica backdrop, a 3-zone `TitleBar` (search, view-mode toggles, overflow menu with the global actions), and a 52px icon **sidebar** (Library / Statistics, collapsible and re-positionable). All views share one dark token palette (`Bridge/Styles/Theme.xaml` — indigo family, Inter Variable font, #007ACC UI accent) with **runtime theme switching** (9 color presets + custom picker, persisted to `theme.json`). Post-overhaul baseline: ~2.6s cold start / ~180MB RAM (see DEVELOPMENT.md). Details in [ADR-3](ARCHITECTURE.md#adr-3-wpf-ui-430-for-the-visual-overhaul-supersedes-the-original-plain-wpf-first-adr).
- `LibraryStatistics` extended with `PlayedCount`/`NotPlayedCount`, `TotalInstallSizeBytes` and human-readable display strings; the ViewModel now exposes the whole object as `Statistics` instead of only a status-bar line.
- Global actions moved out of the main page into the title bar overflow menu (Add Game with Import from Steam / Add Manually / Scan ROMs / Scan Automatically, Support, Sidebar, Theme, Settings, About, Exit; Configure Emulator and IGDB Settings under Settings) — a `File`/`Tools` menu bar before the Fase 7 overhaul. Add Manually opens the `GameEditWindow` in "New Game" mode (no separate `AddGameWindow`); Scan ROMs uses a dedicated prompt dialog (`ScanRomWindow`).
- Game detail panel reorganized: playtime, install directory, save/delete/download buttons, and the Play button now sit in the right column beside the cover; only Description and Background remain below.
- "Set Play Action" field removed from the UI - the Steam play action is resolved automatically; the leftover `SetPlayActionCommand`/`ExecutablePathInput` ViewModel code was removed entirely.

### Fixed
- Transient runtime flags (`IsInstalling`/`IsUninstalling`/`IsLaunching`/`IsRunning`) are reset to `false` on every load from the database — a crash or forced close mid-game no longer leaves a game stuck shown as "running" on the next launch (previously the reset was documented as `Bridge.Storage`'s responsibility but never implemented).
- Steam directory-based playtime tracking now gives up after 5 minutes if the game never spawns a process in its `InstallDirectory` (instead of tracking forever with `IsRunning` stuck true), and only counts the session from the moment the game's process actually appears.
- Release date display no longer renders a dangling hyphen (`2026-08-`) when only the year and month are known.
- Removed dead code: unused `FormatField`, unused `HttpClient` field in `ImageUrlConverter`, the ComboBox-era `EnumValues`/`EnumDescriptionConverter`/`BoolToIndexConverter` (registered in XAML but bound by nothing), and unused `SteamSearchEntry`. Empty JSON list columns now round-trip as empty lists instead of `null`.
- Security: pinned `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 in `Bridge.Storage` to fix CVE-2025-6965 (GHSA-2m69-gcr7-jv3q, high) — the transitive `lib.e_sqlite3` 2.1.11 bundled vulnerable SQLite 3.49.1, now replaced by SourceGear.sqlite3 3.50.4; verified by the EF Core round-trip tests.

## [0.1.0] — 2026-08-06

### Added
- **Core (Fase 1-2):** .NET 10 / WPF shell with list + detail views; manual add/edit of games; entities, contracts and `BridgeDbContext` (EF Core + SQLite); repository layer (`IRepository<T>`, `GameRepository`).
- **Manual edit & import (Fase 3):** create/edit/delete, favorite/hidden flags; launch a game via its `GameAction` and track playtime with a poll-based monitor (`Bridge/Services/GameLauncher.cs` — only `GameActionType.File` and `GameActionType.Emulator`, exact launched PID tracked).
- **Statistics (Fase 4):** playtime/playcount/last-activity tracking and basic library statistics.
- **Steam import:** `Bridge.Import` with `SteamLibraryImporter`, `SteamPaths`, hand-rolled `VdfParser` (ADR-11); registry → `libraryfolders.vdf` → `appmanifest*.acf`, `StateFlags=4` filter. Auto-import on startup — Steam games are detected automatically without clicking a button.
- **Emulation (Fase 6):** `Bridge/Services/RomScanner.cs` (folder scan by extension, dedup by exact path — no CRC/DAT yet); emulator setup UI; `GameAction(Emulator)` launch with `{RomPath}` substitution.
- **Metadata (Fase 5):** `Bridge.Metadata` with `IgdbMetadataProvider` (ADR-10). `IGameMetadataProvider` interface for multi-provider support.
- **Steam Store metadata:** `SteamMetadataProvider` — HTTP-anonymous metadata from the official Steam store (no login, no API key); maps Name, Description, ReleaseDate, CoverImage, BackgroundImage, CriticScore (Metacritic), CommunityScore (SteamDB formula), Developers, Publishers, Genres, Platforms, Features and Links.
- **Auto-metadata on import:** newly imported Steam games automatically receive metadata from the Steam Store on startup — no manual action needed.
- **Multi-provider fallback:** "Download Metadata" tries IGDB first, then falls back to Steam Store. Steam-imported games use their AppID for a guaranteed direct lookup.
- **Revamped detail panel:** cover image, release date, critic/community scores, background image, and install directory now visible in the UI.

### Changed
- Dedup key is `(ExternalId, SourceId)` instead of Playnite's `(GameId, PluginId)` (ADR-6).
- Single `Company` entity covers developer/publisher roles (ADR-7); metadata uses plain IDs instead of `MetadataProperty` (ADR-8).

### Not implemented yet (see PLAN.md)
- Fase 7 (polish) and Fase 9 (consolidation).
- Full scanner pipeline: CRC/serial matching, playlists (.cue/.m3u/.gdi), exclusions, subfolder/archive scanning.
- Multi-mode process tracking (ProcessName, process tree) and Script game action types — only File/URL/Emulator are implemented, with Directory tracking reserved for the auto-resolved Steam play action (`SteamPlayActions`).
- `SoftwareApps`, `ImportExclusions` collections (basic `FilterPresets` now exist — All/Favorite/Most Played/Recently Played — as list presets, not yet as a persisted collection).
- `SkipExistingValues` semantics (metadata downloads unconditionally overwrite).
- Bundled "known emulator" catalog (profiles are user-configured only; see `EmulatorProfile`).
- Full emulation DAT/CRC matching (ROM scanning is filename + extension based).

---

*This changelog follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)*
