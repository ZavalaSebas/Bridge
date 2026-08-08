<div align="center">

# Bridge

### A local game library, unified — no plugins, no bloat, just your games.

[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512bd4?style=flat-square&logo=dotnet&logoColor=white&labelColor=1a1a2e)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-Windows-00a4ef?style=flat-square&logo=windows&logoColor=white&labelColor=1a1a2e)](https://github.com/ZavalaSebas/Bridge)
[![Version](https://img.shields.io/badge/Version-0.1.0-57F287?style=flat-square&labelColor=1a1a2e)](https://github.com/ZavalaSebas/Bridge/releases)

Bridge brings your games — from external libraries, manual entries, and emulated ROMs — into one fast, local catalog. No plugin runtime, no bloat.

[Get Started](#get-started) · [Features](#features) · [How It Works](#how-it-works) · [Build from Source](#build-from-source)

</div>

---

## What is Bridge?

Bridge is an original game library manager: it unifies games from external libraries, manually added entries, and emulated ROMs into one local, self-contained catalog. It keeps what matters day to day — incremental import, local metadata, fast virtualized views, and emulation support — without a plugin runtime or a separate fullscreen frontend.

[Playnite](https://playnite.link/) is an inspiration, not a specification. During development Bridge uses Playnite's *observed behavior* as a reference to understand how features should feel (import semantics, playtime tracking, metadata resolution), but its structure, architecture, and implementation are its own — no shared code, no ported internals, different module layout, different persistence, different UI. See [`PROJECT_FOUNDATION.md`](PROJECT_FOUNDATION.md) for the behavioral notes that inform development.

> **Disclaimer:** Bridge is not affiliated with, endorsed by, or connected to Playnite, Valve/Steam, GOG, or any platform or emulator project referenced in this document. Bridge does not crack, bypass, or circumvent DRM. It organizes games you already own and emulators/ROMs you already have; it does not provide or distribute copyrighted game files.

---

## Screenshot

<div align="center">

> Screenshot coming soon — v0.1.0

</div>

---

## How It Works

Bridge is a modular-monolith WPF application: internal modules (`Core`, `Storage`, `Import`, `Metadata`, `Emulation`, `App`) each own one responsibility, with no UI/domain mixing and no runtime plugin boundary. Games and their metadata are stored locally — images and metadata are cached on first fetch so nothing gets re-downloaded unnecessarily, and library imports run incrementally so re-scanning a source only updates what changed.

The app starts as plain WPF and evolves visually toward WPF UI once the functional core is stable. It ships as a single self-contained `.exe` (~148 MB, verified — one file, no sidecar DLLs) — no .NET runtime install required, no external services beyond what's needed to fetch metadata. Measured cold-start time is ~2 seconds and ~140 MB RAM at rest (see `DEVELOPMENT.md` for the full measurement notes and what was tried).

---

## Get Started

**Download a Release**

Grab the latest `Bridge.exe` from [Releases](https://github.com/ZavalaSebas/Bridge/releases). Self-contained — no .NET required. Just run it.

*(No GitHub release cut yet — the project is in active development. `dotnet publish` has been verified locally to produce a genuine single-file `Bridge.exe`, though.)*

**Build from Source**

```bash
git clone https://github.com/ZavalaSebas/Bridge.git
cd Bridge
dotnet publish Bridge -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Requirements

- Windows 10/11
- No .NET runtime install needed for the packaged `.exe` (self-contained)
- .NET 10 SDK if building from source

---

## Features

- **Steam library auto-import** — detects installed Steam games automatically on startup (registry + `libraryfolders.vdf` + `appmanifest*.acf`)
- **Steam Store metadata** — downloads name, description, release date, cover/background art, critic/community scores, genres, and more from the official Steam store (no login, no API key)
- **IGDB metadata** — text and image metadata from IGDB (requires a free Twitch Developer account)
- **Multi-provider fallback** — metadata search tries IGDB first, then falls back to Steam Store automatically
- **Auto-metadata on import** — Steam games get metadata fetched from the store automatically when first imported
- **Steam icons in the library list** — Steam games show the square 32x32 icon Steam caches locally (`appcache\librarycache\{appid}`), falling back to the `header.jpg` URL when none is cached
- **Search, filter presets and sorting** — filter the list by name, switch between All / Favorites / Most Played / Recently Played, and sort by 22 fields (name, playtime, play count, last played, scores, developer, platform, library, etc.) ascending or descending
- **Statistics tab** — library overview with counts and percentages, total/average play time, total install size, completion status, and a Top Play Time list (like Playnite's Overview, in a tab in front of the game detail)
- Manual game entries with add, edit, and delete
- Launch a game via its `GameAction` and track playtime automatically with poll-based monitoring — Steam games launch with an auto-resolved play action (`steam://rungameid/{appid}` via `steam.exe`, no per-game setup needed)
- Basic statistics (totals, installed/not installed, favorites, total playtime)
- Simple ROM folder scan with emulator matching and `{RomPath}` launching
- Self-contained single-file `.exe` (~148 MB) — no .NET runtime install required

---

## Architecture

Modular monolith, no runtime plugins: `Core` (domain) → `Storage` (persistence) → `Import`/`Metadata`/`Emulation` (use cases) → `App` (WPF UI). See [ARCHITECTURE.md](ARCHITECTURE.md) for the ADRs behind these decisions and [DEVELOPMENT.md](DEVELOPMENT.md#architecture-overview) for the full layer breakdown.

---

## Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for the full project guide, architecture, and workflow rules. See [PROJECT_FOUNDATION.md](PROJECT_FOUNDATION.md) for the behavioral notes on Playnite that inform Bridge's development decisions.

---

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).

---

## Acknowledgments

Bridge's design is informed by studying [Playnite](https://playnite.link/)'s behavior (see `PROJECT_FOUNDATION.md` §27 for the notes). Playnite is an excellent, mature project — Bridge is a separate, smaller, plugin-free take on the same *problem space*, built independently.

---

## Sponsor

If you find Bridge useful, consider supporting the project:

[![GitHub Sponsors](https://img.shields.io/badge/Sponsor-GitHub-EA4AAA?style=flat-square&logo=githubsponsors&logoColor=white&labelColor=1a1a2e)](https://github.com/sponsors/ZavalaSebas)

---

<div align="center">

Made with care by [ZavalaSebas](https://github.com/ZavalaSebas)

</div>
