# Library Batch Ops Known Issues

## Blocker

- No confirmed blocker after Batch-2 implementation.

## Deferred

- `移出资源库` and `删除影片记录` can both be rediscovered by a later scan if the physical local file or WebDAV file still exists. Ignore-list and scan-recovery protection are deferred.
- Detail-page `删除影片记录` entry is deferred; Delete UX-1 only adds the batch-mode action.
- Batch-2 does not add a preview confirmation page; the first valid TMDB candidate is applied automatically by design for the first version.
- Batch-2 does not add a complex confidence threshold; it reuses existing candidate ordering and selects the first candidate.
- Final UI refactoring is deferred and out of scope for this stage.
- TV-series support is deferred and out of scope for this stage.
- Local-folder support is deferred and out of scope for this stage.
- Online subtitle search is deferred and out of scope for this stage.
- User profiling and statistics charts are deferred and out of scope for this stage.
- Database fields and migrations are deferred unless a plan phase proves they are required and records the reason first.
- Video-cache cleanup is deferred. `删除影片记录` does not delete local/WebDAV media files and does not clean any physical cache files in this version.
- `UserMovieCollectionItem` does not have fields for favorite or user rating. Moved-out favorite/rated Movies keep those values on `Movie`; resource-library visibility is provided by `LibraryQueryService`, and Favorites visibility for liked items continues to come from `Movie.IsFavorite`.
- Favorites must continue to show all want-to-watch and favorite movies without a library/external split. Do not add a Favorites library-scope filter as part of Delete UX-1.
- A movie removed with `移出资源库` can still affect Watch Insights statistics and WI-5 profile input if it keeps state or WatchHistory, so it must remain discoverable.
- Runtime visual acceptance is still required for the revised equal-width batch toolbar on narrow windows and long localized button text.

- Source-less recognized Movie rows can remain after their original playback source is later corrected to TV. This is currently a product decision: preserve the previously created source-less Movie record instead of auto-deleting or auto-hiding it.

## Noise

- Batch-2 UI stall / temporary non-response during batch AI identification was reproduced in logs as a slow apply-stage issue and then resolved by the 2026-05-09 apply critical-path patch. Follow-up manual validation reported no stall, and logs showed a 4-item batch at 3,281 ms and a 9-item batch at 9,898 ms. Keep the timing logs for regression diagnosis.
- Player issues, R2.1a, mpv, subtitles, audio tracks, and cache behavior are not part of this stage.
- Scan-main-flow behavior is not part of this stage.
- Delete semantics are split: `移出资源库` is the old soft-remove behavior, while `删除影片记录` deletes software records only. Physical local files and WebDAV files must not be deleted by either operation.
- `删除影片记录` removes Movie-level WatchHistory, RatingSources, collection state, current playback-source records, and related subtitle bindings, so Watch Insights will stop counting that Movie after refresh.
- For the WI-4.1 reset-boundary edge where a MediaFile is still referenced by another Movie's retained WatchHistory, `删除影片记录` detaches and marks that media file deleted instead of hard-deleting it.
- `移出资源库` must not make a Movie with watched/favorite/rating/history state unreachable. Such Movies remain visible through resource-library all/status scopes or Favorites where applicable.
- Batch-2 AI-assisted identification does not call the playback-source reset path and does not call the scan identification main flow.
- Batch-2 no-result and failed items preserve existing identification; only items with a valid candidate and readable TMDB details are overwritten.
- Removed-library movie rows with stale MovieId now fall back to collection snapshot identity; if the snapshot itself lacks TMDB/IMDb/title-year identity, only the remaining collection record can be cleaned up.

- The 2026-06-03 investigation found a TV episode-style filename that first passed the movie auto-match path and was later corrected to TV by batch AI. The old source-less Movie row is intentionally retained per the current decision above.

## Maintenance Rules

- Record only issues supported by logs, code paths, or manual validation.
- Do not record temporary guesses as confirmed facts.
- Move resolved issues into the stage log or mark their resolved sub-stage here.
- Keep player-stage issues in the player documentation, not in this stage.
