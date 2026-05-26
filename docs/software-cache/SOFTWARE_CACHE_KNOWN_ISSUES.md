# Software Cache Management Phase 1 Known Issues

## Blocker

- Other cache clearing depends on the confirmed TMDB / OMDb `ExternalMetadataCache` provider/type whitelist. If those strings change, the whitelist must be updated before clearing is allowed.
- Poster cache deletion must remain restricted to the managed `PosterCache/items` directory.
- Poster cache capacity limit must keep a real trim path; it must not become a display-only field.
- Online subtitle cache deletion must remain restricted to orphan files under the managed online subtitle cache root and must not delete files referenced by active `OnlineSubtitleBinding` rows.

## Deferred

- AI, recommendation, Watch Insights, and user profile cache cleanup.
- Cross-process poster failure markers.
- More granular software cache categories.
- Deliberate deletion of still-bound online subtitle cache files is not exposed; Phase 5.4 clears orphan files only.
- Any future decision to remove or redesign the underlying video cache implementation.

## Noise

- SQLite database file size might not shrink immediately after ExternalMetadataCache rows are deleted.
- Poster cache clear causes first subsequent poster display to download again.
- Poster cache usage can differ slightly from operating system disk usage because file system allocation units are not counted.
- Online subtitle orphan cleanup can leave files in place if they are still referenced by a Movie / Episode / MediaFile binding or if binding references cannot be read safely.
