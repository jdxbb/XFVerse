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

## Phase 5.3 Online Subtitle Cache Use

- Phase 5.3 starts writing downloaded online subtitles into the managed subtitle cache.
- The player can delete Movie / Episode online subtitle bindings, but that operation intentionally does not delete cache files.
- Physical subtitle cache statistics and clearing remain Phase 5.4 software-cache UI scope.
- The cache service remains the boundary for extension allow-list, zip validation, path traversal protection, file-size limits, hash naming, usage, and clearing primitives.

## Phase 5.3b Online Subtitle MediaFile Binding Cache Boundary

- Phase 5.3b persists unidentified playback subtitles as MediaFile-level online subtitle bindings.
- Deleting Movie / Episode / MediaFile online subtitle bindings still does not physically delete cache files.
- Delete-record cleanup clears affected binding records but leaves cached subtitle files for later software-cache management.
- Move-from-library / hide-only operations do not clear online subtitle bindings.
- User-visible subtitle cache statistics and clearing are handled by Phase 5.4 below.

## Phase 5.4 Online Subtitle Cache Management

- Settings > General now includes an Online Subtitle Cache card under software cache management.
- The card shows total managed online subtitle cache size and file count.
- The cache service classifies supported `.srt` / `.ass` / `.ssa` / `.vtt` files under the online subtitle cache root as bound or orphan by checking active `OnlineSubtitleBinding.CacheRelativePath` and `CacheHash` references.
- The clear action deletes only orphan online subtitle cache files and leaves files referenced by active Movie / Episode / MediaFile online subtitle bindings untouched.
- If binding references cannot be read, cleanup is disabled to avoid deleting still-used subtitle files.
- Deleting online subtitle bindings in the player remains a soft-delete operation; physical deletion is deferred to this orphan cleanup path.
- Existing poster cache and TMDB / OMDb other-cache behavior is unchanged.
