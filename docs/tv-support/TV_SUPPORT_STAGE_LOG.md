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

## Phase 4.7 - TV Main Experience Closeout

Phase adjustment:

- Original TV search / ranking work moves from Phase 4.7 to Phase 4.8.
- Original full Phase 4 regression and documentation closeout moves from Phase 4.8 to Phase 4.9.

Implemented scope:

- `TvSeasonDetailPage` now displays rating text from non-persistent metadata reads.
- Season detail metadata renders first; rating uses an independent loading path so only the rating area shows loading / unavailable text.
- TMDB Season rating uses the TV scan / identification Season detail metadata cache path.
- TMDB Season rating is labeled `TMDB 季评分` and includes vote count when TMDB supplies it.
- Series-level IMDb rating is requested after opening Season detail and labeled `IMDb 剧集评分`; no Season-level IMDb rating is fabricated from OMDb Season audit responses.
- Missing Season rating data displays `暂无评分` / `暂无季评分` without blocking Season state buttons.
- Media-library mixed Movie + Season batch selection now supports watched, unwatched, remove from library, and delete record.
- Mixed batch watched / unwatched dispatches Movie items through existing Movie services and Season items through Season whole-season state services.
- Mixed batch remove / delete record dispatches Movie and Season items through their own service branches and never deletes physical local or WebDAV files.
- The batch toolbar still excludes favorite, want-to-watch, and not-interested.
- Series and Episode remain outside batch-operation semantics.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.
- Did not add a migration.
- Did not execute database update.

## Phase 4.7 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created.
3. `TvSeasonDetailPage` shows `TMDB 季评分` when TMDB Season rating data is available.
4. Series-level IMDb data is labeled `IMDb 剧集评分`, not Season IMDb.
5. Missing rating data shows `暂无评分` or `暂无季评分` and does not crash.
6. Rating display does not affect Season state buttons.
7. Media-library batch mode can select Movie and Season items together.
8. Mixed batch watched succeeds.
9. Mixed batch unwatched succeeds.
10. Mixed batch remove from library succeeds.
11. Mixed batch delete record succeeds.
12. Movie items keep existing Movie rules.
13. Season items keep Season rules and use TMDB total Episode count.
14. Season batch watched / unwatched does not create `WatchHistory`.
15. Batch toolbar still excludes favorite, want-to-watch, and not-interested.
16. Series and Episode do not participate as batch units.
17. Movie-only batch and Season-only batch remain valid.
18. Watch Insights and recommendation fingerprint do not include TV data.
19. TV does not enter AI recommendations.
20. Documents and reports do not include secrets or private media locations.

## Phase 4.8 - TV Discovery Search And Rankings

Phase adjustment:

- Original TV search / ranking work is now Phase 4.8.
- Full Phase 4 regression and documentation closeout remains Phase 4.9.

Implemented scope:

- Movie discovery still exposes three top-level tabs only: movie search, rankings, and AI recommendations.
- Search tab now has a Movie / TV second-level selector.
- Movie search keeps the existing movie search and person search behavior.
- TV search calls TMDB TV search and uses dedicated `DiscoveryTvSeriesCardViewModel` data, not `DiscoveryMovieCardViewModel`.
- TV search results display Series cards with title, original name, first-air year, TV genres, overview, TMDB Series rating, poster, library state, and Season state summary when available.
- Ranking tab now has a Movie / TV second-level selector.
- Movie ranking behavior is unchanged.
- TV rankings call TMDB TV popular, top-rated, trending day, and trending week and keep the 200-item display cap.
- TV ranking results display Series cards with rank and do not fabricate Season rankings.
- TV status merge is read-only and resolves `TvSeries` / Season collection summary by TMDB Series ID.
- In-library TV Series navigate to `SeriesOverviewPage`.
- Not-in-library TV Series show a placeholder message for future TV external details and do not navigate to Movie detail or convert to `AiRecommendationItem`.
- TV discovery posters use the existing poster cache image behavior.
- TV discovery remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.
- Did not add a migration.
- Did not execute database update.

## Phase 4.8 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created.
3. Movie discovery still has exactly three top-level tabs.
4. Search tab has Movie / TV second-level switching.
5. Default movie search remains unchanged.
6. Movie title search remains unchanged.
7. Movie person search remains unchanged.
8. TV search can query Series such as Breaking Bad / 绝命毒师.
9. TV search results display Series cards.
10. TV search posters use the existing poster cache behavior.
11. In-library Series clicks navigate to `SeriesOverviewPage`.
12. Not-in-library Series do not navigate to Movie detail and are not converted to `AiRecommendationItem`.
13. Ranking tab has Movie / TV second-level switching.
14. Default movie rankings remain unchanged.
15. TV popular ranking can load.
16. TV top-rated ranking can load.
17. TV trending day can load.
18. TV trending week can load.
19. TV ranking cards show Series and rank.
20. TV ranking does not fabricate Season rankings.
21. TV rating copy is TMDB Series-oriented and never says OMDb rating.
22. AI recommendation tab does not include TV.
23. Watch Insights remains Movie-only.
24. Recommendation fingerprint remains Movie-only.
25. Documents and reports do not include secrets or private media locations.

## Phase 4.8 Bugfix - TV Discovery Parity Fixes

Scope:

- Kept the work inside the Movie Discovery page and did not start the later TV feature-gap audit.
- TV ranking layout now mirrors Movie ranking: page 1 shows rank 1 as a full-width large card and ranks 2-21 as two-column rows; later pages show 20 two-column entries.
- TV ranking page commands are disabled while loading, and request failures restore page command state with a user-facing status message.
- TV search now has a Movie-style basic filter panel for TV genre, region, library / Season state summary, sort direction, sort key, first-air decade, and language.
- TV genre filtering uses the TV genre mapper, not the Movie genre list.
- TV search and TV ranking cards load total Season count from TMDB Series detail with limited concurrency and display `共 N 季`, `季数加载中`, or `季数未知`.
- TV discovery copy now mentions TV search while keeping Movie person search scoped to Movie mode.
- TV search / ranking posters continue to use the existing poster cache image behavior.
- TV ranking remains Series-level; no Season ranking or Episode result is fabricated.
- Not-in-library TV Series still show the deferred TV external-detail prompt and never navigate to Movie detail.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.
- Did not add a migration.
- Did not execute database update.

Phase-outside items recorded only:

- Not-in-library TV detail page.
- Full Series / Season metadata completion for all Seasons.
- Showing source-less Seasons in library entry points.
- TV correction entry points and cross-type correction UI.
- Full TV feature-gap audit and stage reorder.

## Phase 4.8 Bugfix Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created.
3. Movie discovery still has exactly three top-level tabs.
4. Movie search remains unchanged.
5. Movie person search remains unchanged.
6. Movie search filters remain unchanged.
7. TV search has a basic filter panel.
8. TV type filtering uses TV genres, not Movie genres.
9. TV search can query Series such as Breaking Bad / 绝命毒师.
10. TV search cards show total Season count when TMDB detail is available.
11. TV search cards show in-library Season count when local status is available.
12. TV search filters update the result pool and pagination.
13. Movie rankings remain unchanged.
14. TV popular ranking uses the large rank-1 card and two-column rows.
15. TV top-rated ranking uses the large rank-1 card and two-column rows.
16. TV trending day uses the large rank-1 card and two-column rows.
17. TV trending week uses the large rank-1 card and two-column rows.
18. TV ranking page 1 displays 21 entries and later pages display 20 entries.
19. TV ranking page buttons are disabled while loading.
20. TV ranking failure restores page command state and shows an error message.
21. TV rankings do not display Episodes.
22. TV rankings do not fabricate Season rankings.
23. TV search / ranking posters use the existing poster cache behavior.
24. Not-in-library TV Series still use the deferred-detail prompt.
25. AI recommendation tab does not include TV.
26. Watch Insights remains Movie-only.
27. Documents and reports do not include secrets or private media locations.

## Phase 4.10 - TV Metadata Hydration And Unavailable Seasons

Phase adjustment after the Phase 4.9 audit:

- Phase 4.10 covers metadata hydration and unavailable Season / Episode display.
- Phase 4.11 covers TV correction entry UI.
- Phase 4.12 covers TV scan / rescan / history-location hardening.
- Phase 4.13 covers full Phase 4 regression and documentation closeout.

Implemented scope:

- Added a centralized TV metadata hydration service by TMDB Series ID.
- Hydration upserts `TvSeries`, all TMDB Seasons including Season 0, and all TMDB Episodes for each Season.
- Hydration does not create `MediaFile`, does not fabricate playback sources, does not modify existing source rows, and does not write Season collection state.
- TV scan / identification now requests full Series hydration after a successful Series match.
- `SeriesOverviewPage` attempts hydration when opened and refreshes to show all known Seasons.
- `SeriesOverviewPage` displays Season 0 as `特别篇` and shows source-less Seasons as `暂无播放源`.
- `TvSeasonDetailPage` displays Episode air date metadata and disables playback for source-less Episodes.
- TV discovery Series clicks now hydrate metadata and navigate to `SeriesOverviewPage`; not-in-library TV no longer uses the external-detail placeholder.
- TV discovery status now treats metadata-only Series as not in library unless active Episode sources exist.
- Hydration fetches TMDB Season detail outside the database write transaction and batches Episode inserts to avoid blocking the UI while metadata is being refreshed.
- Hydration is serialized per TMDB Series ID so repeated clicks cannot race against the first metadata write.
- Discovery TV open status now uses request-version guards so stale hydration tasks cannot overwrite paging / ranking status after the user moves on.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created.
3. An in-library Series can hydrate all TMDB Seasons when opening `SeriesOverviewPage`.
4. Season 0 / Specials displays as `特别篇`.
5. Source-less Seasons display `暂无播放源`.
6. Source-less Seasons can open `TvSeasonDetailPage`.
7. `TvSeasonDetailPage` displays all known Episode metadata.
8. Source-less Episodes display `暂无播放源`.
9. Source-less Episode play buttons are disabled.
10. Existing Episode sources remain playable.
11. Metadata-only Episodes can still be marked watched / unwatched.
12. Whole-season watched / unwatched still applies to all known TMDB Episodes.
13. Season favorite / want-to-watch / not-interested rules remain unchanged.
14. TV search not-in-library Series clicks hydrate metadata and navigate to `SeriesOverviewPage`.
15. Not-in-library Series hydration does not create `MediaFile`.
16. Not-in-library Series hydration does not fabricate playback sources.
17. TV ranking not-in-library Series clicks use the same hydration path.
18. TV search / ranking do not navigate to Movie detail for TV.
19. TV metadata does not enter AI recommendations.
20. TV metadata does not enter Watch Insights.
21. Existing Movie discovery and Movie ranking behavior remain separate.
22. Documents and reports do not include secrets or private media locations.

## Phase 4.10.1 - Metadata-only TV Library Visibility And Batch Rules

Implemented scope:

- Normal media-library mode no longer shows pure metadata-only TV created by discovery browsing unless the Series has active Episode sources or a Season has user state.
- A source-backed Series remains a Series aggregation card in normal mode.
- Batch mode expands source-backed Series into all known Seasons, including metadata-only Seasons and Season 0.
- A metadata-only Series without playback sources exposes only Seasons with user state in batch mode, limiting default library pollution.
- Season user state now includes collection flags plus explicit watched / unwatched state history, so manually marked metadata-only Seasons can remain visible.
- Metadata-only Seasons can be batch marked watched / unwatched; the operation updates Episode watched state and does not create `WatchHistory`, `MediaFile`, or fake sources.
- Phase 4.10.1 originally skipped metadata-only Seasons and not-in-library Movies with a no-source message; Phase 4.10.4 supersedes this with `LibraryVisibilityState.Hidden`.
- Batch delete record keeps the existing software-record deletion behavior and is documented as not deleting local or WebDAV files.
- Batch toolbar remains limited to watched, unwatched, remove, and delete-record actions.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.1 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created.
3. A metadata-only TV Series with no source and no state does not appear in the default media-library list.
4. A Series with at least one active Episode source appears in normal media-library mode.
5. A source-backed Series expands to all known Seasons in batch mode, including source-less Seasons.
6. Source-less Seasons show `暂无播放源`.
7. A metadata-only Season with user state can appear through library-related views.
8. Metadata-only Seasons can be batch marked watched.
9. Metadata-only Seasons can be batch marked unwatched.
10. Metadata-only Season batch watched / unwatched does not create `WatchHistory`.
11. Metadata-only Season batch watched / unwatched does not create `MediaFile`.
12. Metadata-only Season batch remove is skipped with a no-source message.
13. Metadata-only Season batch delete record uses software-record deletion only.
14. Not-in-library Movies can still be batch marked watched / unwatched through the existing external-state path.
15. Not-in-library Movie batch remove is skipped with a no-source message.
16. Not-in-library Movie batch delete record remains available through the existing external-record path.
17. In-library Movie batch operations remain unchanged.
18. In-library Season batch operations remain unchanged.
19. Batch favorite, want-to-watch, and not-interested actions remain absent from the toolbar.
20. Series still does not carry Season state.
21. Episodes still do not enter media-library top-level lists or batch lists.
22. TV state does not enter Watch Insights.
23. TV state does not enter AI recommendation or recommendation fingerprints.
24. Delete record does not delete local or WebDAV files.
25. Documents and reports do not include secrets or private media locations.

## Phase 4.10.3 - Library Visibility State Schema

Implemented scope:

- Added `LibraryVisibilityState` enum with `Auto = 0`, `Visible = 1`, and `Hidden = 2`.
- Added `LibraryVisibilityState` to `UserMovieCollectionItem`.
- Added `LibraryVisibilityState` to `UserTvSeasonCollectionItem`.
- Added EF defaults so both columns are stored as `INTEGER NOT NULL DEFAULT 0`.
- Added `AddLibraryVisibilityState` migration.
- Kept `UserMovieCollectionItem.IsInLibrary` unchanged.
- Did not change media-library filters, batch remove behavior, add-to-library UI, Discovery wording, Favorites, Home, Watch History, AI recommendations, Watch Insights, or recommendation fingerprints.
- Did not execute database update.

## Phase 4.10.3 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. `LibraryVisibilityState` enum exists.
3. `UserMovieCollectionItem` exposes `LibraryVisibilityState`.
4. `UserTvSeasonCollectionItem` exposes `LibraryVisibilityState`.
5. The default value is `Auto = 0`.
6. The migration only adds two `LibraryVisibilityState` columns.
7. `AppDbContextModelSnapshot` includes both new columns.
8. `UserMovieCollectionItem.IsInLibrary` is not removed or redefined.
9. Media-library filter behavior is unchanged in this phase.
10. Source-less remove behavior is unchanged in this phase.
11. Add-to-library buttons are not added in this phase.
12. AI recommendations, Watch Insights, and recommendation fingerprints are unchanged in this phase.
13. Follow-up phases remain Phase 4.10.4 for filter / remove semantics and Phase 4.10.5 for add-to-library actions.

## Phase 4.10.4 - Media Library Source Visibility Semantics

Implemented scope:

- Media-library filter wording now uses source-state terminology: `全部`, `有播放源`, `无播放源`.
- Library read models expose `HasActiveSource`, `ActiveSourceCount`, `IsVisibleInLibrary`, and `LibraryVisibilityState`.
- Movie visibility resolved from active source first in Phase 4.10.4; Phase 4.10.4f supersedes this so `Hidden` has priority over active source.
- TV Season visibility uses current `UserTvSeasonCollectionItem` flags and Episode watched state; `UserTvSeasonStateChangeHistory` is no longer used as current visibility input.
- TV Series visibility is aggregated from source-backed or visible Seasons; Episodes remain detail/playback units only.
- Source-less Movie and Season remove writes `LibraryVisibilityState.Hidden`, preserving state and metadata.
- Source-backed Movie and Season remove kept the existing source-removal path in Phase 4.10.4; Phase 4.10.4f supersedes this with hide-only behavior.
- Batch remove no longer skips source-less Movies / Seasons with a no-source message.
- Delete-record behavior remains separate and is still the metadata/state cleanup path.
- Favorites, Home, Watch History, AI recommendations, Watch Insights, and recommendation fingerprints were not intentionally changed.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.4 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.10.4 migration is created.
3. Media-library filter labels are `全部`, `有播放源`, `无播放源`.
4. `有播放源` filters by active video `MediaFile` presence.
5. `无播放源` filters visible rows with no active video source.
6. Pure metadata-only, no-state, `Auto` TV remains hidden from media-library lists.
7. `Visible` source-less Movie / Season rows are query-visible when present.
8. `Hidden` source-less Movie / Season rows are media-library hidden.
9. Source-less Season remove writes `Hidden`.
10. Source-less Season remove preserves state and metadata.
11. Source-less Movie remove writes `Hidden`.
12. Source-backed Movie / Season remove does not delete physical files.
13. Superseded by Phase 4.10.4f: source-backed rows are hidden without source removal.
14. Delete record remains the app-record cleanup path.
15. Favorites are not filtered by `Hidden`.
16. Watch History is not filtered by `Hidden`.
17. Batch toolbar remains limited to watched, unwatched, remove, and delete-record.
18. Batch remove source-less rows hides them instead of skipping them.
19. TV remains excluded from Watch Insights / AI / recommendation fingerprints.
20. Documents and reports do not include secrets or private media locations.

## Phase 4.10.4d - Visibility Tail Bugfix

Implemented scope:

- Media-library cards and list labels no longer use `未入库` for visible source-less rows; they use `暂无播放源` / source summaries instead.
- TV source labels in Series and Season detail read models use `有播放源 N 集` or `暂无播放源`.
- Discovery search / ranking source filters and card badges use `有播放源` / `无播放源` instead of old in-library wording.
- Recommendation UI wording was lightly relabeled to existing-source / external-candidate terminology without changing the recommendation algorithm.
- Positive state writes clear `LibraryVisibilityState.Hidden` back to `Auto` for Movie and TV Season: want-to-watch true, favorite true, not-interested true, and watched true.
- Mark-unwatched and cancel-state writes do not clear `Hidden`.
- Movie-only AI/profile/statistics/recommendation input loaders ignore pure visibility-only Movie rows that have no source and no explicit user state.
- Favorites remains a state view; Movie rows with a local `MovieId` navigate to `MovieDetail` even when they are not media-library-visible.
- TV remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.4d Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.10.4d migration is created.
3. Media-library cards / lists do not show `未入库` for source-less visible rows.
4. Source-less visible rows show `无播放源` / `暂无播放源`.
5. Discovery source filters do not show `库内` / `库外`.
6. Discovery cards do not show `已入库` / `未入库` as source status.
7. Hidden Movie rows re-enter media library after want-to-watch / favorite / not-interested / watched is set true.
8. Hidden Season rows re-enter media library after want-to-watch / favorite / not-interested / watched is set true.
9. Mark-unwatched and cancel-state operations do not clear `Hidden`.
10. Batch watched clears `Hidden`; batch unwatched does not.
11. Hidden favorite / want-to-watch rows remain visible in Favorites.
12. Favorites Movie rows with local `MovieId` open `MovieDetail`.
13. Pure visibility-only Movie rows do not enter movie AI/profile/statistics/recommendation fingerprints.
14. Real-state source-less Movie rows remain eligible for movie AI/profile/recommendation inputs.
15. TV still does not enter AI / Watch Insights / recommendation fingerprints.
16. Documents and reports do not include secrets or private media locations.

## Phase 4.10.4f - Hide Library Items Without Removing Sources

Implemented scope:

- Movie remove-from-library now writes `LibraryVisibilityState.Hidden` through the user collection state row and does not mark active `MediaFile` rows deleted.
- Movie remove-from-library does not reset `DefaultMediaFileId`, clear user state, delete metadata, delete history, or delete local / WebDAV files.
- TV Season remove-from-library now writes `LibraryVisibilityState.Hidden` and does not mark Episode `MediaFile` rows deleted.
- Media-library query visibility resolves `Hidden` before active source, so hidden source-backed Movies and Seasons are excluded from `全部`, `有播放源`, and `无播放源`.
- TV Series aggregation only counts visible Seasons for media-library source counts; Hidden source-backed Seasons do not keep a Series visible.
- Batch remove wording reports items hidden from the media library rather than source-less items skipped or playback sources removed.
- Favorites, Watch History, detail pages, playback, scanning, and Discovery were not changed to filter by `Hidden`.
- Existing `MediaFile.IsDeleted` records are not restored.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.4f Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.10.4f migration is created.
3. Source-backed Movie remove hides the Movie from media-library lists.
4. Source-backed Movie remove leaves active `MediaFile` rows active.
5. Source-backed Movie remove preserves state, metadata, history, and files.
6. Source-backed TV Season remove hides the Season / Series from media-library lists when no visible Season remains.
7. Source-backed TV Season remove leaves Episode `MediaFile` rows active.
8. Hidden source-backed rows are excluded from `全部`.
9. Hidden source-backed rows are excluded from `有播放源`.
10. Hidden source-backed rows are excluded from `无播放源`.
11. Favorites can still show Hidden state rows.
12. Detail pages and playback continue to depend on active source rows, not media-library visibility.
13. Delete record remains the software-record cleanup path and does not delete physical files.
14. Old `IsDeleted` rows are not automatically restored; rescanning existing files is the recovery path to validate in Phase 4.13.
15. Documents and reports do not include secrets or private media locations.
