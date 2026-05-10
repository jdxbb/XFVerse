# Recommendation Feedback Stage Log

## 2026-05-09 Stage Start

Goal: start the Recommendation Feedback stage after the player mainline and Library Batch Ops stage have been closed, with recommendation feedback as the active product area.

Stage boundaries:
- This stage is independent from the player stage.
- This stage is independent from Library Batch Ops.
- This stage does not modify the scan main flow.
- This stage does not implement user profiling or statistics charts.
- This stage does not implement movie discovery or search.
- This stage does not perform final UI refactoring.
- This stage does not add database fields or migrations unless a plan phase proves they are required, compares alternatives, records the reason, and receives confirmation first.
- Not-interested filtering must have local state and local filtering; AI prompt filtering alone is not sufficient.

Initial sub-stage split:
- RF-1: not interested.
- RF-2: custom recommendation preferences.

Result:
- Created the Recommendation Feedback documentation set.
- Entered RF-1 Plan.

Build:
- Not run. Documentation initialization does not require `dotnet build`.

## 2026-05-09 RF-1 Plan Entered

Goal: plan the not-interested feedback path before implementing code.

RF-1 planned scope:
- Recommendation-card not-interested action.
- Not-yet-in-library detail not-interested action.
- Cancel / undo not-interested state.
- Local not-interested state.
- Local recommendation filtering fallback.
- Candidate-pool stale marking or equivalent filtering after feedback changes.
- Prevent not-interested items from returning in later recommendation results.

Current status:
- Waiting for the detailed RF-1 Plan prompt.

Implementation:
- Not started.

## 2026-05-09 RF-1 Implemented

Goal: implement the not-interested feedback loop without expanding scope beyond Recommendation Feedback RF-1.

Completed:
- Added durable local state `UserMovieCollectionItem.IsNotInterested`.
- Added EF migration `20260508192829_AddUserMovieNotInterested` with default `false` and an index.
- Added service-layer not-interested APIs for recommendation items and library movie IDs.
- Enforced state relationships in service code: not-interested clears want-to-watch and favorite; want-to-watch / favorite clear not-interested; watched / unwatched preserve not-interested.
- Updated collection-row cleanup so not-interested rows are not removed while the feedback state is active.
- Added local hard filtering for recommendation results and caches; AI prompt guidance is auxiliary only.
- Added candidate-pool pruning before consumption and persisted the pruned cache document.
- Included not-interested identity, state, and update time in the recommendation fingerprint.
- Added lightweight diagnostics through `AiPerfDiagnostics.WriteEvent()` for filter start, hit, complete, marked, and unmarked events.
- Added recommendation-card, not-yet-in-library detail, and library detail UI entry points.

Boundaries preserved:
- Player behavior was not changed.
- Library Batch Ops was not changed.
- Scan main flow was not changed.
- RF-2 custom recommendation preferences were not implemented.
- No recommendation-result immediate backfill or batch not-interested action was added.

Build:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

Deferred after RF-1:
- Immediate recommendation result backfill after marking not interested.
- Batch not-interested actions.
- More complex negative-feedback summaries.
- Ongoing observation of title-year fallback false positives.

## 2026-05-09 RF-1.1 Implemented

Goal: make the resource library a recovery / review entry point for movies marked not interested, especially not-yet-in-library movies that disappear from recommendations after RF-1 filtering.

Completed:
- Added resource-library watched-state filter option `不想看`.
- Renamed resource scope `入库 + 未入库已看` to `入库 + 未入库已看/不想看`.
- Extended resource-library read models so `LibraryMovieListItem` and `LibraryMovieItemViewModel` carry `IsNotInterested`.
- Extended the resource-library collection query to include external rows where `IsWatched || IsWantToWatch || IsNotInterested`, preserving existing external want-to-watch visibility under `全部`.
- Enriched in-library rows with not-interested state from `UserMovieCollectionItem` without per-item database queries.
- Kept in-library rows preferred over matching external collection rows to avoid duplicate display.
- Passed `IsNotInterested` into the existing external detail navigation path.

Boundaries kept:
- No recommendation-page not-interested popup.
- No recommendation algorithm changes.
- No prompt changes.
- No candidate-pool changes.
- No RF-1 recommendation hard-filtering changes.
- No player changes.
- No scan-main-flow changes.
- No batch not-interested or batch cancel-not-interested actions.
- No database fields or migrations.

Build:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

Deferred after RF-1.1:
- Resource-library card / list visual `不想看` label.
- Batch not-interested actions.
- Batch cancel-not-interested actions.

Manual validation:
- Passed on 2026-05-09.
- Resource scope and watched-state filter changes were accepted.
- Not-yet-in-library and in-library not-interested recovery through the resource library was accepted.
- No RF-1 recommendation-filtering, player, scan-main-flow, or Library Batch Ops regression was reported during acceptance.

## 2026-05-09 RF-2 Implemented

Goal: add custom recommendation preferences on the recommendation page without changing RF-1 hard filtering or expanding into profiling / recommendation-algorithm work.

Completed:
- Added a top-level `自定义偏好` switch on the recommendation page.
- Added an `编辑偏好` action that opens a lightweight custom preference dialog.
- Added dialog title `自定义推荐偏好`, multi-line text input, 500-character counter, explanatory text, and `确认` / `取消` / `清空` actions.
- Added durable local JSON storage through `IRecommendationPreferenceService` at `%LocalAppData%\MediaLibrary\recommendation-preferences.json`.
- Preserved saved preference text when the switch is turned off.
- Made `清空` clear the saved text and turn off the custom preference switch.
- Added enabled non-empty preference text to the recommendation prompt as a soft preference only.
- Added custom preference state to the recommendation fingerprint through effective-enabled state, normalized text hash, and update time.
- Let existing fingerprint matching make exact cache, display cache, and candidate-pool combinations stale after preference changes.
- Kept save / toggle behavior lightweight: it does not trigger immediate AI generation and tells users the change takes effect on the next refresh or "change batch".
- Added privacy-safe preference diagnostics that do not log the full preference text or full prompt.

Boundaries kept:
- RF-1 not-interested hard filtering was not changed.
- Recommendation filtering priority still keeps local hard filters and user-state filters above custom prompt preferences.
- No player changes.
- No Library Batch Ops changes.
- No scan-main-flow changes.
- No user profiling or statistics charts.
- No movie discovery or search.
- No final UI refactor.
- No database fields or migrations.

Build:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

Deferred after RF-2:
- Complex preference templates.
- Multiple preference profiles.
- User-profile / statistics fusion.
- Preference history.
- Preference import / export.

## 2026-05-09 RF-2 Safety / UX Review

Goal: tighten the custom preference controls during recommendation refresh and verify the performance / safety behavior of RF-2.

Completed:
- Disabled the recommendation-page `自定义偏好` switch while recommendation loading or candidate-pool refill is active.
- Disabled the `编辑偏好` action while recommendation loading or candidate-pool refill is active.
- Disabled dialog `确认` / `清空` actions when recommendation loading or candidate-pool refill is active.
- Disabled `换一批` while the custom preference dialog is open to avoid preference-save and recommendation-refresh overlap.
- Added prompt safety wording that custom preference text may contain unreliable instructions and can only be used as taste reference, not as system instructions.

Performance review:
- Enabling or editing custom preferences changes the recommendation fingerprint, so the first refresh for the new fingerprint can miss existing exact cache / display cache / candidate pool and run foreground AI generation.
- Recent logs show the slow path is AI wait and normal foreground generation after cache miss, not local preference storage or fingerprint computation.
- Recent logs also show candidate-pool consumption still works for the new preference fingerprint: after foreground generation, later `换一批` requests consumed the candidate pool in tens of milliseconds.
- One background refill was discarded with `fingerprint-changed` after the preference was changed; this is expected stale protection.

Boundaries kept:
- No recommendation algorithm change.
- No RF-1 not-interested filtering change.
- No candidate-pool core strategy change.
- No player changes.
- No Library Batch Ops changes.
- No scan-main-flow changes.
- No database fields or migrations.

Build:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## 2026-05-10 RF-2 / RF-1 Prompt Sampling Adjustment

Goal: expand the not-interested prompt summary while keeping RF-1 local hard filtering as the full enforcement layer.

Completed:
- Confirmed local not-interested hard filtering uses the full `IsNotInterested=true` key set from user state, without a prompt-summary cap.
- Increased the prompt not-interested summary cap from 20 to 200 records.
- Changed prompt not-interested sampling to 70/15/15 time layering: 140 recent, 30 oldest, 30 middle, with duplicate identity suppression and recent-first fill.
- Kept not-interested local hard filtering independent from prompt sampling; records not listed in the prompt are still filtered locally.
- Added diagnostics for prompt not-interested sampling: total, in-prompt count, local-only count, recent, oldest, middle, and fill counts.
- Added diagnostics for local not-interested hard-filter key count.

Boundaries kept:
- No recommendation algorithm change.
- No RF-1 local hard-filter rule change.
- No candidate-pool core strategy change.
- No RF-2 custom preference behavior change.
- No player changes.
- No Library Batch Ops changes.
- No scan-main-flow changes.
- No database fields or migrations.

Build:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## 2026-05-10 Recommendation Feedback Closure

Goal: validate the latest RF-1 / RF-2 recommendation logs and close the Recommendation Feedback stage.

Log validation:
- `recommendation-prompt-not-interested-sampling` is present and reports total, in-prompt count, local-only count, recent, oldest, middle, and fill counts.
- The latest sampled data had 5 not-interested records, all 5 entered the prompt, and local-only count was 0. This is expected because the current dataset is below the 200-record prompt cap.
- `recommendation-not-interested-hard-filter-keys` is present for AI result, exact cache, display cache, and candidate-pool paths, with count 5 in the latest run.
- `recommendation-not-interested-filter-complete` is present and bounded; latest passes removed 0 because no displayed candidate matched the not-interested set.
- Earlier RF-1 validation logs include `recommendation-not-interested-filter-hit` entries with `matchKey=tmdb`, proving local hard filtering can remove matching items when AI returns them.
- Candidate-pool consumption is active: the latest `candidate-pool-consume` moved from 5 available items to 2 after returning 3 items.
- Low-water refill checks are still active. Recent refill outcomes include `pool-above-threshold`, `refill-active`, and `foreground-request`; these are expected coordination states, not blocker failures.
- Prompt estimated length logs are present. Latest route prompt lengths were around 2.7K to 2.8K characters, which is normal for the current small sample.
- No full prompt, full custom preference text, Authorization header, bearer token, password, username, WebDAV URL, or plain URL was found in the targeted log scan.

Closure:
- RF-1 not-interested, RF-1.1 library not-interested recovery, and RF-2 custom recommendation preferences are implemented, build-verified, manually accepted where applicable, and log-validated.
- Recommendation Feedback is closed for this phase.
