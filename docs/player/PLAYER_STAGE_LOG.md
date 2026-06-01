# Player Stage Log

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
