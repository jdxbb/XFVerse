# Phase 7 Known Issues

Last updated: 2026-05-30

This file tracks current blockers, deferred work, risks, noise and user-confirmed non-issues for Phase 7. It should be updated at every Phase 7 substage closeout.

## Blocker

- None currently known for closing Phase 7.3e.

## Deferred

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

### 7.5 Player

- P61-01: player currently has a visible "本地缓存" menu item, but final UI must not expose a video-cache entry.
- Keep `VideoCacheService` / `IVideoCacheService` backend capability.
- Remove the visible player-menu cache entry during 7.5 player menu rebuild.
- Player, player menu, OnlineSubtitleSearchWindow and mpv / HwndHost / Popup / fullscreen regressions need a dedicated 7.5 Plan / Audit.

### 7.6 Settings / Scan / Cache

- Settings, scan tasks and software cache management need later visual and state alignment.
- Cache cleanup copy must stay distinct from deleting real media sources.
- Do not show fake scan progress or fake pause controls if services do not support them.

### 7.7 History / Favorites / Watch Insights

- History, favorites and watch-insights visual consistency remain later Phase 7 work.
- Watch-insights and recommendation Movie-only boundaries must be preserved.

### 7.8 Global Visual Polish

- Local Light / Dark / card polish is deferred to 7.8 or later targeted stages.
- Global button, spacing, color, menu, dialog, card and empty-state polish remains deferred.
- Final large-list scroll and performance review belongs to 7.8 unless a blocker appears earlier.

### 7.2 Residual Deferred Items

- Missing-poster placeholder and logo hookup were handled in the 7.2 final-assets / post-closeout path. Do not reopen them as 7.3 detail work unless a new defect is reported.
- Full manual "aggregate as season" dialog UI is deferred; 7.2 only organizes entry and semantics.

## Risks

| Risk | Impact | Handling |
|---|---|---|
| WindowChrome / DPI / multi-monitor / maximize edge cases | Shell windows can regress outside standard viewport cases | Recheck in 7.8 and any shell-touching phase. |
| PlayerWindow + mpv / HwndHost / Popup / fullscreen | Player controls, menus or video host may regress under WPF chrome changes | 7.5 must start with Plan / Audit and include player smoke checks. |
| ResourceDictionary merge order / StaticResource lookup | Shared styles can fail at build or runtime if introduced in the wrong dictionary | Prefer existing dictionaries and run `dotnet build MediaLibrary.sln` for implementation phases. |
| Light / Dark page-level consistency | Some pages may remain readable but visually uneven | Defer broad polish to 7.8; only fix blockers earlier. |
| Media-library large-list virtualization / scroll performance | Poster/list cards and batch mode can degrade on big libraries | Keep performance checks in 7.2 closeout and 7.8 regression. |
| Poster shadow bitmap cache | Many unique palette colors can still cause cache churn or memory pressure | Cache is bounded; keep palette color quantization and recheck diagnostics if scroll jank returns. |
| Poster shadow edge clipping | First row / first column / last row can clip glow if the virtualized viewport has no paint padding | Use viewport / layout padding for hover-lift cards; inspect ancestor `ClipToBounds` before changing texture parameters. |
| Library recent-update sort drift | Entity `UpdatedAt` can be touched by scan metadata refresh and reorder unrelated cards | Visible media-library sort should use source / user-state recency, not raw Movie / Series / Season metadata `UpdatedAt`. |
| Batch operation semantics | UI wording can imply destructive behavior incorrectly | Keep warning / danger variants and explicit non-file-deletion copy. |
| Delete-record / move-out wording | User may think real files are deleted | Keep product red lines in page docs and confirmation copy. |
| Upstream diff includes historical changes | Later audits may misclassify authorized work as current-phase overreach | Use stage logs and current phase boundaries, not upstream diff alone. |

## Noise

- LF / CRLF warnings from Git may appear in diff checks; record them only as formatting noise when `git diff --check` has no actual errors.
- Cumulative upstream diff may include 7.0, 7.1 or older authorized changes.
- Local button, color, spacing and card details are not all final until 7.8.
- Old visual drafts may show controls, copy or layouts that were superseded by user decisions.

## User-Confirmed Non-Issues

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
