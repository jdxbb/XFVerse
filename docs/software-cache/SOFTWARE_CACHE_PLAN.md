# Software Cache Management Phase 1 Plan

## Stage Goal

Phase 2 adds a user-visible software cache management area in Settings. The user-facing structure is:

- Poster cache
- Other cache
- Online subtitle cache, added by Phase 5.4 for downloaded online subtitle files

The previous standalone video cache settings card is removed from the Settings UI to avoid presenting a confusing cache concept to users. The underlying video cache services, playback integration, mpv-session behavior, and cache directory are not deleted or changed in this phase.

## Poster Cache

- Shows current cache usage and file count.
- Supports manual clear with confirmation.
- Supports a capacity limit in MB.
- Default capacity limit is 512 MB.
- The limit is stored in poster cache settings under the poster cache area.
- Saving a lower limit immediately performs best-effort trimming.
- Successful poster downloads also run best-effort trimming.
- Trimming uses a simple least-recently-used order based on last access time, falling back to last write time.
- Deletion is restricted to the managed `PosterCache/items` directory.

## Other Cache

- Shows reusable external metadata cache status.
- Supports manual clear with confirmation.
- Does not support a capacity limit.
- Phase 2 only includes TMDB / OMDb rows in `ExternalMetadataCache`.
- Clear scope is restricted to the confirmed provider/type whitelist:
  - `TMDB` / `Search`
  - `TMDB` / `Detail`
  - `TMDB` / `ExternalIds`
  - `OMDb` / `Rating`
- SQLite database file size is not promised to shrink immediately after rows are deleted.

## Online Subtitle Cache

- Shows total online subtitle cache usage and file count.
- Shows how many supported subtitle cache files are protected by active `OnlineSubtitleBinding` rows and how many are orphaned.
- Supports manual cleanup of orphan online subtitle cache files only.
- Orphan cleanup is restricted to supported subtitle files under the managed online subtitle cache root.
- A cache file is not orphaned when any active Movie / Episode / MediaFile online subtitle binding references its `CacheRelativePath` or `CacheHash`.
- If binding references cannot be read, cleanup is disabled to avoid deleting still-bound subtitle cache files.
- Deleting a player online subtitle binding does not physically delete the cache file; later orphan cleanup may remove it when no active binding references it.

## Explicit Non-Goals

- No database schema changes and no migration.
- No video cache clear, trim, or behavior changes.
- No player core changes.
- No WebDAV scan changes.
- No subtitle search or subtitle cache directory in the original Phase 1 scope. Phase 5.4 adds only online subtitle cache management for already downloaded online subtitles.
- No local folder support.
- No TV features.
- No final UI polish pass.
- No recommendation algorithm changes.
- No Watch Insights, user profile, or persona logic changes.
- No cleanup of user configuration, API keys, tokens, passwords, local media files, WebDAV media files, persona poster assets, playback progress, collections, watch state, watch history, recommendation preferences, or primary database records.

## Acceptance

- Settings > General shows a software cache area with Poster Cache and Other Cache.
- Settings > General also shows Online Subtitle Cache after Phase 5.4.
- No standalone video cache settings card is visible.
- Poster cache capacity defaults to 512 MB and persists across restart.
- Poster cache clear only removes managed poster cache files.
- Other cache clear only removes TMDB / OMDb external metadata cache rows.
- Online subtitle cache clear only removes orphan online subtitle cache files and does not delete files still referenced by active online subtitle bindings.
- Build succeeds with 0 warnings and 0 errors.
- No migration is added.
