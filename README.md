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

Bridge is a from-scratch rewrite of the functional behavior of [Playnite](https://playnite.link/): a game library manager that unifies games from external libraries, manually added entries, and emulated ROMs into one local, self-contained catalog. It deliberately drops Playnite's plugin architecture and dual desktop/fullscreen frontends to stay small and easy to maintain, while preserving what actually matters day to day — incremental import, local metadata/image caching, fast virtualized views, and emulation support. See [`PROJECT_FOUNDATION.md`](PROJECT_FOUNDATION.md) for the full analysis behind this rewrite.

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

*(No GitHub release cut yet — the project isn't in a git repo yet either, see `PLAN.md`. `dotnet publish` has been verified locally to produce a genuine single-file `Bridge.exe`, though.)*

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

> Bridge is early — the MVP loop below works end-to-end and is verified by actually running it, but plenty of the rough edges listed in [PLAN.md](PLAN.md#development-phases) are still open (no library import from Steam/GOG yet, no automatic emulator detection, no local image caching). See [PLAN.md](PLAN.md#scope-current-vs-future) for full current/future scope.

- Local game library with add, edit, and delete
- Manual game entries
- Launch a game and track playtime automatically
- Basic statistics (totals, installed/not installed, favorites, total playtime)
- Simple ROM import (folder scan) with emulator matching and launching
- Metadata download from IGDB (description, release date, cover art, genres)

---

## Architecture

Modular monolith, no runtime plugins: `Core` (domain) → `Storage` (persistence) → `Import`/`Metadata`/`Emulation` (use cases) → `App` (WPF UI). See [ARCHITECTURE.md](ARCHITECTURE.md) for the ADRs behind these decisions and [DEVELOPMENT.md](DEVELOPMENT.md#architecture-overview) for the full layer breakdown.

---

## Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for the full project guide, architecture, and workflow rules. See [PROJECT_FOUNDATION.md](PROJECT_FOUNDATION.md) for the source analysis this rewrite is based on.

---

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).

---

## Acknowledgments

Bridge's design is informed by a close reading of [Playnite](https://playnite.link/)'s source (see `PROJECT_FOUNDATION.md` §27 for the specific files reviewed). Playnite is an excellent, mature project — Bridge exists to explore a smaller, plugin-free take on the same problem, not to replace it.

---

## Sponsor

If you find Bridge useful, consider supporting the project:

[![GitHub Sponsors](https://img.shields.io/badge/Sponsor-GitHub-EA4AAA?style=flat-square&logo=githubsponsors&logoColor=white&labelColor=1a1a2e)](https://github.com/sponsors/ZavalaSebas)

---

<div align="center">

Made with care by [ZavalaSebas](https://github.com/ZavalaSebas)

</div>
