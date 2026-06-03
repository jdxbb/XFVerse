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

- Added a detail-only poster-cache request option that rewrites TMDB image URLs from fixed `w*` / `h*` sizes to `original` before downloading.
- Increased detail decode widths for Movie, Series, Season, and Episode detail hero images.
- Kept media-library poster/list thumbnails and Series detail Season-list thumbnails on the existing thumbnail-size path to avoid extra list scrolling cost.

Boundaries kept:

- No cache schema, database field, migration, cleanup policy, settings UI, scan rule, or metadata-write behavior changed.

Build:

- `dotnet build MediaLibrary.sln`, 0 warning / 0 error.
