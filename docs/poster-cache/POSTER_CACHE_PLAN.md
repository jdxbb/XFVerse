# Poster Cache Phase 1 Plan

## Stage Goal

Poster Cache Phase 1 adds a basic local file cache for remote movie poster images across the desktop app. The cache is display infrastructure only: it does not change movie metadata, recommendations, watch insights, player behavior, WebDAV scanning, or database schema.

## Coverage

- Home page posters: recently played, recently added, AI recommendation preview.
- Library poster grid.
- Favorites page posters.
- Watch history posters.
- Movie detail poster.
- Movie discovery posters: search results, ranking lead item, ranking list, AI recommendation tab.
- Recommendations page poster cards.

Only `http` and `https` poster URLs are cache candidates. Empty values, local file paths, `pack://` resources, and `Assets/WatchPersonas` resources stay on their existing path.

## Design

- `AppPaths.GetPosterCacheDirectory()` provides the application poster cache root.
- `IPosterCacheService` lives in the App layer and resolves a display path for one poster URL.
- `PosterCacheService` hashes the full remote URL with SHA256 and stores a local file under `PosterCache/items`.
- `PosterCacheImageBehavior` is attached to WPF `Image` controls that display remote movie posters.
- Cache hits return the local file path. Misses download asynchronously. Failures fall back to the original source.
- Concurrent requests for the same URL share one download task. Global download concurrency is capped.
- Failed downloads use an in-memory 10 minute cooldown per hash to avoid repeated requests during one app session.
- Service-level single URL refresh is available through `RefreshAsync`; no refresh UI is added in Phase 1.

## Out of Scope

- No capacity limit.
- No cache size calculation.
- No automatic large-scale cleanup.
- No settings page cache UI.
- No bulk refresh or full cache rebuild.
- No video cache changes.
- No database changes or migration.
- No Watch Insights or persona poster changes.
- No TV, local folder, subtitle, or final UI redesign work.

## Acceptance

- Cached remote posters display from local files after first successful load.
- Remote fallback still works when a download fails.
- Existing local and pack resource images are not cached.
- `dotnet build MediaLibrary.sln` succeeds with 0 warnings and 0 errors.
- Runtime verification should find hash-named image files under `PosterCache/items` and no leftover `.tmp` files after successful downloads.
