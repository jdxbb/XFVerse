# Software Cache Management Phase 1 Known Issues

## Blocker

- Other cache clearing depends on the confirmed TMDB / OMDb `ExternalMetadataCache` provider/type whitelist. If those strings change, the whitelist must be updated before clearing is allowed.
- Poster cache deletion must remain restricted to the managed `PosterCache/items` directory.
- Poster cache capacity limit must keep a real trim path; it must not become a display-only field.

## Deferred

- Subtitle cache directory groundwork exists after Phase 5.1, but the user-visible subtitle cache clearing UI remains deferred to Phase 5.4.
- Online subtitle search integration remains deferred to Phase 5.2 / 5.3.
- AI, recommendation, Watch Insights, and user profile cache cleanup.
- Cross-process poster failure markers.
- More granular software cache categories.
- Any future decision to remove or redesign the underlying video cache implementation.

## Noise

- SQLite database file size might not shrink immediately after ExternalMetadataCache rows are deleted.
- Poster cache clear causes first subsequent poster display to download again.
- Poster cache usage can differ slightly from operating system disk usage because file system allocation units are not counted.
