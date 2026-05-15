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
- No known Phase 4.10 blocker after TV metadata hydration and unavailable Season display; build verification remains the gate.
- No known Phase 4.10.1 blocker after metadata-only TV library visibility and batch-rule refinement; build verification remains the gate.
- No known Phase 4.10.3 schema blocker after adding the visibility-state fields; build verification remains the gate.
- No known Phase 4.10.4 blocker after connecting visibility-state query and remove semantics; build verification remains the gate.
- No known Phase 4.10.4d blocker after visibility wording, Hidden restore, and movie-only AI input cleanup; build verification remains the gate.
- No known Phase 4.10.4f blocker after changing remove-from-library to hide-only semantics; build verification remains the gate.
- No known Phase 4.10.5 blocker after adding explicit add / restore visibility actions; build verification remains the gate.
- No known Phase 4.10.5b blocker after source / state-aware restore and SeriesOverview action consistency fixes; build verification remains the gate.

## Deferred

- `TvSeasonRatingSources` is deferred until a later rating persistence decision.
- Season-level IMDb rating display is deferred unless OMDb responses can be verified as stable and unambiguous.
- Additional TV detail refinements are deferred until the library and discovery phases expose their entry points.
- Complex anime season models remain deferred.
- Multi-episode file expansion remains deferred.
- Final correction dialogs / UI entry points remain deferred.
- Per-episode independent collection state remains deferred.
- Full not-in-library TV external detail pages are replaced by Phase 4.10 metadata hydration and `SeriesOverviewPage` reuse.
- Full advanced TV discovery filters remain deferred; Phase 4.8 Bugfix adds basic client-side filters without forcing Movie person search or Movie genre semantics onto TV.
- Full TV feature-gap audit and stage reorder remains deferred to the next phase.
- Not-in-library TV metadata cleanup remains deferred.
- TV correction entry points and cross-type correction UI remain deferred.
- Full Phase 4 regression and documentation closeout is deferred to Phase 4.9.
- Full TV status filtering in normal Series mode remains deferred; Season state is surfaced in batch mode and favorites.
- Final TV UI polish remains deferred.
- Cleanup for metadata-only TV Series / Seasons / Episodes remains deferred after Phase 4.10.
- Persistent TV metadata refresh / stale-data policy remains deferred after Phase 4.10.
- TV correction entry UI remains Phase 4.11.
- TV scan / rescan / history-location hardening remains Phase 4.12.
- TV AI support remains a Phase 5 candidate and should not be folded into Phase 4.
- Automatic cleanup for metadata-only TV rows created by discovery browsing remains deferred after Phase 4.10.1.
- More granular metadata-only TV library filters remain deferred; Phase 4.10.1 keeps default visibility conservative.
- TV metadata hydration progressive loading remains deferred to Phase 4.10.6.
- A future explicit remove-source action may be needed if users need to detach playback sources without deleting full software records.

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
- Not-in-library TV Series clicks no longer show the old future-detail placeholder; Phase 4.10 hydrates metadata and enters `SeriesOverviewPage`.
- Phase 4.10 changes not-in-library TV Series clicks to write metadata-only TV rows and navigate to `SeriesOverviewPage`; these rows are not playback sources and should not be counted as in-library sources.
- Source-less Episodes can be marked watched / unwatched by design, but they remain non-playable until a real active `MediaFile` is attached.
- Metadata-only TV Series with no source and no user state are intentionally hidden from the default media-library list.
- Phase 4.10.1 batch-remove skip behavior is superseded by Phase 4.10.4; source-less remove now hides rows with `Hidden` while preserving state and metadata.
- Batch delete record removes software records and source rows from the app database only; it must not delete local or WebDAV files.
- Phase 4.10.4d keeps recommendation algorithm semantics unchanged; it only filters pure visibility-only Movie rows out of movie AI/profile/statistics/recommendation input surfaces.
- Phase 4.10.4f does not restore old `MediaFile.IsDeleted` rows created by earlier remove-from-library behavior, because those rows cannot be safely separated from missing-file, removed-path, or delete-record history. If files still exist, rescan recovery should be verified in Phase 4.13.
- Phase 4.10.5 add-to-library writes `Visible` as media-library visibility only; it intentionally does not create playback sources, set preference state, restore old `IsDeleted` rows, or include TV in AI / Watch Insights.
- Phase 4.10.5b supersedes Phase 4.10.5 restore behavior: removed-library restore writes `Auto` when active source or real current state exists, and writes `Visible` only for source-less no-state rows.
