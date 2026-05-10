# Player Known Issues

## Blocker

- No confirmed R4 blocker after current manual validation.
- No confirmed R3.2 embedded subtitle menu blocker after current manual validation.
- No confirmed R5 blocker after current manual validation.
- No confirmed R2.1a blocker after current manual validation.
- No confirmed Final blocker from the current build/source/log audit. Final manual validation is still pending.
- No confirmed Playback History UX blocker after the latest-source recent playback follow-up.

## Deferred

- Default hwdec can stall on HEVC 4K large WebDAV videos; `MEDIA_LIBRARY_MPV_HWDEC=off` was previously verified as a useful diagnostic path. The HEVC 4K WebDAV hwdec safety policy remains enabled by default, with `MEDIA_LIBRARY_MPV_HWDEC` still available as a manual override.
- 70GB+ WebDAV long-duration playback can still stutter. R2.1a only validates resume startup and does not claim this is solved.
- Large WebDAV resume seek can expose cache/range edge cases. R2.1a reduces the reproduced resume-start failure by using load-time `start=`, but long-duration large-video behavior should continue to be observed.
- Large WebDAV embedded subtitle switching can still interact with cache/range behavior; treat further tuning as R2.1/R3 follow-up only if reproduced with evidence.
- Product-level video cache lifecycle is intentionally limited in R6:
  - R6.1 adds cache category statistics.
  - R6.2 adds active mpv-session marker protection plus safe clear for non-active cache.
  - R6.3-lite excludes active markers from mpv-session statistics, cleans empty non-active mpv-session directories, and clarifies settings text.
  - Full-file cache hit playback remains available, but the current product has no active entry point for creating new full-file cache items.
  - Active full-file caching, automatic caching, offline cache, partial range cache, playback reuse of partial ranges, and complex download queues are deferred to a future R7 / cache-enhancement stage.
- Playback preference memory outside UX-1 remains deferred:
  - Subtitle/audio memory remains out of scope.
- UX-2 keeps legacy `StatusMessage` as an internal compatibility field while XAML uses the new display projection. A later cleanup can remove residual writes after manual prompt-state validation.

## Resolved / Validated

- R3/R3.1/R3.2 subtitles and audio tracks are manually validated as acceptable for the current stage:
  - Audio switching is acceptable.
  - External subtitles are normal.
  - Embedded subtitle menu stability passed R3.2 validation: known embedded tracks are preserved across incomplete `track-list` snapshots and late tracks can be appended.
  - Subtitle/audio switching should not use LibVLC.
  - Subtitle/audio switching should not use `VideoCacheProxyService`.
  - Subtitle/audio switching should not trigger `loadfile` or source switch.
- R2.1a WebDAV resume startup is manually validated for the current stage:
  - WebDAV resume uses load-time `start=` when supported by `loadfile`.
  - 9GB / 13.8GB / 70GB WebDAV validation sessions all started and advanced.
  - No `CacheRangeNotRecovered` was observed in those validation sessions.
  - 70GB long-duration playback stutter remains Deferred and is not marked resolved.
- R6.3-lite cache statistics and settings wording are build-validated:
  - mpv-session active markers are not counted as playback disk cache data.
  - Empty non-active mpv-session directories are cleaned during usage refresh / cleanup.
  - Settings wording distinguishes mpv playback-period disk cache from mpv memory / demux cache.
- Final build/source/log audit is complete:
  - Build passes with 0 warning / 0 error.
  - Recent logs show R2.1a load-time start option in use with delayed seek skipped.
  - No recent `CacheRangeNotRecovered`, `END_FILE reason=error`, old LibVLC, or old proxy log entries were found in the sampled log window.
- UX-2 UI prompt state convergence is build-validated:
  - Bottom main playback status uses the new `DisplayStatusText` projection.
  - Subtitle/audio/cache style feedback uses temporary operation notices.
  - Real buffering uses a central buffering overlay projection.
  - Playback core, subtitles/audio command behavior, R2.1a, and cache lifecycle were not changed.
- UX-2.3 startup prompt follow-up is manually validated:
  - The central startup prompt stays visible during the black-screen startup gap until the first real playback position arrives.
  - Early `Playing` / WatchHistory readiness events no longer hide the prompt before real playback progress.
  - Playback core, WatchHistory storage behavior, subtitles/audio, R2.1a, cache lifecycle, DB schema, and migrations were not changed.
- UX-1 global player preference memory is build-validated:
  - Global volume, muted state, and brightness are restored for newly opened players.
  - Preference writes use local JSON storage with debounce and a close-time fallback save.
  - No database schema, migration, playback core, R2.1a, subtitle/audio, cache lifecycle, or WatchHistory progress behavior was changed.
- Playback History UX-1 / UX-1.1 playback progress behavior is build-validated:
  - Same `MovieId` compatible playback sources share the latest valid resume position through read-time projection.
  - Saving still writes only the current `MovieId + MediaFileId`; no database schema, migration, or movie-level history table was added.
  - Duration-incompatible sources and missing-duration cross-source cases are skipped conservatively.
  - Home recently played / Continue Playback uses the latest actual `WatchHistory.MediaFileId` and does not fall back to another compatible playback source.
  - New WatchHistory rows initialize from the source resume position used to open playback, while normal save still updates to the final playback position.

## Noise / Confirmed Non-Causes

- The recent formal mpv playback path does not call `VideoCacheProxyService`.
- The recent formal mpv playback path does not use LibVLC to start playback.
- R5 removed the app LibVLC package references, old LibVLC playback code, `VideoCacheProxyService`, and old segment-proxy cache APIs from source. This is build-validated and passed current manual regression.
- `sid` commands are usually fast; the old subtitle stalls were not caused by slow C# command dispatch.
- `MEDIA_LIBRARY_MPV_SUBTITLE_PREROLL=no/index/yes` did not materially change the old large WebDAV embedded subtitle stall.

## Maintenance Rules

- Record only issues supported by logs, code paths, or manual validation.
- Do not record temporary guesses as facts.
- Move resolved issues into the stage log or mark their resolved stage here.
- Stage-out-of-scope issues are recorded here and are not fixed opportunistically.
