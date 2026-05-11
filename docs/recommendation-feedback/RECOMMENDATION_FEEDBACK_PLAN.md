# Recommendation Feedback Plan

## Stage Goal

Recommendation Feedback focuses on improving recommendation control after the player mainline and Library Batch Ops stage have been closed. The stage first adds a local "not interested" feedback path, then adds optional custom recommendation preferences.

Primary goals:
- Support marking recommendation items as not interested.
- Support undoing the not-interested state.
- Ensure not-interested items are filtered locally from recommendation results.
- Ensure recommendation candidate pools are marked stale or filtered when feedback changes.
- Later support user-defined recommendation preferences that participate in recommendation fingerprinting.
- Keep this stage independent from player work, Library Batch Ops work, and scan-main-flow work.

## Sub-Stages

### RF-1: Not Interested

Status:
- Implemented and build-verified in RF-1.

Scope:
- Add a not-interested action on recommendation cards.
- Add a not-interested action in details for items that are not yet in the local library.
- Support canceling / undoing the not-interested state.
- Make not-interested state participate in recommendation filtering.
- Ensure candidate pools are marked stale or filtered after feedback changes.
- Ensure not-interested items do not appear again in future recommendation results.
- Use local state and local filtering as the required fallback; AI prompt filtering alone is not sufficient.

Plan notes:
- Prefer existing recommendation ViewModel/service patterns.
- Prefer a minimal local-state design that can support both recommended cards and not-yet-in-library detail pages.
- Data model decision: RF-1 uses `UserMovieCollectionItem.IsNotInterested` as the durable local state. This required an EF migration because the state must persist across sessions, cover not-yet-in-library recommendations, apply to library movies, and support efficient local filtering. `Movie.IsNotInterested` was intentionally not added.
- Migration: `20260508192829_AddUserMovieNotInterested` adds a non-null `bool` column with default `false` and a lightweight index on `UserMovieCollectionItems.IsNotInterested`.
- Service rules are centralized in `IUserCollectionService` / `UserCollectionService`. ViewModels call service methods instead of editing `DbContext` directly.
- Local hard filtering is mandatory and implemented before display. AI prompt negative feedback is only auxiliary.
- Candidate-pool old data is pruned before consumption, and pruned cache documents are saved.
- Recommendation fingerprint includes not-interested identity, state, and update time, so mark / unmark naturally invalidates old cache combinations.
- Filtering diagnostics are written through `AiPerfDiagnostics.WriteEvent()` without prompt text, file paths, WebDAV URLs, credentials, or full candidate-pool dumps.
- Do not change player behavior.
- Do not change Library Batch Ops behavior.
- Do not change the scan main flow.

State rules:
- Mark not interested: set `IsNotInterested=true`, clear `IsWantToWatch`, clear library `Movie.IsFavorite`, preserve watched / unwatched state, and do not delete watch history, media records, or files.
- Unmark not interested: set `IsNotInterested=false`, do not restore previous want-to-watch or favorite state, and preserve watched / unwatched state.
- Mark want-to-watch: allowed only for unwatched recommendation items and clears not-interested state.
- Mark favorite: allowed only for watched library movies and clears not-interested state.
- Mark watched: clears want-to-watch and preserves not-interested state.
- Mark unwatched: clears favorite and preserves not-interested state.
- Collection rows are auto-cleaned only when `!IsWatched && !IsWantToWatch && !IsNotInterested`.

Local filtering:
- Match priority is MovieId, TMDB ID, IMDb ID, then conservative title-year fallback.
- AI result, exact cache, display cache, foreground result, local fallback, and candidate-pool consumption paths apply local not-interested filtering.
- Candidate pools are pruned before consumption and saved after pruning.
- Title-year fallback requires both years to exist and match; diagnostics record `matchKey=title-year` for auditability.

Diagnostics:
- `recommendation-not-interested-filter-start`
- `recommendation-not-interested-filter-hit`
- `recommendation-not-interested-filter-complete`
- `recommendation-not-interested-marked`
- `recommendation-not-interested-unmarked`
- Hit logs are capped to 20 per filtering pass and titles are truncated to 80 characters.

Deferred from RF-1:
- Immediate recommendation result backfill after marking not interested.
- Batch not-interested actions.
- More complex negative-feedback summarization.
- Continued observation of title-year fallback false positives.

Acceptance criteria:
- Users can mark a recommended item as not interested from a recommendation card.
- Users can mark a not-yet-in-library detail item as not interested.
- Users can mark a library detail item as not interested.
- Users can undo the not-interested state.
- Not-interested items are excluded from recommendation results by local filtering.
- Candidate pools are refreshed by stale marking or equivalent local filtering after feedback changes.
- A not-interested item does not reappear in later recommendation results.
- The implementation does not rely only on AI prompt instructions to suppress not-interested items.
- Filtering diagnostics show removed items and the match key used.
- `dotnet build MediaLibrary.sln` passes with 0 warnings and 0 errors.
- No player behavior changes.
- No Library Batch Ops behavior changes.
- No scan-main-flow changes.
- No database schema or migration changes unless separately approved after RF-1 plan evidence.

### RF-1.1: Library Not-Interested Filter

Status:
- Implemented, build-verified, and manually accepted in RF-1.1.

Scope:
- Add "不想看" to the resource-library watched-state filter.
- Rename the resource scope "入库 + 未入库已看" to "入库 + 未入库已看/不想看".
- Show not-yet-in-library movies that are marked not interested when the selected scope allows external state items.
- Show in-library movies that are marked not interested when the watched-state filter is "不想看".
- Keep not-yet-in-library detail navigation on the existing external-detail path.
- Do not add a recommendation-page not-interested popup.
- Do not change recommendation filtering, prompt, fingerprint, or candidate-pool behavior.
- Do not add database fields or migrations.

Plan notes:
- RF-1.1 reuses `UserMovieCollectionItem.IsNotInterested`; no new data model or migration is required.
- `LibraryMovieListItem` carries `IsNotInterested`, and `LibraryMovieItemViewModel` exposes it for filtering.
- `LibraryQueryService` includes external collection rows where `IsWatched || IsWantToWatch || IsNotInterested`.
- In-library rows are enriched from collection not-interested state by loading collection identities once and matching by MovieId, TMDB ID, IMDb ID, then conservative title-year fallback. This avoids per-item database lookups.
- If a library movie and a collection item represent the same identity, the library movie wins and duplicate external rows are suppressed.
- Resource scope semantics:
  - "仅入库": only in-library movies.
  - "入库 + 未入库已看/不想看": in-library movies plus external movies where `IsWatched || IsNotInterested`.
  - "全部": keep the existing no-scope-filter behavior, including external want-to-watch rows.
- Watched-state filter semantics:
  - "不想看": only `IsNotInterested=true`.
  - Combined with "仅入库", it only shows in-library not-interested movies.
  - Combined with "入库 + 未入库已看/不想看" or "全部", it can show both in-library and external not-interested movies.
- External detail construction passes `IsNotInterested` through the existing `AiRecommendationItem` path.
- Existing `NotifyCollectionChanged()` and resource-library reload behavior handle refresh after canceling not-interested in detail.

Deferred from RF-1.1:
- Resource-library card / list visual "不想看" label.
- Batch not-interested actions.
- Batch cancel-not-interested actions.

Acceptance criteria:
- Resource scope dropdown shows "入库 + 未入库已看/不想看".
- Watched-state dropdown shows "不想看".
- A not-yet-in-library movie marked not interested can be found from the resource library.
- Clicking a not-yet-in-library not-interested movie opens the existing external detail page.
- Canceling not interested from detail removes the item from the not-interested filter after refresh.
- An in-library movie marked not interested appears in the resource-library not-interested filter.
- In-library and external collection rows for the same movie do not duplicate.
- "全部" scope continues to include external want-to-watch items.
- RF-1 recommendation hard filtering does not change.
- Batch-1 behavior does not change.
- `dotnet build MediaLibrary.sln` passes with 0 warnings and 0 errors.
- No database schema or migration changes are introduced.

Validation:
- Manual acceptance passed on 2026-05-09.

### RF-2: Custom Recommendation Preferences

Status:
- Implemented, build-verified, log-validated, and closed in RF-2.

Scope:
- Add a recommendation-page custom preference switch.
- Add an edit-preference dialog with text input, character count, help text, confirm, cancel, and clear actions.
- Support user-entered custom recommendation preferences.
- Support enabling and disabling custom preferences while preserving saved text when disabled.
- Enforce a 500-character length limit in UI and service code.
- Include effective custom preferences in the recommendation fingerprint.
- Make candidate pools stale through fingerprint changes after preference changes.
- Apply changed preferences on the next "change batch" / refresh action.

Plan notes:
- RF-2 builds on the RF-1 feedback and candidate-pool invalidation model.
- Storage decision: RF-2 uses `%LocalAppData%\MediaLibrary\recommendation-preferences.json` through `IRecommendationPreferenceService`. This avoids adding database fields or migrations and follows the existing local JSON preference pattern used by player preferences.
- `ApplicationSetting.CurrentAiRecommendationsJson` and `RecentAiRecommendationsJson` remain recommendation-cache fields and are not used to store preferences.
- The recommendation page shows the `自定义偏好` switch directly in the top toolbar. `编辑偏好` opens the custom preference dialog.
- Turning the switch off does not delete the saved text. It only prevents the text from entering the prompt.
- `清空` clears the saved text and turns the custom preference switch off.
- Saving text or toggling the switch does not immediately trigger AI recommendation generation. The status message tells the user the change takes effect on the next refresh or "change batch".
- Prompt integration is auxiliary: enabled non-empty preference text is inserted as a soft preference and is explicitly prohibited from overriding local hard filters.
- System priority remains: local hard filtering / safety rules, user state filters, system recommendation rules, then custom preference prompt guidance.
- Fingerprint integration uses the effective-enabled state, normalized text hash, and update time. Old exact cache / display cache / candidate-pool combinations naturally stop matching after preference changes.
- Candidate-pool refill save paths re-check the current fingerprint, so in-flight old-preference refill results are discarded if the preference changed.
- Preference diagnostics do not write the full preference text or full AI prompt to logs; only enabled state, text length, and a short hash may be recorded.
- RF-2 does not change RF-1 not-interested hard filtering.
- RF-2 does not change player behavior.
- RF-2 does not change Library Batch Ops behavior.
- RF-2 does not change the scan main flow.
- 2026-05-10 log validation confirmed prompt sampling diagnostics, full not-interested hard-filter key diagnostics, candidate-pool consumption, and low-water refill coordination are present and behaving as expected.

Deferred from RF-2:
- Complex preference templates.
- Multiple preference profiles.
- User-profile / statistics fusion.
- Preference history.
- Preference import / export.

Acceptance criteria:
- Users can enter, edit, enable, and disable custom recommendation preferences.
- Preference text is length-limited.
- Enabled preference text participates in the recommendation fingerprint.
- Preference changes mark candidate pools stale.
- The next recommendation batch reflects the changed preference.
- RF-2 remains independent from player, Library Batch Ops, and scan-main-flow changes.
- `dotnet build MediaLibrary.sln` passes with 0 warnings and 0 errors.
- No database schema or migration changes are introduced.

## Explicitly Out Of Scope

- Do not modify the player.
- Do not modify Library Batch Ops.
- Do not modify the scan main flow.
- Do not implement user profiling or statistics charts.
- Do not implement movie discovery or search.
- Do not perform final UI refactoring.
- Do not implement TV-series support.
- Do not implement local-folder support.
- Do not implement online subtitle search.
- Do not rely only on AI prompt filtering for not-interested items.
- Do not add database fields or migrations unless the relevant plan phase explains the reason, compares alternatives, and receives confirmation before implementation.

## Stage Acceptance Criteria

- RF-1 is planned before implementation starts.
- RF-1 adds a local not-interested state path with local filtering fallback.
- RF-1 supports both marking and undoing not-interested state.
- RF-1 prevents not-interested items from appearing in later recommendation results.
- RF-1 marks candidate pools stale or filters them after feedback changes.
- RF-2 starts only after RF-1 is closed.
- RF-2 custom preferences participate in recommendation fingerprinting only when enabled.
- No player behavior changes are introduced by this stage.
- No Library Batch Ops behavior changes are introduced by this stage.
- No scan-main-flow changes are introduced by this stage.
- No database schema or migration changes are introduced unless explicitly justified and approved during the relevant plan phase.
- Stage documents are kept current after each completed sub-stage.
- Recommendation Feedback was closed after RF-1, RF-1.1, RF-2, prompt-sampling adjustment, and log validation.

## Watch Insights WI-R Integration

- Watch Insights profile context is now available to AI recommendations as a soft long-term taste background.
- The recommendation prompt can include a compact profile summary when a valid cached profile exists.
- Recommendation requests do not generate or refresh the Watch Profile; they read cache only.
- Custom recommendation preference remains higher priority than profile context.
- Not-interested remains the local hard filter and is never downgraded by profile context.
- Profile context participates in the recommendation fingerprint through a compact profile fingerprint part.
- Recommendation prompt version also participates in the fingerprint so reason-writing prompt changes invalidate old exact cache / candidate-pool entries.
- Recommendation reasons now use a fuller 70-130 character target and can lightly mention profile fit, while avoiding repeated fixed watched / want-to-watch openings.
- The recommendation prompt JSON output example uses the same 70-130 character reason range.
- Same-version reason reuse remains enabled, but the cache document version was bumped once to clear old reason text after the prompt update.
- Missing or unusable profile cache uses stable `profile:none` and preserves the previous recommendation behavior.
- No recommendation UI, database schema, migration, or home/discovery page behavior changed.
