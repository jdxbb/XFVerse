# Library Batch Ops Plan

## Stage Goal

Library Batch Ops focuses on resource-library batch operations after the player mainline has been closed. The stage adds batch selection and batch media-record operations first, then adds AI-assisted identification in a separate sub-stage.

Primary goals:
- Support batch selection mode in the resource library.
- Support batch mark as watched.
- Support batch mark as unwatched.
- Split batch delete semantics into "remove from library" and "delete movie record".
- Later support AI-assisted identification for one or more movies.
- Keep this stage independent from player work and scan-main-flow work.

## Sub-Stages

### Batch-1: Batch Status Operations

Status:
- Implemented and validated.

Scope:
- Add resource-library batch selection mode.
- Support selecting one or more library items.
- Support batch mark as watched.
- Support batch mark as unwatched.
- Support batch deletion of media-library records.
- Require a second confirmation before batch deletion.
- Reuse the same watched/unwatched overwrite rules as the single-item operation.
- Batch mark as watched must not write watch time.
- Batch delete removes media-library records only by updating database state; it must not delete local files or WebDAV files.

Plan notes:
- Prefer existing resource-library ViewModel/service patterns.
- Prefer reusing existing single-item watched-state and deletion behavior where possible.
- Deleting records in this stage means removing media-library records only.
- Do not delete local files or WebDAV files.
- Do not add database fields or migrations unless the Batch-1 plan proves it is required and records the reason first.
- Because scan-main-flow changes are out of scope, a future scan can rediscover a physically present deleted file.

Acceptance criteria:
- Users can enter and exit batch selection mode from the resource library.
- Users can select multiple movie/library records.
- Batch mark as watched applies the same status semantics as the current single-item operation.
- Batch mark as unwatched applies the same status semantics as the current single-item operation.
- Batch mark as watched does not write or synthesize watch time.
- Batch deletion requires explicit confirmation.
- Batch deletion removes selected media-library records only and does not delete physical files.
- Empty selection and partial-failure cases are handled without corrupting unrelated records.
- No player behavior changes.
- No scan-main-flow changes.
- No database schema or migration changes unless separately approved after plan evidence.

### Batch-2: AI-Assisted Identification

Status:
- Implemented in the current Batch-2 pass; final build validation is required before closeout.

Scope:
- Support selecting a single movie or multiple movies from the resource library.
- For each selected movie, run AI + TMDB assisted identification first.
- If there are no identification results, do not reset the current identification.
- If there is at least one valid result, reset the previous identification and automatically select the first result.
- Each item should fail independently.
- Support cancellation.
- Show progress and a result summary.

Plan notes:
- Identification must be result-first, reset-second.
- Existing identification should be preserved when no valid result is found.
- Batch progress and final summary should distinguish success, no result, failed, and canceled items.
- Do not fold this into Batch-1 implementation.
- First version reuses the existing AI search-query generation service.
- First version reuses `MovieIdentificationService.SearchCandidatesAsync` ordering.
- First version automatically applies the first returned candidate.
- No preview confirmation page is added in Batch-2.
- No complex confidence threshold is added in Batch-2.
- Database fields and migrations are not added.
- Player and scan main flow are not modified.

Safety rules:
- AI search-query generation and TMDB search/detail lookup run before any database write.
- If AI returns no usable query, AI fails, TMDB returns no candidate, or TMDB detail lookup fails, the current identification is preserved.
- Previous identification is overwritten only after a valid TMDB candidate and readable TMDB details exist.
- Batch-2 must not call `ResetMediaFileToUnidentifiedAsync`.
- Batch-2 must not call `IdentifyMediaFilesAsync`.
- Reset/apply details belong in the service layer, not in `LibraryViewModel`.
- Reset/apply writes should run inside one DbContext transaction where possible.

Cancellation:
- Batch identification is sequential, not parallel.
- Cancellation stops before starting unnecessary remaining items.
- Once a selected item has entered the write transaction, cancellation is ignored until that item finishes so the transaction can complete or roll back cleanly.

Acceptance criteria:
- Batch identification can run for one or more selected movies.
- Existing identification is not reset when no result is found.
- Existing identification is reset only after a valid result exists.
- The first valid result is selected automatically.
- One item failure does not stop unrelated items unless the operation is canceled.
- Cancellation leaves already-completed items in their completed state and avoids starting unnecessary remaining work.
- Progress and final summary are visible to the user.

### Delete UX-1: Delete Semantics Split

Status:
- Implemented in the current Delete UX-1 pass; build validation is required before closeout.

Scope:
- Rename the original batch "delete record" action to `移出资源库`.
- Keep `移出资源库` on the existing soft-remove path: media files are hidden from the library list, but Movie, WatchHistory, RatingSources, and physical files are retained.
- Add a separate `删除影片记录` batch action.
- Require a separate confirmation dialog for each delete-related operation.
- Delete movie records from software data only: Movie, its current MediaFiles, WatchHistories for that Movie, RatingSources, UserMovieCollectionItems, and subtitle bindings attached to those media files.
- Never delete local files or WebDAV files.
- Notify app data changes so the library, collection, recommendation surfaces, and Watch Insights cache/fingerprint paths can refresh naturally.

Product semantics:
- `移出资源库` means "remove from the current library list". Later scans may rediscover the same physical file.
- If `移出资源库` leaves watched, favorite, user rating, WatchHistory, or collection state on a Movie, that Movie must remain findable through the resource library or Favorites where applicable.
- `删除影片记录` means "remove this movie's software record". The movie no longer contributes to resource-library rows, Watch Insights statistics, or future profile input because its Movie-level history and state are deleted.
- For external collection-only rows without a MovieId, `删除影片记录` removes the UserMovieCollectionItem only.
- For library rows, resource-library cards are Movie-level rows, so `删除影片记录` deletes the whole Movie record and all of that Movie's current playback-source records.
- If an edge-case media file is still referenced by another Movie's historical WatchHistory, it is detached and marked deleted instead of being physically or database-deleted, preserving the other Movie's history.

Boundaries:
- No physical file deletion.
- No WebDAV deletion call.
- No ignore-list or scan-recovery protection.
- No scan-main-flow changes.
- No player, recommendation, Watch Insights statistics, or auto-watched algorithm changes.
- No database fields or migrations.
- No detail-page delete entry.

Acceptance criteria:
- Batch mode shows both `移出资源库` and `删除影片记录`.
- Both actions are disabled without selection and while a batch operation is running.
- Each action has its own confirmation dialog and cancellation leaves data unchanged.
- `移出资源库` keeps the previous soft-remove behavior and preserves playback history.
- `删除影片记录` removes the movie's software records and playback history without touching local/WebDAV files.
- Partial failures keep failed items selected and report a result summary.
- Build remains 0 warning / 0 error.

## Explicitly Out Of Scope

- Do not modify the player.
- Do not modify R2.1a / mpv / subtitle / audio-track / cache behavior.
- Do not modify the scan main flow.
- Do not perform final UI refactoring.
- Do not implement TV-series support.
- Do not implement local-folder support.
- Do not implement online subtitle search.
- Do not implement user profiling or statistics charts.
- Do not add database fields or migrations unless the plan phase proves it is required and the reason is recorded first.
- Do not delete physical files. Delete-related batch actions only update software records and must not delete local files or WebDAV files.

## Stage Acceptance Criteria

- Batch-1 is implemented and validated independently from player and scan-main-flow code.
- Batch-2 is implemented only after Batch-1 plan and implementation are closed.
- All destructive record operations require confirmation.
- Physical files are never deleted by this stage.
- Watched-state operations reuse current single-item semantics.
- Batch watched operations do not write watch time.
- AI-assisted identification preserves existing identification when no valid result exists.
- Delete semantics distinguish `移出资源库` from `删除影片记录`.
- No database schema or migration changes are introduced unless explicitly justified and approved during the relevant plan phase.
- Stage documents are kept current after each completed sub-stage.
