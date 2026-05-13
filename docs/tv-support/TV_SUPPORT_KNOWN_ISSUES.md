# TV Support Known Issues

## Blocker

- No known Phase 4.3 code blocker after service-layer build validation.
- Real database migration validation for Phase 4.1 remains required before broad manual scanning against a user library.

## Deferred

- `TvSeasonRatingSources` is deferred until a later rating persistence decision.
- Season-level IMDb rating display is deferred unless OMDb responses can be verified as stable and unambiguous.
- TV search and TV ranking UI are deferred to Phase 4.7.
- TV detail pages are deferred to Phase 4.4.
- Episode playback is deferred to Phase 4.5.
- Complex anime season models remain deferred.
- Multi-episode file expansion remains deferred.
- Final correction dialogs / UI entry points remain deferred.
- Per-episode independent collection state remains deferred.

## Noise

- TMDB season and episode rating fields are response-dependent and should be treated as optional.
- OMDb season responses can expose episode ratings without proving a stable Season-level rating.
- External metadata cache cleanup already targets the managed TMDB / OMDb cache provider and cache types used by TV metadata.
- Phase 4.3 TV scan grouping is intentionally conservative so ordinary movie folders continue through the movie flow.
- Scan counters still mostly reflect file scanning rather than separate Movie / TV identification subtotals.
- Unsupported multi-episode files are handled as unsupported sources, not as fully modeled Season contents.
