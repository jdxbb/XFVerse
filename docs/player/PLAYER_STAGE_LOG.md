# Player Stage Log

## 2026-06-17 - Fullscreen Icon Polish

Goal:
- Update the player fullscreen toggle iconography to the requested Phosphor simple-arrow icons without changing playback behavior.

Completed:
- Changed the collapsed/fullscreen entry icon to `arrows-out-simple`.
- Changed the active fullscreen exit icon to `arrows-in-simple`.
- Kept the existing fullscreen toggle button size, shortcut behavior, and window-state logic.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:
- Blocker: none.
- Deferred: manual playback-window verification is still needed for hover/pressed visuals and state transitions.
- Noise: none.

## R1

Goal: introduce the new mpv core in parallel and establish the minimum playback baseline.

Changed files:
- `MpvPlayerSession.cs`
- `MpvPlayerSessionFactory.cs`
- `MpvLoadRequest.cs`
- `MpvPlaybackState.cs`
- `MpvSessionEventArgs.cs`
- `MpvSeekKind.cs`
- `MpvPlaybackEngineAdapter.cs`
- `MpvPlaybackEngineFactory.cs`

Result:
- The formal path uses the new mpv core through the existing `IPlaybackEngine` adapter.
- Old `MpvPlaybackEngine`, LibVLC, and `VideoCacheProxyService` remain in the tree but are not deleted in R1.
- R1 only establishes load/play/pause/stop/seek, basic events, session id, and dispose baseline.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Not done: subtitles, audio tracks, large WebDAV stability, product cache lifecycle.

## R1.1

Goal: fix new-core load semantics and the old ViewModel consumption path.

Changed files:
- `IPlaybackEngineFeatureFlags.cs`
- `MpvPlaybackEngineAdapter.cs`
- `PlayerWindowViewModel.cs`

Result:
- `LoadAsync` means the `loadfile` command was submitted, not that media is already playing.
- WatchHistory and timer start after the Playing event.
- R1 adapter marked subtitle/audio features as deferred.
- R1 skipped old subtitle/audio auto initialization.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## R1 Shutdown Blocker

Goal: fix close hang / Ctrl+C cannot exit after closing the player.

Changed files:
- `MpvPlaybackEngineAdapter.cs`
- `MpvPlayerSession.cs`

Result:
- Adapter dispose performs quick detach.
- Session dispose uses background best-effort native destroy.
- Added dispose detach / event-loop cancel / native destroy logs.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## R2

Goal: stabilize WebDAV large-file resume seek and cache range behavior in the new mpv core.

Changed files:
- `MpvPlayerSession.cs`
- `MpvCacheStateSnapshot.cs`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- WebDAV resume seek is applied after `PLAYBACK_RESTART`.
- `loadfile result=0`, `FILE_LOADED`, `PLAYBACK_RESTART`, seek command return, time-pos movement, and cache/range coverage are logged separately.
- Added demuxer cache snapshot and resume seek watchdog.
- If resume seek stalls, the core performs one controlled `seek <resumeSeconds> absolute+keyframes` recovery.
- Recovery failure is surfaced as a core error; playback is not treated as successful silently.
- `MEDIA_LIBRARY_MPV_HWDEC` remains a manual diagnostic switch; no automatic hwdec fallback was added.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Not done: subtitles/audio, product cache lifecycle, old path deletion.

## R3

Goal: reconnect subtitles and audio tracks on the new mpv core without using the old LibVLC/mpv patch chain.

Changed files:
- `MpvPlayerSession.cs`
- `MpvPlaybackEngineAdapter.cs`
- `MpvTrackInfo.cs`
- `MpvTrackListSnapshot.cs`
- `MpvSessionEventArgs.cs`
- `MpvNative.cs`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- New core observes `track-list`, `aid`, and `sid`.
- `track-list` is parsed into a single `MpvTrackListSnapshot`.
- Audio tracks come from mpv audio tracks.
- Embedded subtitles come from mpv subtitle tracks with `external=false`.
- mpv external subtitle tracks are used internally for external subtitle mapping and are not direct menu candidates.
- Subtitle/audio commands go through the adapter and new core using `aid`, `sid`, and `sub-add`.
- Track changes are delayed while R2 resume-seek readiness is blocked.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## R3.1

Goal: stabilize embedded subtitle/audio menu projection under asynchronous mpv `track-list` updates.

Changed files:
- `MpvPlayerSession.cs`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- Added session-local track registry.
- Each mpv `track-list` update merges into the registry instead of replacing menu state.
- Known tracks that are missing from one update are marked stale but kept in the menu projection.
- Startup track discovery publishes an initial menu after a stable window or max wait.
- After publish, late audio/embedded subtitle tracks are incrementally merged.
- mpv external subtitle tracks still do not enter the embedded subtitle menu.
- Embedded subtitle switching has WebDAV cache/range diagnostics and one controlled local seek recovery when the current range is lost.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation result:
- Current manual validation reported subtitles/audio as acceptable.
- Audio switching is acceptable.
- External subtitles are normal.
- Embedded subtitle menu stability was handled by the R3.1 registry strategy.
- Subtitle/audio switching should not call LibVLC, `VideoCacheProxyService`, `loadfile`, or source switch.

## R4

Goal: close / lifecycle convergence.

Changed files:
- `MpvPlayerSession.cs`
- `MpvPlaybackEngineAdapter.cs`
- `PlayerWindowViewModel.cs`
- `PlayerWindow.xaml.cs`

Result:
- Adapter `Stop()` detaches the current session and disposes it in the background.
- Adapter source replacement detaches the old session and cleans it up in the background.
- Adapter event gate logs stale-session / disposed-session discards.
- `MpvPlayerSession.DisposeAsync()` records R4 detach, event-loop cancel/exit/timeout, and native destroy start/complete/timeout.
- Window close notifies the outer shell immediately after the player window is hidden.
- WatchHistory save during shutdown is bounded by a short timeout and continues best-effort in the background if needed.
- ViewModel shutdown logs timer stop, UI release, WatchHistory save, and slow-stage events.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- WebDAV small video playback should not regress.
- pause/play and seek should not regress.
- Stop then Play should create a fresh session.
- Closing during playback should release shell UI quickly.
- Immediate close after opening should not deadlock.
- Closing during seek or track commands should not let stale events update the closed window.
- `dotnet run`, close player, then Ctrl+C should be able to exit.

Not done:
- R2 large WebDAV startup / playback tuning.
- R3 subtitle/audio behavior changes.
- R5 old LibVLC / proxy deletion.
- R6 product cache lifecycle.

Next step:
- R4 shutdown matrix passed by current manual validation.
- R3.2 validation is now acceptable.
- Proceed to R5.

## R3.2

Goal: fix the remaining embedded subtitle menu leak / shrinking edge without changing large-file performance, cache lifecycle, or shutdown behavior.

Changed files:
- `MpvPlayerSession.cs`
- `PlayerWindowViewModel.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- Fixed core track registry classification sticking: a subtitle track can move between mpv `external=true` and `external=false` based on the latest `track-list` update.
- Added classification-change diagnostics for subtitle tracks.
- Added post-publish low-frequency `track-list` probes at 5s / 10s / 20s for late-discovered tracks.
- ViewModel embedded subtitle menu now merges by `TrackId`: new tracks are added, existing tracks are updated, and known tracks missing from one incoming list are preserved.
- New media / source switch remains the boundary that clears embedded subtitle menu state.
- No `loadfile`, source switch, DB, scan, or ffprobe changes were added for this fix.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- Open a WebDAV video with embedded subtitles.
- If the menu first shows 1,2 and later mpv discovers 3, the menu should become 1,2,3.
- If the menu first shows 3 and later mpv discovers 1,2, the menu should become 1,2,3.
- A later incomplete `track-list` update must not remove known embedded subtitle tracks.
- Source switch / new media must clear and rebuild the menu.
- External subtitles and audio tracks should not regress.

Validation result:
- R3.2 embedded subtitle menu stability passed current manual validation.
- Known embedded subtitle tracks are not removed because one `track-list` snapshot omits them.
- Late-discovered embedded subtitle tracks are incrementally appended.
- The track-list / subtitle-audio menu strategy is now close to mainstream player behavior: startup collection, session registry, stable menu projection, and incremental updates.

Mainstream strategy audit result:
- No new immediate Blocker was found.
- The largest remaining maintenance gap is old LibVLC, old proxy, and old subtitle/audio state still existing in the project and `PlayerWindowViewModel`.
- R5 is the recommended next stage.

Not done:
- Large WebDAV startup / playback performance.
- Product cache lifecycle.
- Old LibVLC / proxy deletion.

Next step:
- Proceed to R5.

## R5

Goal: delete old LibVLC / old VideoCacheProxyService formal path / old subtitle-audio state residue without changing R2 large-file strategy, R3 subtitle/audio behavior, or R6 cache lifecycle.

Changed files:
- `MediaLibrary.App.csproj`
- `AppServiceProvider.cs`
- `PlayerWindowService.cs`
- `PlayerWindow.xaml.cs`
- `PlayerWindowViewModel.cs`
- `IVideoCacheService.cs`
- `VideoCacheService.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Deleted files:
- `MpvPlaybackEngine.cs`
- `IVideoCacheProxyService.cs`
- `VideoCacheProxyPlaybackResult.cs`
- `VideoCacheProxyService.cs`
- `VideoCacheSegmentPlaybackContext.cs`

Result:
- Removed LibVLCSharp / VideoLAN package references.
- Removed legacy LibVLC playback, subtitle/audio API calls, `VideoView`/LibVLC comments, and old fallback helpers from the formal player code.
- Removed the old self-hosted WebDAV Range proxy service and DI registration.
- Removed old segment-proxy cache APIs from `IVideoCacheService` / `VideoCacheService`.
- Preserved the new mpv core and adapter, `PlaybackHostView`, WatchHistory, playback source switching, full-file cache hit path, cache settings/usage/clear, ffprobe/MediaProbe, and subtitle candidate business.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- WebDAV small video playback.
- pause/play and seek.
- Stop then Play.
- playback close and immediate close.
- 10GB-range embedded subtitle menu stability.
- embedded subtitle / external subtitle / audio track switching.
- settings video cache page and clear/usage behavior.
- logs should not show old proxy URL creation or LibVLC initialization.

Validation result:
- R5 passed current manual validation.
- No R5 blocker is confirmed after old LibVLC / proxy cleanup.
- Remaining large WebDAV startup/playback and product cache lifecycle items stay deferred.

Not done:
- 70GB+ WebDAV startup/playback performance.
- default hwdec policy.
- product-level cache lifecycle.

Next step:
- Prefer R2.1 before R6 if large WebDAV performance remains the highest user-facing issue.

## R2.1

Goal: large WebDAV / hwdec / cache range performance and stability专项 on the new mpv core.

Changed files:
- `MpvPlayerSession.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- Added `mpv-r21-startup-stage`, `mpv-r21-startup-slow`, `mpv-r21-startup-breakdown`, and `mpv-r21-startup-bottleneck` logs to separate native/session creation, loadfile, file loaded, playback restart, first duration, first position, first cache state, and resume seek healthy.
- Added `mpv-r21-cache-effective-options` and `mpv-r21-cache-state` logs for cache/range auditing without recording full paths or URLs.
- Added large WebDAV playback watchdog logs: `mpv-r21-playback-watch-state`, `mpv-r21-playback-stall-detected`, and one controlled `absolute+keyframes` playback recovery.
- Kept `MEDIA_LIBRARY_MPV_HWDEC` as the manual override.
- Added `MEDIA_LIBRARY_MPV_HWDEC_POLICY`; default is `auto-disable-hevc4k-webdav`, which selects `hwdec=no` when request metadata identifies WebDAV HEVC/H.265 video at 3840x2160 or higher.
- Added `mpv-r21-video-format` and hwdec decision logs.
- Blocker follow-up: a 10GB-range WebDAV resume session showed `time-pos` jumping to the resume target while cache/range stayed empty; this is no longer treated as healthy playback. Large WebDAV resume now starts with `absolute+keyframes`, and actual progress requires movement after the target is already reached.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- WebDAV small video playback should not regress.
- pause/play/seek/close should not regress.
- subtitles/audio should not regress.
- 70GB+ WebDAV from start should record startup phase timings.
- 70GB+ WebDAV resume should record resume seek and cache/range health.
- Default hwdec and `MEDIA_LIBRARY_MPV_HWDEC=off` should be compared.
- 3-5 minute large-video playback should be observed for stalls and checked through `mpv-r21-playback-*` logs.

Not done:
- Product-level cache lifecycle remains R6.
- No UI rewrite or subtitle/audio menu changes.
- R2.1 does not claim that 70GB startup time or playback stutter is solved until manual validation confirms it.

Next step:
- Run R2.1 manual validation. If acceptable, proceed to R6.

## R2.1 Rollback Isolation

Goal: stop the R2.1 regression from stacking more patches and restore the R5/R2 baseline by default.

Changed files:
- `MpvPlayerSession.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- Added `MEDIA_LIBRARY_MPV_R21_ENABLED`.
- Default is disabled for R2.1 seek/watchdog/recovery behavior. Those parts do not run unless `MEDIA_LIBRARY_MPV_R21_ENABLED=1`.
- Disabled by default:
  - R2.1 startup resume `absolute+keyframes` selection.
  - R2.1 stricter resume movement heuristic.
  - R2.1 playback watchdog and playback recovery.
- Retained by default:
  - HEVC 4K WebDAV hwdec safety policy from `MEDIA_LIBRARY_MPV_HWDEC_POLICY`.
  - `MEDIA_LIBRARY_MPV_HWDEC` manual override.
- Kept existing R2/R3/R4/R5 baseline behavior and retained R2.1 diagnostic logs where they do not change playback flow.

Regression evidence:
- A 10GB-range WebDAV resume session reached `FILE_LOADED` and `PLAYBACK_RESTART`, then `time-pos` jumped to the resume target while cache/range stayed empty.
- R2.1 strict resume gating delayed playback-ready publication, which could leave the UI with progress but no video and no published embedded subtitle/audio menu.
- After disabling the R2.1 seek/watchdog path, subtitle/audio tracks were published but the video was still black with `hwdec=auto-safe`; logs showed HEVC 4K and D3D11/DXVA/ffmpeg-video errors. The hwdec safety policy is therefore kept as a blocker fix instead of being disabled with the rest of R2.1.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- 10GB-range WebDAV startup should show video again.
- Embedded subtitle/audio menus should publish again.
- WebDAV small video, pause/play/seek/close should not regress.
- Large WebDAV resume remains observation-only until R2.1 is redesigned.

Not done:
- No product-level cache lifecycle.
- No subtitle/audio menu changes.
- No R2.1 performance tuning or new recovery strategy.

Next step:
- Manually validate the restored baseline.
- Redesign R2.1 behind the explicit switch before enabling any behavior-changing large WebDAV strategy by default.

## R2.1a

Goal: align WebDAV resume loading with the successful `mpv.exe --start=<seconds>` comparison without restoring the old R2.1 initial watchdog/recovery stack.

Changed files:
- `MpvPlayerSession.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- For WebDAV sources with `StartPositionSeconds > 0`, `MpvPlayerSession` first tries load-time start through `loadfile`.
- The primary command form is `loadfile <url> replace -1 start=<seconds>`, matching the local mpv command signature `url flags index options`.
- A direct `loadfile <url> replace start=<seconds>` attempt remains as a compatibility fallback if the indexed option form fails.
- If either start-option command succeeds, the post-`PLAYBACK_RESTART` delayed seek is skipped.
- If start-option loading fails, the previous R2 delayed resume seek path is retained as fallback.
- This stage did not change cache parameters, hwdec defaults, subtitle/audio behavior, shutdown behavior, product cache lifecycle, or old R2.1 playback watchdog/recovery behavior.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- WebDAV small video from start should not regress.
- 9.7GB WebDAV from start should not regress.
- 9.7GB WebDAV resume at 623s should log `mpv-r21a-start-option-used` and should not log delayed seek unless start-option fallback is used.
- Subtitle/audio menus and switching should not regress.
- pause/play/seek/close should not regress.
- 70GB WebDAV remains observation-only for this stage.

Validation result:
- R2.1a passed current manual validation.
- 9GB / 13.8GB / 70GB WebDAV validation sessions all used the load-time start-option path where resume was present.
- The validation sessions started and advanced.
- No `CacheRangeNotRecovered` was observed.
- 70GB long-duration playback stutter remains a Deferred observation item and is not marked resolved by this stage.

Not done:
- No cache parameter tuning.
- No hwdec policy change.
- No new playback watchdog/recovery.
- No product-level cache lifecycle.

Next step:
- R2.1a can be treated as passed for current WebDAV resume-start validation.
- Continue observing 70GB long-duration playback behavior as Deferred.
- Product-level cache lifecycle remains R6.

## R6.1

Goal: audit and expose product video cache categories without changing playback behavior.

Changed files:
- `VideoCacheUsage.cs`
- `VideoCacheService.cs`
- `SettingsViewModel.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- Video cache usage now separates full-file cache, mpv-session cache, and legacy segment residue.
- `VideoCacheService.GetUsageAsync()` still reports the existing cache root and max size, while adding category bytes and item counts.
- Settings uses the existing video cache UI bindings and shows category breakdown text.
- R6.1 does not change playback source selection, mpv `loadfile`, R2.1a start option, cache clear behavior, active lease behavior, or capacity cleanup.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- Settings page should show total cache usage plus full-file / mpv-session / legacy breakdown.
- WebDAV playback should not change.
- Clear cache behavior is not expanded in R6.1; active cache protection and safe clear remain R6.2.

Not done:
- Active mpv-session cache marker / lease protection.
- Safe clear that skips active cache.
- Capacity LRU across full-file, mpv-session, and legacy residue.
- Product cache lifecycle completion.

Next step:
- Manually validate settings usage display.
- Proceed to R6.2 for active cache protection and safe clear.

## R6.2

Goal: protect active playback cache and make cache clearing skip active entries instead of deleting or blocking the whole operation.

Changed files:
- `MpvPlayerSession.cs`
- `VideoCacheClearResult.cs`
- `VideoCacheService.cs`
- `SettingsViewModel.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- WebDAV direct playback creates a managed `VideoCache\mpv-session\<sessionKey>` directory and writes a short-lived `.active.json` marker without URL or credentials.
- `MpvPlayerSession` sets `demuxer-cache-dir` only for WebDAV direct playback; local full-file playback clears it and keeps disk cache disabled.
- Dispose / Stop lifecycle releases only the small active marker and does not synchronously delete mpv cache directories.
- `VideoCacheService.ClearAllAsync()` now clears non-active full-file cache, non-active mpv-session cache, and legacy segment residue.
- Active full-file leases and active mpv-session markers are skipped and reported instead of being deleted.
- Settings clear result reports deleted full-file / mpv-session / legacy counts and active skipped bytes.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- WebDAV playback should create an mpv-session directory and active marker while playing.
- Closing the player should remove the active marker without blocking UI.
- Clearing cache while playing should skip active cache and not interrupt playback.
- Clearing cache after playback should remove non-active full-file, mpv-session, and legacy residue.
- R2.1a WebDAV resume start option should not regress.

Not done:
- Capacity LRU across cache categories.
- Startup stale marker aging policy.
- Product cache lifecycle completion.

Next step:
- Manually validate safe clear behavior.
- Proceed to R6.3-lite for statistics / wording closeout after R6.2 validation.

## R6.3-lite

Goal: finish cache statistics and settings wording without changing playback behavior or adding full-file / offline / partial-range caching.

Changed files:
- `VideoCacheService.cs`
- `SettingsViewModel.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- mpv-session usage excludes `.active.json`, so the active marker is not shown as playback disk cache data.
- Empty non-active mpv-session directories are cleaned during usage refresh / cleanup.
- Active mpv-session directories are still protected and are not deleted while playback is active.
- Settings text now labels mpv-session cache as mpv playback-period disk cache and explicitly notes that it does not include mpv memory / demux cache.
- Full-file cache statistics, legacy residue statistics, downloading statistics, and full-file cache hit playback are unchanged.
- No playback logic, R2.1a start option, subtitles/audio, Stop/Close lifecycle, cache parameters, DB schema, or migration was changed.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Not done:
- Active full-file cache creation entry point.
- Automatic full-file caching.
- Offline cache.
- Partial range cache / playback reuse of partial ranges.
- Complex download queue or playback-time switch to completed local files.

Validation scope:
- Settings should no longer show the active `.active.json` marker as 161B of mpv playback cache.
- Clearing cache during playback should still skip active cache.
- Closing the player should leave mpv-session usage at zero or cleanable empty directories.
- WebDAV playback, R2.1a resume start option, subtitles/audio, and Stop/Close should not regress.

Next step:
- Proceed to Final full player regression after R6.3-lite manual validation.

## Final Full Regression

Goal: close the player mainline by validating R1-R6 behavior without adding new playback features.

Changed files:
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- Build audit passed with 0 warning / 0 error.
- Source search confirmed old LibVLC / old proxy formal path has not returned.
- R2.1a code path still uses load-time `start=` for WebDAV resume and skips delayed seek when the start option succeeds.
- R3/R3.2 subtitle/audio registry, late probes, external subtitle mapping, and audio/subtitle command paths are still present.
- R4 quick detach / background native destroy / stale event discard paths are still present.
- R6 cache category statistics, active marker skip, marker-excluded mpv-session usage, and settings wording are still present.
- Recent log sample shows R2.1a start-option usage and delayed seek skip; no fallback, old pending seek, `CacheRangeNotRecovered`, `END_FILE reason=error`, old LibVLC, or old proxy entries were found in the sampled window.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation status:
- Code and log audit are complete.
- Manual Final matrix is still pending user execution:
  - WebDAV small playback / pause / play / seek / Stop / close.
  - 9GB / 13GB / 70GB WebDAV resume.
  - embedded subtitles / external subtitles / audio switching.
  - settings cache statistics and active-cache clear behavior.
  - log security spot check.

Not done:
- No new playback features.
- No volume / brightness memory. That belongs to the planned standalone Player UX-1 stage.
- No subtitle/audio memory.
- No active full-file cache creation, automatic cache, offline cache, partial range cache, or product download queue.

Next step:
- If the manual Final matrix passes, close the player mainline.
- Then start Player UX-1 for global volume / brightness memory only.

## Player UX-2 UI Prompt State Convergence

Goal: separate player UI prompts into main playback status, temporary operation notice, and central buffering overlay without changing playback core behavior.

Changed files:
- `PlayerWindowViewModel.cs`
- `PlayerWindow.xaml`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- Added a ViewModel-level `MainPlaybackUiState` projection for the bottom main player status.
- Added temporary operation notices for subtitle/audio/cache style feedback, separate from the bottom main status.
- Added central buffering overlay projection using `IsBufferingOverlayVisible` / `BufferingOverlayText`.
- The bottom control bar now binds to `DisplayStatusText` instead of the legacy `StatusMessage`.
- Subtitle/audio switch feedback now uses operation notices and no longer needs to occupy the bottom main playback status.
- The buffering slider indicator and central overlay bind to the new buffering overlay projection, avoiding duplicate bottom buffering text.
- Follow-up correction after log review: buffering overlay and operation notice are now hosted in WPF `Popup`s so they render above the native mpv HWND surface, and bottom status no longer displays buffering text while the central overlay is responsible for buffering feedback.
- Playback core, R2.1a start option, subtitles/audio command behavior, cache lifecycle, DB schema, and migration files were not changed.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Not done:
- Legacy `StatusMessage` remains as an internal compatibility field for staged cleanup.
- Button availability was not fully remodeled.
- Global volume / brightness memory was not implemented.
- Subtitle/audio memory was not implemented.

Validation scope:
- WebDAV playback should show a stable bottom main status.
- Subtitle/audio switch success or failure should appear as a temporary notice, not overwrite the main playback state.
- Real buffering should show a central overlay and hide after playback recovers.
- Stop / Close should clear notices and avoid stale prompt residue.

Next step:
- Manually validate UX-2 prompt behavior.
- Proceed to Player UX-1 for global volume / brightness memory only after UX-2 validation, unless prompt-state blockers are found.

## Player UX-2.1 Seek / Buffering Prompt Alignment

Goal: align user-visible seek feedback with buffering-style player behavior without changing playback core logic.

Changed files:
- `PlayerWindowViewModel.cs`
- `PlayerWindow.xaml`

Result:
- Seek and buffering now share the same central waiting overlay from the user's perspective.
- The bottom main status no longer shows a separate `正在跳转...` text during seek; playback remains represented as the main state while waiting feedback is shown in the overlay.
- The central buffering overlay now uses a spinner animation instead of an indeterminate progress bar.
- The bottom progress bar no longer shows a separate buffering progress strip, avoiding duplicate or misleading buffering UI.
- Playback core, R2.1a start option, subtitles/audio command behavior, cache lifecycle, DB schema, and migration files were not changed.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- During seek, the user should see the central spinner overlay rather than bottom `正在跳转...`.
- During real buffering, the same central spinner overlay should appear and hide after playback resumes.
- Buffering percentage is only shown when mpv reports a reliable buffering phase; seek itself does not fake progress.

## Player UX-2.2 Startup Waiting Prompt

Goal: show clear central startup feedback before first playback progress without changing playback core behavior.

Changed files:
- `PlayerWindowViewModel.cs`
- `PLAYER_STAGE_LOG.md`

Result:
- The central waiting overlay now appears during `Opening`, `LoadingMetadata`, `Starting`, `Seeking`, `Buffering`, and `Recovering`.
- Startup overlay text uses the current main playback UI state, for example `正在打开媒体...`, `正在读取媒体信息...`, or `正在准备播放...`.
- Startup does not fake a buffering percentage; percentage is still shown only when mpv reports a reliable buffering phase.
- Playback core, R2.1a start option, subtitles/audio business logic, cache lifecycle, DB schema, and migrations were not changed.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- Opening a WebDAV video should show a central spinner before the first frame / first playback progress.
- WebDAV resume startup should show the central spinner until playback begins.
- Real buffering may show a percentage only when mpv reports one.
- Seek should still use the same central waiting overlay.

## Player UX-1 Global Volume / Brightness Memory

Goal: remember global player volume, muted state, and brightness without changing playback core behavior.

Changed files:
- `PlayerPreferencesModel.cs`
- `IPlayerPreferencesService.cs`
- `PlayerPreferencesService.cs`
- `AppServiceProvider.cs`
- `PlayerWindowViewModel.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- Added local JSON player preference storage under the app data directory.
- Restores global volume, muted state, and brightness when a new player ViewModel is created.
- Applies restored volume / mute / brightness to the current mpv adapter when available.
- User volume and brightness changes save after an 800ms debounce.
- Player close / dispose triggers a short best-effort fallback save without changing the R4 shutdown strategy.
- Preferences are global player defaults, not per-video and not per-source.
- Playback core, R2.1a start option, subtitles/audio business logic, WatchHistory progress, cache lifecycle, DB schema, and migrations were not changed.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Not done:
- Subtitle/audio memory.
- Same-video unified playback progress.
- Any playback core, WebDAV, cache, or WatchHistory changes.

Validation scope:
- Change volume, close the player, open any video, and confirm the volume is restored.
- If muted, close the player, open any video, and confirm muted playback restores with the previous non-zero volume available on unmute.
- Change brightness, close the player, open any video, and confirm brightness is restored.
- Confirm volume / brightness popups do not alter the main playback status projection.
- Confirm WebDAV playback, subtitles/audio, and Stop/Close do not regress.

## Player UX-2.3 Startup Prompt Release Gate

Goal: keep the central startup waiting prompt visible during the black-screen startup gap until real playback progress arrives.

Changed files:
- `PlayerWindowViewModel.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- Log review showed `Playing` and `watch-history-ready` could arrive before `FILE_LOADED` / the first `time-pos`, causing the central startup prompt to disappear while the video surface was still black.
- Added a ViewModel UI-only startup progress gate: while waiting for the first real playback position, early `Playing`, `watch-history-ready`, `playback-state`, `buffering-clear`, and similar state projections cannot replace `Opening` / `Starting` with `Playing` / `Paused` / `Idle`.
- The startup prompt is released when the first real `time-pos` is received.
- Added diagnostic logs such as `player-ui-state-held` and `player-ui-startup-overlay-released` to verify the prompt lifecycle.
- Playback core, R2.1a start option, subtitles/audio, cache lifecycle, WatchHistory storage behavior, DB schema, and migrations were not changed.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation status:
- Manual validation passed for the startup black-screen prompt behavior after this follow-up.

Not done:
- Same-video unified playback progress remains a later Playback History UX stage.
- Subtitle/audio memory remains out of scope.

## Playback History UX-1 Same-Movie Unified Resume

Goal: let compatible playback sources under the same movie share the latest valid resume position without changing WatchHistory schema or playback core behavior.

Changed files:
- `PlaybackSourceService.cs`
- `PlayerWindowViewModel.cs`
- `PLAYER_REWRITE_PLAN.md`
- `PLAYER_KNOWN_ISSUES.md`
- `PLAYER_STAGE_LOG.md`

Result:
- `PlaybackSourceService.GetPlaybackSessionAsync(...)` now reads WatchHistory records for all current source-list `MediaFileId`s under the same `MovieId` and computes a unified resume projection.
- The latest compatible unfinished resume is projected onto compatible sources in the current playback source list.
- Saving remains unchanged: playback still starts and saves progress against the current source's `MovieId + MediaFileId`.
- Existing records remain compatible because the unified behavior is read/projection-only.
- Completed history is handled as the latest movie-level state for the current source list and clears older unfinished resume projection.
- Cross-source resume uses duration compatibility: duration difference must not exceed both 60 seconds and 2%.
- Missing-duration cross-source resume is skipped conservatively; source-specific resume remains available.
- `PlayerWindowViewModel` updates compatible source-list items in memory when the active source progress snapshot is saved, without writing extra history rows.
- Playback core, R2.1a start option, subtitles/audio, cache lifecycle, DB schema, and migrations were not changed.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Not done:
- No movie-level WatchHistory table.
- No database field or migration.
- No subtitle/audio memory.
- No playback core or WebDAV seek/cache changes.
- Runtime source-list update is an in-memory projection only; manual validation should confirm the source menu refresh behavior is sufficient.

Validation scope:
- Same movie with two compatible WebDAV sources A/B.
- Play A to 10 minutes, close, open B, and confirm B resumes from 10 minutes.
- Play B to 20 minutes, close, open A, and confirm A resumes from 20 minutes.
- Complete A, then confirm B does not keep showing older unfinished resume.
- Incompatible-duration source should not reuse the unified resume.
- Missing-duration source should not cross-apply another source resume.
- R2.1a start option should receive the unified resume seconds.
- Stop / Close, WebDAV playback, subtitles/audio should not regress.

## Playback History UX-1.1 Recently Played Latest Source Follow-up

Goal: make Home recently played / Continue Playback follow the latest actually played source instead of borrowing another compatible source's progress.

Changed files:
- `IWatchHistoryService.cs`
- `WatchHistoryService.cs`
- `HomeDashboardQueryService.cs`
- `PlayerWindowViewModel.cs`

Result:
- Home recently played now uses the latest actual WatchHistory row's `MediaFileId` for both display identity and Continue Playback.
- Home recently played no longer falls back to another compatible playback source's progress.
- New WatchHistory rows initialize `LastPlayPositionSeconds` from the source resume position used to start playback, avoiding a transient latest 0-second row when the player is closed quickly.
- Normal periodic and close-time progress saves still overwrite the initial resume position with the latest real playback position.
- No database field, migration, playback core, R2.1a, subtitle/audio, or cache lifecycle behavior was changed.

Build: `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

Validation scope:
- Add or select a short playback source after a long source has progress.
- Open the short source and close quickly.
- Home recently played should show the short source and its own start/resume position, not the long source progress and not `暂无进度` from an initial 0-second row.
- Continue Playback should open the latest short source.
- If the short source is played longer, Home should update to the final saved playback position.

## Player Mainline Closeout

Status:
- Player rewrite R1-R6, R2.1a, Final audit, UX prompt follow-ups, global volume/brightness memory, and Playback History UX follow-ups are complete for the current accepted scope.
- Remaining items are deferred enhancements, not active player-mainline blockers.

Deferred after closeout:
- 70GB long-duration playback stutter observation.
- Subtitle/audio memory.
- Active full-file cache creation, automatic cache, offline cache, partial range cache, and download queue features.

## Phase 5.2 Online Subtitle Search Entry

Goal: add the online subtitle player-menu entry and search dialog without changing existing subtitle switching or adding download/binding behavior.

Changed player surface:
- The subtitle menu now keeps the existing `None`, embedded, and external groups and adds an online downloaded subtitles submenu as a peer group.
- The online submenu exposes `Search online subtitles...`.
- Current Movie / Episode online subtitle bindings are shown read-only when available; an empty placeholder is shown otherwise.
- Opening the search dialog pauses active playback and does not auto-resume after close.

Kept boundaries:
- Existing embedded subtitle discovery and switching are unchanged.
- Existing scanned external subtitle binding and switching are unchanged.
- No online subtitle download, cache write, binding write, delete-binding UI, or downloaded-subtitle auto-switch was added in this stage.
- No playback core, scan logic, database schema, or migration change was added.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

## Phase 5.3 Online Subtitle Download And Switching

Goal: complete the player-side online subtitle download, binding, delete-binding, and auto-switch loop without changing embedded subtitle or scanned external subtitle behavior.

Changed player surface:
- Search-result rows now expose a download action.
- Download success adds the cached online subtitle into the player online subtitle menu and attempts immediate mpv `sub-add` selection.
- The online subtitle submenu now allows selecting cached online subtitles and deleting the current Movie / Episode binding.
- Temporary online subtitles for unidentified playback are session-only and can be removed from the current player menu.

Kept boundaries:
- Existing `None`, embedded, and scanned external subtitle menu behavior is unchanged.
- Online subtitles are not written to scan-owned `SubtitleBinding` and are not represented as `MediaFile`.
- Deleting a player online subtitle binding does not physically delete the cache file.
- No player core reload path was introduced for online subtitle switching.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

## Phase 5.3b Online Subtitle MediaFile Persistence

Goal: persist online subtitles downloaded for unidentified playback sources without changing existing embedded or scanned external subtitle behavior.

Changed player surface:
- Unidentified playback downloads now create MediaFile-level online subtitle bindings and appear again when the same playback source is opened later.
- Recognized Movie / Episode playback now shows entity-level online subtitles plus current MediaFile-level online subtitles.
- The online subtitle submenu can switch and delete Movie, Episode, and MediaFile online subtitle bindings.

Kept boundaries:
- Recognized new downloads still bind to Movie / Episode instead of MediaFile.
- Existing embedded subtitle and scanned external subtitle switching are unchanged.
- Online subtitles are still not written to scan-owned `SubtitleBinding` and are still not represented as `MediaFile`.
- Deleting an online subtitle binding does not physically delete the cache file.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

## Phase 5.4 Online Subtitle Cache Management Polish

Goal: connect online subtitle cache cleanup to software cache management without changing player subtitle switching semantics.

Changed player-adjacent surface:
- Search/download quota wording now makes unknown remaining quota and provider-side download denial clearer.
- Player online subtitle menu behavior remains the 5.3b behavior: Movie / Episode / MediaFile bindings can be displayed, selected, and soft-deleted; deleting a binding does not physically delete the cached subtitle file.

Kept boundaries:
- Existing `None`, embedded, scanned external subtitles, and online subtitle switching are unchanged.
- No player core reload path, scan-time `SubtitleBinding` change, or MediaFile masquerading was introduced.
- Physical deletion of online subtitle cache files is handled only by software-cache orphan cleanup.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

## Phase 5.5 Online Subtitle Player Closeout

Goal: verify Phase 5 online subtitle player behavior against current code and docs without adding new player scope.

Closeout result:
- The subtitle menu still keeps `None`, embedded, scanned external, and online downloaded subtitle groups.
- The online group still opens search, pauses active playback on dialog open, displays Movie / Episode / MediaFile online bindings, switches cached online subtitles, and soft-deletes bindings only.
- Existing embedded subtitle and scanned external subtitle switching paths were not changed in 5.5.
- Online subtitle cache-file availability now uses a strict managed-cache-root boundary check before a binding row is treated as playable.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

## Maintenance Rules

- Update this file after every stage.
- Update `PLAYER_REWRITE_PLAN.md` only when stage plan, scope, or route changes.
- Update `PLAYER_KNOWN_ISSUES.md` only for important issues with evidence.
- Do not record temporary guesses as facts.
- Documentation does not replace build and manual validation.
- Keep each document short; archive old content when it grows beyond about 200 lines.

## Default Fullscreen Startup Follow-up

Goal: keep the player default-fullscreen behavior without rebuilding window chrome during playback initialization.

Completed:
- The player now enters default fullscreen before the window is shown instead of queueing a fullscreen transition at `ApplicationIdle`.
- Fullscreen still uses borderless monitor bounds; restore still returns to the previous window style, resize mode, and state.
- Playback source selection, mpv load semantics, and watch-history gating remain unchanged.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Manual runtime regression is still required for first-open Movie playback and Episode playback.

## External Subtitle Startup Responsiveness Follow-up

Goal: keep Movie playback startup responsive when an automatically selected WebDAV subtitle is slow or unavailable.

Completed:
- Changed mpv external subtitle `sub-add` from synchronous `mpv_command` to `mpv_command_async`.
- Kept subtitle switch tracking and timeout behavior; added async command reply diagnostics without logging subtitle URLs or credentials.
- Episode playback behavior, default fullscreen behavior, playback source selection, and watch-history gating remain unchanged.

Evidence:
- Recent desensitized player logs showed Movie startup reaching `mpv-r3-external-subtitle-add-start` without a command return, while Episode samples without external subtitles did not enter this path.
- Historical desensitized player logs also contained a failed synchronous `sub-add` call that blocked for about 19 seconds.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Manual runtime regression is still required for Movie playback with an automatically selected slow or unavailable WebDAV subtitle.

## 2026-06-03 Player Passive Input / Restore Centering Follow-up

Goal: keep hidden fullscreen chrome hidden for passive playback controls and center the player after leaving maximize/fullscreen.

Changed behavior:
- Left/right seek, up/down volume, `M` mute, and mouse-wheel volume/brightness no longer reshow the hidden fullscreen title bar or bottom bar.
- The scope is intentionally limited to those passive inputs; play/pause, menus, fullscreen toggles, and pointer movement keep the existing chrome behavior.
- Exiting fullscreen now recenters the restored player window on the current monitor.
- Restoring from normal maximized state also recenters the window, while drag-restore from a maximized title bar keeps the drag-position behavior.

Boundaries kept:
- No playback engine, mpv, subtitle, watch-history, cache, or data-model changes.
- No migration, database update, commit, or push.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Manual runtime validation is still required for all restore paths: fullscreen button, `F`, `Esc`, video double-click, caption restore, and OS maximize restore.

## 2026-06-06 Phase 7.5a Player Shell And Bottom Control Bar

Goal: align the player shell and bottom control bar with the Phase 7.5 draft without changing playback core behavior.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `Player.Controls.xaml`
- `PHASE_7_5_PLAYER_UI_PLAN.md`
- `PLAYER_STAGE_LOG.md`

Completed:
- Added a left return / close entry to both windowed and fullscreen custom player title bars while keeping existing minimize, maximize / restore, and close controls.
- Added title tooltips for long playback titles in the top chrome and bottom control bar.
- Reworked `ControlBarPopup` into a compact bottom bar: play / pause, previous episode, next episode, current time, progress slider, duration, volume area, playback source, subtitles, audio track, and fullscreen.
- Removed the visible `Stop` button from the final bottom control bar surface. `StopCommand` itself was not changed.
- Moved playback source, subtitle, audio track, and fullscreen actions into the main bottom control row and kept their existing click handlers / menu-opening behavior.
- Exposed the existing `ToggleMute()` behavior from the bottom volume icon and kept the existing volume slider binding and feedback popup.
- Added player-specific control button, label, time, and status text styles in the player resource dictionary.

Boundaries kept:
- No mpv core, WebDAV, subtitle / audio discovery, playback source switching, WatchHistory, cache lifecycle, database schema, or migration behavior was changed.
- No subtitle menu content refactor, playback source menu refactor, audio track menu refactor, volume hover popup, brightness / volume side overlay redesign, or online subtitle search window change was included.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only existing line-ending normalization warnings for touched XAML / C# files.

Known Issues:
- Blocker: none confirmed for 7.5a.
- Deferred: 7.5b loading / brightness / volume overlays; 7.5c subtitle menu and online subtitle entry; 7.5d playback source and audio menus; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: draft colors are still only structural references, not final player colors.

Next step:
- Proceed to 7.5b after manual runtime validation of windowed / fullscreen title bar visibility, bottom control bar hit targets, and existing command behavior.

## 2026-06-06 Phase 7.5b Player Loading, Notice, Brightness, And Volume Overlays

Goal: align player loading / prompt overlays, side brightness / volume feedback, and the bottom volume hover popup without changing playback or input business logic.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `PlayerWindowViewModel.cs`
- `Player.Controls.xaml`
- `PHASE_7_5_PLAYER_UI_PLAN.md`
- `PLAYER_STAGE_LOG.md`

Completed:
- Moved the central waiting overlay and bottom notice toast onto player-specific dark overlay styles.
- Kept the central spinner model and reliable-percent rule; buffering percentage is shown only through existing `BufferingPercent` projection.
- Differentiated seek overlay text from generic buffering text with a UI-only `正在跳转...` projection.
- Restyled the left brightness side overlay as a 0-100 vertical meter using player dark resources.
- Restyled the right volume side overlay as a 0-200 vertical meter with a visible 100% marker and a low-saturation boost range.
- Added a bottom volume hover popup from the volume area, with icon, vertical 0-200 volume slider, current percentage text, 100% marker, and boost range.
- Kept the bottom volume icon click as mute / unmute, not as a menu opener.
- Closed the volume hover popup when player menus open, the control bar hides, the player deactivates, or the cursor leaves the volume hover area.

Boundaries kept:
- No mpv core, WebDAV, playback state machine, subtitle / audio discovery, playback source switching, WatchHistory, cache lifecycle, database schema, or migration behavior was changed.
- Existing keyboard and mouse-wheel brightness / volume logic was reused.
- The hover popup adds UI state only and does not change stored volume, mute, or brightness semantics.
- No subtitle menu, playback source menu, audio track menu, or online subtitle search window refactor was included.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only line-ending normalization warnings for touched files.

Known Issues:
- Blocker: none confirmed for 7.5b.
- Deferred: 7.5c subtitle menu and online subtitle entry; 7.5d playback source and audio menus; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: draft colors remain structural references; player overlays continue to use fixed player dark resources.

Next step:
- Proceed to 7.5c after manual runtime validation of loading / seek / buffering prompts, wheel brightness / volume side overlays, bottom volume hover popup, and menu-open suppression.

## 2026-06-06 Phase 7.5c Subtitle Menu And Online Subtitle Entry

Goal: align the player subtitle popup menu and online subtitle entry with the Phase 7.5 draft without changing subtitle switching, online binding, or cache semantics.

Changed files:
- `PlayerWindow.xaml.cs`
- `Player.Controls.xaml`
- `PHASE_7_5_PLAYER_UI_PLAN.md`
- `PLAYER_STAGE_LOG.md`

Completed:
- Reworked the player popup menu styling into player-local dark ContextMenu, MenuItem, submenu, and separator styles based on the Library menu density and right-opening behavior.
- Applied the player menu styling to the playback source, subtitle, and audio menus so the 7.5c subtitle menu sits in the same interaction shell as adjacent player menus.
- Changed the subtitle menu top level to show `无字幕`, `内嵌`, `外挂`, and `在线下载字幕`.
- Kept `内嵌`, `外挂`, and `在线下载字幕` as right-opening submenus with visible loading / empty states.
- Kept the existing online subtitle search entry and bound / downloaded online subtitle list.
- Replaced direct online subtitle delete clicks with a lightweight confirmation submenu. The confirmation copy states that the binding or temporary player record is removed and the cached subtitle file is not deleted.

Boundaries kept:
- Existing `NotifySubtitleMenuOpened`, `OpenOnlineSubtitleSearch`, `OnlineSubtitleMenuItems`, `SelectOnlineSubtitleFromMenu`, and `DeleteOnlineSubtitleFromMenu` flows were reused.
- No OpenSubtitles API, download, save, bind, cache cleanup, settings, detail-page entry, subtitle memory, mpv core, database schema, migration, or database update behavior was changed.
- No non-player page was modified; Library menu code was reference-only.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for 7.5c.
- Deferred: 7.5d playback source and audio menus; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for actual WPF menu placement, hover timing, and delete-confirm submenu feel.

Next step:
- Proceed to 7.5d after manual runtime validation of subtitle menu grouping, right-opening submenus, current subtitle highlighting, online search entry, online subtitle selection, and delete-confirm copy.

## 2026-06-06 Phase 7.5d Playback Source And Audio Menus

Goal: align playback source and audio track popup menus with the Phase 7.5 draft without changing source selection, media probing, audio discovery, or mpv switching behavior.

Changed files:
- `PlayerWindow.xaml.cs`
- `PlayerWindowViewModel.cs`
- `PHASE_7_5_PLAYER_UI_PLAN.md`
- `PLAYER_STAGE_LOG.md`

Completed:
- Changed the playback source menu to a compact table layout with the header `播放源 / 分辨率 / 码率 / 大小`.
- Playback source rows now use existing `PlaybackSourceItem` display fields: `FileName`, `ResolutionShortText`, `BitrateText`, `FormattedFileSize`, `SourceTypeText`, `VideoCacheStatusText`, and existing resume text.
- Kept current source highlighting and the existing `SelectedSource` click path.
- Removed the raw `FilePath` line from playback source tooltips. Tooltips now use existing source summary and playback history only.
- Hid raw video-cache error details in the player source menu status row to avoid exposing paths or remote details in the popup.
- Added a player UI projection for audio-menu state: discovery not ready, switching in progress, and previous switch failure.
- Audio menu now shows `正在读取音轨...`, `暂无可用音轨`, existing audio rows with current-track highlighting, and the existing switch-failure message when applicable.

Boundaries kept:
- No playback source selection algorithm, default-source business rule, MediaProbe / ffprobe probing, mpv core, audio memory, database schema, migration, or database update behavior was changed.
- The new audio state properties are read-only UI projection; audio reading and switching still use the existing refresh and `SelectAudioTrackFromMenu` flows.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for 7.5d.
- Deferred: 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for actual menu sizing, table alignment, and audio failure message timing.

Next step:
- Proceed to 7.5e after manual runtime validation of playback source table columns, current source highlighting, source switching, audio loading / empty / selected states, and absence of raw WebDAV URL or account details in the popup.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish

Goal: apply focused player-window control polish after the 7.5a-7.5d UI pass without changing playback, source switching, subtitle, audio, history, or database behavior.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `PlayerWindowViewModel.cs`
- `Player.Controls.xaml`
- `PLAYER_STAGE_LOG.md`

Completed:
- Removed the top-left return buttons from normal and fullscreen player captions, shifted the caption title left, and added hover / pressed feedback to non-close caption buttons.
- Reduced the bottom control bar height, lifted the control bar placement, and removed the duplicate movie title from the bottom bar.
- Reordered the transport controls so play / pause sits between previous and next.
- Made disabled player buttons visibly dimmer through shared button styles and glyph foreground binding.
- Moved the play-state text to the left side of the first control row, aligned with the first button in the second row.
- Removed the bottom horizontal volume slider so the bottom bar keeps only the volume icon entry point.
- Changed playback source, subtitle, and audio controls to compact icon-only buttons.
- Rebuilt the main progress slider with transparent track background, blue played range, and a small hover-only thumb.
- Removed the loading spinner panel background so loading / buffering keeps only the spinner and text.
- Reworked the side volume feedback and bottom volume hover meter so the full 0-200 range uses one continuous track, with color changing only after 100%.

Brightness audit:
- The current brightness setting still uses the mpv `brightness` video equalizer property. UI value 100 maps to mpv brightness 0, and UI value 0 maps to mpv brightness -40.
- This changes decoded video brightness / levels, not the monitor backlight or a separate dimming overlay, so it can visually feel closer to exposure / level adjustment than true display brightness reduction.

Boundaries kept:
- No mpv playback lifecycle, media scanning, playback source algorithm, subtitle binding, audio track switching, WatchHistory, settings schema, database schema, migration, or database update behavior was changed.
- Brightness behavior was audited only in this pass; no alternate dimming implementation was introduced.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming needs a separate design because the current implementation intentionally uses mpv video equalizer brightness; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for exact WPF hover feel, popup lift distance, and progress thumb visibility on real playback windows.

Next step:
- Proceed to 7.5e only after manual runtime validation of titlebar buttons, compact bottom controls, icon-only menu buttons, modern progress slider, loading overlay, and volume boost meter behavior.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 2

Goal: refine the player titlebar and bottom control bar after manual visual feedback without changing playback, subtitle, audio, source, history, settings, or database behavior.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `PlayerWindowViewModel.cs`
- `Player.Controls.xaml`
- `PLAYER_STAGE_LOG.md`

Completed:
- Added a visible border hover treatment for normal titlebar caption buttons.
- Restored the close caption hover / pressed colors to the shared pink danger palette.
- Changed the lifted bottom control bar to a complete rounded rectangle with a full border instead of a top-only rounded panel.
- Made the resume prompt clear 3 seconds after actual playback starts; the countdown no longer starts during loading.
- Added conditional tooltip behavior for the bottom `正在播放` status text only when the text is actually trimmed.
- Removed the movie-title tooltip from normal and fullscreen player titlebars.
- Kept player chrome visible while the cursor rests on the titlebar, bottom control bar, or volume hover area.
- Shortened the main progress track to half of its previous available span with a one-third left / two-thirds right trim split.
- Increased bottom button spacing around the shortened progress layout.
- Changed the playback source icon to the Segoe MDL2 movie / clapperboard glyph.
- Made the unplayed half of the progress track visible while keeping only the thumb hover-only.

Boundaries kept:
- No mpv playback lifecycle, source switching, subtitle binding, audio switching, WatchHistory, player preference schema, database schema, migration, or database update behavior was changed.
- No brightness behavior change was included in this follow-up.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for exact WPF hover contrast, conditional tooltip timing, control-bar bottom rounding, and clapperboard glyph rendering on the target Windows font set.

Next step:
- Proceed to 7.5e only after manual runtime validation of titlebar hover, close hover color, chrome auto-hide guard zones, resume prompt timeout, shortened progress layout, and status-text tooltip behavior.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 3

Goal: close the remaining player titlebar and progress-bar visual gaps from manual feedback without changing playback, source, subtitle, audio, history, settings, or database behavior.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `Player.Controls.xaml`
- `PLAYER_STAGE_LOG.md`

Completed:
- Changed the player close caption hover color to the same shared danger hover behavior used by the main software caption close button.
- Removed hover tooltips from normal and fullscreen titlebar caption buttons, including the dynamically updated maximize / restore button.
- Extended the bottom control bar slightly downward by increasing its bottom padding and lowering the popup lift.
- Moved position time, progress track, and duration time into one shortened progress group so the time labels shorten / move with the progress bar.
- Added a dedicated always-visible progress display layer behind the interactive slider so the full track and played progress remain visible, while only the thumb stays hover-only.

Boundaries kept:
- No mpv playback lifecycle, playback source switching, subtitle binding, audio switching, WatchHistory, player preference schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for the ProgressBar indicator width, exact bottom-bar lift, and titlebar hover contrast on the target Windows display.

Next step:
- Proceed to 7.5e only after manual runtime validation of close hover color, titlebar no-tooltip behavior, bottom-bar height, grouped shortened progress timing labels, and always-visible progress track.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 4

Goal: fix the remaining player titlebar and bottom spacing issues after repeated visual feedback, while keeping playback behavior and data semantics unchanged.

Changed files:
- `PlayerWindow.xaml`
- `Player.Controls.xaml`
- `PLAYER_STAGE_LOG.md`

Completed:
- Increased the player-local titlebar height from the shared 44px caption token to 46px.
- Replaced the player close caption button with an independent close-button template so its hover background is drawn with the shared main-window `BrushDanger` color instead of being overridden by the generic player button hover trigger.
- Moved the bottom playback status row upward by 3px and increased bottom control-bar padding by the same amount.
- Replaced fixed button margins with weighted spacer columns around the transport, progress, volume, source, subtitle, audio, and fullscreen buttons.
- Allocated the shortened progress group to half of the available spacing span and distributed the freed space into left / right button gaps using the requested one-third / two-thirds split.

Boundaries kept:
- No mpv playback lifecycle, playback source switching, subtitle binding, audio switching, WatchHistory, player preference schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for exact weighted button gaps and close-hover color on the target display.

Next step:
- Proceed to 7.5e only after manual runtime validation of player titlebar height, close-hover color, status row position, and weighted bottom button spacing.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 5

Goal: refine the player bottom control-bar width and titlebar button alignment after another manual layout pass, while keeping player behavior unchanged.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Changed the bottom control bar popup from full-width to a centered 75% width of the available player surface.
- Replaced the previous left / right weighted gap split with uniform `*` spacer columns between every adjacent bottom control group, including the gaps around the progress time labels.
- Kept the progress time / track / duration group as a single `10*` column inside the shortened control bar.
- Moved the normal and fullscreen titlebar button stacks 2px left with a right-side margin.

Boundaries kept:
- No mpv playback lifecycle, playback source switching, subtitle binding, audio switching, WatchHistory, player preference schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors.
- Plain `dotnet build MediaLibrary.sln` was blocked because the app executable was currently running and locked by `MediaLibrary.App`.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for centered 75% control-bar width and uniform gap feel across narrow and wide player windows.

Next step:
- Proceed to 7.5e only after manual runtime validation of centered control-bar width, uniform button spacing, and 2px titlebar button offset.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 6

Goal: refine the player control-bar popups and side feedback meters after another visual pass, while keeping playback and source/subtitle/audio behavior unchanged.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `PlayerWindowViewModel.cs`
- `Player.Controls.xaml`
- `PlayerMeterBrushConverter.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Centered the volume hover popup to the volume button and narrowed the volume / brightness feedback panels.
- Removed the volume hover menu's 100% marker, 100% label, visible thumb, and bottom "volume" label prefix; the menu now shows only the percentage.
- Added a shared player meter brush converter: 0-100 transitions from light blue to the movie progress blue, and boosted volume above 100 transitions from the progress blue to a darker blue.
- Applied the same meter color logic to the side volume and brightness feedback bars.
- Removed the playback source menu's top local-cache status row and the separator above the source table header.
- Renamed the source bitrate header to "视频码率", centered table headers to their fields, removed selected-row checkmarks, removed cache status from the source row secondary text, and capped the source menu height for a short hidden-scroll list.
- Centered the subtitle menu to the subtitle button, renamed the online group to "在线字幕", added separators between root subtitle groups, centered the root subtitle choices, and replaced selected subtitle checkmarks with the same selected-row color treatment as sources.
- Moved the titlebar button stack another 2px left through the current XAML margin.

Boundaries kept:
- No mpv playback lifecycle, playback source switching semantics, subtitle binding semantics, audio switching behavior, WatchHistory, player preference schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for popup centering, hidden-scroll source list height, exact selected-row contrast, and the boosted-volume color ramp on the target Windows display.

Next step:
- Proceed to 7.5e only after manual runtime validation of volume/source/subtitle popup placement, hidden source-list scrolling, feedback panel width, and meter color behavior at 0 / 100 / boosted volume values.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 7

Goal: fix the remaining control-bar popup interaction and vertical meter visibility issues found during manual validation.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `Player.Controls.xaml`
- `PLAYER_STAGE_LOG.md`

Completed:
- Fixed the vertical player meter template by adding the required `PART_Track`, so WPF can size `PART_Indicator` and show the current volume / brightness color.
- Removed the visible volume hover slider thumb by replacing the thumb with a zero-size transparent template while keeping the invisible interaction layer.
- Removed manual checkmarks from the selected audio-track menu row; selection remains represented by the existing selected-row style.
- Removed the 350ms same-button menu reopen suppression that could eat legitimate source / subtitle / audio menu clicks after a close.
- Switched the audio-track menu to the same centered-above-button placement path as source and subtitle menus.
- Reduced bottom control-bar first-open work by setting popup layout before opening and deferring the native popup move to loaded priority.
- Increased normal and fullscreen titlebar button right margin from 4px to 8px so the left shift is perceptible.

Boundaries kept:
- No mpv playback lifecycle, playback source switching semantics, subtitle binding semantics, audio switching behavior, WatchHistory, player preference schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for WPF vertical meter rendering, menu click reliability, first-open control-bar smoothness, and the more visible titlebar button offset.

Next step:
- Proceed to 7.5e only after manual runtime validation of volume / brightness current-value colors, audio selected-row styling, menu open-close reliability, and titlebar button offset.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 8

Goal: fix the remaining invisible vertical meter fill and remove bottom control-button hover text.

Changed files:
- `PlayerWindow.xaml`
- `Player.Controls.xaml`
- `PlayerMeterBrushConverter.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Added `PlayerMeterFillHeightConverter` and bound the vertical meter indicator height explicitly to `Value / (Maximum - Minimum) * ActualHeight`.
- Kept the shared segmented blue brush behavior for brightness, side volume, and bottom volume while making their current filled height independent of WPF's built-in `ProgressBar` vertical template sizing.
- Removed bottom control-bar button tooltips from previous episode, play / pause, next episode, volume, source, subtitle, audio, and fullscreen buttons.

Boundaries kept:
- No mpv playback lifecycle, playback source switching semantics, subtitle binding semantics, audio switching behavior, WatchHistory, player preference schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for actual vertical meter rendering in WPF and for confirming no bottom control button hover text appears.

Next step:
- Proceed to 7.5e only after manual runtime validation of brightness / side-volume / bottom-volume fill visibility and tooltip removal.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 9

Goal: correct the volume / brightness meter semantics and reduce high-frequency UI cost in the player control bar, without changing playback behavior.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `PlayerWindowViewModel.cs`
- `Player.Controls.xaml`
- `PlayerMeterBrushConverter.cs`
- `PLAYER_STAGE_LOG.md`

Performance audit findings:
- Bottom / side volume and brightness feedback were paying for height-based meter updates even though the desired behavior is color-depth change only.
- Bottom volume popup used both a `ProgressBar` and a `Slider` bound to `Volume`, doubling UI update work during drag.
- `ShowInteractionFeedbackPopup()` repositioned the full-window transparent feedback popup twice on every wheel / shortcut volume or brightness update.
- `ShowControlBar()` recalculated and moved the native control-bar popup even when the control bar was already open.
- Control-bar auto-hide timer restart stopped and started the timer on every high-frequency pointer / wheel path, even though the timer already uses the latest activity tick.
- Player preference saving created and cancelled a `CancellationTokenSource` plus delayed task for every volume / brightness change during dragging.
- Meter brush creation allocated a new frozen brush for every feedback update.
- Volume changes sent `SetMute(false)` repeatedly to the playback engine even when muted state did not change.

Completed:
- Reverted vertical meters to fixed width / fixed height bars where the whole bar changes color depth; no value-based fill height or apparent thickness / length change is used.
- Kept the segmented color semantics: below 100 is lighter, 100 is the movie progress blue, above 100 becomes darker.
- Added cached meter brushes for 0-100 brightness and 0-200 volume to avoid per-tick brush allocation.
- Removed the extra `ProgressBar` layer from the bottom volume popup; the visible vertical slider now owns the fixed-width colored bar and input handling.
- Added a 10px volume thumb dot that appears only while the bottom volume slider is hovered or dragged.
- Changed interaction feedback popup opening so it positions only when first opened, then reuses the existing popup during repeated wheel / shortcut updates.
- Changed control-bar showing so it positions only when the popup is first opened; size / location updates still go through the existing placement path.
- Changed the auto-hide timer restart path to avoid stop/start churn while the timer is already running.
- Replaced player-preference async delay / CTS churn with a single dispatcher debounce timer.
- Cached the last applied player volume / muted / brightness values so mpv receives only actual changes.

Boundaries kept:
- No mpv playback lifecycle, playback source switching semantics, subtitle binding semantics, audio switching behavior, WatchHistory, player preference schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required for perceived drag smoothness, the hover-only volume thumb, and whether remaining mpv volume / brightness calls feel smooth on the target machine.

Next step:
- Proceed to 7.5e only after manual runtime validation of fixed-width meter color changes, bottom volume drag smoothness, control-bar first-show smoothness, and shortcut / wheel feedback smoothness.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 10

Goal: restore current-value fill semantics for volume / brightness meters and reduce the risk of playback-time control-bar clicks being swallowed by native video input handling.

Changed files:
- `PlayerWindow.xaml`
- `PlayerWindow.xaml.cs`
- `Player.Controls.xaml`
- `PlayerMeterBrushConverter.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Restored fixed-width current-value fill height for side brightness, side volume, and bottom volume, while keeping the bar thickness fixed.
- Kept the fill color ramp tied to the current value: below 100 is lighter, 100 is the movie progress blue, and boosted volume becomes darker.
- Kept the bottom volume thumb at a fixed 10px size and visible only while hovering / dragging.
- Added hand cursor behavior to the bottom volume slider.
- Added cached control-bar screen bounds with a small hit-test tolerance so native video input handling can more reliably detect that the cursor is over the WPF control bar.
- Reset native video double-click tracking on control-bar left-button down to prevent rapid control-bar clicks from being mistaken for video double-click gestures.

Performance / input audit note:
- The highest-risk playback-only click swallowing path is the native video input hook: while video is playing, mouse messages can arrive through the native child-window path before WPF button handling. If the control-bar hit test is stale, the second click in a rapid sequence can be treated as video input. This follow-up keeps a screen-space control-bar bounds cache and clears video double-click state on control-bar clicks without changing playback behavior.

Boundaries kept:
- No mpv playback lifecycle, playback source switching semantics, subtitle binding semantics, audio switching behavior, WatchHistory, player preference schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required while video is actively playing, especially rapid repeated clicks on the bottom control buttons and bottom volume slider drag.

Next step:
- Proceed to 7.5e only after manual runtime validation of current-value fill height, hand cursor on bottom volume slider, and rapid control-bar clicks during active playback.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 11

Goal: address the remaining playback-time control-bar jank and click loss, refine boosted-volume color semantics, and reduce the chrome restore delay.

Changed files:
- `PlayerWindow.xaml.cs`
- `PlayerWindowViewModel.cs`
- `PlayerMeterBrushConverter.cs`
- `PLAYER_STAGE_LOG.md`

Root-cause findings:
- The low-level mouse hook was handling `WmLButtonDown` to synthesize native-video double-click fullscreen behavior. Because the control bar is a WPF popup over a native video child, this path can misclassify rapid control-bar clicks as video-area input and return `1`, which swallows the click before WPF buttons receive it.
- Playback position events were dispatched to the UI on every mpv `time-pos` update. Each update refreshed `PositionSeconds`, the visible progress bar, the progress slider, and `PositionText`; this only happens while video is playing, matching the observed pause-vs-playback difference.
- Chrome restoration over the native video area depended on the cursor poll timer when WPF mouse-move events were not delivered by the native child window; the 80ms poll interval created a visible delay after the chrome was hidden.

Completed:
- Removed left-button handling from the low-level mouse hook. The hook now only consumes native mouse wheel messages, while double-click fullscreen remains handled through the existing `WmLButtonDblClk` window-message paths.
- Added UI-side throttling for playback position updates before queuing work to the Dispatcher. Position UI updates now skip same-second repeats that arrive within 125ms, while still updating on second changes and resetting the throttle for each new playback load.
- Reduced the native-video cursor poll interval from 80ms to 25ms so hidden titlebar / control-bar restoration reacts faster when the cursor moves over the native video child.
- Updated shared player meter colors so volume above 150 transitions from deep blue toward red. Brightness remains on the 0-100 light-blue to progress-blue curve.

Boundaries kept:
- No playback source switching semantics, subtitle binding semantics, audio switching behavior, WatchHistory persistence contract, player preference schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only existing LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5e online subtitle search window; 7.5f regression closeout.
- Noise: manual runtime validation is still required while video is actively playing, especially rapid repeated clicks on bottom buttons, volume drag behavior above 150, and chrome restore latency over the native video area.

Next step:
- Proceed to 7.5e only after manual runtime validation confirms playback-time clicks are no longer swallowed and chrome restore delay is no longer visible.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 12

Goal: restyle the online subtitle search window to match the correction dialog and media-library filter controls.

Changed files:
- `OnlineSubtitleSearchWindow.xaml`
- `OnlineSubtitleSearchWindow.xaml.cs`
- `OnlineSubtitleSearchViewModel.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Changed the online subtitle search window to a rounded dialog shell with a rounded content surface and an 8px-left-shifted titlebar close button.
- Removed the duplicated content-area title and the old two-line current playback/source header.
- Added a single first-row current source line using larger white text, ellipsis trimming, and trimmed-only tooltip behavior.
- Replaced the old ComboBox search controls with a media-library-style search box, search icon button, and centered popup filter buttons for sort, subtitle type, and language.
- Localized the subtitle language menu display names to Chinese without changing the OpenSubtitles language codes sent to the API.
- Replaced quota/total-count footer copy with one trimmed status line.
- Reworked result rows toward the movie-correction result layout: no poster column, subtitle title plus match score on the first row, two metadata columns split by a vertical divider, and a text-width download button.
- Kept row download status feedback, but removed the old OpenSubtitles total-count display from the bottom of the window.

Boundaries kept:
- No OpenSubtitles API contract, search request semantics, download/save/bind behavior, playback subtitle apply behavior, settings schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only existing LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5f regression closeout.
- Noise: manual runtime validation is still required for exact menu alignment, result-row visual parity with the correction dialog, and trimmed tooltip behavior on long source/status/subtitle names.

Next step:
- Proceed to manual runtime validation of the online subtitle search window before considering 7.5e UI complete.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 13

Goal: finish the online subtitle search dialog polish pass after runtime interaction feedback.

Changed files:
- `OnlineSubtitleSearchWindow.xaml`
- `OnlineSubtitleSearchWindow.xaml.cs`
- `OnlineSubtitleSearchViewModel.cs`
- `PlayerWindow.xaml.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Added explicit focus clearing for clicks outside text input areas and for the search-box clear button.
- Removed alphabetical secondary ordering from the language list and kept common languages at the top through an explicit priority list.
- Added a sort-direction icon button directly after the search button without changing the underlying search fields.
- Brightened the result-row download button and kept the disabled state distinct for already downloaded rows.
- Pre-marked results that already match an active cached binding so their button shows `已下载` and is disabled as soon as the results render.
- Removed per-row download-status text from below the download button; download feedback now stays in the single status line.
- Reduced result-row height, tightened row spacing, shortened the vertical divider, and fixed the left metadata column width so the divider no longer shifts with text length.
- Replaced the loading progress bar with a centered spinner and status text in the result area.
- Kept the search dialog non-draggable by leaving no titlebar `DragMove()` path on this window.
- Shifted the status line under the search box slightly right.
- Narrowed the player subtitle menu's online-subtitle second-level rows and changed those rows to centered, fixed-width, ellipsis-trimmed text with trimmed-only tooltips.
- Added a subtle glass-style dialog surface and button/menu chrome without changing the search/download/binding semantics.

Boundaries kept:
- No OpenSubtitles API contract, cache layout, binding model, player subtitle application semantics, settings schema, database schema, migration, database update, or non-player page behavior was changed.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5f regression closeout.
- Noise: manual runtime validation is still required for exact glass appearance, input focus clearing, existing-binding disabled state, result-row density, and the narrowed online-subtitle submenu tooltip behavior.

Next step:
- Run build and migration diff checks, then manually validate the online subtitle search dialog in the player.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 14

Goal: adjust subtitle submenu placement and clarify the online subtitle binding deletion path.

Changed files:
- `PlayerWindow.xaml.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Changed player submenu popup placement from right/top alignment to right/bottom alignment, so subtitle second-level menus expand upward from their menu row instead of downward.
- Kept the existing online subtitle binding deletion path intact: downloaded/bound online subtitle rows still contain `删除绑定`, temporary rows still contain `移除临时字幕`, and both still use a confirm/cancel submenu before executing.

Boundaries kept:
- No subtitle binding semantics, cache deletion semantics, database schema, migration, settings schema, OpenSubtitles API behavior, or non-player UI behavior was changed.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5f regression closeout.
- Noise: manual runtime validation is still required for submenu placement near screen edges and for discoverability of the online subtitle delete-binding entry after the narrowed submenu change.

Next step:
- Run build and migration diff checks, then manually validate subtitle submenu placement and online subtitle delete-binding discoverability.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 15

Goal: fix subtitle submenu upward-placement offset and restore reliable third-level menu opening for online subtitle rows.

Changed files:
- `PlayerWindow.xaml.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Fixed submenu native-window repositioning so the upward submenu placement uses screen-coordinate bottom-right anchoring, avoiding mixed pixel/DIP height math that could offset menus under display scaling.
- Kept the submenu attached to the right side of the corresponding menu row while placing the popup above that row.
- Disabled trimmed-name tooltip behavior on online subtitle rows that own third-level menus, so hover opens the third-level submenu instead of competing with a text tooltip.

Boundaries kept:
- No subtitle binding/delete semantics, cache semantics, database schema, migration, settings schema, OpenSubtitles behavior, or non-player UI behavior was changed.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5f regression closeout.
- Noise: manual runtime validation is still required for submenu positioning under the user's display scaling and for online subtitle third-level menu hover behavior.

Next step:
- Run build and migration diff checks, then manually validate submenu placement and online subtitle third-level menu opening.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 16

Goal: restore online subtitle third-level menu opening and allow direct switching from downloaded online subtitle rows.

Changed files:
- `PlayerWindow.xaml.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Added an explicit mouse-enter submenu open path for player submenu items, so custom-styled submenu rows actively open their child menu instead of relying only on default WPF hover behavior.
- Disabled tooltip service on narrowed online-subtitle submenu rows to prevent text hover popups from competing with submenu opening.
- Added direct left-click switching on cached online subtitle rows. Clicking the downloaded online subtitle row now calls the same switch path as the `切换到此字幕` child command, while clicks inside child menu items still go to those child actions.

Boundaries kept:
- No subtitle binding/delete semantics, cache semantics, database schema, migration, settings schema, OpenSubtitles behavior, or non-player UI behavior was changed.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5f regression closeout.
- Noise: manual runtime validation is still required for online subtitle row hover, direct row click switching, and delete-binding submenu access.

Next step:
- Run build and migration diff checks, then manually validate online subtitle third-level menu opening and direct row switching.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 17

Goal: flatten the online subtitle row menu after direct row-click switching became the primary switch action.

Changed files:
- `PlayerWindow.xaml.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Removed the `切换到此字幕` child item from downloaded online subtitle rows.
- Changed cached online subtitle row click handling to run on direct parent-row mouse down, so a direct click switches immediately and closes the menu instead of opening the child menu.
- Flattened the delete/remove confirmation menu: the previous fourth-level hint, confirm, and cancel items now appear directly in the online subtitle row's hover submenu.
- Kept unavailable-cache rows non-switchable while preserving the delete-binding confirmation path.

Boundaries kept:
- No subtitle binding/delete semantics, cache deletion semantics, database schema, migration, settings schema, OpenSubtitles behavior, or non-player UI behavior was changed.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5f regression closeout.
- Noise: manual runtime validation is still required for direct row-click switching versus hover submenu opening.

Next step:
- Run build and migration diff checks, then manually validate online subtitle row click and hover behavior.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 18

Goal: widen the online subtitle second-level menu and simplify its hover child menu.

Changed files:
- `PlayerWindow.xaml.cs`
- `PLAYER_STAGE_LOG.md`

Completed:
- Increased the online subtitle second-level row width and visible title width so downloaded subtitle names have more room before ellipsis.
- Restored full-name hover text on online subtitle rows while keeping the explicit hover path that opens the third-level menu.
- Reduced the online subtitle third-level menu to a single action: `删除绑定` for bound subtitles, or `移除临时字幕` for temporary subtitles.
- Removed the previous third-level explanation, confirm, cancel, and cache-unavailable hint rows so the child menu width adapts to the single action text.

Boundaries kept:
- No subtitle binding/delete semantics, cache deletion semantics, database schema, migration, settings schema, OpenSubtitles behavior, or non-player UI behavior was changed.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5f regression closeout.
- Noise: manual runtime validation is still required for simultaneous full-name tooltip plus third-level menu display, and for the direct-delete action discoverability.

Next step:
- Run build and migration diff checks, then manually validate online subtitle row hover tooltip, third-level delete menu, and direct row switching.

## 2026-06-06 Phase 7.5 Player Control Bar Follow-up Polish 19

Goal: restore visible full-text hover for online subtitle rows, compact the online subtitle menu, and reduce subtitle-menu jank after online subtitles are downloaded.

Changed files:
- `PlayerWindow.xaml.cs`
- `PLAYER_STAGE_LOG.md`

Root-cause findings:
- The default menu tooltip path was unreliable once a submenu row was explicitly opened on hover; the row entered submenu mode before the normal tooltip timer could visibly show the full text.
- After downloading online subtitles, the subtitle menu gained additional submenu rows. The previous submenu placement code configured each submenu popup during `Loaded`, then configured again on hover/open. That meant opening the subtitle menu did extra template lookup and popup setup work for every submenu row even before the user hovered them.
- The extra online-subtitle row submenus made this heavier than source/audio menus, matching the observation that the menu became sluggish only after online subtitles existed.

Completed:
- Added an explicit full-text `ToolTip` for online subtitle rows, opened immediately on row hover and placed to the left so it can be visible while the third-level menu opens on the right.
- Made online subtitle rows more compact with smaller local padding, lower row height, and a smaller font while keeping the widened row/title width.
- Made the single delete/remove third-level action compact: smaller font, narrow padding, and no forced wide content.
- Removed eager submenu popup configuration from `Loaded`; popup placement is now configured only when a submenu is actually opened.
- Removed duplicate subtitle-group submenu-open wiring and let the shared submenu open handler own placement.
- Simplified the mouse-enter handler so it only opens the submenu; placement work is centralized in `SubmenuOpened`.

Boundaries kept:
- No subtitle binding/delete semantics, cache deletion semantics, database schema, migration, settings schema, OpenSubtitles behavior, or non-player UI behavior was changed.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for this follow-up.
- Deferred: true perceptual dimming still needs a separate design; 7.5f regression closeout.
- Noise: manual runtime validation is still required for hover full-text placement, third-level delete menu width, and subtitle-menu smoothness during active playback.

Next step:
- Run build and migration diff checks, then manually validate tooltip visibility and subtitle-menu responsiveness after downloading online subtitles.

## 2026-06-06 Phase 7.5f Regression Closeout Audit

Goal: move the online subtitle search dialog title 10px to the right, then run the 7.5f regression / boundary audit and report mismatches without fixing code.

Changed files:
- `OnlineSubtitleSearchWindow.xaml`
- `PLAYER_STAGE_LOG.md`

Completed:
- Moved the online subtitle search dialog title row from a 16px left margin to a 26px left margin.
- Re-read the Phase 7.5 plan, player rewrite plan, player known issues, online subtitle known issues, player page spec, online subtitle search spec, design rules, resource notes, and player screenshots.
- Audited the current player subtitle menu, online subtitle search window, Settings OpenSubtitles entry, and page-level online subtitle references against the 7.5f boundary.
- Confirmed the search dialog still opens from the player path, pauses active playback before opening, and does not contain OpenSubtitles credential editing UI.
- Confirmed Settings remains the OpenSubtitles configuration surface and page-level search found no detail-page online subtitle entry.

Reported mismatches only:
- The Phase 7.5 plan and player page spec still require the subtitle menu group label `在线下载字幕`, but current player code labels that group `在线字幕`.
- The Phase 7.5 plan and player page spec require lightweight confirmation for deleting online subtitle bindings, with copy that says cache files are not deleted. Current player code exposes a direct `删除绑定` / `移除临时字幕` action and only reports after deletion that the cache file was retained.
- External subtitle tooltip construction still appends `完整路径` and `URL` for subtitle items. If a scanned external subtitle uses a WebDAV playback URL, that may violate the full WebDAV URL no-bare-display rule.
- `PHASE_7_5_PLAYER_UI_PLAN.md` still says `7.5e - 7.5f 尚未实现`, while the stage log contains multiple 7.5e online subtitle search passes and this 7.5f audit.

Boundaries kept:
- No player playback core, source switching algorithm, subtitle binding semantics, audio switching behavior, OpenSubtitles API contract, cache deletion semantics, settings schema, database schema, migration, or database update behavior was changed.
- No mismatch found during this audit was fixed in code.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed by build or migration checks.
- Deferred: true perceptual dimming still needs a separate design; subtitle/audio memory, large WebDAV long-play stutter, active full-file/offline/partial-range cache, and global UI 7.8 closeout remain out of 7.5 scope.
- Noise: manual runtime validation is still required for the 10px title offset, popup/menu placement under the target DPI, subtitle-menu hover/delete discoverability, and active-playback control responsiveness.

Next step:
- Decide whether to fix the reported mismatches in a follow-up patch or intentionally update the 7.5 acceptance wording to match the current implemented behavior.

## 2026-06-06 Phase 7.5 Stage End Title Offset

Goal: move the player window top-left title 10px to the right and close Phase 7.5.

Changed files:
- `PlayerWindow.xaml`
- `PHASE_7_5_PLAYER_UI_PLAN.md`
- `PLAYER_STAGE_LOG.md`

Completed:
- Moved the normal player titlebar title row from a 12px left margin to a 22px left margin.
- Moved the fullscreen player titlebar title row from a 12px left margin to a 22px left margin, keeping popup / fullscreen title alignment consistent with the normal window.
- Marked Phase 7.5 as ended in the Phase 7.5 plan and recorded 7.5e / 7.5f validation status.
- Kept the previous 7.5f audit mismatches as reported items only; this pass did not change subtitle menu wording, online subtitle delete flow, external subtitle tooltip path / URL behavior, or any player business semantics.

Boundaries kept:
- No mpv playback core, playback source switching, subtitle binding/delete semantics, audio switching behavior, OpenSubtitles API contract, cache deletion behavior, settings schema, database schema, migration, or database update behavior was changed.
- No non-player page was modified.

Verification:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` reported only LF/CRLF normalization warnings.

Known Issues:
- Blocker: none confirmed for Phase 7.5 closeout.
- Deferred: true perceptual dimming still needs a separate design; subtitle/audio memory, large WebDAV long-play stutter, active full-file/offline/partial-range cache, and global UI 7.8 closeout remain out of 7.5 scope.
- Noise: manual runtime validation is still required for the final 10px player-title offset in normal and fullscreen states, plus popup placement and online subtitle menu behavior under the target DPI.

Next step:
- Phase 7.5 is closed. Proceed only to a new explicit follow-up phase or to a focused patch for any previously reported audit mismatch.

## 2026-06-16 Watch Duration Progress-accounting Follow-up

Goal:
- Prevent paused, buffered, background, or idle player wall-clock time from inflating `DurationWatchedSeconds`.

Completed:
- Replaced watch-duration persistence from wall-clock session age with accumulated playback-position progress.
- Reset the watch-duration baseline when a watch history starts from a resume position.
- Reset the baseline when the user seeks, so forward seek jumps are not counted as watched time.
- Existing progress, resume, completion, auto-watched, and history persistence service contracts remain unchanged.

Not done:
- No database cleanup was performed for existing historical rows.
- No database schema, migration, playback engine, subtitle/audio, cache, commit, or push behavior was changed.

Validation:
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- Read-only database inspection of the reported movie showed 28 history rows with raw statistic-eligible duration totaling 4505 seconds while max playback position was only 919 seconds, matching the prior wall-clock overcount failure mode.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: existing inflated rows still require an explicit database-cleanup task if the user wants current historical totals rewritten.
- Noise: future sessions count playback-position advance; seek-heavy validation should be done manually in the WPF player.

## 2026-06-16 Playback History Refresh And Percent Follow-up

Goal:
- Make a short valid player session appear on Home continue-watching after close, and align Playback History progress text precision with library progress.

Completed:
- Player progress persistence now emits a playback-history refresh after a meaningful save completes.
- The refresh is position-deduplicated within the current playback history session to avoid repeated UI refreshes for the same saved position.
- Playback History progress text and poster label now keep one decimal place, matching small-percent progress display expectations.
- Home continue-watching progress text and media-library poster-card progress labels now also use the shared display rule: exact zero shows `0%`, otherwise the percentage keeps one decimal place.
- Home continue-watching no longer formats `ProgressValue` directly in XAML; it binds the preformatted progress text from the read model.

Not done:
- No database cleanup was performed for existing rows.
- No database schema, migration, playback engine, subtitle/audio, cache, commit, or push behavior was changed.

Validation:
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: manually verify a pause-and-close session after around 7 seconds appears on Home continue-watching immediately after close.
- Noise: Home may still receive an earlier close-lifecycle refresh before the save completes; the new post-save refresh is intended to resolve the visible stale state.

## 2026-06-16 Resume Message Playback-start Timing Follow-up

Goal:
- Make the `已从 xx 继续播放` message auto-hide timer start only after real playback progress begins.

Completed:
- Removed the early auto-hide scheduling from the player `Playing` event.
- The resume message now records its resume-position baseline when shown.
- The auto-hide timer is scheduled only when the player is currently playing and the observed playback position has advanced beyond that baseline.
- Both playback position event updates and timer-poll position updates use the same scheduling guard.

Not done:
- No database cleanup, schema change, migration, playback engine, subtitle/audio, cache, commit, or push behavior was changed.

Validation:
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: manually verify WebDAV startup where `Playing` fires before the first real time position; the resume message should remain visible until playback position advances.
- Noise: extremely small initial position jitter below the guard threshold is intentionally ignored.

## 2026-06-19 Player Volume And Brightness Standards Audit

Goal:

- Verify whether player volume and brightness values at 100 follow the intended mpv semantics, and change only confirmed defects.

Completed:

- Confirmed UI volume 100 maps directly to mpv volume 100, which is the standard neutral level with no reduction or amplification.
- Confirmed UI brightness 100 maps to mpv brightness 0, preserving the source video's original brightness.
- Added mpv initialization option `volume-max=200` so the existing UI boost range from 101% to 200% is not restricted by mpv's default 130% ceiling.

Not done:

- Did not boost or reinterpret volume 100.
- Did not change brightness mapping, monitor backlight, contrast, gamma, player preference schema, database schema, migration, database update, commit, or push behavior.

Validation:

- `dotnet build MediaLibrary.sln -m:1 -v:minimal` passed with 0 warnings and 0 errors.
- Static code audit confirmed volume 100 maps to mpv 100, brightness 100 maps to mpv 0, and mpv initialization now sets `volume-max=200`.

Known Issues:

- Blocker: none confirmed by build.
- Deferred: manually compare 100%, 130%, and 200% volume on a quiet source and watch for source-dependent clipping above 100%.
- Noise: source mastering, Windows system volume, output-device gain, and display characteristics can make neutral 100% feel quiet or dark.
