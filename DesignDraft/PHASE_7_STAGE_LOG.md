# Phase 7 UI Rebuild Stage Log

Last updated: 2026-05-30

This is the living Phase 7 handoff log. Keep entries concise and stage-oriented. Do not turn this into a full code diff.

## Path Context

- Historical initial UI rebuild docs remain in `docs/ui-redesign/UI_REDESIGN_PLAN.md`, `UI_REDESIGN_STAGE_LOG.md` and `UI_REDESIGN_KNOWN_ISSUES.md`.
- Phase 7 is a later formal UI rebuild stage, so ongoing Phase 7 maintenance lives in `DesignDraft/PHASE_7_PLAN.md`, this log and `DesignDraft/PHASE_7_KNOWN_ISSUES.md`.
- Design source docs live in `DesignDraft`, especially `PHASE_6_COVERAGE_MATRIX.md` and `page-spec`.

## Phase 7.0 - Complete

### 7.0a - Token / ResourceDictionary Baseline

- Established global visual token and ResourceDictionary baseline for the WPF app.
- Scope stayed in App UI resources and shared styling.
- Did not change media-library, scanning, playback or recommendation business logic.

Known validation:
- Build was expected for implementation-stage closeout.

Suggested commit message:
- `Phase 7.0a token resource baseline`

### 7.0b - Basic Controls And State Components Baseline

- Established reusable control and state styling baseline.
- Covered buttons, inputs, menus, cards, status surfaces and related visual primitives.
- Did not introduce a new UI framework.

Known validation:
- Build was expected for implementation-stage closeout.

Suggested commit message:
- `Phase 7.0b shared controls and states`

### 7.0c - MainWindow Custom Title Bar

- MainWindow moved toward a custom shell / title-bar baseline.
- Window behavior risk was tracked for DPI, resize, maximize and multi-monitor regression.
- Did not rewrite application navigation.

Known validation:
- Build and manual window smoke checks were expected.

Suggested commit message:
- `Phase 7.0c main window custom chrome`

### 7.0d - PlayerWindow / OnlineSubtitleSearchWindow Fixed-Dark Baseline

- Player-related windows received a fixed dark visual baseline.
- This was UI baseline work only; player business behavior and subtitle services remained out of scope.
- P61-01 was not resolved here.

Known validation:
- Build and player-window smoke checks were expected.

Suggested commit message:
- `Phase 7.0d player dark window baseline`

### 7.0e - Global Regression Closeout

- Closed Phase 7.0 with global static and build regression checks.
- Confirmed no DB / migration work was part of 7.0.

Suggested commit message:
- `Phase 7.0e global UI baseline closeout`

## Phase 7.1 - Complete

### 7.1a - Shell, Navigation, Brand, Account Menu, Theme Entry

- Reworked shell, navigation and brand presentation.
- Account menu uses local placeholder semantics; no real account system was introduced.
- Theme entry shows current theme.

Explicit non-goals:
- no account backend;
- no recommendation / player / scan business changes.

Suggested commit message:
- `Phase 7.1a shell navigation and account menu`

### 7.1b - HomePage Rebuild

- Rebuilt HomePage around current dashboard, recently watched / added and recommendation surfaces.
- Home may use minimal navigation state to open the AI recommendation tab.
- Statistics trend can reuse existing watch statistics / discovery data sources.

Explicit non-goals:
- no recommendation algorithm changes;
- no new database fields.

Suggested commit message:
- `Phase 7.1b home page rebuild`

### 7.1c - User Profile Dialog And Confirmation Dialog Variants

- User profile dialog remains a local placeholder / visual surface.
- Confirmation dialog variants support normal, warning and danger semantics.
- Dangerous wording keeps "移出媒体库", "删除记录" and "缓存清理" distinct from deleting media files or sources.

Suggested commit message:
- `Phase 7.1c profile and confirmation dialogs`

### 7.1d - Phase 7.1 Regression Closeout

- Closed 7.1 regression.
- Confirmed shell, home and dialog changes did not require DB / migration work.

Suggested commit message:
- `Phase 7.1d shell and home closeout`

## Phase 7.2 - Media Library

### 7.2 Plan / Audit - Complete

- Planned and audited media-library work before implementation.
- Page specs and Phase 6 coverage matrix were used as written specifications.
- Design drafts were visual references only.

Suggested commit message:
- `Phase 7.2 media library plan audit`

### 7.2a - Toolbar, Submitted Search, Filters, Sorting, Result Summary

- Added media-library toolbar structure.
- Preserved submitted search semantics: typing does not immediately filter; Enter / search icon submits.
- Added clear search, clear filters, filter combinations, sorting and result summary.
- Result summary includes "有播放源 / 无播放源" by confirmed product decision.

Explicit non-goals:
- no query-service business rewrite;
- no player or detail-page changes.

Suggested commit message:
- `Phase 7.2a media library toolbar filters and search`

### 7.2b - Poster / List Layout, Layout Memory, Media Item Visual Baseline

- Added poster / list layout switching.
- Added layout memory without database schema changes.
- Established media-item visual baseline for Movie, Series, Season and Other style rows/cards.
- Kept missing-poster placeholder as a resource hookup item for final-assets closeout.

Explicit non-goals:
- no DB field or migration for layout memory;
- no global visual polish beyond 7.2 scope.

Suggested commit message:
- `Phase 7.2b media library layout modes`

### 7.2c - Batch Entry, Batch Toolbar, Selection State, Special Item Hints

- Batch entry remains visible only when the sidebar is collapsed.
- Batch toolbar, selection state, select-all / clear-selection and special-item hints were aligned.
- Batch move-out and delete-record confirmations keep warning / danger semantics.

Explicit non-goals:
- no business semantic change to hide, restore or delete-record services;
- no fake full manual aggregation UI.

Suggested commit message:
- `Phase 7.2c media library batch mode`

### 7.2d - Removed Library Overlay

- Implemented removed-library in-page overlay.
- Added Movie / TV grouping, empty state and operation area.
- Restore, detail and delete-record actions use existing service semantics.
- User confirmed overlay should remain in-page rather than a drawer / side panel.

Explicit non-goals:
- no deletion of local or WebDAV files;
- no restore-as-rescan behavior.

Suggested commit message:
- `Phase 7.2d removed library overlay`

### 7.2e - Regression Audit Closeout

- Regression audit was executed across 7.2a-d.
- Some initial conclusions required correction according to user decisions:
  - "有播放源" in result summary is confirmed product wording, not a bug;
  - grouped placeholder currently passes user acceptance and is not a 7.2 blocker;
  - relative upstream diff may include authorized 7.0 / 7.1 / historical changes and must not be treated as 7.2 overreach by itself.

Explicit non-goals:
- audit did not fix code;
- audit did not introduce migrations.

Suggested commit message:
- `No commit recommended for audit`

### 7.2-final-assets - Current Closeout Item

- Assets directory contains `logo.svg`, `poster-placeholder.png` and `WatchPersonas`.
- Missing-poster placeholder and logo SVG are the final Phase 7.2 asset hookup scope.
- Current working tree contains resource-hookup work for the logo and shared poster placeholder; if not yet committed, treat it as pending final user review and manual commit.
- WPF has no built-in direct SVG rendering mechanism in the current project; the minimal approach is to use existing WPF resource / XAML vector mechanisms rather than add a large UI framework.

Explicit non-goals:
- no layout rewrite;
- no search / filter / sort changes;
- no player, detail, Core query, DB or migration changes.

Suggested commit message:
- `Hook up app logo and shared poster placeholder`

### 7.2-poster-card-texture - Reusable Texture Template

- HomePage poster cards reuse the media-library poster-card texture without changing card fields or data semantics.
- Reusable poster texture pattern:
  - fixed-size outer `Grid` instead of a clipping-only `Border`;
  - rounded `ClipMask` border plus `VisualBrush` opacity mask for image / overlay clipping;
  - palette shadow host with `PosterPaletteShadowBehavior` so the outer shadow can derive from the loaded poster;
  - secondary base shadow for stable depth when the poster is missing or not loaded;
  - dark surface brush under the placeholder / image;
  - shared `PosterPlaceholderTemplate` beneath the real poster image;
  - real poster image above placeholder with `RenderOptions.BitmapScalingMode="HighQuality"`, `SnapsToDevicePixels="True"` and decode width chosen by rendered poster size;
  - bottom dark depth gradient and top white highlight gradient inside the clipped content layer.
  - hover lift on the outer poster `Grid` with `TranslateTransform Y=-2`, matching the media-library poster-card interaction.
  - hover-lift posters must reserve at least 2-3px of top layout space or disable parent clipping; otherwise the lifted poster top edge can be clipped, especially in compact rows such as HomePage "最近新增".
- Decode width guidance from this pass:
  - small 82px-wide home posters use `DecodePixelWidth=180`;
  - 112px-wide recommendation posters use `DecodePixelWidth=240`;
  - 158px-wide continue-watching posters use `DecodePixelWidth=320`.

Explicit non-goals:
- no HomePage field, command, navigation or ViewModel changes;
- no media-library search / filter / sort changes;
- no player, detail, Core query, DB or migration changes.

Suggested commit message:
- `Polish home poster card texture`

### 7.2-poster-card-shadow-cache - Media Library Scroll Rendering

- Media-library poster cards keep the original glow / shadow texture parameters, but no longer attach per-item runtime `DropShadowEffect` instances in the virtualized list.
- `PosterCachedShadowBehavior` renders the original WPF `DropShadowEffect` parameters offscreen once and caches the resulting `ImageSource`.
- The palette glow still uses `PosterPaletteShadowBehavior`; when the target is an `Image`, it updates the cached shadow image color instead of cloning and replacing a live effect.
- The scroll-time lightweight texture mode was removed. Scrolling and idle states now use the same complete poster-card texture.
- Reusable cached-shadow pattern:
  - preserve original `BlurRadius`, `Direction`, `Opacity`, `ShadowDepth`, card size, corner radius and inner margin;
  - render the shadow with enough transparent padding so it is not clipped;
  - for 180x270 media-library posters, keep the cached shadow bitmap at least 84px padded on each side; smaller padding can expose rectangular glow edges during hover / scroll composition;
  - show the cached result as a behind-card `Image` with matching negative margins;
  - quantize palette glow colors only for cache reuse, not for base shadow;
  - do not reintroduce scroll-time visual toggles unless there is an explicit visual design for the state change.

Explicit non-goals:
- no media-library search / filter / sort or query semantic changes;
- no player, detail, Core query, DB or migration changes;
- HomePage still uses its existing poster texture implementation unless a later phase explicitly migrates it to cached shadows.

Suggested commit message:
- `Cache media library poster card shadows`

## Phase 7 Documentation Sync - Current

- Added Phase 7 dedicated maintenance docs:
  - `DesignDraft/PHASE_7_PLAN.md`
  - `DesignDraft/PHASE_7_STAGE_LOG.md`
  - `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- This is a docs-only phase.
- It should not modify business code, XAML, ViewModels, resource dictionaries, migrations or assets.

Validation:
- `git diff --check` should be run at phase end.
- Migrations diff should be empty.
- Build may be skipped if only Markdown changed.

Suggested commit message:
- `Sync Phase 7 maintenance docs`

## Current State Summary

- 7.0 complete.
- 7.1 complete.
- 7.2a-d complete.
- 7.2e audit executed.
- 7.2-final-assets and subsequent poster / Home / library polish follow-ups have been handled as post-closeout work.
- 7.3 Plan, 7.3a, 7.3b, 7.3c, 7.3d and 7.3e are complete.
- Recommended next detail stage: 7.3f regression closeout.

## Maintenance Rule

At the end of every future Phase 7 substage, update:

- plan: scope and sequencing changes;
- stage log: what actually changed, validation, and explicit non-goals;
- Known Issues: blockers, deferred items, risks, noise and user-confirmed non-issues.

Codex must not require commit / push in these docs. User manages Git operations unless explicitly requested.

## Phase 7.3 - Details

### 7.3 Plan - Details Chain

Completed:

- Added `DesignDraft/PHASE_7_3_DETAILS_PLAN.md` as the dedicated 7.3 handoff plan.
- Confirmed 7.3 scope as Movie / Series / Season / Episode details, unified detail return, special-state detail experiences and unified correction-dialog alignment.
- Confirmed old design drafts are visual references only; current page specs and Phase 7 maintenance docs define the stage boundary.

Explicit non-goals:

- no player menu work;
- no P61-01 local cache menu removal;
- no scan / AI recognition threshold changes;
- no recommendation algorithm changes;
- no database schema, migration or database update.

Suggested commit message:

- `Plan Phase 7.3 detail pages`

### 7.3a - Shared Detail Foundation

Completed:

- Added a unified content-area icon back affordance for Movie, Series, Season and Episode detail pages.
- Added minimal App-layer in-memory detail origin tracking in `NavigationStateService`.
- `MainWindowViewModel` records accepted navigation requests before page activation finishes, so slow Home / Library activation cannot leave a stale detail origin on the stack.
- Page activation now displays the destination page first, then starts content loading on the Dispatcher background queue with a navigation-version / cancellation guard. This keeps the first detail back button responsive and prevents stale detail loads after a quick return.
- Detail fallback remains deterministic: Movie / Series to Library, Season to Series when possible, Episode to Season when possible.
- Existing Season / Episode text-return commands were preserved internally but now route through the shared detail-back behavior.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors before the running app locked the normal output exe.
- Follow-up compile checks used `dotnet build MediaLibrary.sln -p:UseAppHost=false`; the final check passed with 0 warnings and 0 errors while the desktop app was running.

Explicit non-goals:

- no detail page field/content rebuild yet;
- no source action semantic changes;
- no correction-flow service changes;
- no player, scan, recommendation, Core query, database schema, migration, database update, commit or push.

Suggested commit message:

- `Phase 7.3a shared detail navigation`

### 7.3b - Movie Detail

Completed:

- Rebuilt `MovieDetailPage` into the Phase 7 detail-page layout: poster hero, title/original title, status chips, overview, base metadata, action area, rating cards, tag cards and source list.
- Removed the old persistent detail `TabControl` presentation from Movie detail and kept subtitles out of the Movie detail surface.
- Moved Movie source correction from the old permanent correction tab into an in-page overlay entry. The overlay reuses the existing Movie / TV episode / unknown-season correction commands and services.
- Added lightweight Movie detail state helpers for ratings and source presence so empty rating and no-source states render without changing query or source semantics.
- Added a small rating-star converter for the Movie detail rating card display.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no Series / Season / Episode detail visual rebuild in this slice;
- no player menu, subtitle-management or P61-01 local cache work;
- no scan / AI recognition threshold changes;
- no source operation semantic changes;
- no Core service, recommendation algorithm, database schema, migration, database update, commit or push.

Suggested commit message:

- `Phase 7.3b movie detail baseline`

### 7.3c - Series And Season Details

Completed:

- Rebuilt `SeriesOverviewPage` into the Phase 7 detail-page layout: poster hero, unified back affordance, title/original title, status chips, overview, base metadata, source summary, collection action and Season list.
- Rebuilt `TvSeasonDetailPage` around poster hero, unified back affordance, season status chips, overview, metadata, season user-state actions, correction entry and Episode list.
- Kept recognized, unrecognized, metadata-only, no-source and grouped-placeholder detail states on the existing TV read models; no TV query semantics were changed.
- Kept Episode list actions wired to the existing detail, play and watched-state commands, including disabled play behavior for episodes without playable sources.
- Kept Season correction as an in-page overlay using existing commands and services, and converted visible Season-correction status/button text to Chinese.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors at closeout.

Explicit non-goals:

- no Episode detail visual rebuild in this slice;
- no player menu, subtitle-management or P61-01 local cache work;
- no scan / AI recognition threshold changes;
- no TV query service or correction service semantic changes;
- no delete-record / move-out detail actions;
- no Core service, recommendation algorithm, database schema, migration, database update, commit or push.

Suggested commit message:

- `Phase 7.3c series and season detail baseline`

### 7.3d - Episode Detail

Completed:

- Rebuilt `EpisodeDetailPage` into the Phase 7 detail-page layout: still / placeholder hero, unified back affordance, episode status chips, overview, single-episode metadata, primary actions, state summary and source list.
- Added Episode still-image binding through the existing poster-cache image behavior, while keeping the shared placeholder beneath missing still images. The detail still image uses an explicit 640px decode width so the 420px-wide hero image does not fall back to the shared 240px poster-cache default.
- Replaced the old persistent Episode detail tab / correction panel presentation with an in-page correction overlay entry.
- Preserved existing Episode commands for play, source play, set default source, manual probe, source split, watched state, refresh and correction.
- Kept unknown-season selection as a nested overlay inside the existing Episode correction flow.
- Removed temporary stage wording from the final Episode detail UI surface.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors at closeout.

Explicit non-goals:

- no shared cross-page correction-dialog shell beyond the Episode page overlay; that remains 7.3e;
- no subtitle-management, online subtitle, player menu or P61-01 local-cache work;
- no scan / AI recognition threshold changes;
- no source, TV query or correction service semantic changes;
- no delete-record / move-out detail actions;
- no Core service, database schema, migration, database update, commit or push.

Suggested commit message:

- `Phase 7.3d episode detail baseline`

### 7.3 Navigation Follow-up - Library First-load State

Completed:

- Fixed a 7.3a navigation timing regression where entering the media library from Home could show the real empty-state placeholder before the first library refresh completed.
- Added a media-library initial-loading gate so the first render shows a loading state until the first library query finishes.
- Kept the 7.3a behavior that switches pages before async content activation, so detail back responsiveness is not regressed.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.

Explicit non-goals:

- no media-library query, search, filter, sort, batch or detail-navigation semantic changes;
- no player, scan, Core service, database schema, migration, database update, commit or push.

Suggested commit message:

- `Fix library first-load placeholder during navigation`

### 7.2/7.3 Follow-up - Media Library Poster Shadow First-frame Stability

Completed:

- Fixed a media-library poster-card shadow / glow flicker that could appear after page switches or filter changes recreated poster card visuals.
- The cached shadow bitmap itself was already reused, but `PosterCachedShadowBehavior` delayed assigning it through a Dispatcher `Loaded` callback. Newly created cards could therefore render one frame with an empty shadow image before the cached shadow appeared.
- Changed cached-shadow application to assign synchronously once the target image is loaded, and to subscribe to the actual `Loaded` event for not-yet-loaded images. This keeps the existing full poster texture and cached shadow parameters unchanged.
- Stabilized palette glow color on recreated cards by caching computed shadow colors by poster source URL. When the same poster returns after filtering or navigation, the palette glow can be applied before the poster bitmap raises another delayed source-change pass.
- Avoided forcing palette glow back to the fallback color while the poster image source is temporarily null during async reload; the existing shadow color is preserved until a real poster bitmap or cached source color is available.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.

Explicit non-goals:

- no poster texture, shadow, glow, color, spacing, hover or cache-size parameter changes;
- no media-library query, search, filter, sort, batch or detail-navigation semantic changes;
- no player, scan, Core service, database schema, migration, database update, commit or push.

Suggested commit message:

- `Stabilize media library poster shadow first frame`

### 7.2-post-closeout follow-up - Poster Edges, Home AI, TV Coverage, Stable Library Sort

Completed:

- Media-library poster grid now reserves explicit virtualized viewport padding for top / left / bottom poster glow and the `Y=-2` hover lift. This keeps the existing cached glow / shadow texture and fixes edge clipping by giving the list viewport real paint space instead of changing the card texture.
- Home recent-poster cells reserve extra top / side layout room for the same hover-lift / shadow pattern.
- Home "recently added" now includes movie items and TV season items, including recognized and unrecognized seasons with active episode sources.
- Home AI recommendation preview now shows the recommendation status text from the AI recommendation view model instead of static scope copy, and exposes the same not-interested command beside the want-to-watch command.
- Media-library unidentified TV-like items now use season-style watched episode progress text instead of movie percent text.
- Batch mode no longer shows a duplicate "Other" hint when the batch hint would be identical to the existing category tag.
- Library visible-list "recently updated" ordering no longer uses Movie / TV Series / TV Season entity `UpdatedAt` directly. Old key: metadata entity `UpdatedAt`, which can be touched by scan identification, metadata refresh or AI tag refresh. New visible-list key: collection-state update, active media-source row update, and initial entity creation fallback. This preserves real source / user-state recency while avoiding pure metadata scan refresh from reshuffling unrelated cards.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no poster texture parameter change;
- no media-library search / filter / batch business semantic change;
- no scan confidence threshold change;
- no player, detail, database schema, migration, database update, commit or push.

Maintenance notes:

- Do not rewire visible media-library sort back to raw Movie / Series / Season `UpdatedAt` without first proving that scan metadata refresh cannot touch those fields.
- If poster edge clipping reappears, inspect viewport / ancestor clipping and layout padding first; do not blindly enlarge the cached shadow bitmap.

Suggested commit message:

- `Stabilize library sort and home TV previews`

### 7.2-post-closeout follow-up - Home Polish, Filter Multi-Select, Poster Paint Padding

Completed:

- Media-library poster grid paint padding was split from column-count calculation. Left / top / bottom padding can reserve shadow and hover-lift paint room without reducing the number of poster columns.
- Media-library poster grid horizontally centers by the real visible card width, not by the larger item slot width. This keeps the first card's left edge and the last card's right edge visually balanced.
- Media-library content type and decade filters support multi-select for non-`全部` options. `全部` remains a single-select close action; selecting every non-`全部` option resets back to `全部` and closes the menu.
- Collection status keeps multi-select, but does not collapse `喜爱` + `想看` + `不想看` back to `全部`, because those three user states are not equivalent to all collection states. `全部` still clears the other selections.
- Watch status returned to a normal single-select menu with `全部` / `已看` / `未看`; `不想看` belongs to collection status, not watch status.
- Home AI recommendation status is rendered as two controlled lines with expanded / collapsed sidebar widths, so the first line no longer truncates prematurely.
- Home AI preview buttons, rating badge, reason scrollbar, bottom discovery button, library metrics, recent-poster title alignment, section arrows and continue-watching progress bar received narrow scoped visual polish.
- Home library metrics, row height distribution, recent-poster safety spacing, section title gaps, AI recommendation vertical position and sidebar logo / collapse button alignment were tuned without changing dashboard data semantics.
- Home continue-watching / recently-added section arrows now use icon-only ghost buttons with hover background, and the shell sidebar toggle uses a local sidebar-panel glyph instead of text glyphs. No icon library was introduced for this narrow replacement.
- Added opt-in HomePage navigation / render diagnostics under `XFVERSE_POSTER_CACHE_DIAGNOSTICS=1` to separate HomeViewModel refresh time, HomePage first-render / visual-tree timing, live `DropShadowEffect` count, cached shadow image count, and palette-shadow target kind. This is diagnostic-only and does not change poster texture behavior.
- Global button focus styling no longer draws a click/focus border on ordinary mouse interaction.
- Sidebar collapse / expand button uses a more modern shell glyph and no longer shows hover text while expanded.

Validation:

- Temp-output App build passed with 0 warnings and 0 errors when the running desktop app locked the normal `bin` output.

Explicit non-goals:

- no media-library query-service business rewrite;
- no search submit semantics change;
- no poster texture parameter change;
- no player, detail, database schema, migration, database update, commit or push.

Maintenance notes:

- Poster shadow safety padding must not be subtracted from virtualized poster column-count width. Treat it as paint room, not item spacing.
- When poster slots include trailing spacing, center the row by visible card width if the design requirement is equal left / right card-edge distance.
- If only one side of poster glow is clipped, inspect the corresponding ancestor clipping / viewport padding before changing glow, cached shadow or card dimensions.
- Home AI status width should track the usable text column: expanded width must end before the refresh button, and collapsed width may grow only with the actual AI panel width.

Suggested commit message:

- `Polish home cards and media filters`

### 7.2-post-closeout follow-up - Home Navigation Stall Diagnosis

Completed:

- Reproduction diagnostics showed Home navigation stalls were dominated by repeated `HomeViewModel` library-overview refreshes on every normal reactivation, not by Home poster glow / shadow rendering.
- Home now reuses the already loaded dashboard on normal page reactivation and only reapplies active playback state. Data-change events still drive targeted refreshes for playback history, collection, recommendation, scan, metadata and library changes.
- No Home poster texture, glow, shadow, corner radius or cached-shadow strategy was changed in this pass.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migrations diff remained empty.
- `git diff --check` passed with line-ending warnings only.

Maintenance notes:

- If Home navigation lag returns, reintroduce a short-lived navigation / render timing probe before changing poster visuals; the previous temporary Home diagnostics were removed after the cache fix was verified.
- Use poster cached-shadow migration for Home only if visual diagnostics prove poster effects are the dominant cost.

### 7.2-post-closeout follow-up - Home Diagnostics Closed And Library Toolbar Polish

Completed:

- Home page navigation / render timing diagnostics were removed after the reactivation-cache fix solved the reported switch-to-home stall. The Home dashboard reactivation cache behavior remains in place.
- Media-library content-type filter now starts from the product default of Movie + TV selected instead of All; clearing filters returns to the same Movie + TV default.
- Media-library "clear filters" button now uses the same toolbar button sizing as the batch-selection entry and is right-aligned with that action column.
- Sidebar account avatar was reduced slightly so the collapsed navigation account button no longer clips the avatar.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migrations diff remained empty.
- `git diff --check` passed with line-ending warnings only.

### 7.3e - Unified Correction Dialog Shell

Completed:

- Added a shared `CorrectionDialogShell` control for detail-page correction overlays.
- Movie detail, Season detail and Episode detail now use the shared shell for the overlay mask, dialog card, title / summary header, close button, scroll container and footer action area.
- Existing Movie / Episode correction bodies continue to own target-type selection, TMDB search, AI assist, candidate cards, unknown-season selection and submit commands.
- Existing Season correction body continues to own recognized / unknown target selection, AI assist, TMDB search, episode-number mapping, status text and confirm command.
- Source split remains a separate warning-confirmation flow and was not merged into the correction dialog.
- No delete-record, move-out, subtitle, player-menu, local-cache or artificial-aggregation actions were added to ordinary detail pages.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors at closeout.
- Migrations diff remained empty.
- `git diff --check` passed with line-ending warnings only.

Explicit non-goals:

- no correction ViewModel, candidate-search, AI-recognition, TMDB, source-split or correction-service semantic changes;
- no player, subtitle, P61-01 local-cache, scan, Core service, database schema, migration, database update, commit or push;
- no visual redesign beyond extracting the existing correction overlay chrome into the shared shell.

Maintenance notes:

- Future correction-dialog polish should first extend `CorrectionDialogShell` for shared chrome changes, while keeping page-specific correction bodies owned by their page ViewModels unless a dedicated correction-body refactor is planned.
- Do not move source split into the correction dialog; it is governed by `page-spec/global-dialogs.md` warning confirmation semantics.

Suggested commit message:

- `Phase 7.3e unified correction dialog shell`

### 7.3 Follow-up - Movie Detail Draft Alignment

Completed:

- Re-aligned `MovieDetailPage` with the movie-detail page-spec and the old full-screen draft structure while keeping current Phase 7 styling and product semantics.
- Moved the top detail area closer to the draft hierarchy: left poster column with the unified back button, center title / original title / status chips / ratings / scrollable overview, and right-side movie information plus tag cards.
- Moved the main action buttons into a dedicated action row above the play-source panel, matching the page-spec requirement that playback and state actions sit above sources.
- Restored the play-source panel to a full-width bottom section instead of sharing the lower row with ratings and tags.
- Added lightweight semantic glyphs to the main action buttons using the existing Segoe MDL2 Assets glyph set; no icon library was introduced.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no Movie detail ViewModel, correction-service, source-service, player, subtitle, scan, Core service, database schema, migration, database update, commit or push changes;
- no fake director / writer / actor / production-company fields were added because the current `MovieDetailModel` does not expose those values;
- no change to correction dialog body behavior beyond preserving the existing 7.3e shared shell hookup.

Suggested commit message:

- `Align movie detail layout with draft`
