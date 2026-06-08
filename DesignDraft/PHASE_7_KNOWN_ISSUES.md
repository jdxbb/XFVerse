# Phase 7 Known Issues

Last updated: 2026-06-08

This file tracks current blockers, deferred work, risks, noise and user-confirmed non-issues for Phase 7. It should be updated at every Phase 7 substage closeout.

## Blocker

- None currently known after the Phase 7.4e Discovery regression closeout, build validation and migration diff verification.

## Deferred

### 7.4 Discovery

- Dedicated 7.4 plan is tracked in `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md`.
- 7.4a completed Discovery search toolbar, shared Movie / TV search modes, dynamic placeholders, result status copy, baseline poster / list layout switching and Discovery-specific file-backed layout memory.
- 7.4a added TV person search as a scoped Discovery search behavior through TMDB `person/{id}/tv_credits`; TV remains excluded from Movie AI recommendations, Watch Insights, profile/persona inputs and recommendation fingerprints.
- 7.4b completed Discovery search UI structural alignment: media-library-like toolbar / filter structure, result summary, scroll / density pass, poster / list containers and final result cards. Movie cards expose want-state actions; TV cards only show a `想看` tag when at least one season is want-to-watch.
- Exact media-library visual parity for Discovery search controls remains deferred per user instruction. Known differences include button size, layout-toggle button position, search input size, search icon treatment and dropdown menu styling.
- 7.4c owns ranking visual consistency only. TMDB ranking order, ranking source inclusion and paging semantics must remain unchanged unless explicitly documented as a blocker fix.
- 7.4d owns the embedded AI recommendation tab and custom preference dialog polish only. Movie recommendation algorithms, prompts, profile/persona inputs, fingerprints and TV exclusion remain out of scope.
- 7.4e completed Discovery regression closeout, docs updates and cross-page impact accounting.
- Discovery layout memory uses an App-layer preference file and must continue to avoid database fields or migrations.

### 7.3 Details

- Dedicated 7.3 plan is tracked in `DesignDraft/PHASE_7_3_DETAILS_PLAN.md`.
- Unified detail return behavior has a 7.3a baseline: minimal in-memory origin stack plus deterministic fallback.
- Movie detail has a 7.3b visual baseline: poster hero, overview, metadata, actions, ratings, tags, source list and in-page correction overlay using existing correction commands.
- Series and Season details have a 7.3c visual baseline: poster hero, overview, metadata, state/action area, Season list, Episode list and in-page Season correction overlay using existing commands.
- Episode detail has a 7.3d visual baseline: still / placeholder hero, overview, metadata, actions, source list and in-page Episode correction overlay using existing commands.
- 7.3e adds the shared correction dialog shell for Movie / Season / Episode overlay chrome. Page-specific correction body content, candidate lists, mapping fields and service commands remain deliberately page-owned.
- 7.3f remains the detail regression closeout stage, including detail entry / return / correction / source-state regression and 7.2 media-library smoke checks.
- If grouped placeholder detail experience needs deeper polish, handle it in 7.3. Do not treat current grouped placeholder entry as a 7.2 blocker.
- Full restoration of complex source-page state such as scroll offset, deep filter snapshots or page-specific tab state remains deferred unless an existing page already exposes that state safely. 7.3a only owns reliable page-level origin return and detail hierarchy fallback.
- Series / Season / Episode rating cards now use existing TMDB / OMDb clients as read-only cached lookups. Persisting TV rating snapshots is deferred unless a later performance audit shows a concrete need.
- Movie correction candidate detail enrichment is intentionally bounded: Movie loads at most 10 details, TV loads at most 12 details and both use a concurrency limit of 4. Failed detail rows fall back to search-only data instead of blocking the full result list.
- Delete the commented legacy Movie correction XAML after manual acceptance of the new dialog. It is retained temporarily only as a local source comparison and is not active at runtime.

### 7.5 Player

- P61-01: player currently has a visible "本地缓存" menu item, but final UI must not expose a video-cache entry.
- Keep `VideoCacheService` / `IVideoCacheService` backend capability.
- Remove the visible player-menu cache entry during 7.5 player menu rebuild.
- Player, player menu, OnlineSubtitleSearchWindow and mpv / HwndHost / Popup / fullscreen regressions need a dedicated 7.5 Plan / Audit.

### 7.6 Settings / Scan / Cache

- 7.6a established shared UI component shells for sensitive inputs, field rows, API config cards, cache category cards and scan path cards.
- 7.6b wired the Settings `通用` Tab and software cache management to the 7.6a field-row and cache-card components.
- 7.6b follow-up aligned the Settings `通用` Tab card hierarchy closer to the sketch: cache settings and behavior settings are now two large section cards with continuous inner row-list panels.
- 7.6b second follow-up removed the visible full-page Settings card shell, so Settings general and API cards are not nested inside a larger page card; behavior-setting descriptions now use single-line ellipsis with true-truncation Tooltip behavior.
- 7.6b tab follow-up confirms Settings tabs must reuse Movie Discovery's manual tab-button placement/template; `通用` has no page-level scroll, `API 配置` keeps modern vertical scrolling, and the extra in-page top-right theme toggle is removed.
- 7.6b detail follow-up confirms non-clickable Settings cards / rows must use an arrow cursor, About is a standalone bottom row with the XFVerse icon, and the poster-cache limit input carries the `MB` unit inside the input chrome.
- 7.6b confirmed the real software cache UI has only poster, metadata / other and online subtitle cache categories. Do not restore a video-cache management UI.
- 7.6b now persists theme mode, close-window behavior, play-open fullscreen and startup WebDAV auto-scan. Close-window behavior, player fullscreen and startup WebDAV auto-scan use an App-layer local preference file with no Core schema or migration.
- 7.6c wired the Settings `API 配置` Tab to shared API config cards, field rows and sensitive inputs. TMDB, OMDb and OpenSubtitles have independent save / test actions; AI has save only because no existing test command is available.
- 7.6c API detail follow-up confirms sensitive-input reveal buttons live inside the input, API input hover must still scroll the API tab, API field-level helper text is removed, TMDB optional key copy is `API Key（可选）`, API status badges use green / yellow / red breathing lights for test passed / untested / failed, and OpenSubtitles default languages display as Chinese with common languages first.
- AI connection testing remains Deferred unless a later stage defines a real service contract and safe error wording. Do not add a fake AI test button.
- 7.6d completed scan task configuration alignment and path-picker work. WebDAV / local path rows reuse `ScanPathCard`; WebDAV directory browsing uses `IWebDavService.ListDirectoryAsync` through an App-layer picker window.
- ScanTasksPage is confirmed as a no page-level scroll surface for this stage: only WebDAV path list, local path list and history log list scroll internally with the modern scrollbar behavior.
- 7.6e completed scan progress / history-log visual closeout and local-path remove Popover work. Scan history uses `ScanLogCard`; local scan-path removal uses `LocalScanPathRemovePopover`.
- 7.6e removed the fake `已识别` / `未识别` placeholder metrics because the current scan progress / result / log read models do not expose stable recognized / unrecognized counts.
- Cache cleanup copy must stay distinct from deleting real media sources.
- Do not show fake scan progress or fake pause controls if services do not support them.

### 7.7 History / Favorites / Watch Insights

- Dedicated 7.7 plan is tracked in `docs/ui-redesign/PHASE_7_7_HISTORY_FAVORITES_INSIGHTS_PLAN.md`.
- 7.7a completed the shared top-tab style baseline in `PageTabs.xaml`. Later Favorites and Watch Insights work should use those shared style keys for Movie Discovery-style top tabs.
- 7.7b rebuilt Watch History as a date-filtered, scrollable date-group poster grid and copied Media Library poster-card visuals for history items.
- 7.7c rebuilt Favorites as Movie Discovery-style top tabs and a scrollable Movie / Season poster-card grid with item-level remove loading / failure / disabled state.
- Watch-insights visual consistency remains later Phase 7 work after 7.7c.
- External Movie favorites without a stable `MovieId` still have no model-level cancel path. 7.7c shows a real disabled reason instead of pretending the action is available; adding a persistent external favorite identifier is deferred to a separate data-model stage.
- Watch-insights and recommendation Movie-only boundaries must be preserved.
- Movie Discovery and Settings still keep their accepted page-local tab templates. Retargeting accepted pages to `PageTabs.xaml` is deferred unless a later global polish stage explicitly owns the cross-page regression.

### 7.8 Global Visual Polish

- Local Light / Dark / card polish is deferred to 7.8 or later targeted stages.
- Global button, spacing, color, menu, dialog, card and empty-state polish remains deferred.
- Home, Library and current detail pages now use the shared layered liquid-glass ResourceDictionary baseline for rounded content cards, background-bearing buttons and primary selected states. Light and Dark themes use differentiated translucent levels, edge highlights and cached diffuse detail backgrounds; extending the same baseline to later rebuilt pages, navigation items, popups, dialogs, tags and equivalent components remains deferred to their page phases and the 7.8 consistency audit.
- The shared liquid-glass baseline must preserve readability and clear interaction states. Avoid page-local hardcoded variants, excessive transparency, high-saturation glow and decorative effects that reduce scanability.
- Final large-list scroll and performance review belongs to 7.8 unless a blocker appears earlier.

### 7.2 Residual Deferred Items

- Missing-poster placeholder and logo hookup were handled in the 7.2 final-assets / post-closeout path. Do not reopen them as 7.3 detail work unless a new defect is reported.
- Full manual "aggregate as season" dialog UI is deferred; 7.2 only organizes entry and semantics.

## Risks

| Risk | Impact | Handling |
|---|---|---|
| WindowChrome / DPI / multi-monitor / maximize edge cases | Shell windows can regress outside standard viewport cases | Recheck in 7.8 and any shell-touching phase. |
| PlayerWindow + mpv / HwndHost / Popup / fullscreen | Player controls, menus or video host may regress under WPF chrome changes | 7.5 must start with Plan / Audit and include player smoke checks. |
| ResourceDictionary merge order / StaticResource lookup | Shared styles can fail at runtime if introduced before their base style dictionary, even when build passes | Prefer same-dictionary bases where practical; run `dotnet build MediaLibrary.sln` and a runtime page-load probe for changed lazy-loaded views. |
| Light / Dark page-level consistency | Some pages may remain readable but visually uneven | Defer broad polish to 7.8; only fix blockers earlier. |
| Media-library large-list virtualization / scroll performance | Poster/list cards and batch mode can degrade on big libraries | Keep performance checks in 7.2 closeout and 7.8 regression. |
| Poster shadow bitmap cache | Many unique palette colors can still cause cache churn or memory pressure | Cache is bounded; keep palette color quantization and recheck diagnostics if scroll jank returns. |
| Poster shadow edge clipping | First row / first column / last row can clip glow if the virtualized viewport has no paint padding | Use viewport / layout padding for hover-lift cards; inspect ancestor `ClipToBounds` before changing texture parameters. |
| Library recent-update sort drift | Entity `UpdatedAt` can be touched by scan metadata refresh and reorder unrelated cards | Visible media-library sort should use source / user-state recency, not raw Movie / Series / Season metadata `UpdatedAt`. |
| Batch operation semantics | UI wording can imply destructive behavior incorrectly | Keep warning / danger variants and explicit non-file-deletion copy. |
| Delete-record / move-out wording | User may think real files are deleted | Keep product red lines in page docs and confirmation copy. |
| Upstream diff includes historical changes | Later audits may misclassify authorized work as current-phase overreach | Use stage logs and current phase boundaries, not upstream diff alone. |
| Existing databases need the approved TV metadata migration before the new detail fields can persist | Old installations otherwise keep the previous `TvSeries` schema | Apply `AddTvSeriesProductionMetadata` during the normal upgrade path; this implementation does not execute database update. |
| Existing databases need the approved TV crew migration before creator fields can persist | TV director, writer and actor rows otherwise remain empty after hydration | Apply `AddTvSeriesCrewMetadata` after `AddTvSeriesProductionMetadata` during the normal upgrade path; this implementation does not execute database update. |
| Live WPF backdrop blur can regress scrolling and detail navigation latency | A literal blur effect on stacked cards would be expensive and can reintroduce UI stalls | Keep the Apple-like blur impression static: cached diffuse background, translucent surfaces, edge highlights and bounded shadows only. |
| Existing TV rows can still store raw TMDB English genre names | Older hydrated rows can contain values such as `Sci-Fi & Fantasy` | Normalize TV genre names when media-library rows load and when new TMDB TV details are parsed; do not require a migration. |
| Movie correction detail enrichment adds bounded TMDB fan-out | Candidate rows need real crew / metadata without making the dialog unresponsive | Limit Movie to 10 rows, TV to 12 rows, use concurrency 4, keep busy UI visible and fall back per row on detail failure. |
| 7.4 TV person search can accidentally cross recommendation boundaries | Person-based TV search is a Discovery feature but TV must stay out of Movie AI / Watch Insights / profiles | If added, use TV-capable TMDB credits only for Discovery result projection and document the business change in 7.4a / 7.4e closeout. |
| 7.4 shared style extraction can regress Home / Media Library / Recommendation pages | Reusing cards, buttons and badges can alter accepted pages indirectly | Every substage must list touched non-stage pages/resources and verify visible impact before closeout. |
| 7.4 Discovery layout memory can be overbuilt | Layout preference should not require schema or migration work | Use file-backed UI preference patterns only; keep migration diff empty. |

## Noise

- LF / CRLF warnings from Git may appear in diff checks; record them only as formatting noise when `git diff --check` has no actual errors.
- Cumulative upstream diff may include 7.0, 7.1 or older authorized changes.
- Local button, color, spacing and card details are not all final until 7.8.
- The current shared liquid-glass colors are a restrained implementation baseline, not the final global palette. Final cross-page color tuning remains owned by 7.8.
- Old visual drafts may show controls, copy or layouts that were superseded by user decisions.
- The accepted Movie detail page is the stable TV-detail comparison baseline. Repeating unchanged Movie screenshots is unnecessary noise unless a visual blocker or explicit verification request requires a fresh capture.
- Existing persistent TMDB TV detail cache rows use the previous schema key. New crew-capable requests use `v3`; old rows remain normal cache-cleanup noise.

## User-Confirmed Non-Issues

- Discovery TV search-card want-season tag copy `当前想看` is a later semantic update and passes 7.4e closeout without code changes.
- Media-library result summary includes "有播放源"; this is confirmed product wording and is not a 7.2 Should Fix.
- PlayerWindow / MainWindow / Core service UI-related changes seen in cumulative upstream diff are authorized historical changes and are not automatically 7.2 overreach.
- Grouped placeholders currently pass user acceptance and can enter detail pages; they are not a 7.2 blocker.
- Removed-library panel remains an in-page overlay; not using a side drawer is intentional for 7.2.
- Account username / avatar placeholders in 7.1 are acceptable until a real account phase exists.
- User profile dialog is local placeholder / visual work only.

## Safety Semantics To Preserve

- "移出媒体库" hides from the media-library view; it is not deletion.
- "删除记录" deletes software records and related metadata/state as defined by existing services; it does not delete local files, WebDAV files or real media sources.
- "恢复" is not rescan and should not clear metadata or user state.
- Batch move-out keeps warning semantics.
- Batch delete-record keeps danger semantics.
- Cache cleanup does not delete media sources.
- Layout memory must not add database fields or migrations.

## Maintenance Rule

Every future Phase 7 substage must update this file when it:

- discovers or resolves a blocker;
- moves a deferred item into active scope;
- changes a user-confirmed decision;
- introduces a new risk or closes an old one;
- decides a previously suspected problem is noise or a confirmed non-issue.
