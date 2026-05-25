# Software Cache Management Phase 1 Stage Log

## Implementation Notes

- Replaced the user-visible Settings cache card with a software cache area.
- Added poster cache usage, clear, settings, and best-effort capacity trimming.
- Added a 512 MB default poster cache capacity limit.
- Added software cache aggregation for the Settings page.
- Added ExternalMetadataCache maintenance for TMDB / OMDb cache rows using a strict provider/type whitelist.
- Kept video cache services and playback behavior in place while removing the standalone video cache settings UI.

## Boundaries Kept

- No migration was added.
- No video cache files are cleared by software cache management.
- No VideoCacheService or player core behavior is changed.
- No user configuration or primary media data is cleared.
- No Watch Insights, user profile, persona poster, recommendation preference, playback progress, collection, or watch history data is cleared.

## Verification Checklist

- Run `dotnet build MediaLibrary.sln`.
- Open Settings > General and confirm the software cache area appears.
- Confirm the old standalone video cache settings card is not visible.
- Save a poster cache MB limit and restart to verify persistence.
- Clear poster cache and verify only the managed poster cache is affected.
- Clear other cache and verify only TMDB / OMDb ExternalMetadataCache rows are affected.
- Run `git status --short --branch` and `git diff --stat`.
- Confirm no migration was created.

## Phase 5.1 Online Subtitle Cache Groundwork

- Phase 5.1 adds a managed online subtitle cache service and wires subtitle cache usage / clear primitives into the software cache service model.
- The Settings software-cache UI is not expanded in 5.1; user-visible subtitle cache clearing remains Phase 5.4.
- The subtitle cache root is isolated from poster cache, video cache, media files, and WebDAV files.
- Supported cached subtitle extensions are `.srt`, `.ass`, `.ssa`, and `.vtt`.
- Deleting an online subtitle binding remains separate from physical cache cleanup.
