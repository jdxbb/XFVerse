# 2026-06-04 Poster Loading / Retry Follow-up

## Goal

- Keep poster placeholders in a loading state until the current poster request succeeds or reaches a final failure.
- Replace retry sleep intervals with short progressive request timeouts.

## Completed

- Poster loading state is now synchronized through nearby poster containers so placeholder templates can reliably show the spinner while the bound image request is active.
- Poster placeholder templates now read the inherited load state from their own visual elements instead of only the templated parent, so sibling image requests update both the spinner and final no-poster label.
- The poster spinner animation now starts when the loading indicator becomes visible, not only on the initial template load, so retrying after an empty / failed state can still animate.
- Poster load-state synchronization is now scoped to the requesting image's direct poster host instead of multiple ancestor containers. This prevents another poster / backdrop image on the same page from overwriting a still-loading poster's placeholder state.
- The detail backdrop sampling image in `MainWindow` now disables load-state propagation. Offline diagnostics showed that the hidden 1x1 backdrop image could mark the whole shell grid as `Loaded` while a detail poster was still retrying, hiding the poster spinner until the final failure state.
- Poster placeholder status text now distinguishes final states: `Empty` shows `无海报`, and failed loading after retries shows `加载失败`.
- Poster diagnostics now include `ui-load-state` events and per-attempt elapsed time. Set `XFVERSE_POSTER_CACHE_DIAGNOSTICS=1` before starting the app, then reproduce and inspect `logs/poster-cache-debug.log`.
- Remote poster attempts now use per-attempt request timeouts of 1 / 2 / 3 / 4 seconds.
- Retry attempts no longer wait on separate delay intervals between failures.
- The no-poster label is still shown only after the active request returns empty or fails after the available attempts.

## Not Done

- No cache schema, persistent failed-download marker, cleanup policy, settings UI, database schema, migration, database update, commit, or push was changed.
- No network-retry setting was added to the UI.

## Verification

- `dotnet build MediaLibrary.sln`: passed, 0 warnings / 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`: empty.

## Known Issues

### Blocker

- None after build verification.

### Deferred

- Persistent failed-download markers that survive app restart remain a Phase 2 cache decision.
- Runtime offline acceptance should still verify the exact spinner-to-no-poster transition on the user's network environment.

### Noise

- The failure cooldown remains in memory only and resets when the app restarts.
