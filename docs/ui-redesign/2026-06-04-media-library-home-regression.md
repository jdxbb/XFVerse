# 2026-06-04 Media Library / Home Regression Follow-up

## Goal

- Fix tested media-library interaction regressions without changing media-library delete / hide semantics.
- Keep the home page from falling back into a false initial-loading overlay after data-refresh notifications.

## Completed

- List layout rows now route the whole row surface through the existing open-or-select command, including padding areas around the progress bar.
- Poster and list layout scroll offsets are kept in memory for the current app session and restored when switching pages or returning from details.
- Scroll restoration uses direct `ScrollToVerticalOffset` retry on layout idle, so returning to the library does not introduce a visible scroll animation.
- TV list rating overrides are cached in memory and applied when list item view models are rebuilt, reducing per-row rating flicker after returning from details.
- Media-library empty state now shows only a concise centered title in the result area, without the previous background frame or secondary description.
- Home pending refresh on activation no longer resets an already loaded dashboard to initial-loading state, and delayed refresh work rechecks whether the page is still active.
- Home first-load no longer waits for AI recommendations before releasing the full-page initial-loading overlay; the dashboard and recent playback load first, then AI recommendations refresh in the background.
- Home component empty states for continue watching, recently added, and AI recommendations now use only centered concise text, without framed containers or secondary descriptions.

## Not Done

- No database schema, migration, database update, scan rule, recommendation algorithm, player behavior, commit, or push was changed.
- Runtime UI acceptance for scroll position on very large libraries remains manual.

## Verification

- `dotnet build MediaLibrary.sln`: passed, 0 warnings / 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`: empty.

## Known Issues

### Blocker

- None after build verification.

### Deferred

- Differential media-library refresh remains a later performance phase; this follow-up keeps the existing refresh pipeline and only preserves session UI state around it.
- Automated UI regression tests are still unavailable in the solution.

### Noise

- Scroll offsets intentionally reset after the app closes.
- Rating override cache is process-local and only prevents the current-session visible flicker; it is not a persisted rating store.
