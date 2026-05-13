# Software Cache Management Phase 1 Plan

## Stage Goal

Phase 2 adds a user-visible software cache management area in Settings. The user-facing structure is:

- Poster cache
- Other cache

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

## Explicit Non-Goals

- No database schema changes and no migration.
- No video cache clear, trim, or behavior changes.
- No player core changes.
- No WebDAV scan changes.
- No subtitle search or subtitle cache directory.
- No local folder support.
- No TV features.
- No final UI polish pass.
- No recommendation algorithm changes.
- No Watch Insights, user profile, or persona logic changes.
- No cleanup of user configuration, API keys, tokens, passwords, local media files, WebDAV media files, persona poster assets, playback progress, collections, watch state, watch history, recommendation preferences, or primary database records.

## Acceptance

- Settings > General shows a software cache area with Poster Cache and Other Cache.
- No standalone video cache settings card is visible.
- Poster cache capacity defaults to 512 MB and persists across restart.
- Poster cache clear only removes managed poster cache files.
- Other cache clear only removes TMDB / OMDb external metadata cache rows.
- Build succeeds with 0 warnings and 0 errors.
- No migration is added.
