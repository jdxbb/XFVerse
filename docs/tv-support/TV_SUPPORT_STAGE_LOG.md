# TV Support Stage Log

## Phase 4.2 - TV Metadata Services And Rating Audit

Implemented service-layer metadata support for TV:

- Added TMDB TV service methods for search, series detail, season detail, external ids, popular, top rated, and trending.
- Added dedicated TV metadata read models.
- Added OMDb / IMDb series rating and season rating audit methods.
- Reused existing TMDB credentials, language defaults, base URL fallback, HTTP concurrency limiting, diagnostics style, and external metadata persistent cache.
- Kept TV metadata separate from movie discovery and AI recommendation models.
- Did not add `TvSeasonRatingSources`.
- Did not add a migration.
- Did not connect TV to scanning, UI, playback, discovery, recommendations, Watch Insights, profiles, or fingerprints.

## Manual Acceptance Checklist

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created in Phase 4.2.
3. Existing movie TMDB search and ranking interfaces remain unchanged.
4. TV search returns Series-level result models when API credentials are configured.
5. TV series detail returns series metadata and season summaries.
6. TV season detail returns season metadata and episode list.
7. TV external ids returns IMDb id and available external ids.
8. TV popular, top rated, and trending return Series-level result models.
9. OMDb / IMDb rating policy is documented.
10. No UI changes are included.
11. TV remains excluded from AI, Watch Insights, profiles, and recommendation fingerprint.
12. Documents and reports do not include secrets or private media locations.

## Phase 4.3 - TV Season Scanning And Correction

Implemented service-layer TV scanning and correction support:

- Added TV episode file name parsing for common season / episode formats.
- Added detection for unsupported multi-episode file names without expanding them.
- Added TV season candidate grouping by parent folder.
- Added TV season identification before movie identification in both WebDAV and Local scan flows.
- Added `TvSeries`, `TvSeason`, and `TvEpisode` upsert logic from TMDB TV metadata.
- Added unidentified Season-level fallback using `TvSeason` identification status.
- Attached episode videos through existing `MediaFile` with `EpisodeId`; no `EpisodeMediaFile` was added.
- Added service-layer correction from Movie media file to `TvEpisode`.
- Added service-layer correction from Episode media file to Movie.
- Kept correction source-only: no watched state, collection state, or WatchHistory migration.
- Did not add a migration.
- Did not connect TV to UI, playback, discovery, recommendations, Watch Insights, profiles, or fingerprints.

## Phase 4.3 Manual Acceptance Checklist

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created in Phase 4.3.
3. Existing movie scan logic remains present.
4. Existing WebDAV scan entry remains present.
5. Existing Local scan entry remains present.
6. A folder with `S01E01` and `S01E02` style files can be handled as a TV season candidate.
7. A folder with `E01` and `E02` style files can default to Season 1 when treated as a season context.
8. Multi-episode file names are skipped as unsupported without creating multiple episodes.
9. Unidentified TV seasons stay Season-level and do not scatter into unidentified Movie rows.
10. Subtitle binding and media probing still run through existing `MediaFile`-based flows.
11. Cross-type correction service methods exist for Movie source to TV episode and TV episode source to Movie.
12. TV remains excluded from AI, Watch Insights, profiles, and recommendation fingerprint.
13. Documents and reports do not include secrets or private media locations.

## Phase 4.4 - TV Series And Season Detail Pages

Implemented hidden TV detail pages and query models:

- Added `SeriesOverviewPage` / `SeriesOverviewViewModel`.
- Added `TvSeasonDetailPage` / `TvSeasonDetailViewModel`.
- Added TV detail query service and read models for Series overview, Season list, Season detail, and Episode list.
- Added hidden navigation keys and navigation state for selected TV Series, Season, and optional Episode.
- Registered DataTemplates and DI services for TV detail pages.
- Series page displays Series metadata and Season summaries without user-state actions.
- Season page displays Season metadata, aggregate progress, in-library count, source summary, unidentified Season notice, and Episode list.
- Episode play buttons are placeholders and do not call playback.
- Did not add a migration.
- Did not connect TV to media-library Series cards, playback, favorites, home, watch history, discovery, recommendations, Watch Insights, profiles, or fingerprints.

## Phase 4.4 Manual Acceptance Checklist

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created in Phase 4.4.
3. `SeriesOverviewPage` and `SeriesOverviewViewModel` exist.
4. `TvSeasonDetailPage` and `TvSeasonDetailViewModel` exist.
5. App DataTemplates are registered.
6. Hidden navigation keys are registered without changing fixed main navigation.
7. Series overview can load Series metadata and Season list when navigation state has a Series id.
8. Season detail can load Season metadata and Episode list when navigation state has a Season id.
9. Unidentified seasons display `未识别电视剧季`.
10. Episode play placeholders do not trigger playback.
11. Movie detail behavior remains isolated from TV detail pages.
12. TV remains excluded from AI, Watch Insights, profiles, and recommendation fingerprint.
13. Documents and reports do not include secrets or private media locations.

## Phase 4.5 - Episode Playback Navigation

Implemented Episode playback through the existing player:

- Added an Episode playback entry point on `IPlayerWindowService`.
- Added Episode playback sessions to `IPlaybackSourceService`.
- Kept the existing Movie playback entry point compatible.
- Episode source selection supports explicit source first, then Local, then WebDAV.
- Local Episode files play directly and do not use the WebDAV video cache.
- WebDAV Episode files reuse the existing WebDAV playback and cache path.
- Added player previous / next controls for Episode sessions.
- Movie sessions keep previous / next disabled.
- Episode previous / next stays inside the same Season.
- Auto-next starts only the adjacent next Episode in the same Season.
- Episode playback writes `WatchHistory.EpisodeId`, not `MovieId`.
- Episode playback updates `TvEpisode` lightweight summary fields.
- `TvSeasonDetailPage` Episode play buttons now open Episode playback.
- Did not add a migration.
- Did not connect TV to media-library Series cards, home continue watching, watch-history UI, favorites, discovery, recommendations, Watch Insights, profiles, or fingerprints.

## Phase 4.5 Developer Acceptance Checklist

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created in Phase 4.5.
3. Existing Movie playback entry remains compatible.
4. Episode playback entry exists.
5. `TvSeasonDetailPage` Episode play button calls the Episode playback entry.
6. Local Episode source uses local-file playback.
7. Local Episode source does not use the video cache.
8. WebDAV Episode source reuses WebDAV playback and cache behavior.
9. Movie playback disables previous / next controls.
10. Episode first item disables previous.
11. Episode middle item can use previous / next when adjacent sources exist.
12. Episode last item disables next.
13. Auto-next stays inside the same Season.
14. Last Episode playback stops instead of crossing Season.
15. Episode history writes `EpisodeId` and leaves `MovieId` empty.
16. `TvEpisode` lightweight summary fields are updated by playback progress.
17. TV remains excluded from AI, Watch Insights, profiles, and recommendation fingerprint.
18. Documents and reports do not include secrets or private media locations.

## Phase 4.6 - TV Seasons Integration Into Library, Home, History And Favorites

Implemented the user-visible TV integration layer:

- Media library normal mode can load Movie items plus Series aggregate cards.
- Media library batch mode reloads TV items as Season cards instead of Series cards.
- Content type filtering now supports all, Movie, and TV.
- Series cards navigate to `SeriesOverviewPage`.
- Season batch items can navigate to `TvSeasonDetailPage`.
- Season single-item actions support favorite, want-to-watch, and not-interested from Season detail / favorites card interactions.
- Batch actions support watched / unwatched, remove from library, and delete record.
- Season watched / unwatched only updates in-library Episodes and does not create `WatchHistory`.
- Movie + Season mixed batch selections are rejected with a split-operation message for MVP safety.
- Home continue watching can show and resume Episode playback.
- Watch history can show Episode entries and navigate to the target Season detail with selected Episode.
- Favorites can show Season cards in favorite and want-to-watch tabs; Series and Episode cards are not added there.
- Delete Season record follows the software-record-only model and never deletes local or WebDAV files.
- Did not add a migration.
- Did not connect TV to discovery, recommendations, Watch Insights, profiles, or fingerprints.

## Phase 4.6 Manual Acceptance Checklist

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created in Phase 4.6.
3. Media library normal mode shows Movie + Series.
4. Series cards show poster fallback, Season count, source summary, and progress summary.
5. Clicking a Series card opens `SeriesOverviewPage`.
6. `SeriesOverviewPage` can open `TvSeasonDetailPage`.
7. Episode list playback remains available from Season detail.
8. Content type filter can isolate Movie or TV items.
9. Source filter still applies to Series / Season source aggregates.
10. Batch mode expands Series into Season cards.
11. Season single-item state actions update `UserTvSeasonCollectionItem` and state history.
12. The batch toolbar only exposes watched, unwatched, remove from library, and delete record.
13. Season batch watched / unwatched only affects in-library Episodes and creates no WatchHistory rows.
14. Season remove marks Episode media files removed without deleting physical files.
15. Season delete removes software records only and does not delete physical files.
16. Favorites favorite tab includes favorite Seasons.
17. Favorites want-to-watch tab includes want-to-watch Seasons.
18. Favorites do not show Series or Episode cards.
19. Home continue watching can resume Episodes through the Episode player entry.
20. Watch history displays Episode-specific titles and navigates to Season detail.
21. Existing Movie library, batch, home, history, favorites, playback, scanning, recommendations, and Watch Insights remain separate from TV.
22. Documents and reports do not include secrets or private media locations.

## Phase 4.6 Bugfix - TV Seasons Integration Validation Fixes

Validation fixes applied before Phase 4.7:

- Auto-next Episode playback now reuses the same adjacent Episode switching path as manual next and runs the UI-affecting switch on the UI dispatcher.
- `TvSeasonDetailPage` now has Season-level favorite, want-to-watch, and not-interested toggle buttons.
- Episode rows now expose watched / unwatched actions that update `TvEpisode` summary fields without creating `WatchHistory`.
- The media-library batch toolbar is restricted to watched, unwatched, remove from library, and delete record.
- Batch favorite, want-to-watch, not-interested, and batch AI identify are not shown in the toolbar.
- `SeriesOverviewPage` Season list now uses bounded grid layout so the list can scroll.
- Watch Insights / Watch Profile audit confirmed TV is excluded through Movie-only statistics and profile input queries.
- Did not add a migration.
- Did not start TV discovery search / ranking UI.

## Phase 4.6 Bugfix Manual Acceptance Checklist

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created.
3. Manual next Episode still works.
4. Auto-next no longer reports switching failure.
5. Auto-next stays inside the same Season.
6. Last Episode stops instead of crossing Season.
7. Season detail shows favorite, want-to-watch, and not-interested toggle buttons.
8. Season state changes are visible in favorites after refresh.
9. Episode rows show watched / unwatched actions.
10. Episode watched / unwatched changes refresh aggregate progress.
11. Episode watched / unwatched does not create WatchHistory rows.
12. Batch toolbar only shows watched, unwatched, remove from library, and delete record.
13. Batch toolbar does not show favorite, want-to-watch, or not-interested.
14. Movie batch watched / unwatched / remove / delete remain available.
15. Season batch watched / unwatched still avoids WatchHistory.
16. `SeriesOverviewPage` Season list scrolls when content exceeds the page height.
17. Watch Insights total and monthly watch time do not include Episode history.
18. Watch count, calendar, distributions, profile input, persona input, and recommendation fingerprint remain Movie-only.
19. TV does not enter AI recommendations.
20. Documents and reports do not include secrets or private media locations.

## Phase 4.6 Bugfix 2 - Season State Rules And Poster Cache

Validation fixes applied before Phase 4.7:

- TV poster bindings now use the existing poster cache image behavior on Library, Series overview, Season detail, Favorites, Home, and Watch History surfaces.
- `UserTvSeasonCollectionItem.IsWantToWatch` no longer defaults to true in code, fixing the favorite action accidentally creating want-to-watch state.
- Season favorite is allowed only when the full TMDB Episode count is watched.
- Season want-to-watch is allowed only when watched Episode count is zero.
- Season not-interested cancels favorite and want-to-watch but does not alter Episode watched state.
- Season watched / unwatched actions now operate on all populated TMDB Season Episode metadata rows, not only Episodes with active media sources.
- TV identification and manual TV correction populate the full TMDB Season detail Episode list, so unavailable Episodes can still be marked watched / unwatched while remaining non-playable.
- Season progress uses watched Episode count over TMDB total Episode count.
- Episode watched / unwatched actions refresh Season aggregate state and can cancel invalid favorite / want-to-watch state.
- Batch Season watched / unwatched uses the same all-TMDB-Episode rule as the Season detail actions.
- Watch Insights, Watch Profile, persona inputs, and recommendation fingerprint remain Movie-only.
- Did not add a migration.
- Did not start TV discovery search / ranking UI.

## Phase 4.6 Bugfix 2 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created.
3. Season / Series / Episode poster surfaces use the existing poster cache behavior.
4. Season favorite no longer creates want-to-watch.
5. Unwatched Season cannot be marked favorite.
6. Fully watched Season cannot be marked want-to-watch.
7. Not-interested cancels favorite / want-to-watch.
8. Not-interested does not change Episode watched state.
9. Season detail has whole-season watched / unwatched actions.
10. A 7-Episode TMDB Season can show `7 / 7` after whole-season watched.
11. The same Season can show `0 / 7` after whole-season unwatched.
12. Batch Season watched / unwatched follows the same `7 / 7` and `0 / 7` total-count rule.
13. Episode watched / unwatched updates aggregate progress.
14. Manual Season and Episode watched / unwatched actions do not create `WatchHistory`.
15. Unavailable Episodes can be watched / unwatched but remain without playable source.
16. Batch toolbar still excludes favorite, want-to-watch, and not-interested.
17. Watch Insights and recommendation fingerprint do not include TV data.
18. Existing Movie state and Movie batch behavior remain separate.
19. Documents and reports do not include secrets or private media locations.
