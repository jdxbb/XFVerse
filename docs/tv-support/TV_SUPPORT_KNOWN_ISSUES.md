# TV Support Known Issues

## Blocker

- No known Phase 4.3 code blocker after service-layer build validation.
- Real database migration validation for Phase 4.1 remains required before broad manual scanning against a user library.
- No known Phase 4.4 build blocker after hidden route and page validation.
- No known Phase 4.5 build blocker after Episode playback integration.
- No known Phase 4.6 build blocker after media-library, home, history, and favorites integration.
- No known Phase 4.6 Bugfix blocker after validation fixes are applied; build verification remains the gate.
- No known Phase 4.6 Bugfix 2 blocker after Season state and poster-cache fixes; build verification remains the gate.
- No known Phase 4.7 blocker after rating display and mixed batch closeout; build verification remains the gate.
- No known Phase 4.8 blocker after TV discovery search and rankings integration; build verification remains the gate.
- No known Phase 4.8 Bugfix blocker after TV discovery UI / filtering / ranking parity fixes; build verification remains the gate.

## Deferred

- `TvSeasonRatingSources` is deferred until a later rating persistence decision.
- Season-level IMDb rating display is deferred unless OMDb responses can be verified as stable and unambiguous.
- Additional TV detail refinements are deferred until the library and discovery phases expose their entry points.
- Complex anime season models remain deferred.
- Multi-episode file expansion remains deferred.
- Final correction dialogs / UI entry points remain deferred.
- Per-episode independent collection state remains deferred.
- Full not-in-library TV external detail pages remain deferred; Phase 4.8 shows a placeholder instead of reusing Movie detail.
- Full advanced TV discovery filters remain deferred; Phase 4.8 Bugfix adds basic client-side filters without forcing Movie person search or Movie genre semantics onto TV.
- Full TV feature-gap audit and stage reorder remains deferred to the next phase.
- Not-in-library TV detail pages remain deferred; Phase 4.8 / Bugfix still use a prompt instead of Movie detail.
- Full Series / Season metadata completion and source-less Season display remain deferred.
- TV correction entry points and cross-type correction UI remain deferred.
- Full Phase 4 regression and documentation closeout is deferred to Phase 4.9.
- Full TV status filtering in normal Series mode remains deferred; Season state is surfaced in batch mode and favorites.
- Final TV UI polish remains deferred.

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
- Batch favorite, want-to-watch, and not-interested are intentionally not in the batch toolbar. Use Season detail or favorites card actions instead.
- Watch Insights and Watch Profile stay Movie-only; Episode watch history is available in history UI but excluded from statistics, profile, persona, and recommendation fingerprints.
- Existing matched Seasons with only in-library Episode rows need a rescan or TV correction refresh to populate missing TMDB Episode metadata rows.
- Phase 4.8 TV rankings are Series-level because TMDB TV ranking endpoints return Series, not Seasons.
- TV discovery does not request OMDb / IMDb ratings; cards show TMDB Series rating or no rating.
- Not-in-library TV Series clicks intentionally show a future-detail placeholder and do not enter the Movie detail route.
