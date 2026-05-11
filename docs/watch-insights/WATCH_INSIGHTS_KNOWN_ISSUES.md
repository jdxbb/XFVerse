# Watch Insights Known Issues

## Blocker

- No confirmed blocker after WI-4.1 manual log revalidation.

## Deferred

- Library-external collection AI tag snapshot fields are deferred. WI-1 did not add `UserMovieCollectionItem.AiTagsText`, `EmotionTagsText`, or `SceneTagsText`.
- Automatic watched setting storage is deferred. WI-1 did not add an automatic watched switch or setting field.
- Watch history page is not implemented yet. Calendar date click behavior remains a later integration point.
- Profile-driven recommendation is not connected in this stage and should remain a separate later stage.
- Final Watch Insights visual polish is deferred.
- AI profile generation service is implemented in WI-5, and the Profile Analysis Tab is connected to it in WI-6.
- Profile-driven recommendation remains deferred and must be connected only in a later dedicated stage.
- AI profile input intentionally excludes RF-2 custom recommendation preferences.
- AI profile cache uses `WatchInsightCacheEntries` with `kind=profile` and `scopeKey=global`.
- AI profile generation is skipped when signal movies are fewer than 8, signal buckets are fewer than 2, or no usable tags exist.
- AI profile JSON parse failure or AI call failure preserves the previous cached profile when available.
- Watch Profile persona type is restricted to the final fixed 20-type set. Unknown AI persona types fall back to `多元杂食者` with a warning.
- Persona boundary definitions are included in the WI-5 prompt, but persona image/poster assets are still deferred.
- Persona poster/image resources are not implemented. WI-6 displays text-only persona data.
- Profile manual-refresh UI is implemented in WI-6, but final visual polish is still deferred.
- Profile automatic refresh is independent from real-time statistics refresh and remains gated by fingerprint change plus at least 1 day since the last automatic profile refresh.
- Resolved in WI-6.1 UI口径: Watch Statistics status overview no longer displays `未看`.
- Resolved in WI-6.1 status-history correction: status overview no longer uses `Movie.UpdatedAt` or `UserMovieCollectionItem.UpdatedAt` to infer status timing.
- Resolved in WI-6.1 status-history correction: `本月` status overview uses `UserMovieStateChangeHistories` and the small comparison text means `本月比上月`, not `本周比上周`.
- Resolved in WI-6.1 status-history correction follow-up: if a state is marked true and then canceled in the same month, the latest in-month state is false and it no longer counts as a current-month addition.
- Resolved in WI-6.1 status identity follow-up: reset / unidentified placeholder / re-identification paths do not inherit old watched, favorite, want-to-watch, or not-interested state into the newly identified identity.
- Resolved in WI-6.1 status identity follow-up: collection rows linked to unidentified or identification-failed movies are excluded from Watch Statistics unless they already match a stable identified target identity.
- Resolved in WI-6.1 status identity follow-up: moving a marked movie out of the library and scanning it back in does not count as a new status addition because scan only restores the media file's library visibility.
- Resolved in WI-6.1 status identity follow-up: AI assisted/manual re-identification does not create status activation rows. The target movie's existing state is preserved as existing state, not a current-month addition.
- Same-TMDB duplicate merges may preserve state because they represent the same movie identity; different-TMDB reassignment does not transfer watched, favorite, user rating, automatic-watched baseline, or collection status.
- Resolved in WI-6.1 delete-record follow-up: deleting a movie record removes its owned state-history rows, and Watch Statistics ignores orphaned state-history rows left by earlier builds.
- Resolved in WI-6.1 profile fingerprint follow-up: moving a marked movie out of the library preserves state/history and no longer changes the profile fingerprint through collection visibility timestamp churn alone.
- Historical state changes before `20260510163406_AddUserMovieStateChangeHistories` cannot be reconstructed. Existing pre-migration true states count in `全部`, but are not backfilled into `本月新增`.
- Status-history coverage depends on future state changes going through `MovieManagementService`, `UserCollectionService`, or the automatic-watched path. Any future direct field write must add a state-history record in the same transaction.
- Language distribution currently uses the stored `Language` field. A dedicated TMDB `original_language` field is not available yet.
- Library-external collection items still lack AI type/emotion/scene snapshot fields; WI-2 uses collection genres where external tag data is needed.
- Calendar previous/next month controls are visible but disabled in WI-3 because WI-2 only exposes the current-month global statistics snapshot.
- Resolved in WI-6.1: Calendar previous/next month controls now switch the displayed calendar month and remain independent from the statistics time range.
- Resolved in WI-6.1: Calendar heat levels now use fixed thresholds instead of relative monthly maximums.
- Calendar date click only shows a placeholder notice. It does not navigate until the watch history page exists.
- Profile analysis cards are connected to real `WatchProfileSnapshot` data in WI-6; WI-3 only had structural empty/deferred states.
- Profile refresh button is implemented in WI-6 and remains separate from statistics refresh.
- Profile automatic refresh is implemented at the service layer and is invoked only when the profile Tab/page loads, not from statistics real-time refresh events.
- AI profile output quality needs more real-data observation; prompt/schema failures should continue falling back to old cache where available.
- WI-6 UI intentionally does not show persona or DNA confidence. These confidence values are AI/self-estimated or service-normalized fields, not rigorous product metrics.
- WI-6 UI intentionally hides the standalone liked-direction, disliked-direction, future-preference, and caveats cards. Those service fields remain available for later stages.
- Narrative tags are profile-result-only data. They are not stored on `Movie`, and no narrative-tag database field exists.
- Manual profile refresh no longer calls AI when the profile input fingerprint is unchanged and a valid cache exists. A future "force regenerate anyway" control is deferred.
- Profile cache JSON now includes a profile schema version and prompt version. Old caches without these fields remain readable and can be upgraded by manual refresh.
- Same source fingerprint with an old prompt/schema version is not treated as fully current. Page load can show old cache, but manual refresh regenerates once.
- Same source fingerprint plus same prompt/schema version still skips AI on manual refresh to prevent profile copy drift.
- DNA descriptions are normalized for tag repetition and rhythm/exploration score consistency, but AI writing quality still needs real-data observation.
- Taste summary text is intentionally allowed to be longer than persona/DNA descriptions, while keywords remain capped at 6.
- Watch-more-vs-like ranking lists are local statistics, not AI-ranked lists. AI only provides the optional explanation/conclusion.
- WI-6 has an initial profile UI polish pass: DNA uses a 3x2 grid and the quadrant is a positioning map, but final visual design is still deferred.
- WI-6.1 keeps Profile Analysis as a long-term/global profile; statistics time range and calendar month do not affect profile cache or AI refresh.
- WI-6.1 changes profile quadrant X/Y to AI-provided values. Missing or non-numeric X/Y in newly generated AI output is treated as a generation error.
- WI-6.1 invalid persona fallback may make one additional AI call only when AI returns a persona type outside the fixed 20-type set; if that fails, a fixed `多元杂食者` description is used.
- Statistics cache scope is range-aware after WI-6.1. Old `statistics/global` cache rows may remain in the cache table but are no longer used by the range-aware service.
- Resolved in WI-6 profile input filter fix: unidentified or identification-failed movies must not affect profile samples or profile fingerprint through linked collection status.
- Resolved in WI-6 profile semantic fingerprint fix: moving a movie out of the library no longer changes profile fingerprint when profile signals are unchanged.
- Resolved in WI-6 profile semantic fingerprint fix: profile type/emotion/scene distributions no longer include identified movies that have no watched, favorite, want-to-watch, not-interested, or valid-history signal.
- Automatic watched settings UI is still deferred. WI-4 implements the algorithm without adding a user-facing switch or setting storage.
- Old watch history rows are not backfilled. WI-4 only evaluates completion when progress is saved from now on.
- Duration-missing playback cannot be automatically completed; completion detection requires player duration, media-file duration, or movie runtime.
- Completion thresholds may need tuning after real usage: 90% progress, 300-second end tolerance, minimum watch duration, 80% aggregate watched duration, and 70% aggregate max position.
- Aggregate completion can mark `Movie.IsWatched=true`, but a short current run is not marked as `WatchHistory.IsCompleted=true` unless that run itself satisfies the single-session rule.
- Resolved in WI-4 misclassification fix: external player `isCompleted=true` no longer bypasses watched-duration protection.
- Resolved in WI-4 misclassification fix: a pure seek to the ending no longer contributes aggregate `maxPositionSeconds` unless it belongs to an effective watch run.
- WI-4.1 baseline is null for old data by default, so movies that were never manually marked unwatched keep the previous aggregate behavior.
- After manual unwatched, old `WatchHistory` is still retained and still feeds statistics/profile/history, but it no longer feeds automatic watched aggregation.
- If a playback run starts before manual unwatched and continues after it, the run is excluded from aggregate history; it can still complete through the single-run rule if it meets real watched-duration and ending conditions.
- Reset-to-unidentified placeholders do not participate in Watch Statistics because unidentified/no-TMDB movies are filtered out.
- Batch-2 AI assisted identification is distinct from reset-to-unidentified. Successful same-TMDB merges intentionally preserve/move histories according to existing identification semantics.
- Resolved in WI-4.1 Batch-2 compatibility audit: when Batch-2 merges into an existing target movie, `AutoWatchedBaselineAtUtc` is merged by taking the newer baseline.
- Resolved in WI-4.1 manual log revalidation: manual unwatched baseline prevented old watch history from immediately auto-marking watched again.
- Resolved in WI-4.1 manual log revalidation: reset-to-unidentified logged `movedWatchHistory=false` and `reset-resume-cleared`.
- Resolved in WI-4.1 manual log revalidation: Batch-2 AI assisted identification completed successfully for the reset placeholder path without no-result, failure, or cancellation.

## Noise

- WI-1 must not be treated as a UI page implementation phase.
- `WatchHistory.IsCompleted=0` in current local data must not be misread as "no watch data"; later statistics should use actual watched duration, playback start time, and progress-based completion rules.
- Watch Insights cache JSON belongs in `WatchInsightCacheEntries`, not in `ApplicationSetting`.
- Player, Recommendation Feedback, Library Batch Ops, and scan-main-flow issues belong in their own stage documents, not here.
- WI-2 statistics intentionally use `DurationWatchedSeconds > 60` as the valid watch threshold and do not use `WatchHistory.IsCompleted` as the sole source.
- WI-2 does not introduce background timers; later pages should call `IWatchStatisticsService` and let the 12-hour cache rule decide whether to recompute.
- WI-3 is a usable functional UI pass, not final visual polish.
- WI-3 must not be mistaken for AI profile generation; no AI profile data is produced on this page yet.
- Statistics refresh may run automatically after local data changes; this must not be interpreted as permission to call AI for the profile Tab.
- `WatchHistory.IsCompleted` is now improved for new saves, but existing rows with `IsCompleted=0` still need to be interpreted through actual watched duration and progress in statistics.
- `logs/watch-completion.log` is the durable diagnostic source for WI-4 completion decisions; `logs/mpv-playback.log` still records player progress-save inputs.
- Reset recognition should not be interpreted as deleting old movie history. It only changes the reset media file's current movie assignment.

## Maintenance Rules

- Record only issues supported by code, migration output, build output, or manual validation.
- Keep Watch Insights documentation independent from player, Library Batch Ops, and Recommendation Feedback documents.
- Move resolved WI issues into the stage log or mark them resolved here.
