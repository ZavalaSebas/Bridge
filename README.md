<div align="center">

# Bridge

### A local game library, unified — no plugins, no bloat, just your games.

[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512bd4?style=flat-square&logo=dotnet&logoColor=white&labelColor=1a1a2e)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-Windows-00a4ef?style=flat-square&logo=windows&logoColor=white&labelColor=1a1a2e)](https://github.com/ZavalaSebas/Bridge)
[![Version](https://img.shields.io/badge/Version-0.6.0-57F287?style=flat-square&labelColor=1a1a2e)](https://github.com/ZavalaSebas/Bridge/releases)

Bridge brings your games — from external libraries, manual entries, and emulated ROMs — into one fast, local catalog. No plugin runtime, no bloat.

[Get Started](#get-started) · [Features](#features) · [How It Works](#how-it-works) · [Build from Source](#build-from-source)

</div>

---

## What is Bridge?

Bridge is an original game library manager: it unifies games from external libraries, manually added entries, and emulated ROMs into one local, self-contained catalog. It keeps what matters day to day — incremental import, local metadata, fast virtualized views, and emulation support — without a plugin runtime or a separate fullscreen frontend.

[Playnite](https://playnite.link/) was the **original inspiration** when Bridge was first conceived — the idea of one local catalog for every game. Bridge is an independent project: its own code, architecture, UI, and scope (no plugin runtime, no shared internals).

> **Disclaimer:** Bridge is not affiliated with, endorsed by, or connected to Playnite, Valve/Steam, GOG, or any platform or emulator project referenced in this document. Bridge does not crack, bypass, or circumvent DRM. It organizes games you already own and emulators/ROMs you already have; it does not provide or distribute copyrighted game files.

---

## Screenshot

<div align="center">

> Screenshot coming soon — v0.2.0

</div>

---

## How It Works

Bridge is a modular-monolith WPF application: internal modules (`Core`, `Storage`, `Import`, `Metadata`, `Emulation`, `App`) each own one responsibility, with no UI/domain mixing and no runtime plugin boundary. Games and their metadata are stored locally — images and metadata are cached on first fetch so nothing gets re-downloaded unnecessarily, and library imports run incrementally so re-scanning a source only updates what changed.

The app uses [WPF-UI](https://github.com/lepoco/wpfui) 4.3.0 with a custom dark theme, Mica backdrop, Inter variable font, and sidebar-based navigation. It ships as a single self-contained `.exe` (~155 MB, verified — one file, no sidecar DLLs) — no .NET runtime install required, no external services beyond what's needed to fetch metadata. Measured cold-start time is ~2.6 seconds and ~180 MB RAM at rest (see `DEVELOPMENT.md` for the full measurement notes).

---

## Get Started

**Download a Release**

Grab the latest `Bridge.exe` from [Releases](https://github.com/ZavalaSebas/Bridge/releases). Self-contained — no .NET required. Just run it.

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
- **IGDB metadata** — text and image metadata from IGDB with **zero configuration** (via Bridge's own Cloudflare Worker; a user-supplied Twitch key is optional). The Worker matches an exact name first (`where name ~`), falling back to IGDB's fuzzy `search` for titles that need it (ROM names with accents/hyphens), and Bridge shows a clear "no internet connection" message instead of "no metadata" when offline
- **Multi-provider fallback** — metadata search tries Bridge's IGDB Worker first, then a legacy public IGDB proxy (`PlayniteIgdbProvider`), then the user's IGDB key, then Steam Store automatically
- **Auto-metadata on import** — Steam games get metadata fetched from the store automatically when first imported
- **Epic Games support** — detects installed Epic games from the launcher's local files, launches via the Epic client, and shows each game's exe icon
- **Steam icons in the library list** — Steam games show the square 32x32 icon Steam caches locally (`appcache\librarycache\{appid}`), falling back to the `header.jpg` URL when none is cached
- **Search, filter presets, sorting and grouping** — filter the list by name, switch between All / Favorites / Installed / Not Played / Recently Played, sort by 22 fields (name, playtime, play count, last played, scores, developer, platform, library, etc.) ascending or descending, and group by 21 fields (library, developer, platform, genre, playtime buckets, install size buckets, release year, etc.)
- **Three view modes** — **List** (list + collapsible detail panel with cover, metadata, Play), **Covers** (cover wall with hover animations — scale + shadow + icon-only Play/Info overlay; compact side panel with screenshot strip, Details, and Overview), **Table** (themed grid with dynamic-width Name column) — plus a full-width **Statistics** dashboard overlay (library overview, playtime, completion, Top Played). Search/filter/sort/group apply across all game views
- **How Long to Beat** — completion-time estimates (main / extras / completionist) from howlongtobeat.com, fetched with metadata sync and shown in the Details hero as a segmented progress bar filled by your real playtime
- **Cinematic screenshot gallery** — in the Table view and as a compact strip in Covers, games with screenshots show them as a carousel: a large image floating over a frosted backdrop, a drag-to-scroll thumbnail strip, counter, arrow buttons, keyboard navigation, click-to-expand into a full-window dark overlay, and auto-advance
- **Sidebar navigation** — Icon rail (52px) with Library, ROMs, Favorites, Sources, Show hidden, Statistics, and Settings shortcuts, collapsible and re-positionable
- **Dark theme + Mica + runtime theming** — custom indigo dark palette with Inter variable font, Mica backdrop on Windows 11, and a runtime accent switcher (9 color presets + custom picker) that recolors the whole UI
- Manual game entries with a dedicated editor (Sorting Name, create-on-the-fly genres/devs/publishers/platforms, and web image search), edit, and delete
- **Scan Automatically** — detect games installed on the PC from start-menu shortcuts, a folder, or a single executable (with installers/helpers filtered out), then import them with one click
- Launch a game via its `GameAction` and track playtime automatically with poll-based monitoring — Steam games launch with an auto-resolved play action (`steam://rungameid/{appid}` via `steam.exe`, no per-game setup needed)
- Basic statistics (totals, installed/not installed, favorites, total playtime)
- **Zero-setup ROM support** — recursively scans folders (including `.zip`/`.7z` archives), detects supported systems from ROM extensions (`RomPlatformCatalog`: NES, SNES, N64, GB/GBC/GBA, NDS, Genesis, Master System, Game Gear, Atari, PC Engine, Lynx, WonderSwan), enriches games through the normal IGDB metadata pipeline, and installs/updates Bridge-managed RetroArch + the required core on first play. The Play button reads **Download** (then **Downloading…**) until the frontend/core is installed, then Play/Stop as usual
- **RetroArch cheats** — context menu **Cheats** for managed ROMs: fetch from libretro-database, toggle per game, optional auto-apply on launch via emulator settings
- **Refresh Library** — logo menu command re-imports Steam/Epic, rescans configured ROM and installed-game folders, and downloads missing metadata on demand (same core sync as startup, without checking for Bridge app updates)
- **Self-updating** — Bridge checks GitHub Releases at startup (and on demand via **Check for updates…** in the app menu), downloads the new `Bridge.exe`, and restarts into it with a safe swap (running exe kept as `.old` until the new one proves it starts; the DB is backed up first). **Schema changes apply automatically too** — EF Core migrations update your existing `bridge.db` in place on the next launch, so a release can change the DB without you re-downloading or losing your library. Skip an update and a download button appears in the title bar until you apply it. See [DEVELOPMENT.md](DEVELOPMENT.md#version-management)
- **Settings hub** — sidebar gear opens a unified preferences screen: **Profile** (name + avatar), emulator/IGDB shortcuts, appearance (theme, language, tray, detail panel position, detail section layout, keep selection across views), library backup & restore, update check, **beta channel**, **start with Windows**, and About
- **English / Spanish UI** — switch language in Settings; Bridge restarts to apply
- **Library backup & restore** — zip your database, preferences, and artwork cache; restore on next launch
- **System tray** — close the window to keep Bridge running in the notification area; double-click the icon to reopen
- **Start with Windows** — optional sign-in startup (published exe only)
- **First-run setup wizard** — asks for display name, avatar, external games folder, and ROM folder; detects Steam/Epic automatically
- **What's New on update** — summarized release notes from `CHANGELOG.md` after each app update
- **Watched scan folders** — saved ROM and installed-game folders auto-import new files on startup and when they appear
- **User profile** — display name and avatar in Statistics; editable in Settings → Profile
- Self-contained single-file `.exe` (~155 MB) — no .NET runtime install required

---

## Architecture

Modular monolith, no runtime plugins: `Core` (domain) → `Storage` (persistence) → `Import`/`Metadata`/`Emulation` (use cases) → `App` (WPF UI). ROM scanning and Bridge-managed RetroArch live in **`Bridge.Emulation/`**; launch/playtime tracking and app services live in **`Bridge/Services/`**. See [ARCHITECTURE.md](ARCHITECTURE.md) for the ADRs behind these decisions and [DEVELOPMENT.md](DEVELOPMENT.md#architecture-overview) for the full layer breakdown.

**IGDB metadata without configuration:** Bridge gets IGDB metadata (cover, description, developers, genres, scores, links, **screenshots**) for any game — including Epic-only titles like Genshin Impact — via its own [Cloudflare Worker](Bridge.Infra/igdb-proxy-worker/) that holds the IGDB credentials server-side (Worker Secrets, never in the app). A public IGDB proxy fallback and a user-configured IGDB key act as additional providers. The Worker returns IGDB's real screenshots too, so Epic/manual games get the same Table-view screenshot gallery as Steam games. See [ADR-13](ARCHITECTURE.md#adr-13-own-cloudflare-worker-as-the-igdb-metadata-backend).

---

## Development

| Document | Purpose |
|----------|---------|
| [DEVELOPMENT.md](DEVELOPMENT.md) | Build, test, architecture, migrations, updater, key files |
| [ARCHITECTURE.md](ARCHITECTURE.md) | ADRs (design decisions) |
| [PLAN.md](PLAN.md) | Phase tracker and scope |
| [CHANGELOG.md](CHANGELOG.md) | Release history |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Contribution standards |
| [PROJECT_FOUNDATION.md](PROJECT_FOUNDATION.md) | Archival notes from project inception (internal; not a Bridge spec) |
| [Bridge.Infra/igdb-proxy-worker/README.md](Bridge.Infra/igdb-proxy-worker/README.md) | IGDB Worker deploy guide |

See [DEVELOPMENT.md](DEVELOPMENT.md) for the full project guide.

---

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).

---

## Acknowledgments

Bridge was originally conceived after studying unified game library managers; [Playnite](https://playnite.link/) was the first inspiration for that idea. Bridge is built independently — a separate, smaller, plugin-free product in the same problem space.

---

## Sponsor

If you find Bridge useful, consider supporting the project:

[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white&labelColor=1a1a2e)](https://ko-fi.com/sebastianzavala82573)
[![GitHub Sponsors](https://img.shields.io/badge/Sponsor-GitHub-EA4AAA?style=flat-square&logo=githubsponsors&logoColor=white&labelColor=1a1a2e)](https://github.com/sponsors/ZavalaSebas)

---

<div align="center">

Made with care by [ZavalaSebas](https://github.com/ZavalaSebas)

</div>
