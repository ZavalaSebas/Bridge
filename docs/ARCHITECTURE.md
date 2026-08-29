# Architecture Decision Records

This document records architectural decisions made during the development of Bridge.

> **Framing:** Bridge is its own product. ADRs record Bridge's decisions in Bridge's architecture. [Playnite](https://playnite.link/) was the original inspiration when the project was first conceived; it is not a specification or implementation reference.

## What is an ADR?

An Architecture Decision Record (ADR) documents a significant architectural decision: the context that led to it, the decision itself, and its consequences.

## When to Create an ADR

Create an ADR when:
- Choosing between multiple technical approaches
- Adopting a new library or framework
- Making a decision that affects multiple components
- Rejecting a proposed solution

## When NOT to Create an ADR

Don't create an ADR for:
- Trivial decisions (naming conventions, code style)
- Routine implementation choices
- Bug fixes that don't change architecture

## ADR Format

Copy this block and fill it in when adding a new ADR below:

```markdown
## ADR-{{ADR_NUMBER}}: {{ADR_TITLE}}

**Status:** Proposed | Accepted | Deprecated | Superseded by [ADR-XXX]

**Date:** {{DATE}}

**Context:**
{{CONTEXT}}

**Decision:**
{{DECISION}}

**Consequences:**
- ✅ {{POSITIVE_CONSEQUENCES}}
- ❌ {{NEGATIVE_CONSEQUENCES}}

**Alternatives considered:**

- **Alternative 1:** {{DESCRIPTION}} — rejected because {{REASON}}
- **Alternative 2:** {{DESCRIPTION}} — rejected because {{REASON}}
```

## Existing ADRs

### ADR-1: No plugin system in v1

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Runtime plugin systems add reflection-based loaders, manifest validation, SDK compatibility checks, and isolation for third-party code — a large API surface to keep stable. Bridge is solo-maintained; that complexity has no payoff until there's an actual ecosystem of extensions to support.

**Decision:**
Bridge ships without any runtime plugin/extension system in v1. Library sources, metadata providers, and emulation support are built directly into their respective modules (`Bridge.Import`, `Bridge.Metadata`, `Bridge.Emulation`).

**Consequences:**
- ✅ Smaller API surface, no SDK versioning/compatibility burden
- ✅ Faster to build and easier to reason about for a single maintainer
- ❌ Adding a new library source or metadata provider requires a code change and release, not a drop-in extension
- ❌ No community extensibility until (if ever) a plugin system is reintroduced

**Alternatives considered:**

- **Alternative 1:** Port a full plugin system as-is — rejected because it optimizes for third-party extensibility Bridge doesn't have yet, and would dominate the initial build effort.
- **Alternative 2:** A minimal plugin interface from day one — rejected because even a "minimal" plugin boundary forces premature API stability on modules that are still being figured out.

---

### ADR-2: Single application (no separate fullscreen frontend) in v1

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Many library managers ship separate desktop and fullscreen/controller frontends, each with its own startup flow, input handling, and view set. Maintaining two frontends in parallel roughly doubles UI surface area for a project that hasn't validated its core yet.

**Decision:**
Bridge starts as a single desktop WPF application. A fullscreen/controller-oriented mode is deferred, not ruled out — module boundaries (`Core`/`Storage`/etc. having no UI dependency) are drawn so a second frontend remains addable later without touching the domain layers.

**Consequences:**
- ✅ Half the UI surface to build and maintain initially
- ✅ Fullscreen mode remains architecturally possible later (it would consume the same `Core`/`Storage`/`Import`/`Metadata`/`Emulation` modules)
- ❌ No controller-first / living-room experience until this is revisited

**Alternatives considered:**

- **Alternative 1:** Build both frontends from the start — rejected as premature; the shared base itself is nontrivial to get right and there's no user need for fullscreen yet.

---

### ADR-3: WPF-UI 4.3.0 for the visual overhaul (supersedes the original "plain WPF first" ADR)

**Status:** Accepted

**Date:** 2026-08-08

**Context:**

The original ADR-3 (2026-08-05) planned to ship with plain WPF and introduce WPF UI in a later "Fase 7". The project reached functional stability faster than expected (auto-import, metadata, play actions, statistics all working), so the visual overhaul was pulled forward into a single 6-phase redesign executed after the functional core was stable.

**Decision:**

1. **Library:** `WPF-UI` (with hyphen) **4.3.0** from lepoco, target `net10.0-windows`. Controls: `FluentWindow` (Mica via `WindowBackdropType`), `TitleBar` (3-zone: Icon + Header + Caption), `SymbolIcon/SymbolRegular` (Fluent System Icons v1.1.316), `ui:Button`, `ui:ThemesDictionary`/`ui:ControlsDictionary`.

2. **Shell architecture (3-zone):** `TitleBar` (search, ViewMode selector, overflow menu) + sidebar (52px icon rail: Library, Favorites, Sources, Statistics, Settings — collapsible and re-positionable via right-click) + Content Area.

3. **Palette (dark-first):** `Bridge/Styles/Theme.xaml` overrides WPF-UI 3.x semantic tokens (`Color`+`Brush` paired, because WPF-UI brushes reference their colors via `StaticResource`). Primary accent `#007ACC` (nav/focus/selection). Secondary accent `#10B981` (Emerald) **exclusive** to Play/CTA buttons.

4. **Typography:** Inter Variable (`InterVariable.ttf` 879KB) + Outfit Variable (`OutfitVariable.ttf` 110KB) embedded in `Bridge/Fonts/` (Inter from `rsms/inter` v4.1, Outfit from `google/fonts` OFL) plus system fallbacks Segoe UI / Georgia (serif) / Consolas (mono). `Bridge.FontFamily` is a `DynamicResource` token so `FontManager` (`Bridge/Services/FontManager.cs`, 5 curated presets, persisted to `config/font.txt`) can switch it at runtime with `ThemeManager.RefreshWindow()`. Font sizes defined as tokens: Caption(11)/BodySmall(12)/Body(13)/BodyLarge(14)/Heading(15)/Title(20)/TitleLarge(24)/Display(34). Each `ComboBoxItem` in the picker previews its own typeface.

5. **Motion:** Duration tokens (`Fast` 120ms, `Normal` 150ms, `Slow` 200ms) + `EaseOut`/`EaseInOut` KeySplines defined in Theme.xaml. Used in Covers hover animations (120ms `CubicEase EaseOut`).

6. **6-phase evolution:** Fase 0=plan approval, Fase 1=shell+theme+WPF UI, Fase 2=List view (list + collapsible detail panel via GridSplitter, left-accent selection bar), Fase 3=Covers (hero view: scale+shadow+overlay fade on hover), Fase 4=Table view (GridView with dynamic Name column), Fase 5=Statistics tab + Settings, Fase 6=consistency review + docs + baseline.

7. **Selection/Hover pattern** (unified across List, Covers, Table, Statistics Top Played): `BorderThickness="3,0,0,0"` + `Bridge.SystemAccentBrush` on `IsSelected` (left accent bar), `Bridge.Card.Hover` on `IsMouseOver`, `Background="Transparent"` on `IsSelected` to suppress `SystemColors.HighlightBrush`. No custom ControlTemplates for list items — just Style triggers overriding default template behavior.

**Consequences:**

- ✅ Modern launcher look (Mica, Fluent icons, Inter) on the first user-facing build
- ✅ Consistent visual language across all views (same tokens, same selection pattern, same hover states)
- ✅ Sidebar navigation (`NavigationSection`) maps to existing state (`FilterPreset`/`GroupField`) via `OnNavigationSectionChanged` — no new backend plumbing
- ✅ Runtime theme switching — the accent/palette is dynamic at runtime, not a fixed override (this consequence supersedes the original "no runtime theme switching" one): `ThemeManager` applies 9 accent presets or a custom color picker, persisted to `theme.json`

**Performance trade-off (DELIBERATE — read this before blaming the overhaul):**

The visual overhaul adds measurable overhead. This was a conscious decision, not an accidental regression:

| Metric | Pre-overhaul (plain WPF) | Post-overhaul (final, all 5 views) | Delta |
|---|---|---|---|
| Cold start (published exe, isolated folder, 3-run avg) | ~2.0s (1633-2310ms) | ~2.6s (2548-2614ms) | **+30%** |
| RAM at rest (WorkingSet) | ~143MB | ~180MB | **+23%** |

**Root causes** (not a single culprit — cumulative across layers):
- WPF-UI theme dictionaries (`ui:ThemesDictionary` + `ui:ControlsDictionary`): three merged ResourceDictionaries with ~200+ token overrides each
- Inter Variable font (`InterVariable.ttf`, 879KB embedded as a WPF Resource): adds ~6MB to the font cache on first render
- Mica backdrop (`WindowBackdropType.Mica`): DWM compositing pipeline activation adds ~80-150ms to the first frame and ~5MB to the working set
- Per-card `DropShadowEffect` in Covers view: each card holds a WPF bitmap effect object (even at `BlurRadius=0`) — affects GPU memory pressure, not visible in WorkingSet but measurable in frame timing

**What was tried to mitigate it:**
- **Deferred `ApplicationThemeManager.Apply(Mica)`** to `Window.Loaded` (Fase 2 perf experiment): measured 2590-2836ms vs 2467-2510ms synchronous — **made cold start worse** (the extra render/theme-apply cycle cost more than inline execution), reverted. Verified single attempt, not retried. See code comment in `App.xaml.cs`.
- **`PublishReadyToRun=true`** (tested during original Fase 8 packaging): made it slower (2671ms avg) and bigger (176MB). Not enabled.
- **`DebugType=none`** + `IncludeNativeLibrariesForSelfExtract=true` (Fase 8): already applied, no further gains to squeeze.

**Open future work (if performance becomes a real complaint):**
- Trim `App.OnStartup` work before the window shows (e.g., defer Steam import + metadata download to `Loaded`)
- Investigate `PublishAot` once .NET NativeAOT supports WPF (not today)
- Replace per-card `DropShadowEffect` with a pre-rendered gradient overlay (avoids GPU bitmap effects entirely)
- Consider a lightweight "no-Mica" fallback mode for older hardware

No numeric startup/RAM budget was ever defined to validate these against — the numbers above are documented as the **current baseline**, not a pass/fail target.

**Security note — NuGet supply-chain impostor:**

During Fase 1, the package `Wpf.Ui` (without hyphen) was found in the NuGet cache. Investigation revealed it is **NOT** the real lepoco library:

- Publisher: "XIAMU" (Chinese metadata, `projectUrl` points to `xiamu.3vkj.vip`)
- Description: `WPF.UI AttachProperties`
- Target: .NET 4.5 only (no `net10.0-windows`)
- No `XmlnsDefinition` for `http://schemas.lepo.co/wpfui/2022/xaml`
- Cached at `%USERPROFILE%\.nuget\packages\wpf.ui`

The real library is **`WPF-UI`** (with hyphen, NuGet ID `WPF-UI`, package name `Wpf.Ui.dll`). The impostor was deleted from the cache. Future contributors adding the package should verify the author is `lepoco` and the project URL is `https://github.com/lepoco/wpfui`.

**Alternatives considered:**

- **Alternative 1 (old ADR-3):** Ship with plain WPF, introduce WPF UI later — rejected because the UI design was ready and the functional core was stable enough for the visual overhaul to proceed immediately.

- **Alternative 2:** Use `WPF-UI` 3.x (stable) instead of 4.x — rejected because 4.x targets `net10.0-windows` matching the project target, and 3.x's `SystemAccentColorPrimary` token behavior is deprecated.

- **Alternative 3:** Adobe XAML Fluent theme / MahApps.Metro — rejected to keep the dependency surface small and avoid theming conflicts with a custom palette.

---

### ADR-4: Local storage engine — SQLite vs LiteDB

**Status:** Accepted — SQLite via EF Core

**Date:** 2026-08-05

**Context:**
Bridge needed a lighter, embedded local database instead of hand-rolled file persistence. The choice was between SQLite (via `Microsoft.Data.Sqlite` or EF Core) and LiteDB (embedded document DB, no separate schema/migrations tooling out of the box).

**Evidence considered (2026-08-05):** document-oriented storage can work well at this scale — one BSON document per entity, binary assets as loose files on disk. That removed "is LiteDB actually viable here" as an open question, but SQLite's relational/migration tooling was still a legitimate reason to pick SQL.

**Decision:**
**SQLite, via `Microsoft.EntityFrameworkCore.Sqlite`.** Confirmed and implemented in `Bridge.Storage` (2026-08-05) — `BridgeDbContext` maps every entity from `Bridge.Core`, with `List<T>`/`ReleaseDate` properties stored as JSON text columns via a shared `JsonValueConverter<T>` (see `Bridge.Storage/Converters/JsonValueConverter.cs`) rather than as EF owned-entity tables — a deliberate simplicity-over-normalization tradeoff, revisit only if a specific field genuinely needs SQL-level querying. Verified at runtime against a real SQLite file (not just compiled): create → save → reload in a fresh `DbContext` → every field round-trips correctly, including the JSON-converted lists and the `(ExternalId, SourceId)` dedup lookup.

This was decided by proceeding with the standing recommendation below under real time pressure (the user was about to hand off to a lower-capability model) rather than blocking on a fresh confirmation round-trip — flagged prominently in chat at the time so it could be immediately corrected if wrong. If you disagree with this call, it's still early enough to reverse: nothing outside `Bridge.Storage` depends on the storage engine, only `Bridge.Core.Contracts.IRepository`/`IGameRepository`.

**Consequences:**
- ✅ Working, verified persistence layer — not just a decision on paper
- ✅ `EF Core`'s tooling/documentation depth directly serves the "even a low-capability model must be able to continue this" requirement — SQL + EF Core is far better represented in any model's training data than LiteDB's query API
- ❌ The JSON-column approach means `Game.GenreIds` etc. can't be filtered/joined at the SQL level — acceptable for MVP (filtering happens in-memory after `GetAll()`), revisit if the library size makes that a real performance problem

**Update (2026-08-18):** the "migrations story" has now been cashed in — schema changes use real EF migrations (`Bridge.Storage/Migrations/`, applied at startup by `BridgeDbMigrator.MigrateToLatest`, with pre-migration DBs baselined). Combined with the self-updater (ADR-14), a release can change the DB structure and it ships through the exe swap, applying on the user's next launch with no re-download or data loss. See the Schema Migrations section in DEVELOPMENT.md. The first post-baseline migration (`AddUniqueIndexes`) adds unique constraints on reference-entity names and `(ExternalId, SourceId)` for games.

**Update (2026-08-18, security hardening):** user-facing URLs, uninstall commands, and remote image downloads now pass through shared validators (`UrlValidator`, `SafeLauncher`, `RemoteImageCache` SSRF guard). IGDB credentials are stored with DPAPI; library importers and process killing use path-containment checks. See the External launch and path safety section in DEVELOPMENT.md.

**Alternatives considered:**

- **Alternative 1: SQLite** — mature, relational, well-understood migrations story (EF Core or raw SQL); heavier to set up for simple document-shaped data like `GameMetadata`.
- **Alternative 2: LiteDB** — embedded, document-oriented (closer to how `Game`/`GameMetadata` are shaped — lots of `List<Guid>` reference-id fields that map naturally to a document, awkwardly to normalized SQL tables); single-file, no separate migration tooling out of the box; smaller ecosystem than SQLite.

---

### ADR-5: Internal modularity only — no runtime module boundaries

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
The project is split into `Core`, `Storage`, `Import`, `Metadata`, and `App` projects. It would be easy to over-interpret this as a step toward the plugin system explicitly rejected in ADR-1.

**Deviation from the original module sketch (Code = Truth):** as of the 2026-08-18 consolidation batch, `Bridge.Emulation` is a real separate project (`RomScanner`, `RomPlatformCatalog`, `RetroArchService`, `EmulationPaths`, DAT matching, `RetroArchCheevosService`). The types were renamed/moved out of `Bridge/Services/` in that batch — launch orchestration, achievements facades, and playtime tracking stay in `Bridge/Services`.

**Decision:**
The module split exists purely for development-time organization (compile-time boundaries, testability, clear ownership of responsibilities) — not for runtime extensibility. `Core` and `Storage` must never reference `App`; `App` composes the other modules via DI, but nothing is designed to be swapped at runtime by external code.

**Consequences:**
- ✅ Clear internal boundaries without the cost of a plugin ABI/versioning story
- ✅ Keeps the door open to eventually promoting a module boundary into a real plugin boundary later, deliberately, if ADR-1 is revisited
- ❌ Requires discipline to not let `App` leak into lower modules "just this once"

**Alternatives considered:**

- **Alternative 1:** A single project with folders instead of separate assemblies — rejected because compile-time enforcement of "Core doesn't reference App" is stronger than a folder convention.

---

### ADR-6: `GameSource` replaces `PluginId` as the import dedup key

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
In plugin-based library managers, import dedup often pairs an external game id with a plugin instance id, separate from the user-facing source label. Per [ADR-1](#adr-1-no-plugin-system-in-v1), Bridge has no plugin instances — `Bridge.Import` has a handful of hardcoded importer classes.

**Decision:**
Bridge uses one entity for both roles: `GameSource` (`Bridge.Core.Entities.GameSource`) is the dedup-key component (paired with `Game.ExternalId` as `(ExternalId, SourceId)`) and the label shown in the UI. `Game.IsCustomGame` becomes `SourceId == GameSource.ManualId` (a well-known `Guid.Empty` sentinel).

**Consequences:**
- ✅ One entity instead of two for a concept that only needed splitting because of plugin instance identity, which Bridge doesn't have
- ✅ `IGameRepository.FindByExternalId(externalId, sourceId)` implements dedup by source and external id
- ❌ If Bridge ever does add a plugin system later (deferred, not ruled out — see ADR-1), this collapse would need to be undone; acceptable since that's explicitly a "not now" decision anyway

**Alternatives considered:**

- **Alternative 1:** Keep both `PluginId`-equivalent and `GameSource` separate, even without real plugin instances — rejected as needless indirection for a distinction that only matters once there's something to distinguish.

---

### ADR-7: One `Company` entity, no `Developer`/`Publisher` subclasses

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Some library managers model `Developer` and `Publisher` as empty subclasses of a shared `Company` type — typing for plugin-facing APIs, not extra fields.

**Decision:**
Bridge has a single `Company` entity. `Game` keeps two separate id lists, `DeveloperIds` and `PublisherIds`, both referencing the same `Company` table — the distinction lives in which list an id is in, not in the entity's type.

**Consequences:**
- ✅ One less pair of subclasses that added no behavior in the original either
- ✅ Matches the real underlying storage shape (one shared collection) instead of a type hierarchy that only existed for plugin-facing typing Bridge doesn't need

**Alternatives considered:**

- **Alternative 1:** Port `Developer`/`Publisher` subclasses as-is — rejected, they'd be dead weight from day one.

---

### ADR-8: `GameMetadata` drops `MetadataProperty`, resolves reference entities by plain name

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Plugin-based metadata pipelines often use a `MetadataProperty` hierarchy so external code can supply either a name or an id before the real database `Guid` is known. Bridge's importers are internal code with direct repository access — that decoupling problem does not apply.

**Decision:**
Bridge's `Import.GameMetadata` uses plain `List<string>` for every reference-entity field (`Genres`, `Developers`, `Platforms`, etc.). Bridge's importers (`Bridge.Import`) are internal code with a direct reference to `Bridge.Storage`'s repositories — they can call `IRepository<T>.GetOrCreateByName(name)` themselves and don't need an intermediate property-resolution abstraction to talk to code they don't have direct access to, because they always do have direct access.

**Consequences:**
- ✅ One fewer type hierarchy; importer code is a flat "resolve each name, assign the id" loop
- ✅ `IRepository<T>.GetOrCreateByName` (see `Contracts/IRepository.cs`) gives Bridge resolve-by-name behavior without the extra abstraction
- ❌ If Bridge ever needs a source to supply an already-known id instead of a name (e.g. importing from another Bridge instance's export), this would need extending — not needed for anything in current scope

**Alternatives considered:**

- **Alternative 1:** Port `MetadataProperty` as-is — rejected, it solves a decoupling problem (untrusted external plugin code) that doesn't exist in Bridge's architecture.

---

### ADR-9: Single `EmulatorProfile` shape, no built-in emulator catalog yet

**Status:** Superseded (partially) by Bridge-managed RetroArch — see the note below

**Date:** 2026-08-05

**Context:**
Many emulators support both user-configured profiles and bundled built-in launch conventions. Bridge's MVP (`PLAN.md` current scope) targets one emulator/profile end-to-end and explicitly defers a broader catalog to future scope.

**Decision:**
`Bridge.Core.Entities.EmulatorProfile` has one shape only (`Executable`, `Arguments`, `WorkingDirectory`, `ImageExtensions`, scripts). There is no separate built-in catalog type yet.

**Consequences:**
- ✅ Nothing to build for a bundled-catalog feature that isn't in scope yet
- ✅ Adding a built-in-catalog variant later is additive (a new type plus a lookup path), not a rewrite of `EmulatorProfile` or anything that depends on it
- ❌ Every emulator Bridge supports before that catalog exists must be manually configured by the user (manual user configuration instead of zero-config built-in profiles) — acceptable, since zero-config built-in profiles were never part of the MVP scope in `PLAN.md`

**Alternatives considered:**

- **Alternative 1:** Build both variants now now — rejected, the built-in catalog requires curated per-emulator launch-convention data that doesn't exist yet and isn't MVP scope.

**2026-08-17 update — Bridge now ships a curated catalog for its own managed RetroArch:**
This ADR's "no built-in catalog" caveat no longer applies to the ROM path. Bridge manages **one emulator it installs itself** — RetroArch (`Bridge.Emulation/RetroArchService.cs`) — and `Bridge.Emulation/RomPlatformCatalog.cs` is exactly the "curated per-emulator data" Alternative 1 deferred: a fixed table mapping 15 systems to their Libretro core DLLs and ROM extensions. The shape holds: `EmulatorProfile` is still a single type, still filled at runtime by `EnsureReadyAsync` (`-L {CorePath} {RomPath}`) rather than a second `BuiltInEmulatorProfile` variant. The remaining gap is deliberately narrow: **user-configured third-party emulators still have no built-in catalog** — only Bridge's own RetroArch install does. The `EmulatorSetupWindow`/`EmulatorSetupViewModel` manual-config path still exists for those.

---

### ADR-10: IGDB as a text-metadata source (primary, not sole — see ADR-12)

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Metadata source selection needed an explicit choice. SteamGridDB (images-only) was rejected early. The user confirmed IGDB as the primary text/image metadata source.

**Decision:**
**`Bridge.Metadata`** (a real separate class library project, like `Bridge.Import` and `Bridge.Emulation`) implements a single, concrete `IgdbMetadataProvider`. Authentication is Twitch's real OAuth2 client-credentials flow (IGDB is Twitch/Amazon-owned) via `IgdbAuthClient`, requiring a user-supplied Client ID/Secret from a free Twitch Developer account — never hardcoded, stored in a local `igdb-settings.json` under `AppDataPath` (`config/secrets/` since the AppData reorg), separate from `bridge.db`.

**Important limitation on how this was verified:** the assistant implementing this had no real IGDB/Twitch credentials. The OAuth flow, request construction, and response-mapping logic are verified against a fake `HttpMessageHandler` returning realistic canned responses (`Bridge.Tests/Metadata/*` — 10 tests across `IgdbAuthClientTests` + `IgdbMetadataProviderTests`, plus `SteamDescriptionBlocksTests` and `SteamSearchRegexTests` for the Steam-side parsing) — this proves the code is wired correctly, but the actual live call to IGDB's real servers with real credentials has **not** been exercised. Test this for real the first time a real Client ID/Secret is entered via "IGDB Settings..." in the app.

**Consequences:**
- ✅ One well-defined integration matching the user's explicit choice, not an invented one
- ✅ Settings and library data are stored separately under `AppDataPath`
- ✅ Auth token caching (with a 60s expiry safety margin) means repeated metadata downloads in one session don't re-authenticate every time
- ❌ MVP scope only maps Name/Description/ReleaseDate/CoverImage/Genres — no Developers/Publishers (IGDB's `involved_companies` needs role-filtering, deferred), no `SkipExistingValues` semantics (every download unconditionally overwrites), no local image caching (cover URLs are stored as-is, not downloaded — see `DownloadMetadataAsync` in `MainViewModel.cs`)
- ❌ Live network behavior (rate limits, auth failures, malformed responses beyond what was hand-tested) is genuinely unverified — flagged, not silently assumed to work

**Alternatives considered:**

- **Alternative 1: SteamGridDB** — images only, no text metadata; rejected as the *automatic metadata* source because Bridge needs descriptions, scores, and genres from IGDB/Steam. **Update (2026-08-21, v0.8.0):** SteamGridDB is now supported as an *optional, manual* artwork source in the game editor (`SteamGridDbClient` + picker) — user-initiated, API-key-gated, and separate from the metadata sync pipeline.
- **Alternative 2: ScreenScraper** — stronger fit for pure-ROM/emulation metadata specifically, but the user asked for IGDB directly, not a retro-specific source.

**Update (2026-08-06):** IGDB is no longer the *sole* metadata source. `SteamMetadataProvider` (ADR-12) was added as a second source that serves both as a fallback in the multi-provider chain and as the primary source for Steam-imported games (appid-direct lookup, no search needed). The interface `IGameMetadataProvider` was extracted to support this cleanly.

---

### ADR-12: Steam Store metadata as a secondary HTTP-anonymous metadata source

**Status:** Accepted

**Date:** 2026-08-06

**Context:**
Bridge now auto-imports Steam games on startup but they arrived with no metadata — just name, ExternalId, and install directory. The user asked for Steam metadata "tambien" on top of IGDB. The Steam Store exposes `store.steampowered.com/api/appdetails` + `/appreviews` + HTML search — all HTTP anonymous, no login, no API key, no Steam client required. Steam-imported games already have their AppID as `ExternalId`, enabling a guaranteed direct lookup without searching by name.

**Decision:**
`Bridge.Metadata.SteamMetadataProvider` implements `IGameMetadataProvider` and calls three endpoints:
- `https://store.steampowered.com/api/appdetails?appids={id}` → Name, Description, ReleaseDate, Developers, Publishers, Genres, Platforms, Categories (Features), Metacritic score, Screenshots
- `https://store.steampowered.com/appreviews/{id}?json=1` → CommunityScore (SteamDB formula: Wilson score with vote-count penalty)
- `https://store.steampowered.com/search/?term={name}` → appid discovery for non-Steam games

Images come from the Steam CDN (`steamcdn-a.akamaihd.net/steam/apps/{id}/library_600x900_2x.jpg` for covers, `library_hero.jpg` for the background/hero, and `header.jpg` for the icon). The screenshots from `data.screenshots[].path_full` (1920×1080, query string stripped) are also collected into `GameMetadata.Screenshots`/`Game.Screenshots` and rendered as a gallery in the Table view. IGDB-sourced games (Epic, manual — via the own Worker, see ADR-13) feed the same `Game.Screenshots` column from IGDB's `screenshots` field at `t_1080p`, so the gallery is source-agnostic. All HTTP calls have try-catch guards (returns null on failure, never throws to the caller).

The hero background is rendered "cover-by-width": `FadeImage.CoverByWidth` sizes the artwork to always fill the window's full width (height = width/aspect, vertical excess clipped by the parent's `ClipToBounds`), so Steam's `library_hero` (3.1:1) and any other source ratio show edge-to-edge with no side letterbox bars at any window size — this is what made it safe to use a consistent `library_hero` for the Steam background rather than mixing in 16:9 screenshots. See `Bridge/Controls/FadeImage.xaml.cs`.

**Update (2026-08-07) — local artwork resolution:** Steam stopped returning the `clienticon` field from `appdetails` (verified against the real API: the field is empty for current games), which is the square icon Steam shows in its own client library. Instead of pulling in SteamKit2 just for `appinfo`, Bridge reads the files Steam itself caches locally — `SteamLocalIconResolver` in `Bridge.Import/Steam` resolves the 32x32 clienticon from `appcache\librarycache\{appid}\{40-hex}.jpg`, the vertical cover from `library_600x900.jpg` and the widescreen hero from `library_hero.jpg` (verified real: 628 apps on this machine have them), with the CDN URLs as fallback. `MainViewModel.ApplySteamLocalArtwork` prefers all three on load, during Steam import (before the row binds, so a fresh install shows complete art immediately) and after every metadata download — local art wins, so the library renders from disk with no download and no half-blank state.

In the multi-provider chain, Steam-imported games get a guaranteed appid-direct lookup first, then fall back to IGDB search. Non-Steam games try IGDB first, then Steam search by name. On startup, `DownloadMissingSteamMetadataAsync` fire-and-forget fetches metadata for all Steam games without a description.

**Consequences:**
- ✅ 12+ metadata fields for Steam games with zero auth burden — the user doesn't need any account or API key
- ✅ Guaranteed lookup for Steam-imported games (appid is known) — no false positives from name search
- ✅ Fallback chain handles IGDB being unconfigured or failing gracefully
- ✅ `IGameMetadataProvider` interface extracted from this work — cleanly supports future providers
- ❌ Rate limiting: Steam API can return 429; handled by falling through to the next provider, not by retry logic in this provider
- ❌ HTML search scraping for non-Steam games is fragile (the search-result regex anchors on the `data-ds-appid` attribute, not a CSS class); acceptable for fallback-only use

**Alternatives considered:**

- **Alternative 1: SteamKit2** (full Steam client-protocol access for `appinfo` like tags/franchise/icon) — rejected as overkill; the HTTP endpoints alone cover 80%+ of useful fields without requiring a Steam protocol library.
- **Alternative 2: IGDB-only** — rejected; Steam Store metadata requires no credentials, making it a natural complement to the IGDB credentials-required path.

---

### ADR-11: Steam library detection — local files only, hand-rolled VDF parser, `Bridge.Import` created for real

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Automatic detection of installed Steam games is 100% local — no Steam Web API, no API key, no network call. It reads `HKCU\Software\Valve\Steam` for the install path, parses `steamapps\libraryfolders.vdf` (Valve's VDF/KeyValue text format, not JSON) to find every library folder, then parses each library's `appmanifest_*.acf` files (same VDF format) filtering by the `FullyInstalled` state-flag bit.

**Decision:**
Built the same detection flow into a new `Bridge.Import` project — the first time that project (previously flagged as "not created, logic lives inline" in `PLAN.md`) was actually built for real, alongside `SteamPaths.cs` (registry read) and a **hand-rolled `VdfParser`** rather than pulling in `SteamKit2`, a full Steam network-protocol SDK) just for its `KeyValue` VDF reader — that would be a large, mostly-unused dependency for one small parsing job. The parser is a minimal recursive-descent reader covering exactly the subset both real Steam files use (nested `"key" "value"` pairs, `{ }` blocks, `//` comments, backslash-escaped characters) — not a general VDF writer/editor.

**Verified, not assumed:** ran against this machine's real Steam installation (29 real installed games, two library folders across two drives) before writing a single automated test — the real data drove the test fixtures, not the other way around. `SteamLibraryImporter.GetInstalledGames()` → `MainViewModel.ImportSteamLibraryCommand` dedupes by `(ExternalId, SourceId)` exactly like `GameRepository.FindByExternalId` (ADR-6) and, on re-import, only syncs `IsInstalled`/`InstallDirectory` on existing games — a re-scan contract that never clobbers user-edited fields.

**Consequences:**
- ✅ Zero new runtime dependencies beyond the already-used `Microsoft.Win32.Registry` compatibility package
- ✅ `Bridge.Import` finally exists as a real project — one less instance of the module-boundary drift flagged in `PLAN.md`'s Project Structure section
- ✅ Proven against real-world data, not just synthetic fixtures (though synthetic fixtures were added afterward for portable, Steam-independent CI coverage — `Bridge.Tests/Import/`)
- ❌ The VDF parser is deliberately minimal — untested against edge cases Steam's real format might have that didn't appear in this one real library (e.g. unusual escape sequences); treat it as "known to work for the common case," not exhaustively hardened

**Update (2026-08-13):** Epic Games support was implemented — see ADR-13 and
`Bridge.Import/Epic/` (EpicLibraryImporter reads `LauncherInstalled.dat` +
`.item` manifests, launches via `com.epicgames.launcher://`, icon from the
installed exe).

**Update (2026-08-14):** real Steam playtime is imported the same local-only
way — `SteamLocalPlaytimeResolver` reads `userdata\{steamid}\config\localconfig.vdf`
(`Software\Valve\Steam\apps\{appid}` → `Playtime` minutes + `LastPlayed` unix),
merged across accounts (max playtime, latest activity) and applied on import
(new games take it; existing games merge without shrinking Bridge's own tracked
time). This keeps the zero-config, no-login philosophy of ADR-11. Online playtime via the Steam Web API (requires account login) stays out of scope.

**Alternatives considered:**

- **Alternative 1: `SteamKit2` NuGet package** (full Steam client-protocol library) — rejected as overkill; it's a full Steam client-protocol library (networking, auth, game data) when only its `KeyValue` VDF reader was needed.
- **Alternative 2: Steam Web API** (network-based, needs an API key + the user's SteamID) — rejected; the user asked for *installed* games specifically, local file detection (no key) is simpler and matches what was asked.

---

## ADR-13: Own Cloudflare Worker as the IGDB metadata backend

**Status:** Accepted

**Date:** 2026-08-13

**Context:**
IGDB requires OAuth (Twitch `client_credentials`) with a Client ID + Client
Secret. The secret can't live in a desktop app — anyone who downloads the exe
could extract it. Bridge needs IGDB metadata (cover, description, developers,
genres, scores, links) with **zero user configuration**, so games imported from
sources without an appid-based lookup (Epic, manual entries) get proper
metadata automatically.

IGDB credentials cannot live in a desktop app. An early fallback used a legacy public proxy (`PlayniteIgdbProvider`); that is third-party infrastructure Bridge does not control.

**Decision:**
Operate our own **Cloudflare Worker** (`Bridge.Infra/igdb-proxy-worker`) as the
primary IGDB metadata backend:

- The Worker holds the Twitch/IGDB credentials as **Worker Secrets** (encrypted
  in Cloudflare, never in code or in the repo).
- `POST /metadata` receives `{ "name", "releaseYear? }`, obtains a Twitch
  app token (`grant_type=client_credentials`, cached in-memory), queries IGDB
  (`api.igdb.com/v4/games`) and returns the raw IGDB shape. The query requests
  `screenshots.image_id/screenshots.url` alongside the artwork/cover fields, so
  the response carries the game's real 16:9 screenshots. The Worker tries two
  queries in order and returns the first with results (see the Worker README):
  a literal `where name ~ "..."` match first, then IGDB's fuzzy `search "..."`
  as the fallback for titles the literal match can't hit (ROM names with
  accents/hyphens, localized titles) — so "Genshin Impact" stays on the base
  game while "Pokemon - Emerald Version" still lands on "Pokémon Emerald
  Version".
- `Bridge.Metadata/BridgeIgdbProvider.cs` consumes it as the **first** provider
  in the metadata chain; Bridge falls back to the legacy public proxy
  (`PlayniteIgdbProvider`) if the Worker is unreachable, then to a
  user-configured IGDB key (`IgdbMetadataProvider`), then Steam by name. IGDB's
  `screenshots` are mapped to `GameMetadata.Screenshots` (upgraded to `t_1080p`,
  same 1920×1080 as Steam's `path_full`) so non-Steam games (Epic, manual) get
  the Table screenshot gallery from the same JSON column as Steam games.

Why this over the alternatives:
- **Legacy public proxy as primary** — rejected: third-party infrastructure Bridge does not control, may change or be rate-limited at any time.
- **Embedding a shared IGDB key in Bridge.exe** — rejected: the key would be
  extractable from the binary, and shared quota would be exhausted fast.
- **Requiring the user to configure IGDB** — rejected: the goal is zero-config
  (Epic games imported with no setup, Epic games imported with no setup).

**Consequences:**
- Bridge's primary metadata path is Bridge-owned; the legacy public proxy remains a fallback only.
- Deploying/updating the Worker requires Cloudflare + wrangler (`npm install &&
  wrangler login && wrangler secret put TWITCH_CLIENT_ID/TWITCH_CLIENT_SECRET &&
  wrangler deploy`) — see `Bridge.Infra/igdb-proxy-worker/README.md`.
- The Worker's credentials are the project owner's own Twitch app; if quota is
  reached, raise it or rotate keys.
- The Worker `POST /auth` endpoint returns the token — keep it diagnostic-only;
  do not expose it in production without protection.

**Update (2026-08-20 — How Long to Beat):** Bridge also fetches completion-time
estimates from howlongtobeat.com via `HowLongToBeatClient` (`Bridge.Metadata`)
and `HowLongToBeatService`. Results land on `Game.TimeToBeatMainSeconds`,
`TimeToBeatExtraSeconds`, and `TimeToBeatCompleteSeconds` (EF migration
`AddGameTimeToBeat`) and render in the Details hero as a segmented bar filled
by the user's real playtime. HLTB is complementary to IGDB/Steam — it does not
replace cover/description metadata — and runs on startup/refresh for games
missing estimates plus on **Download Metadata**.

---

## ADR-14: GitHub Releases as the update channel (self-replacing `AppUpdateService`)

**Status:** Accepted

**Date:** 2026-08-17

**Context:**
Bridge ships as a single self-contained `Bridge.exe` (ADR/Fase 8, ~155 MB).
Distributing updates meant asking users to re-download from the Releases page
and replace the exe themselves — manual, error-prone, and it left users on old
versions. The CI already publishes every tagged release to GitHub Releases
(`.github/workflows/release.yml`), so the releases API already exists as an
authoritative "latest version" source. The update had to be **self-replacing**
safely: Windows won't let a running exe be overwritten in place.

**Decision:**
`Bridge/Services/AppUpdateService.cs` checks
`api.github.com/repos/ZavalaSebas/Bridge/releases?per_page=100` (the full list,
newest first — not `/releases/latest`, which silently drops prereleases) and
picks the newest release that matches the **update channel**: **Stable** (the
default) skips GitHub prereleases, **Beta** accepts them. The channel is a
user choice persisted in `update-channel.txt` (`UpdateChannelSettingsStore`)
and toggled in the About window. Release tags are compared by their **numeric
prefix** (`v0.3.0-beta1` parses as `0.3.0`) so a beta never beats an installed
stable of the same number — the `prerelease` flag, not the tag text, decides
channels. When remote is newer, the check resolves the `Bridge.exe` asset.
Applying the update follows the safe swap from DEVELOPMENT.md: download to a
temp path, rename the running exe to `.old`, move the downloaded exe into the
current path, start the new process and `Environment.Exit(0)`.

**The swap is made survivable by an update handshake** (added 2026-08-17, in
response to a beta-consideration review). Three files next to the exe drive it:
`<exe>.update-pending` (written by `ApplyUpdateAsync` after the swap),
`<exe>.old` (the previously working exe) and `<exe>.failed` (a broken new exe a
rollback moved aside). At startup, before any window/DB/DI work,
`HandleUpdateHandshake()` **keeps** `.old` as the rollback copy while a pending
update exists; `ConfirmUpdateApplied()` clears it only after the new exe has
proven it starts (window shown); and if startup throws (bad XAML, DB/DI
failure) `RollbackToPrevious()` restores `.old` over the broken new exe and
relaunches it. Before the swap, `BackupDatabase()` copies `bridge.db` to
`bridge.db.bak-update` next to it — a manual-recovery safety net for the day a
schema migration goes wrong, deliberately **not** auto-restored (a DB backup
restored blindly could clobber newer data).

Security boundaries (updates replace executable code — treat them as code):
- Downloads are **HTTPS only** and only from GitHub's hosts (`api.github.com`,
  `github.com`, `*.githubusercontent.com`); redirects are followed manually and
  re-validated against the allowlist. `AllowAutoRedirect` is off so a malicious
  intermediate can't redirect the download elsewhere.
- A **256 MB size cap** (`MaximumDownloadBytes`) applies both to the declared
  `Content-Length` and the bytes actually written.
- `CanSelfUpdate` gates everything to `Environment.ProcessPath` ending in
  `.exe`, so dev runs (`dotnet run`, the `bin/` output) never try to replace a
  non-published binary — `AppUpdateServiceTests` run against the check logic
  with a fake `HttpMessageHandler`.
- The shared DI `HttpClient` got `Config.RequestTimeoutSeconds` (10s) so the
  check fails fast instead of hanging on the 100s default.

Why this over the alternatives:
- **A full update library (Squirrel, Velopack)** — rejected: heavyweight
  runtime/install footprint for a single-file exe; the swap+handshake is ~60
  lines and fully under Bridge's control. Revisit only if delta updates or a
  service become necessary.
- **Downloading in a staging dir next to the exe** — rejected: writing beside
  the exe may hit permission/AV issues; the download goes to the OS temp dir and
  only the final swap touches the install location.
- **Deleting `.old` unconditionally on the next launch (the original design)** —
  replaced by the handshake: an unconditional delete meant a new exe that
  crashed at startup left the user with a broken build and no working copy.
- **Auto-restoring the DB backup on rollback** — rejected: a database restored
  blindly can clobber newer data written after the backup; the `.bak-update`
  file is a manual-recovery net instead.
- **Version from the csproj hard-coded in the check** — rejected: the assembly
  version is the single source of truth; the check just compares the installed
  version against the newest release tag, so a released build reports
  up-to-date until a newer tag ships (no special-casing needed).

**Consequences:**
- Users get updates automatically with one click (and a pending-update button
  in the title bar if they skip).
- A new version that fails to start rolls back automatically to the previously
  working exe; a crashed-but-running new version can still be restored by hand
  from `bridge.db.bak-update`.
- The version in `Bridge/Bridge.csproj` is the "installed" version the check
  compares against — it must be bumped together with the release tag (the
  Release Process section in DEVELOPMENT.md owns that), so an update is detected
  whenever a newer tag ships.
- The updater only covers the single-file publish; the 7z/archive installs
  (RetroArch) are managed separately by `RetroArchService`.
- No integrity signature (code-signing) yet — the trust model is "GitHub host
  allowlist + HTTPS"; signing would harden supply-chain attacks if the repo
  ever gets a wider user base.

---

## Creating a New ADR

1. Copy the ADR format block from the section above
2. Assign the next sequential number (e.g., `ADR-10`, `ADR-11`, …)
3. Paste it at the end of this document, before the "Creating a New ADR" section
4. Fill in the sections with concrete information
5. Add it as a new entry in the "Existing ADRs" section above
