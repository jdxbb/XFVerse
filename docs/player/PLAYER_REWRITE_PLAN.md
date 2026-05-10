# Player Rewrite Plan

## Current Decision

- The formal target player core is mpv/libmpv.
- Do not fall back to LibVLC as a product strategy.
- Do not restore `VideoCacheProxyService` or the custom HTTP Range proxy.
- The old mpv-4 / mpv-5 / mpv-6 route is paused.
- The active route is R1-R6.
- Old LibVLC, old proxy, and old mpv patch code have been removed from the formal player code path in R5; historical mentions may remain only in docs.

## Stages

- R1: new mpv core minimum playback baseline.
- R2: WebDAV large-file / resume seek / cache range stability.
- R3: subtitle / audio track reconnection.
- R4: close / lifecycle convergence.
- R5: remove old LibVLC / old proxy / old subtitle-audio state.
- R6: product-level cache lifecycle, replacing the old mpv-5 scope in a limited product-management form.

## Current Stage Status

- R1 complete:
  - Added `MpvPlayerSession`.
  - Added `MpvPlaybackEngineAdapter`.
  - Factory routes formal `IPlaybackEngine` creation to the adapter.
  - Supports WebDAV/file load, play/pause/stop/seek, basic events, session id, and `MEDIA_LIBRARY_MPV_HWDEC`.

- R1.1 complete:
  - `LoadAsync` means `loadfile` command submitted.
  - WatchHistory/timer start after Playing.
  - Old subtitle/audio auto initialization is skipped while features are deferred.

- R2 complete as first pass:
  - Delayed resume seek after `PLAYBACK_RESTART`.
  - Cache/range snapshot and resume seek watchdog.
  - One controlled recovery for stalled WebDAV resume seek.

- R3/R3.1/R3.2 complete by current manual validation:
  - Subtitles/audio tracks reconnected to the new core.
  - Track registry prevents menu loss from one incomplete `track-list` update.
  - R3.2 fixes `IsExternal` classification sticking and ViewModel embedded subtitle menu replacement risk.
  - R3.2 embedded subtitle menu stability passed manual validation.
  - The current track-list / subtitle-audio menu strategy is close to mainstream player behavior: observe `track-list`, actively query `track-list`, merge into a session registry, preserve known tracks across incomplete snapshots, and append late tracks.
  - Subtitle/audio switching should not call LibVLC, `VideoCacheProxyService`, `loadfile`, or source switch.

- R4 complete by current manual validation:
  - Adapter Stop/source replacement detach old sessions and dispose them in the background.
  - Session dispose does not synchronously wait for native destroy.
  - Window close releases shell UI immediately after hiding the player.
  - WatchHistory save during shutdown has a short timeout.

- R5 complete by build validation and current manual regression:
  - Removed LibVLC package references and legacy LibVLC playback code from the app.
  - Removed `VideoCacheProxyService`, its interface/result model, and the old segment proxy service registration.
  - Removed segment-proxy-only cache APIs from `IVideoCacheService` / `VideoCacheService`.
  - Preserved new mpv core, full-file cache hit logic, cache settings/usage/clear, WatchHistory, playback source, ffprobe/MediaProbe, and subtitle scan/candidate business.
  - R5 validation passed: the old LibVLC / proxy cleanup did not block the current playback regression scope.
  - R5 does not handle large WebDAV performance or product-level cache lifecycle; those remain R2.1/R6 scopes.

- R2.1 initial implementation is isolated by default after manual regression:
  - Added startup phase breakdown logs for native/session creation, loadfile submit/return, `FILE_LOADED`, `PLAYBACK_RESTART`, first duration, first position, first cache state, and resume seek healthy.
  - Added cache effective option audit logs without recording paths or URLs.
  - The risky seek/watchdog/recovery parts of R2.1 are now behind `MEDIA_LIBRARY_MPV_R21_ENABLED=1`.
  - Default behavior restores the R5/R2 seek baseline: no R2.1 playback watchdog/recovery, no keyframe startup resume seek, and the original R2 resume movement heuristic.
  - The HEVC 4K WebDAV hwdec safety policy remains enabled by default because logs showed `hwdec=auto-safe` produced D3D11/DXVA/ffmpeg-video errors and black screen.
  - `MEDIA_LIBRARY_MPV_HWDEC` remains the manual override.
  - R2.1 caused a 10GB-range WebDAV startup regression during manual validation; it should be redesigned before being enabled by default again.
  - R2.1 does not implement product-level cache lifecycle; that remains R6.

- R2.1a complete by current manual validation:
  - The focused scope is WebDAV resume startup alignment with `mpv.exe --start=<seconds>`.
  - For WebDAV sources with `StartPositionSeconds > 0`, the new core now first tries a load-time `start=` option on `loadfile`.
  - If the load-time start option succeeds, the old post-`PLAYBACK_RESTART` delayed seek is skipped.
  - If the start option command fails, the code falls back to the previous R2 delayed seek path.
  - R2.1a does not change cache parameters, hwdec defaults, subtitle/audio behavior, shutdown, or product cache lifecycle.
  - Manual validation covered 9GB / 13.8GB / 70GB WebDAV sessions; all started and advanced with no `CacheRangeNotRecovered`.
  - 70GB long-duration playback stutter remains Deferred and is not considered fully solved by R2.1a.

- R6.1 complete by build validation; manual validation pending:
  - Video cache usage is now reported by category: full-file cache, mpv-session cache, and legacy segment residue.
  - Settings still uses the existing video cache entry, now with category breakdown text.
  - R6.1 does not change playback, mpv cache options, clear behavior, active cache leases, or LRU cleanup.
  - Active cache protection, safe clear, and capacity cleanup remain R6.2/R6.3.

- R6.2 complete by build validation; manual validation pending:
  - WebDAV direct playback now uses a managed mpv-session cache directory with a short-lived active marker.
  - Local full-file playback clears `demuxer-cache-dir` and keeps `cache-on-disk=no`.
  - Clear cache now deletes non-active full-file, mpv-session, and legacy residue while skipping active cache.
  - Stop/Close/Dispose only release the active marker and still do not synchronously delete large cache directories.
  - R6.2 does not implement capacity LRU.

- R6.3-lite complete by build validation; manual validation pending:
  - mpv-session statistics exclude `.active.json`, so the active marker is not displayed as playback cache data.
  - Empty non-active mpv-session directories are cleaned during usage refresh / cleanup.
  - Settings text now clarifies that mpv-session cache means playback-period disk cache and does not include mpv memory / demux cache.
  - R6.3-lite does not change playback logic, R2.1a start-option resume, subtitles/audio, Stop/Close lifecycle, or cache parameters.
  - Full-file cache hit playback remains available, and the old write infrastructure remains in code.
  - There is no current product entry to create new full-file cache items; active full-file download, automatic caching, offline cache, partial range cache, and complex download queues are deferred to a future R7 / cache-enhancement stage.

- Final full regression is in progress:
  - Build/source/log audit confirms the R1-R6 code paths remain present and the old LibVLC / old proxy path has not returned.
  - Recent logs show R2.1a WebDAV resume using load-time `start=` and skipping delayed seek.
  - Manual validation matrix is still required before declaring the player mainline closed.
  - Final manual validation should still be used as the player core closeout gate.

- Player UX-2 complete by build validation; manual validation pending:
- Player UX-2 / UX-2.3 prompt state follow-up complete for the current validated scope:
  - Added a unified main playback UI state projection.
  - Split player prompts into bottom main playback status, temporary operation notices, and a central buffering overlay.
  - Added a startup progress gate so early `Playing` / WatchHistory readiness events do not hide the central prompt before the first real `time-pos`.
  - Kept legacy `StatusMessage` as an internal compatibility field for staged cleanup.
  - Did not change playback core, R2.1a start option, subtitles/audio business logic, cache lifecycle, DB schema, or migrations.
  - Manual validation passed for the startup black-screen waiting prompt behavior.
  - Player UX-1 global volume / brightness memory remains a standalone follow-up. Subtitle/audio memory remains out of scope.

- Player UX-1 complete by build validation; manual validation pending:
  - Added global player preference persistence for volume, muted state, and brightness.
  - Preferences are stored in a local JSON file, not in the database.
  - The preferences are global player defaults, not per-video or per-source settings.
  - User changes are saved with debounce and a short close-time fallback save.
  - Playback core, R2.1a start option, subtitles/audio, WatchHistory progress, cache lifecycle, DB schema, and migrations were not changed.
  - Subtitle/audio memory and same-video unified playback progress remain out of scope.

- Playback History UX-1 / UX-1.1 complete by build validation and current manual follow-up:
  - Same `MovieId` compatible playback sources now share the latest valid resume position at read/projection time.
  - WatchHistory saving still writes only the current `MovieId + MediaFileId`; no movie-level history table was added.
  - Existing source-specific records remain compatible because unified progress is computed from current source-list records.
  - Duration compatibility is required before applying a different source resume point: sources are rejected when duration differs by more than 60 seconds and more than 2%.
  - Missing duration is handled conservatively: cross-source resume is skipped, while source-specific resume can still be used.
  - Latest completed history clears shared resume projection for the current movie source list.
  - Current player-session source list projection is updated for compatible sources when progress is saved.
  - Home recently played / Continue Playback now follows the latest actual `WatchHistory.MediaFileId` strictly and does not use cross-source compatible progress fallback.
  - New WatchHistory rows initialize `LastPlayPositionSeconds` from the source resume position used to open playback, so very fast close does not leave the latest recent-played row at 0 seconds.
  - Normal periodic / close-time progress saves still overwrite the initial resume position with the latest real playback position.
  - Playback core, R2.1a start option, subtitles/audio, cache lifecycle, DB schema, and migrations were not changed.

- Next stage decision:
  - Player mainline R1-R6 / R2.1a / Final / UX follow-ups in this route are complete for the current accepted scope.
  - Continue observing 70GB long-duration playback stutter as Deferred.
  - Subtitle/audio memory remains a separate deferred experience stage.
  - Future cache enhancements such as active full-file caching, offline cache, partial range cache, and download queues remain outside this completed player mainline.

## Stage Baseline Development Mode

- Each stage only handles its declared target.
- Out-of-stage issues go to `PLAYER_KNOWN_ISSUES.md`; do not fix them opportunistically.
- At stage completion, output:
  - changed files,
  - target completion status,
  - not-done items,
  - `dotnet build MediaLibrary.sln` result,
  - key logs,
  - manual validation matrix,
  - Known Issues update,
  - whether to proceed to the next stage.
- Update `PLAYER_STAGE_LOG.md` after every stage.
- Update this plan only when route, scope, or stage boundaries change.
- Update `PLAYER_KNOWN_ISSUES.md` only for important evidence-backed issues.
- Do not write temporary guesses as facts.
- Documentation does not replace build and manual validation.
- Keep each document short; archive old content when it grows beyond about 200 lines.
