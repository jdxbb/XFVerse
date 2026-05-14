# TV Support Known Issues

## Blocker

- No known Phase 4.3 code blocker after service-layer build validation.
- Real database migration validation for Phase 4.1 remains required before broad manual scanning against a user library.
- No known Phase 4.4 build blocker after hidden route and page validation.
- No known Phase 4.5 build blocker after Episode playback integration.
- No known Phase 4.6 build blocker after media-library, home, history, and favorites integration.

## Deferred

- `TvSeasonRatingSources` is deferred until a later rating persistence decision.
- Season-level IMDb rating display is deferred unless OMDb responses can be verified as stable and unambiguous.
- TV search and TV ranking UI are deferred to Phase 4.7.
- Additional TV detail refinements are deferred until the library and discovery phases expose their entry points.
- Complex anime season models remain deferred.
- Multi-episode file expansion remains deferred.
- Final correction dialogs / UI entry points remain deferred.
- Per-episode independent collection state remains deferred.
- TV discovery entry points are deferred to Phase 4.7.
- Rich mixed Movie + Season batch semantics remain deferred; Phase 4.6 asks users to split mixed selections.
- Full TV status filtering in normal Series mode remains deferred; Season state is surfaced in batch mode and favorites.

## Noise

- TMDB season and episode rating fields are response-dependent and should be treated as optional.
- OMDb season responses can expose episode ratings without proving a stable Season-level rating.
- External metadata cache cleanup already targets the managed TMDB / OMDb cache provider and cache types used by TV metadata.
- Phase 4.3 TV scan grouping is intentionally conservative so ordinary movie folders continue through the movie flow.
- Scan counters still mostly reflect file scanning rather than separate Movie / TV identification subtotals.
- Unsupported multi-episode files are handled as unsupported sources, not as fully modeled Season contents.
- Phase 4.5 Episode playback exists, but before Phase 4.6 users may still lack a natural media-library click path into TV pages.
- Previous / next and auto-next intentionally stay inside one Season.
- Missing adjacent Episode sources show a friendly notice instead of falling back across seasons.
- Phase 4.6 uses existing card layouts with minimal TV fields; final visual polish remains deferred.
