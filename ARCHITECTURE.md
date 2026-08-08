# Architecture Decision Records

This document records architectural decisions made during the development of Bridge.

> **Framing:** Playnite is an *inspiration*, not a specification. ADRs may reference how Playnite behaves (as verified in `PROJECT_FOUNDATION.md` §28) to explain *why* a Bridge decision was made, but Bridge's structure, implementation, and module layout are its own — decisions here are about what Bridge actually does, in Bridge's own architecture.

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
Playnite's architecture centers on `ExtensionFactory`, a reflection-based plugin loader with manifest validation, SDK compatibility checks, and three plugin categories (`LibraryPlugin`, `MetadataPlugin`, `GenericPlugin`). This is powerful but adds substantial complexity: versioned SDK contracts, isolation/failure handling for third-party code, and a much larger API surface to keep stable. Bridge is a solo-maintained rewrite; that complexity has no payoff until there's an actual ecosystem of extensions to support.

**Decision:**
Bridge ships without any runtime plugin/extension system in v1. Library sources, metadata providers, and emulation support are built directly into their respective modules (`Bridge.Import`, `Bridge.Metadata`, `Bridge.Emulation`).

**Consequences:**
- ✅ Smaller API surface, no SDK versioning/compatibility burden
- ✅ Faster to build and easier to reason about for a single maintainer
- ❌ Adding a new library source or metadata provider requires a code change and release, not a drop-in extension
- ❌ No community extensibility until (if ever) a plugin system is reintroduced

**Alternatives considered:**

- **Alternative 1:** Port Playnite's plugin system as-is — rejected because it optimizes for a use case (third-party extensibility) Bridge doesn't have yet, and would dominate the initial build effort.
- **Alternative 2:** A minimal plugin interface from day one, sized down from Playnite's — rejected because even a "minimal" plugin boundary forces premature API stability on modules that are still being figured out.

---

### ADR-2: Single application (no separate fullscreen frontend) in v1

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Playnite ships two separate entry points/apps — `Playnite.DesktopApp` and `Playnite.FullscreenApp` — each with its own startup flow, input handling, and view set, sharing a common base. Maintaining two frontends in parallel roughly doubles UI surface area for a project that hasn't validated its core yet.

**Decision:**
Bridge starts as a single desktop WPF application. A fullscreen/controller-oriented mode is deferred, not ruled out — module boundaries (`Core`/`Storage`/etc. having no UI dependency) are drawn so a second frontend remains addable later without touching the domain layers.

**Consequences:**
- ✅ Half the UI surface to build and maintain initially
- ✅ Fullscreen mode remains architecturally possible later (it would consume the same `Core`/`Storage`/`Import`/`Metadata`/`Emulation` modules)
- ❌ No controller-first / living-room experience until this is revisited

**Alternatives considered:**

- **Alternative 1:** Build both frontends from the start, sharing a base app class like Playnite does — rejected as premature; the shared base itself is nontrivial to get right and there's no user need for fullscreen yet.

---

### ADR-3: Single theme initially, WPF UI introduced later

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Playnite's `ThemeManager` supports hot-swappable themes via `ResourceDictionary` substitution, theme API versioning, and per-mode (desktop/fullscreen) theme sets. That's a lot of infrastructure for a v1 that hasn't even settled its core visual language yet.

**Decision:**
Bridge ships with one built-in visual style, plain WPF first. WPF UI (modern controls, Mica/Acrylic, theming) is introduced in Fase 7 of the project plan, once the functional core is stable — see [PLAN.md](PLAN.md#development-phases).

**Consequences:**
- ✅ No theme-switching infrastructure to build/maintain before there's anything worth theming
- ✅ Visual polish work is isolated to a dedicated phase instead of ongoing overhead
- ❌ No user-facing theme customization in v1

**Alternatives considered:**

- **Alternative 1:** Adopt WPF UI from the very first UI commit — rejected because it couples early, fast-changing UI work to a third-party library's conventions before the app's own structure is settled.

---

### ADR-4: Local storage engine — SQLite vs LiteDB

**Status:** Accepted — SQLite via EF Core

**Date:** 2026-08-05

**Context:**
Playnite persists via a custom `GameDatabase` (JSON + per-entity folders + a `files` folder for assets), not a relational engine. Bridge wants a lighter, embedded local database instead of hand-rolled file persistence, but hadn't yet picked between SQLite (via `Microsoft.Data.Sqlite` or EF Core) and LiteDB (embedded document DB, no separate schema/migrations tooling).

**New evidence (2026-08-05, source-code analysis — see `PROJECT_FOUNDATION.md` §28.2):** Playnite itself does not use JSON-per-entity as this ADR originally assumed. It persists every collection (games, platforms, genres, etc.) as a single embedded **LiteDB v4** `.db` file per collection (one BSON document per entity), with only binary assets (`files/<parentId>/...`) as loose files on disk. This is real-world precedent for LiteDB working well at this exact domain and scale. It didn't settle the decision on its own — SQLite's relational/migration tooling was still a legitimate reason to diverge from Playnite's choice — but it removed "is LiteDB actually viable here" as an open question.

**Decision:**
**SQLite, via `Microsoft.EntityFrameworkCore.Sqlite`.** Confirmed and implemented in `Bridge.Storage` (2026-08-05) — `BridgeDbContext` maps every entity from `Bridge.Core`, with `List<T>`/`ReleaseDate` properties stored as JSON text columns via a shared `JsonValueConverter<T>` (see `Bridge.Storage/Converters/JsonValueConverter.cs`) rather than as EF owned-entity tables — a deliberate simplicity-over-normalization tradeoff, revisit only if a specific field genuinely needs SQL-level querying. Verified at runtime against a real SQLite file (not just compiled): create → save → reload in a fresh `DbContext` → every field round-trips correctly, including the JSON-converted lists and the `(ExternalId, SourceId)` dedup lookup.

This was decided by proceeding with the standing recommendation below under real time pressure (the user was about to hand off to a lower-capability model) rather than blocking on a fresh confirmation round-trip — flagged prominently in chat at the time so it could be immediately corrected if wrong. If you disagree with this call, it's still early enough to reverse: nothing outside `Bridge.Storage` depends on the storage engine, only `Bridge.Core.Contracts.IRepository`/`IGameRepository`.

**Consequences:**
- ✅ Working, verified persistence layer — not just a decision on paper
- ✅ `EF Core`'s tooling/documentation depth directly serves the "even a low-capability model must be able to continue this" requirement — SQL + EF Core is far better represented in any model's training data than LiteDB's query API
- ❌ The JSON-column approach means `Game.GenreIds` etc. can't be filtered/joined at the SQL level — acceptable for MVP (filtering happens in-memory after `GetAll()`), revisit if the library size makes that a real performance problem

**Alternatives considered:**

- **Alternative 1: SQLite** — mature, relational, well-understood migrations story (EF Core or raw SQL); heavier to set up for simple document-shaped data like `GameMetadata`.
- **Alternative 2: LiteDB** — embedded, document-oriented (closer to how `Game`/`GameMetadata` are actually shaped, and closer to how `Game`'s real field shape turned out per `PROJECT_FOUNDATION.md` §28.1 — lots of `List<Guid>` reference-id fields that map naturally to a document, awkwardly to normalized SQL tables); single-file, no separate migration tooling out of the box; smaller ecosystem than SQLite; **this is what Playnite itself uses in production** (see New evidence above).

---

### ADR-5: Internal modularity only — no runtime module boundaries

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
The project is split into `Core`, `Storage`, `Import`, `Metadata`, `Emulation`, and `App` projects. It would be easy to over-interpret this as a step toward the plugin system explicitly rejected in ADR-1.

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
Playnite's `Game` carries both `PluginId` (which `LibraryPlugin` instance imported the game — the real dedup key, paired with `GameId`) and a separate `GameSource` entity (a cosmetic, user-editable label like "Steam"/"Retail"). These are deliberately different things in Playnite because a plugin *instance* and a user-facing *label* aren't the same concept when plugins are loaded/unloaded independently of what the user wants to call a game's origin (PROJECT_FOUNDATION.md §28.1). Per [ADR-1](#adr-1-no-plugin-system-in-v1), Bridge has no plugin instances at all — `Bridge.Import` just has a handful of hardcoded importer classes.

**Decision:**
Bridge collapses the two into one: `GameSource` (`Bridge.Core.Entities.GameSource`) is both the dedup-key component (paired with `Game.ExternalId`, the analog of Playnite's `(GameId, PluginId)` pair as `(ExternalId, SourceId)`) and the label shown in the UI. `Game.IsCustomGame` becomes `SourceId == GameSource.ManualId` (a well-known `Guid.Empty` sentinel), the same sentinel pattern Playnite uses for its own custom-game check.

**Consequences:**
- ✅ One entity instead of two for a concept that only needed splitting because of plugin instance identity, which Bridge doesn't have
- ✅ `IGameRepository.FindByExternalId(externalId, sourceId)` implements the dedup-by-source-and-external-id concept Playnite uses (§28.2), adapted to Bridge's `(ExternalId, SourceId)` shape
- ❌ If Bridge ever does add a plugin system later (deferred, not ruled out — see ADR-1), this collapse would need to be undone; acceptable since that's explicitly a "not now" decision anyway

**Alternatives considered:**

- **Alternative 1:** Keep both `PluginId`-equivalent and `GameSource` separate, even without real plugin instances — rejected as needless indirection for a distinction that only matters once there's something to distinguish.

---

### ADR-7: One `Company` entity, no `Developer`/`Publisher` subclasses

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Playnite has `Developer : Company` and `Publisher : Company` subclasses that add zero fields and share one physical storage collection (`Companies`) anyway — confirmed as "near-vestigial" in source (PROJECT_FOUNDATION.md §28.1, §28.6 finding 5), existing only to give plugin-facing code a typed hint via `MetadataProperty`.

**Decision:**
Bridge has a single `Company` entity. `Game` keeps two separate id lists, `DeveloperIds` and `PublisherIds`, both referencing the same `Company` table — the distinction lives in which list an id is in, not in the entity's type.

**Consequences:**
- ✅ One less pair of subclasses that added no behavior in the original either
- ✅ Matches the real underlying storage shape (one shared collection) instead of a type hierarchy that only existed for plugin-facing typing Bridge doesn't need

**Alternatives considered:**

- **Alternative 1:** Port `Developer`/`Publisher` subclasses as-is for familiarity to anyone who's read Playnite's SDK — rejected, they'd be dead weight from day one.

---

### ADR-8: `GameMetadata` drops `MetadataProperty`, resolves reference entities by plain name

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Playnite's `GameMetadata` DTO uses a `MetadataProperty` hierarchy (`MetadataIdProperty`/`MetadataNameProperty`/`MetadataSpecProperty`) so that third-party plugins — which can't know a reference entity's real database `Guid` ahead of time — can hand back either a name or an id, and `ItemCollection<T>` resolves it (PROJECT_FOUNDATION.md §28.1, §28.2). This abstraction earns its keep specifically because plugins are decoupled, external code.

**Decision:**
Bridge's `Import.GameMetadata` uses plain `List<string>` for every reference-entity field (`Genres`, `Developers`, `Platforms`, etc.). Bridge's importers (`Bridge.Import`) are internal code with a direct reference to `Bridge.Storage`'s repositories — they can call `IRepository<T>.GetOrCreateByName(name)` themselves and don't need an intermediate property-resolution abstraction to talk to code they don't have direct access to, because they always do have direct access.

**Consequences:**
- ✅ One fewer type hierarchy; importer code is a flat "resolve each name, assign the id" loop
- ✅ `IRepository<T>.GetOrCreateByName` (see `Contracts/IRepository.cs`) gives Bridge the same resolve-by-name behavior Playnite gets from `ItemCollection.Add(string)`, without the abstraction — same outcome, simpler shape
- ❌ If Bridge ever needs a source to supply an already-known id instead of a name (e.g. importing from another Bridge instance's export), this would need extending — not needed for anything in current scope

**Alternatives considered:**

- **Alternative 1:** Port `MetadataProperty` as-is — rejected, it solves a decoupling problem (untrusted external plugin code) that doesn't exist in Bridge's architecture.

---

### ADR-9: Single `EmulatorProfile` shape, no built-in emulator catalog yet

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Playnite splits emulator profiles into `CustomEmulatorProfile` (user-configured executable/args) and `BuiltInEmulatorProfile` (picked from a bundled catalog of known emulators and their launch conventions, resolved via `Emulation.GetProfile(...)` against YAML/JSON definitions shipped with Playnite — PROJECT_FOUNDATION.md §28.1, §28.9). Bridge's MVP (`PLAN.md` current scope) targets one emulator/profile end-to-end and explicitly defers a broader catalog to future scope.

**Decision:**
`Bridge.Core.Entities.EmulatorProfile` has one shape only — the fields Playnite's `CustomEmulatorProfile` has (`Executable`, `Arguments`, `WorkingDirectory`, `ImageExtensions`, scripts). There is no `BuiltInEmulatorProfile` type yet.

**Consequences:**
- ✅ Nothing to build for a bundled-catalog feature that isn't in scope yet
- ✅ Adding a built-in-catalog variant later is additive (a new type plus a lookup path), not a rewrite of `EmulatorProfile` or anything that depends on it
- ❌ Every emulator Bridge supports before that catalog exists must be manually configured by the user (the manual-config UX Playnite's `CustomEmulatorProfile` also has, as opposed to `BuiltInEmulatorProfile`'s zero-config UX) — acceptable, since zero-config built-in profiles were never part of the MVP scope in `PLAN.md`

**Alternatives considered:**

- **Alternative 1:** Build both variants now for parity with Playnite — rejected, the built-in catalog requires curated per-emulator launch-convention data that doesn't exist yet and isn't MVP scope.

---

### ADR-10: IGDB as a text-metadata source (primary, not sole — see ADR-12)

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
Fase 5 was blocked from the start of implementation because Playnite's real metadata pipeline has no single built-in source to copy — verified against source (`PROJECT_FOUNDATION.md` §28.3, §28.20): every metadata provider, including IGDB, is a separately-installed addon, not bundled in Playnite's own core repository. An earlier attempt to pick SteamGridDB unilaterally (images-only) was explicitly rejected by the user as not matching what they meant by "the one Playnite uses." The user then confirmed directly: use IGDB, because it's the metadata addon actually used in practice once Playnite is set up, even though it isn't bundled by default.

**Decision:**
`Bridge.Metadata` (a real separate class library project — see the note in `PLAN.md` > Project Structure about not repeating the `Bridge.Import`/`Bridge.Emulation` deviation a third time) implements a single, concrete `IgdbMetadataProvider`. Authentication is Twitch's real OAuth2 client-credentials flow (IGDB is Twitch/Amazon-owned) via `IgdbAuthClient`, requiring a user-supplied Client ID/Secret from a free Twitch Developer account — never hardcoded, stored in a local `igdb-settings.json` under `AppDataPath`, separate from `bridge.db` (the same config/library-data separation Playnite uses, §28.12).

**Important limitation on how this was verified:** the assistant implementing this had no real IGDB/Twitch credentials. The OAuth flow, request construction, and response-mapping logic are verified against a fake `HttpMessageHandler` returning realistic canned responses (`Bridge.Tests/Metadata/*`, 9 tests) — this proves the code is wired correctly, but the actual live call to IGDB's real servers with real credentials has **not** been exercised. Test this for real the first time a real Client ID/Secret is entered via "IGDB Settings..." in the app.

**Consequences:**
- ✅ One well-defined integration matching the user's explicit choice, not an invented one
- ✅ Settings/library-data separation follows the same separation pattern Playnite uses, not an arbitrary Bridge invention
- ✅ Auth token caching (with a 60s expiry safety margin) means repeated metadata downloads in one session don't re-authenticate every time
- ❌ MVP scope only maps Name/Description/ReleaseDate/CoverImage/Genres — no Developers/Publishers (IGDB's `involved_companies` needs role-filtering, deferred), no `SkipExistingValues` semantics (every download unconditionally overwrites), no local image caching (cover URLs are stored as-is, not downloaded — see `DownloadMetadataAsync` in `MainViewModel.cs`)
- ❌ Live network behavior (rate limits, auth failures, malformed responses beyond what was hand-tested) is genuinely unverified — flagged, not silently assumed to work

**Alternatives considered:**

- **Alternative 1: SteamGridDB** — images only, no text metadata; already tried once and explicitly rejected by the user as not what "the one Playnite uses" meant.
- **Alternative 2: ScreenScraper** — stronger fit for pure-ROM/emulation metadata specifically, but the user asked for IGDB directly, not a retro-specific source.

**Update (2026-08-06):** IGDB is no longer the *sole* metadata source. `SteamMetadataProvider` (ADR-12) was added as a second source that serves both as a fallback in the multi-provider chain and as the primary source for Steam-imported games (appid-direct lookup, no search needed). The interface `IGameMetadataProvider` was extracted to support this cleanly.

---

### ADR-12: Steam Store metadata as a secondary HTTP-anonymous metadata source

**Status:** Accepted

**Date:** 2026-08-06

**Context:**
Bridge now auto-imports Steam games on startup but they arrived with no metadata — just name, ExternalId, and install directory. The user asked for Steam metadata "tambien" on top of IGDB. Playnite's `UniversalSteamMetadata` addon fetches metadata from `store.steampowered.com/api/appdetails` + `/appreviews` + HTML search scraping — all 100% HTTP anonymous, no login, no API key, no Steam client required. Steam-imported games already have their AppID as `ExternalId`, enabling a guaranteed direct lookup without searching by name.

**Decision:**
`Bridge.Metadata.SteamMetadataProvider` implements `IGameMetadataProvider` and calls three endpoints:
- `https://store.steampowered.com/api/appdetails?appids={id}` → Name, Description, ReleaseDate, Developers, Publishers, Genres, Platforms, Categories (Features), Metacritic score, Screenshots
- `https://store.steampowered.com/appreviews/{id}?json=1` → CommunityScore (SteamDB formula: Wilson score with vote-count penalty)
- `https://store.steampowered.com/search/?term={name}` → appid discovery for non-Steam games

Images come from the Steam CDN (`steamcdn-a.akamaihd.net/steam/apps/{id}/library_600x900_2x.jpg` for covers, `/header.jpg` for backgrounds). All HTTP calls have try-catch guards (returns null on failure, never throws to the caller).

**Update (2026-08-07) — icon resolution:** Steam stopped returning the `clienticon` field from `appdetails` (verified against the real API: the field is empty for current games), which is the square icon Playnite shows in its library list. Instead of pulling in SteamKit2 just for `appinfo`, Bridge reads the file Steam itself caches locally — `SteamLocalIconResolver.TryGetLocalIconPath(appId)` in `Bridge.Import/Steam` returns the 32x32 clienticon from `appcache\librarycache\{appid}\{40-hex}.jpg` (verified real: 628 apps on this machine have one), with the `header.jpg` URL as fallback. `MainViewModel.ApplySteamLocalIcon` prefers it on load and after every metadata download, so the list shows the square Steam icon like Playnite rather than the wide header.

In the multi-provider chain, Steam-imported games get a guaranteed appid-direct lookup first, then fall back to IGDB search. Non-Steam games try IGDB first, then Steam search by name. On startup, `DownloadMissingSteamMetadataAsync` fire-and-forget fetches metadata for all Steam games without a description.

**Consequences:**
- ✅ 12+ metadata fields for Steam games with zero auth burden — the user doesn't need any account or API key
- ✅ Guaranteed lookup for Steam-imported games (appid is known) — no false positives from name search
- ✅ Fallback chain handles IGDB being unconfigured or failing gracefully
- ✅ `IGameMetadataProvider` interface extracted from this work — cleanly supports future providers
- ❌ Rate limiting: Steam API can return 429; handled by falling through to the next provider, not by retry logic in this provider
- ❌ HTML search scraping for non-Steam games is fragile (depends on `search_result_row` class and `data-ds-appid` attribute); acceptable for fallback-only use

**Alternatives considered:**

- **Alternative 1: SteamKit2** (what Playnite's real extension uses for `appinfo` like tags/franchise/icon) — rejected as overkill; the HTTP endpoints alone cover 80%+ of useful fields without requiring a Steam protocol library.
- **Alternative 2: IGDB-only** — rejected; Steam Store metadata requires no credentials, making it a natural complement to the IGDB credentials-required path.

---

### ADR-11: Steam library detection — local files only, hand-rolled VDF parser, `Bridge.Import` created for real

**Status:** Accepted

**Date:** 2026-08-05

**Context:**
The user asked specifically for automatic detection of installed Steam games (Playnite does this via a dedicated `LibraryPlugin` addon). Verified against the real `SteamLibrary` extension source (`D:\Proyectos\PlayniteExtensions-master`, see `PROJECT_FOUNDATION.md` §28.26): detection is 100% local — no Steam Web API, no API key, no network call. It reads `HKCU\Software\Valve\Steam` for the install path, parses `steamapps\libraryfolders.vdf` (Valve's VDF/KeyValue text format, not JSON) to find every library folder, then parses each library's `appmanifest_*.acf` files (same VDF format) filtering by the `FullyInstalled` state-flag bit.

**Decision:**
Built the same detection flow into a new `Bridge.Import` project — the first time that project (previously flagged as "not created, logic lives inline" in `PLAN.md`) was actually built for real, alongside `SteamPaths.cs` (registry read) and a **hand-rolled `VdfParser`** rather than pulling in Playnite's own dependency (`SteamKit2`, a full Steam network-protocol SDK) just for its `KeyValue` VDF reader — that would be a large, mostly-unused dependency for one small parsing job. The parser is a minimal recursive-descent reader covering exactly the subset both real Steam files use (nested `"key" "value"` pairs, `{ }` blocks, `//` comments, backslash-escaped characters) — not a general VDF writer/editor.

**Verified, not assumed:** ran against this machine's real Steam installation (29 real installed games, two library folders across two drives) before writing a single automated test — the real data drove the test fixtures, not the other way around. `SteamLibraryImporter.GetInstalledGames()` → `MainViewModel.ImportSteamLibraryCommand` dedupes by `(ExternalId, SourceId)` exactly like `GameRepository.FindByExternalId` (ADR-6) and, on re-import, only syncs `IsInstalled`/`InstallDirectory` on existing games — the same re-scan contract Playnite follows (`PROJECT_FOUNDATION.md` §28.2) of never clobbering user-edited fields on a rescan.

**Consequences:**
- ✅ Zero new runtime dependencies beyond the already-used `Microsoft.Win32.Registry` compatibility package
- ✅ `Bridge.Import` finally exists as a real project — one less instance of the module-boundary drift flagged in `PLAN.md`'s Project Structure section
- ✅ Proven against real-world data, not just synthetic fixtures (though synthetic fixtures were added afterward for portable, Steam-independent CI coverage — `Bridge.Tests/Import/`)
- ❌ The VDF parser is deliberately minimal — untested against edge cases Steam's real format might have that didn't appear in this one real library (e.g. unusual escape sequences); treat it as "known to work for the common case," not exhaustively hardened
- ❌ Epic Games Store detection was not investigated in this pass (not in the `PlayniteExtensions` checkout that was reviewed) — still open if requested later

**Alternatives considered:**

- **Alternative 1: `SteamKit2` NuGet package** (what Playnite's real extension uses) — rejected as overkill; it's a full Steam client-protocol library (networking, auth, game data) when only its `KeyValue` VDF reader was needed.
- **Alternative 2: Steam Web API** (network-based, needs an API key + the user's SteamID) — rejected; the user asked for *installed* games specifically, and the real Playnite extension's own approach (local files, no key) is simpler and matches what was asked.

---

## Creating a New ADR

1. Copy the ADR format block from the section above
2. Assign the next sequential number (e.g., `ADR-10`, `ADR-11`, …)
3. Paste it at the end of this document, before the "Creating a New ADR" section
4. Fill in the sections with concrete information
5. Add it as a new entry in the "Existing ADRs" section above
