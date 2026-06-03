# Poster Cache Phase 1 Stage Log

## Implementation Notes

- Added App-layer poster cache service and interface.
- Added a WPF Image attached behavior for remote poster display.
- Added the minimal AppPaths poster cache directory helper.
- Registered the poster cache service in the app service provider.
- Switched remote movie poster Image bindings to the poster cache behavior.

## Explicit Non-Goals Kept

- No poster cache capacity setting.
- No cache size statistics.
- No automatic cleanup service.
- No settings page cache management UI.
- No video cache changes.
- No database entity, DbContext, or migration changes.
- No Watch Insights, user profile, or persona poster changes.

## Verification

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Runtime cache check confirmed `PosterCache/items` exists under the app data directory.
- Runtime cache check found 96 image cache files and 0 `.tmp` files.
- Runtime cache check found all sampled cache files use SHA256-style 64-character hex names under two-character shard folders.
- Runtime cache check found all sampled cache files had valid JPEG signatures.
- Log check found no PosterCache-specific log output and no poster URL logging; this matches the Phase 1 privacy boundary.
- Manual verification confirmed poster display and cache file creation across the pages listed in the plan.

## Current Runtime Evidence

- Cached image count: 96.
- Cached temporary file count: 0.
- Cached image extension set observed: `.jpg`.
- Total observed poster cache bytes: about 8.1 MB.
- Latest observed poster cache write time: 2026-05-13 during manual acceptance.

## 2026-06-02 Detail Image Quality Follow-up

Goal: improve detail-page poster/still clarity without changing list thumbnail behavior.

Changed behavior:

- Added a detail-only poster-cache request option that can rewrite TMDB image URLs from fixed `w*` / `h*` sizes before downloading. The original-size detail path from this follow-up was later superseded by the 2026-06-03 `w780` detail image quality follow-up below.
- Increased detail decode widths for Movie, Series, Season, and Episode detail hero images.
- Kept media-library poster/list thumbnails and Series detail Season-list thumbnails on the existing thumbnail-size path to avoid extra list scrolling cost.

Boundaries kept:

- No cache schema, database field, migration, cleanup policy, settings UI, scan rule, or metadata-write behavior changed.

Build:

- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## 2026-06-03 Poster Loading State Follow-up

Goal: separate poster loading from true no-poster / failed-poster states.

Changed behavior:

- Poster placeholders keep the existing poster-placeholder image as the background.
- Active poster requests now show a spinner on top of that placeholder image instead of the `无海报` label.
- The `无海报` label is shown only when the bound poster source is empty or the current poster request cannot produce a local cached image after its available attempts.
- Rebinding a poster source starts from the loading state again before the new request result is known.
- Remote fallback is no longer treated as a third visible poster state in the UI; if the cache path cannot provide a local image for the current request, the placeholder switches to failed / no-poster display.

Boundaries kept:

- No poster cache schema, database field, migration, cleanup policy, settings UI, scan rule, metadata-write behavior, commit, or push changed.

Build:

- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## 2026-06-03 Offline Failure Visibility Follow-up

Goal: ensure a failed poster request leaves the user-visible no-poster state instead of an empty placeholder.

Changed behavior:

- Poster request state is now synchronized to the sibling placeholder controls inside the same poster container, not only to the `Image` element and inherited parent.
- The poster service retry path is unchanged. A poster request still uses the existing download attempts before the UI switches to failed / no-poster display.
- Rebinding or re-entering a page still starts from loading state and goes through the existing request path again; this follow-up does not add failed-download cooldown short-circuiting.

Boundaries kept:

- No poster cache schema, database field, migration, cleanup policy, settings UI, scan rule, metadata-write behavior, commit, or push changed.

Build:

- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.

## 2026-06-03 Detail Image Quality Follow-up

Goal: keep detail-page hero poster / still clarity high without the loading cost of TMDB `original`.

Changed behavior:

- Movie, Series, Season, and Episode detail hero images now request TMDB `w780` and decode at width 780.
- The Series detail Season-list small posters remain on the existing thumbnail-sized binding.
- The poster-cache behavior still supports the older original-size option, but the detail pages no longer opt into it.

Boundaries kept:

- No media-library poster/list thumbnail behavior, cache schema, database field, migration, cleanup policy, settings UI, scan rule, or metadata-write behavior changed.

Build:

- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.
