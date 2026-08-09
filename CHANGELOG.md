# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Three view modes** — the main content area switches between **List** (list + collapsible detail panel), **Covers** (a cover wall where hovering a cover reveals **Play** and **Info** buttons over the artwork) and **Details** (a flat `ListView`/`GridView` with columns: Name + icon, Release Date, Genre, Last Played, Time Played, Library, and Play/Info actions). Switched with the title bar icon toggles (`ViewMode` enum; a ComboBox before the Fase 7 overhaul).
- **Compact info window** (`GameInfoWindow`) — opened by the hover **Info** button (in Covers and Details views) using the hovered game, not the list selection; shows all details and description without images. `PlayGameCommand` now takes the game as an optional parameter so covers/rows can launch their own game.
- **Grouping in the library list** — group by 21 fields ("Don't group" + Name, Library, Developer, Publisher, Platform, Genre, Installation Status, Completion Status, Time Played, Play Count, Install Size, Install Drive, Last Played, Recent Activity, Release Year, Date Added, Date Modified, Community/Critic/User Score). Uses `ListCollectionView.GroupDescriptions` fed by a pure, unit-tested `GameGroupResolver` (buckets for playtime/install size/scores, reference names via lookups); the list shows group headers.
- **Search, filter presets and sorting in the library list** — a name search box (case-insensitive substring), filter presets (`All` / `Favorite` / `Most Played` / `Recently Played`, combinable with the search), and sort by field + direction (22 fields: Name, Time Played, Play Count, Last Played, Recent Activity, Favorite, Hidden, Install Size, Installation Folder, Installation Status, Release Date, Date Added, Date Modified, Version, Community/Critic/User Score, Developer, Publisher, Platform, Genre, Library). Sorting uses `ListCollectionView.CustomSort` with a pure, unit-tested `GameSortComparer`; reference entities sort by resolved display name. Empty/unset values always sort last, regardless of direction.
- **Statistics dashboard** — launched from the sidebar button as a full-width overlay that hides the detail panel while open (before the Fase 7 overhaul it was a tab in a right-panel `TabControl`). Replicates Playnite's Overview: library counts with percentages (All/Installed/Not installed/Hidden/Favorite), total/average play time, total install size (sum of `InstallSizeBytes`), completion status (Not played/Played), and a Top Play Time list.
- **Automatic Steam play action** — a Steam-imported game with no configured `GameAction` launches via `steam://rungameid/{appid}` passed to `steam.exe -silent` (the same approach Playnite's Steam addon takes for Steamworks-DRM games, never the local `.exe`), tracked by directory (watch processes running from the game's `InstallDirectory`). Resolution logic is pure and unit-tested in `Bridge.Import/Steam/SteamPlayActions.cs`.
- **Developers/Publishers/Platforms shown in the detail panel** — metadata is now resolved to real `Company`/`Platform` entities and displayed under the install directory (labels collapse when empty). Steam platform slugs (`pc_windows`/`macintosh`/`pc_linux`) now map to readable names (`Windows`/`macOS`/`Linux`).
- **Local Steam artwork in the library** — icons, covers and hero backgrounds are read from Steam's local `appcache\librarycache\{appid}\` cache (32x32 clienticon, `library_600x900.jpg`, `library_hero.jpg`) the moment a game is imported/loaded, so a fresh install shows complete art for every Steam game without waiting on the web metadata — resolved by `Bridge.Import/Steam/SteamLocalIconResolver.cs` and applied by `MainViewModel.ApplySteamLocalArtwork` (local art wins over web URLs). Games without a cached file fall back to the `header.jpg`/cover URLs from metadata.

### Changed
- **Fase 7 — WPF-UI 4.3.0 visual overhaul.** The shell was rebuilt on WPF-UI: `FluentWindow` with Mica backdrop, a 3-zone `TitleBar` (search, view-mode toggles, overflow menu with the global actions), and a 44px icon **sidebar** (Library / Statistics). All views now share one dark token palette (`Bridge/Styles/Theme.xaml` — #1E1E1E family, Inter Variable font, #007ACC UI accent / #10B981 Play accent). Post-overhaul baseline: ~2.6s cold start / ~180MB RAM (see DEVELOPMENT.md). Details in [ADR-3](ARCHITECTURE.md#adr-3-wpf-ui-430-for-the-visual-overhaul-supersedes-the-original-plain-wpf-first-adr).
- `LibraryStatistics` extended with `PlayedCount`/`NotPlayedCount`, `TotalInstallSizeBytes` and human-readable display strings; the ViewModel now exposes the whole object as `Statistics` instead of only a status-bar line.
- Global actions moved out of the main page into the title bar overflow menu (Add Game, Import Steam Library, Exit; Scan ROMs, Configure Emulator, IGDB Settings) — a `File`/`Tools` menu bar before the Fase 7 overhaul. Add Game and Scan ROMs now use dedicated prompt dialogs (`AddGameWindow`, `ScanRomWindow`).
- Game detail panel reorganized: playtime, install directory, save/delete/download buttons, and the Play button now sit in the right column beside the cover; only Description and Background remain below.
- "Set Play Action" field removed from the UI for now — the Steam play action is resolved automatically, and the field only made sense for manual non-Steam games (still in the ViewModel as `SetPlayActionCommand`/`ExecutablePathInput`, unused).

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
- Multi-mode process tracking (Directory, ProcessName, process tree) and URL/Script game action types **for user-configured actions** — the URL action + Directory tracking now exist only for the auto-resolved Steam play action (`SteamPlayActions`), not as a general feature.
- `SoftwareApps`, `ImportExclusions` collections (basic `FilterPresets` now exist — All/Favorite/Most Played/Recently Played — as list presets, not yet as a persisted collection).
- Local image caching (covers/backgrounds stored as raw URLs for now).
- `SkipExistingValues` semantics (metadata downloads unconditionally overwrite).
- `Features` are downloaded but not yet displayed in the UI (stored as entity IDs).
- Sort/group fields that need repositories Bridge doesn't have yet (Age Rating, Category, Feature, Series, Region, Tag) — listed in the `GameSortField`/`GameGroupField` enum doc comments as future work.

---

*This changelog follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)*
