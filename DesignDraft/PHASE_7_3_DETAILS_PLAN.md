# Phase 7.3 Details Plan

Last updated: 2026-05-30

## Purpose

Phase 7.3 brings the four detail surfaces to the current Phase 7 product and visual baseline:

- `MovieDetailPage`
- `SeriesOverviewPage`
- `TvSeasonDetailPage`
- `EpisodeDetailPage`

The phase covers detail-page navigation, state presentation, source handling and correction-flow UI alignment. It does not change scan identification rules, playback engine behavior, recommendation algorithms, database schema or destructive media semantics.

## Source Of Truth

Required reading for this phase:

- `AGENTS.md`
- `DesignDraft/PHASE_7_PLAN.md`
- `DesignDraft/PHASE_7_STAGE_LOG.md`
- `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- `DesignDraft/PHASE_6_COVERAGE_MATRIX.md`
- `DesignDraft/page-spec/global-shell.md`
- `DesignDraft/page-spec/movie-detail-page.md`
- `DesignDraft/page-spec/tv-detail-page.md`
- `DesignDraft/page-spec/episode-detail-page.md`
- `DesignDraft/page-spec/correction-flow.md`
- `DesignDraft/page-spec/media-library-special-items.md`
- detail-page screenshots and drafts under `DesignDraft/screenshots`

Design drafts are old visual references. Use them for poster proportion, information hierarchy, source-list density and correction-dialog atmosphere only. If a draft conflicts with page specs, Phase 7 maintenance docs or user-confirmed decisions, follow the written specs and user decisions.

Historical `docs/ui-redesign/*` files describe an earlier initial redesign stage and are not the current Phase 7 stage boundary.

## Confirmed Boundaries

In scope:

- unified detail-page back affordance inside the content area;
- minimal App-layer detail origin stack / fallback behavior;
- Movie / Series / Season / Episode detail visual rebuilds;
- no-source, no-default-source, metadata-only, unknown and grouped-placeholder detail states;
- unified correction dialog shell and page-specific hookups using existing services.

Out of scope:

- player menu rebuild;
- P61-01 local cache menu removal;
- online subtitle UX beyond keeping details compatible with player entry;
- scan recognition thresholds and AI matching rules;
- recommendation algorithm changes;
- database fields, migrations or `database update`;
- delete-record / move-out actions on detail pages;
- deleting local files, WebDAV files or real media sources.

## Stage Sequence

### 7.3a - Shared Detail Foundation

- Add a unified icon back button style for hidden detail routes.
- Add a minimal in-memory detail origin stack in the App navigation layer.
- Record activated page requests from `MainWindowViewModel` so details can return to the real previous detail or visible page when available.
- Add fallback behavior:
  - Movie detail -> Library
  - Series detail -> Library
  - Season detail -> Series when possible, else Library
  - Episode detail -> Season when possible, else Library
- Add the unified back command to Movie, Series, Season and Episode detail ViewModels.
- Do not change page content fields, source operations, correction logic or business services.

### 7.3b - Movie Detail

Status: completed 2026-05-30.

- Rebuild Movie detail visual layout around poster, hero information, ratings, tags, overview, actions and source list.
- Keep no-source, no-default-source, metadata-only, external-candidate and removed-library states clear.
- Move correction from persistent tab / panel into the unified correction dialog entry.
- Keep subtitle UX out of detail pages.

Implementation note: the 7.3b Movie correction surface is an in-page overlay using existing Movie detail correction commands. The fully shared correction-dialog shell across Movie / Season / Episode remains 7.3e scope.

### 7.3c - Series And Season Details

Status: completed 2026-05-30.

- Rebuild Series and Season detail visual layout.
- Keep recognized, unrecognized, metadata-only, no-source and grouped-placeholder states distinguishable.
- Keep Season and Episode lists usable without changing TV query semantics.
- Replace old text return affordances with the unified detail return pattern.

Implementation note: 7.3c keeps the existing TV query, metadata hydration, Season collection actions, episode play/detail commands and season-correction services. It only updates Series / Season detail presentation and Chinese UI copy around the current Season correction overlay.

### 7.3d - Episode Detail

Status: completed 2026-05-30.

- Rebuild Episode detail visual layout and source-list presentation.
- Preserve play, default-source, manual probe, source split and watched-state commands.
- Move correction from persistent tab / panel into the unified correction dialog entry.
- Remove temporary stage wording from final UI copy.

Implementation note: 7.3d keeps the existing Episode detail commands, source operations, watched-state command, source split warning flow and correction services. The Episode correction surface is now an in-page overlay using existing Episode correction commands. The fully shared cross-page correction-dialog shell remains 7.3e scope.

### 7.3e - Unified Correction Dialog

Status: completed 2026-05-30.

- Introduce one correction-dialog shell for Movie, Season, Episode and unknown/grouped detail contexts.
- Reuse existing correction ViewModel/service capabilities.
- Keep source split as a separate warning-confirmation flow.
- Do not add delete, move-out or artificial aggregation actions to ordinary detail pages.

Implementation note: 7.3e adds a shared `CorrectionDialogShell` for the Movie, Season and Episode detail correction overlays. The shell owns the overlay chrome, dialog card, title/summary area, close affordance, scroll container and footer action area; each page still supplies its existing correction body, commands, candidate collections, mapping fields and service behavior. Source split remains a separate warning-confirmation flow.

### 7.3f - Regression Closeout

- Run detail entry / return / correction / source-state regression.
- Smoke check 7.2 media-library search, filters, layout memory, batch mode and removed overlay.
- Update Phase 7 maintenance docs with final status, validation and Known Issues.

## Acceptance Focus

- Every detail page has the unified content-area back button.
- Back returns to the previous reliable detail or visible page when the origin is known.
- Fallback behavior is deterministic when no origin exists.
- Grouped placeholders and unknown items still enter detail pages.
- No-source and metadata-only states remain inspectable.
- Source operations keep their existing safety semantics.
- No detail page exposes player-menu, subtitle-management or local-cache redesign work.
- Build passes and migrations diff remains empty.
