# Library Batch Ops Stage Log

## 2026-05-08 Stage Start

Goal: start the Library Batch Ops stage after the player mainline closeout, with the resource library as the active product area.

Stage boundaries:
- This stage is independent from the player stage.
- This stage does not modify R2.1a / mpv / subtitles / audio tracks / cache.
- This stage does not modify the scan main flow.
- This stage does not perform final UI refactoring.
- This stage does not add database fields or migrations unless a plan phase proves it is required and records the reason first.

Initial sub-stage split:
- Batch-1: batch status operations.
- Batch-2: AI-assisted identification.

Result:
- Created the Library Batch Ops documentation set.
- Entered Batch-1 Plan.

Build:
- Not run. Documentation initialization does not require `dotnet build`.

## 2026-05-08 Batch-1 Plan Entered

Goal: plan resource-library batch status operations before implementing code.

Batch-1 planned scope:
- Resource-library batch selection mode.
- Batch mark as watched.
- Batch mark as unwatched.
- Batch delete media-library records.
- Second confirmation before deletion.
- Reuse single-item status overwrite rules.
- Batch mark as watched does not write watch time.

Current status:
- Waiting for the detailed Batch-1 Plan prompt.

Implementation:
- Not started.

## 2026-05-08 Batch-1 Implementation

Goal: implement minimum resource-library batch status operations without expanding scope.

Changed behavior:
- Added resource-library batch selection mode.
- Added selected-count display and batch operation buttons.
- Added batch mark as watched and batch mark as unwatched using existing single-item watched-state services.
- Added batch delete records with a required confirmation dialog.
- Batch delete updates media-library database records only and does not delete local files or WebDAV files.
- Partial failures keep failed items selected and show a result summary.

Boundaries kept:
- No player changes.
- No scan-main-flow changes.
- No AI-assisted identification.
- No database fields or migrations.
- No physical file deletion.

Build:
- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## 2026-05-10 Delete UX-1 Documentation Closeout

Confirmed semantics:
- `移出资源库` is a soft move-out operation. It does not delete Movie, MediaFile history, WatchHistory, RatingSources, user state, local files, or WebDAV files.
- `删除影片记录` deletes the movie's software records, including Movie-level history/state/ratings/source records, but still does not delete local files or WebDAV files.
- Movies moved out of the resource library but still carrying user state or WatchHistory must remain discoverable from the resource library or Favorites.
- Favorites shows all want-to-watch and favorite movies without separating library and external items.
- A movie should disappear from the resource library, Favorites, Watch Insights statistics, and future profile inputs only after `删除影片记录`.
- Later scans may rediscover existing files. Ignore-list and scan-recovery protection remain deferred.

Watch Insights relation:
- Soft moved-out movies with retained history/state can still contribute to Watch Insights statistics and WI-5 profile input, so they must remain visible to the user.
- Deleted movie records remove the corresponding software data, so Watch Insights no longer counts them after refresh.

## 2026-05-10 Delete UX-1 Polish: Unidentified Library Row De-duplication

Goal: fix a validation case where two similarly named playback sources appeared as one resource-library row while both were still unidentified.

Changed behavior:
- Resource-library de-duplication now keeps in-library Movies without TMDB/IMDb identity keyed by `MovieId`.
- Identified in-library Movies still de-duplicate by TMDB or IMDb identity.
- External collection-only rows keep the previous TMDB/IMDb/title-year matching behavior.

Reason:
- Before this polish, two unidentified in-library placeholder Movies with the same parsed title and year could be grouped into one row.
- After one placeholder was identified, its key changed to TMDB identity, making the other placeholder appear later. The underlying records were intact; the issue was list-level grouping.

Boundaries kept:
- No database fields or migrations.
- No scan-main-flow changes.
- No identification/apply logic changes.
- No player, recommendation, Watch Insights statistics, or auto-watched algorithm changes.

## 2026-05-10 Delete UX-1 Bug Fix: Removed Library State Visibility

Goal: fix a validation case where `移出资源库` preserved Movie state/history in the database but left some removed movies without a visible resource-library or collection entry.

Root cause:
- `移出资源库` soft-deleted active `MediaFile` rows from the visible library but did not always create/update a `UserMovieCollectionItem`.
- `LibraryQueryService` previously loaded only Movies with active video sources plus external collection rows.
- Therefore a removed Movie with `Movie.IsWatched`, `Movie.IsFavorite`, `Movie.UserRating`, or `WatchHistory` could still affect Watch Insights while becoming difficult to find.

Changed behavior:
- `RemoveFromLibraryAsync` now checks whether the Movie has watched/favorite/rating/history or collection state before moving it out.
- If the Movie has external collection state or watched state, it creates/updates a `UserMovieCollectionItem` with `IsInLibrary=false` and preserves watched / want-to-watch / not-interested flags.
- Favorites are not forced into collection rows; the Favorites page already reads `Movie.IsFavorite`, including moved-out Movies.
- `LibraryQueryService` now includes moved-out Movies that still have watched, favorite, user rating, WatchHistory, or linked collection state.
- Movies without user state and without history can still disappear after `移出资源库`.

Boundaries kept:
- `删除影片记录` behavior is unchanged.
- No physical file or WebDAV deletion.
- No database fields or migrations.
- No player, recommendation-system, Watch Insights statistics, auto-watched, scan-main-flow, or final UI refactor changes.

Build:
- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## 2026-05-08 Batch-1 Polish

Goal: apply small experience fixes after Batch-1 manual validation.

Changed behavior:
- Batch mark watched, batch mark unwatched, and batch delete now exit batch selection mode and clear selection after at least one item succeeds.
- If every selected item fails, batch selection mode stays active and failed items remain selected for retry.
- Canceling the delete confirmation keeps batch selection mode and the current selection unchanged.
- In batch selection mode, the poster card and list item cursor now matches the clickable selection area.

Boundaries kept:
- No watched-state rule changes.
- No delete semantics changes.
- No confirmation dialog text changes.
- No database fields or migrations.
- No player, scan-main-flow, Batch-2, or AI-assisted identification changes.

Build:
- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## 2026-05-08 Batch-2 Implementation

Goal: implement AI-assisted batch identification from the resource library without expanding Batch-1, player, or scan-main-flow scope.

Changed behavior:
- Added a resource-library batch `AI 辅助识别` action.
- Added cancellation for the batch AI identification operation.
- Batch identification runs sequentially and records success, no result, failed, and canceled counts.
- The final batch summary explicitly states that no-result movies keep their original identification.
- At least one successful item exits batch selection and clears selection when the operation was not canceled.
- All failed/no-result operations keep batch mode and selection for retry.
- Cancellation keeps unsuccessful and unstarted items selected.

Service behavior:
- Added service-layer automatic identification through `AutoIdentifyWithFirstResultAsync`.
- The service first generates an AI search query, then searches TMDB, then reads TMDB details.
- Database writes start only after a valid first candidate and readable TMDB details exist.
- First version automatically applies the first candidate returned by existing candidate ordering.
- Reset/apply writes use a DbContext transaction for the automatic path.
- After write begins, cancellation is ignored until the current item completes or rolls back, then remaining items are skipped.

Boundaries kept:
- No database fields or migrations.
- No preview confirmation page.
- No complex confidence threshold.
- No player changes.
- No scan-main-flow changes.
- No calls to `ResetMediaFileToUnidentifiedAsync`.
- No calls to `IdentifyMediaFilesAsync`.
- No physical file deletion.

Build:
- Initial implementation check: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.
- Final validation: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## 2026-05-08 Batch-2 Performance Patch

Goal: reduce the chance of short UI stalls after batch AI identification and add enough timing data to diagnose future reports.

Changed behavior:
- Added Batch-2 timing events to `ai-perf-debug.log` for batch start/complete, item start/complete, AI suggestion, TMDB search, apply, and resource-library refresh.
- Batch-2 now suppresses the current resource-library view's own metadata/collection refresh notifications after it has already refreshed itself once.
- Batch-2 success still notifies metadata and collection changes so other pages can update lightweight state.
- Batch-2 no longer sends an immediate recommendation-changed notification after successful identification, avoiding an immediate full AI recommendation reload from this path.
- Recommendation data can still refresh through the recommendation page's existing load/refresh paths.

Boundaries kept:
- No identification rule changes.
- No reset/apply transaction changes.
- No candidate sorting changes.
- No recommendation algorithm changes.
- No candidate-pool strategy changes.
- No player changes.
- No database fields or migrations.
- No resource-library UI refactor.

Build:
- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation note:
- This patch improves observability and removes one duplicate-refresh path, but it does not claim the intermittent UI stall is fully resolved until manual validation confirms it.

## 2026-05-09 Batch-2 Apply Critical-Path Patch

Goal: address a manual validation case where selecting four movies for Batch-2 AI-assisted identification took about 127 seconds, with every item spending about 30 seconds in the apply stage.

Changed behavior:
- Batch-2 automatic first-candidate apply now skips OMDb rating lookup inside the apply transaction.
- Manual metadata correction and scan identification keep the existing OMDb rating behavior by default.
- Added Batch-2 timing events for TMDB detail read, apply start, OMDb skip, apply DB work, and transaction commit.
- Batch-2 still applies TMDB metadata and the first sorted candidate exactly as before.

Boundaries kept:
- No identification rule changes.
- No candidate sorting changes.
- No recommendation algorithm changes.
- No player changes.
- No scan-main-flow changes.
- No database fields or migrations.

Validation note:
- Manual validation after this patch reported that Batch-2 no longer stalls.
- The follow-up logs support the validation result: a 4-item batch completed in 3,281 ms with apply stages at 136 ms, 11 ms, 16 ms, and 6 ms; a 9-item batch completed in 9,898 ms with 7 successes and 2 no-result items.
- Resource-library refresh stayed lightweight at 4 ms and 2 ms in those two runs.
- Recommendation refresh remained deferred from the Batch-2 path.
- Keep the segmented timing logs in place to detect regressions, but the Batch-2 apply-stage stall is considered resolved by this validation.

## 2026-05-10 Delete UX-1: Delete Semantics Split

Goal: split the previous batch "delete record" semantics into a safer soft-remove action and a separate software-record delete action.

Changed behavior:
- Renamed the original batch `删除记录` action to `移出资源库`.
- Updated the soft-remove confirmation text to state that local files, WebDAV files, and playback history are not deleted.
- Added a new batch `删除影片记录` action with its own destructive confirmation dialog.
- Added service-layer software-record deletion for Movie-level records.
- Added collection-only deletion for external UserMovieCollectionItem rows without a MovieId.
- Added lightweight diagnostics for library remove and movie-record delete batch operations.

`移出资源库` semantics:
- Reuses the previous `RemoveFromLibraryAsync` / `RemoveCollectionRecordAsync` path.
- Marks library media-file records as removed from the visible library list or removes external collection state.
- Does not delete Movie, WatchHistory, RatingSources, local files, or WebDAV files.
- Later scans may rediscover physical files.

`删除影片记录` semantics:
- Deletes the selected Movie software record, current MediaFiles, WatchHistories for that Movie, RatingSources, related UserMovieCollectionItems, and subtitle bindings attached to the Movie's media files.
- Does not delete local files or WebDAV files.
- For external rows without MovieId, deletes only the matching UserMovieCollectionItem.
- Resource-library cards are Movie-level rows, so multi-source library rows delete the whole Movie record.
- If a media file is still referenced by another Movie's retained WatchHistory, the media file is detached and marked deleted rather than hard-deleted, preserving that other Movie history.

Boundaries kept:
- No database fields or migrations.
- No physical file deletion.
- No WebDAV deletion.
- No ignore-list or scan-main-flow changes.
- No player, recommendation-system, Watch Insights statistics, or auto-watched algorithm changes.
- No detail-page delete entry.

Build:
- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.
