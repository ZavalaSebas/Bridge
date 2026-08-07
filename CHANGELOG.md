# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] — 2026-08-06

### Added
- **Core (Fase 1-2):** .NET 10 / WPF shell with list + detail views; manual add/edit of games; entities, contracts and `BridgeDbContext` (EF Core + SQLite); repository layer (`IRepository<T>`, `GameRepository`).
- **Manual edit & import (Fase 3):** create/edit/delete, favorite/hidden flags; launch a game via its `GameAction` and track playtime with a poll-based monitor (`Bridge/Services/GameLauncher.cs` — only `GameActionType.File` and `GameActionType.Emulator`, exact launched PID tracked).
- **Statistics (Fase 4):** playtime/playcount/last-activity tracking and basic library statistics.
- **Steam import (Fase 5):** `Bridge.Import` with `SteamLibraryImporter`, `SteamPaths`, hand-rolled `VdfParser` (ADR-11); registry → `libraryfolders.vdf` → `appmanifest*.acf`, `StateFlags=4` filter. Auto-import on startup — Steam games are detected automatically without clicking a button.
- **Emulation (Fase 6):** `Bridge/Services/RomScanner.cs` (folder scan by extension, dedup by exact path — no CRC/DAT yet); emulator setup UI; `GameAction(Emulator)` launch with `{RomPath}` substitution.
- **Metadata (Fase 8):** `Bridge.Metadata` with `IgdbMetadataProvider` (ADR-10). `IGameMetadataProvider` interface for multi-provider support.
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
- Multi-mode process tracking (Directory, ProcessName, process tree) and URL/Script game action types.
- `SoftwareApps`, `FilterPresets`, `ImportExclusions` collections.
- Local image caching (covers/backgrounds stored as raw URLs for now).
- `SkipExistingValues` semantics (metadata downloads unconditionally overwrite).
- `Developers`/`Publishers`/`Platforms`/`Features` are downloaded but not yet displayed in the UI (stored as entity IDs).

---

*This changelog follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)*
