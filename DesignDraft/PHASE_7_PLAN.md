# Phase 7 UI Rebuild Plan

Last updated: 2026-05-30

## Purpose

Phase 7 is the XFVerse WPF desktop UI rebuild phase. Its goal is to modernize and unify the existing WPF application without changing core media semantics:

- unify visual tokens, ResourceDictionary usage, shell, windows, controls, dialogs, menus, cards and states;
- align each page with the current `DesignDraft/page-spec` intent where it still matches product decisions;
- keep existing business behavior intact unless a phase explicitly approves a minimal UI-facing adjustment;
- preserve media-library safety semantics around hide, delete-record, restore, cache cleanup, playback sources and grouped placeholders.

This document is a living maintenance plan for later Codex / GPT handoff. It does not replace page specs, stage logs or Known Issues.

## Required Reading Before Each Phase 7 Substage

- `AGENTS.md`
- `DesignDraft/PHASE_7_PLAN.md`
- `DesignDraft/PHASE_7_STAGE_LOG.md`
- `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- `DesignDraft/UI-REBUILD-README.md`
- `DesignDraft/PHASE_6_COVERAGE_MATRIX.md`
- the relevant `DesignDraft/page-spec/*.md`
- relevant visual drafts, screenshots and mockups under `DesignDraft`

Design drafts are old visual references. They are useful for atmosphere, spacing, density, hierarchy and layout proportions, but they are not final product specifications. If a draft conflicts with current page specs, Phase 7 documents or explicit user decisions, use the written docs and user decisions.

## Git And Verification Rules

- Codex must not commit or push unless the user explicitly asks.
- Do not run full `git diff` repeatedly. Prefer final checks: changed-file scope, diff scale, migrations diff and `git diff --check`.
- Do not execute `database update`.
- Do not add migrations unless the user explicitly approves.
- For implementation phases, run `dotnet build MediaLibrary.sln`.
- For docs-only phases, build is optional; if skipped, record that only Markdown changed.
- Every Phase 7 substage must update this plan, the stage log and Known Issues if the phase changes status, scope, decisions or risks.

## Confirmed Product Decisions

### Phase 7.1

- The theme icon shows the current active theme.
- Account username and avatar may be hard-coded or local placeholders.
- Home may use minimal navigation parameter / state passing to open the AI recommendation tab directly.
- Home statistics trend can reuse watch-discovery / watch-statistics tab data sources.
- User profile dialog is local placeholder and visual work only; it does not introduce a real account system.
- Confirmation dialogs support normal, warning and danger variants.
- "移出媒体库" is not delete.
- "删除记录" is not deletion of media files.
- "缓存清理" is not deletion of media sources.

### Phase 7.2

- Media-library result summary keeps "有播放源" and "无播放源"; this is a user-confirmed product wording and must not be reclassified as a bug.
- Batch entry is visible only when the sidebar is collapsed.
- Layout memory is required.
- Layout memory must not add database fields or migrations; prefer App-layer local preference storage.
- Grouped placeholders currently pass user acceptance and can enter detail pages. They are not a Phase 7.2 blocker.
- If grouped placeholder detail experience needs deeper work, handle it in Phase 7.3.
- Removed-library UI remains an in-page overlay. Do not convert it to a right-side drawer / side panel for Phase 7.2.
- Full manual "aggregate as season" dialog UI is deferred; 7.2 only organizes entry and semantics.
- Missing-poster placeholder and current logo assets live under `src/MediaLibrary.App/Assets` and are Phase 7.2 final-assets scope.
- Local Light / Dark / card polish is deferred to later phases or 7.8.

### Upstream Diff Interpretation

- Cumulative diff against upstream may include 7.0, 7.1 and earlier authorized work.
- Do not classify PlayerWindow, MainWindow or Core-service UI-related changes as 7.2 scope violations solely from upstream diff.
- The user confirmed those UI changes were authorized.
- Scope decisions should use the actual phase report, changed-file list and the current phase boundary.

### P61-01

- The player currently has a visible "本地缓存" menu item.
- Final UI must not expose a video-cache entry.
- `VideoCacheService` / `IVideoCacheService` backend capability must remain.
- Removing the player-menu "本地缓存" entry belongs to Phase 7.5 player menu rebuild.
- Do not delete backend video-cache services early.

## Current Phase Overview

| Phase | Status | Summary |
|---|---|---|
| 7.0 | Complete | Global token / ResourceDictionary, basic controls, MainWindow custom title bar, fixed-dark PlayerWindow and OnlineSubtitleSearchWindow, regression closeout. |
| 7.1 | Complete | Shell, navigation, brand, account menu, theme entry, HomePage rebuild, profile dialog, confirmation dialog variants, regression closeout. |
| 7.2 Plan / Audit | Complete | Media-library scope planned and audited before implementation. |
| 7.2a | Complete | Toolbar, submitted search, filters, sorting and result summary. |
| 7.2b | Complete | Poster/list layout switch, layout memory and media-item visual baseline. |
| 7.2c | Complete | Batch-entry rule, batch toolbar, selection states and special-item hints. |
| 7.2d | Complete | Removed-library in-page overlay, Movie / TV grouping, empty state, restore / detail / delete-record actions. |
| 7.2e | Executed | Regression audit ran; some conclusions needed correction according to user-confirmed decisions. |
| 7.2-final-assets | Complete | Missing-poster placeholder and logo asset hookup were handled before the 7.3 detail phase; later poster-card work is tracked as 7.2 post-closeout follow-up polish. |
| Phase 7 documentation sync | Complete | Created and maintains `PHASE_7_PLAN.md`, `PHASE_7_STAGE_LOG.md`, `PHASE_7_KNOWN_ISSUES.md`. |
| 7.3 Plan | Complete | Detail-page phase plan created before implementation. |
| 7.3a | Complete | Shared detail back affordance and origin / fallback behavior. |
| 7.3b | Complete | Movie detail visual baseline and in-page Movie correction overlay. |
| 7.3c | Complete | Series / Season detail visual baseline and in-page Season correction overlay. |
| 7.3d | Complete | Episode detail visual baseline and in-page Episode correction overlay. |
| 7.3e | Complete | Unified cross-page correction dialog shell for Movie, Season and Episode detail correction surfaces. |
| 7.3 follow-up polish | Complete | Movie rating / player polish, Series / Season / Episode layout alignment, cached detail backdrop performance fix, placeholder backdrop fallback and layered shared liquid-glass baseline. |
| 7.3f | Next | Detail regression closeout and 7.2 media-library smoke regression. |

## Later Phase Plan

### 7.3 Details

Status: 7.3a, 7.3b, 7.3c, 7.3d and 7.3e completed. Detailed phase plan lives in `DesignDraft/PHASE_7_3_DETAILS_PLAN.md`.

Completed implementation slices:

- 7.3a shared detail foundation;
- 7.3b Movie detail visual baseline;
- 7.3c Series / Season detail visual baseline;
- 7.3d Episode detail visual baseline;
- 7.3e unified correction dialog shell.
- follow-up polish: compact detail-route title bar, Movie layout feedback closeout, exact Series / Season / Episode alignment against the accepted Movie baseline, cached poster-derived backdrop, async poster effects and shared liquid-glass baseline.

Stable comparison rule:

- once the Movie detail baseline is unchanged and accepted, reuse it as the comparison reference for Series / Season / Episode work; do not repeat runtime screenshots unless a visual blocker, a changed Movie baseline or an explicit verification request makes a new capture necessary.

Scope:
- MovieDetail, SeriesOverview, TvSeasonDetail and EpisodeDetail visual rebuild;
- unified back affordance and source-state handling;
- no-source, metadata-only, unknown and grouped-placeholder detail experiences;
- correction flow alignment, preferably dialog-based per page specs.

Not in scope:
- player menu rebuild;
- online subtitle UX beyond detail-entry compatibility;
- broad `NavigationStateService` rewrite without a scoped plan;
- new database fields unless separately approved.

7.3a explicitly owns only the shared detail back affordance and minimal in-memory origin/fallback behavior. It did not change detail page data fields, source business operations, correction services, player menus, scan rules, recommendation logic, database schema or migrations.

7.3b owns only the Movie detail page visual baseline and existing Movie correction entry presentation. It does not rebuild Series / Season / Episode details, does not add subtitle management to details, and does not change source, scan, player, recommendation, Core service, database schema or migration semantics.

7.3c owns only the Series and Season detail visual baselines plus Chinese UI copy around existing Season correction controls. It does not rebuild Episode detail, does not change TV query or correction services, does not add subtitle / delete / move-out detail actions, and does not touch player, scan, recommendation, Core service, database schema or migration semantics.

7.3d owns only the Episode detail visual baseline, source-list presentation and in-page correction overlay entry using existing Episode detail commands. It does not introduce subtitle management, delete-record / move-out detail actions, scan or AI threshold changes, source-service semantic changes, player work, Core query changes, database schema changes or migrations.

7.3e owns only the shared correction dialog shell for existing Movie, Season and Episode correction overlays. It does not change correction ViewModels, candidate search, AI / TMDB matching, source split semantics, player / subtitle work, Core services, database schema or migrations.

### 7.4 Discovery

Scope:
- movie search, ranking and AI recommendation tab visual alignment;
- Movie / TV card consistency;
- source labels and external candidate states;
- recommendation preference UI if already supported by current services.

Not in scope:
- changing recommendation algorithms;
- expanding TV recommendation or watch-insight semantics;
- account system.

### 7.5 Player

Must start with Plan / Audit.

Scope:
- player shell, controls, menus, subtitle / audio / source menus;
- OnlineSubtitleSearchWindow visual alignment;
- P61-01: remove visible "本地缓存" menu entry while preserving video-cache backend services;
- fixed dark visual baseline and mpv / HwndHost / Popup / fullscreen regression.

Not in scope:
- deleting real media files;
- deleting WebDAV files;
- removing backend cache services;
- subtitle editing / OCR unless separately approved.

### 7.6 Settings / Scan / Cache

Scope:
- settings visual structure and sensitive-field handling;
- scan tasks and safe log display;
- software cache and subtitle cache management UI;
- cache cleanup wording that does not imply deleting media sources.

Not in scope:
- fake scan progress if the service cannot provide it;
- new scanning business behavior;
- video-cache UI resurrection.

### 7.7 History / Favorites / Watch Insights

Scope:
- watch history, favorites and watch insights final visual alignment;
- Movie / Episode history and Movie / Season collection display;
- empty, loading and date-filter states;
- chart and calendar polish.

Not in scope:
- moving TV into Movie-only recommendation / persona insight semantics;
- changing watch-statistics business rules without a separate plan.

### 7.8 Global Visual Consistency / Polish / Regression

Must start with Plan / Audit.

Scope:
- global Light / Dark readability and consistency;
- spacing, cards, chips, menus, dialogs and control states;
- audit and complete the shared liquid-glass visual baseline already introduced for Home, Library and current detail pages; extend the same ResourceDictionary-driven styles to later rebuilt pages instead of creating page-local variants;
- keep the liquid-glass treatment restrained and resource-driven: use cached diffuse backgrounds, subtle transparency, soft highlights, low-contrast borders and limited depth cues rather than decorative excess, and verify that text readability and interaction-state clarity remain primary;
- preserve distinct nested-card levels in both Light and Dark themes with differentiated opacity, edge highlights and shadow depth; keep the blur-like appearance static and cached rather than adding live backdrop blur;
- loading / empty / error / disabled / no-source consistency;
- performance and large-list scroll checks;
- final cross-page regression.

Not in scope:
- introducing a new UI framework;
- new product features;
- database schema changes.

## Maintenance Rule

Every future Phase 7 substage must update:

- `DesignDraft/PHASE_7_PLAN.md` if scope, sequence or confirmed decisions change;
- `DesignDraft/PHASE_7_STAGE_LOG.md` with completed work, validation and explicit non-goals;
- `DesignDraft/PHASE_7_KNOWN_ISSUES.md` with blockers, deferred items, risks, noise and user-confirmed non-issues.

Git commit and push remain user-managed unless explicitly requested.
