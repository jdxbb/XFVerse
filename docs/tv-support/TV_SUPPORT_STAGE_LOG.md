# TV Support Stage Log

## Phase 4.12-post-fix-follow-up - Restrict Recognized Reattach To Same Directory

Completed:

- Recognized Episode automatic reattach now requires the candidate source directory to match an existing active source directory on the target recognized Episode.
- Sibling directories are no longer accepted for recognized Episode reattach, even when they share source connection, scan root, parent folder, and bare numeric episode names.
- Same-directory duplicate / multi-version files remain eligible for recognized Episode multi-source attachment.
- Reattach candidate and skipped diagnostics now emit directory hashes instead of directory text for this safety gate.
- Skip logs use `recognized-reattach-requires-same-directory` when a recognized target exists but only in a different directory.
- Unknown Season append logic was not changed by this follow-up and keeps its existing safe-unknown matching rules.

Not done:

- No historical cleanup was added for already-wrong Episode source bindings.
- No manual correction entry, batch correction, AI strategy change, SP/OAD/OVA mapping, migration, database update, commit, or push was performed.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. Migration diff is empty.
3. A derivative / sibling show folder with `01` through `08` files no longer recognized-reattaches to an already matched Season 1 in a neighboring folder.
4. Sibling folders such as `S01` and `S01 1080p` no longer auto-reattach to the same recognized Season.
5. Same-directory `502` plus duplicate-copy variants can still attach as multiple sources for the same recognized Episode.
6. Same-directory `S01E02` plus duplicate-copy variants can still attach as multiple sources for the same recognized Episode.
7. Different-directory reattach skips include `recognized-reattach-requires-same-directory` and directory hashes.
8. Skipped sibling sources continue into normal identification, unknown append, placeholder, or later manual correction paths.
9. Unknown Season append for compatible failed no-TMDB Seasons still runs after recognized reattach and before placeholder grouping.
10. TV remains excluded from Watch Insights, Watch Profile, AI recommendation input, and recommendation fingerprints.

## Phase 4.12-post-fix - Safe Unknown Series Display, Grouping, And Append

Completed:

- Normal media-library mode now projects persisted all-failed no-TMDB TV-like containers as `Other` Series items, including one-Season unknown Series, so users enter Series overview before Season detail.
- Batch media-library mode still expands TV-like content to Season / grouped-item granularity for focused correction and bulk operations.
- Local and WebDAV scans now run unknown-Season append after recognized reattach and before placeholder / orphan grouping.
- Unknown append handles active unbound videos and failed Movie placeholders only when a single normal Episode number is parsed and the target failed no-TMDB Season is uniquely compatible.
- Existing target Episode numbers receive the new source as an additional playback source; missing target Episode numbers create only that Episode and never create middle empty Episodes.
- Local/WebDAV append now scans the whole active scan-path unknown candidate set, so unchanged failed Movie placeholders as well as unchanged orphans can still attach to existing unknown Seasons.
- Episode parsing and grouped placeholder ranges now treat trailing duplicate-copy suffixes such as `(1)` / full-width `(1)` as the same Episode number in strong TV context, enabling same-folder duplicate files to become multi-source rows.
- Duplicate-copy suffix stripping now removes only known file extensions before suffix matching, so dotted episode names such as title-plus-episode-number keep the episode token intact.
- Unknown append now skips structural non-episode tokens such as part / disc / trailer forms before strong title-number fallback can attach them to a normal Season.
- Grouped unknown placeholder parsing now skips multi-episode file names before duplicate-copy suffix normalization, keeping combined-episode files out of automatic single-Episode grouping.
- Grouped unknown creation now derives a temporary source/root/title key from existing `MediaFile` context, reuses one safe compatible no-TMDB Series / failed Season when possible, and falls back to a new container on conflict.
- The temporary key uses source connection, scan path, series root or carrying directory, season directory, and normalized title context; logs emit hashes / normalized titles instead of complete paths or URLs.
- No historical bulk merge of existing duplicate unknown containers was added.

Not done:

- No manual aggregate-to-Season workflow, correction-to-existing-unknown-Season UI, batch button rule change, AI strategy change, unified Movie / TV correction entry, SP/OAD/OVA mapping, course / collection specialty handling, migration, database update, commit, or push was performed.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. Migration diff is empty.
3. Normal `Other` shows persisted unknown TV-like containers as unknown Series items.
4. Unknown Series with one Season opens Series overview and shows that Season.
5. Batch mode still exposes unknown Season / grouped item granularity.
6. Recognized TV Series remain `Series` items and recognized Seasons remain unchanged in batch mode.
7. Orphan single files and failed Movie placeholders remain `Other` items.
8. Compatible grouped ranges under the same source/root/title reuse one unknown Series when unique.
9. Same range titles under different source connections or unrelated parent roots do not merge.
10. Re-scanned E703 can append to an existing compatible unknown Season containing E700-E702.
11. Re-scanned duplicate E701 appends as another source on E701.
12. Re-scanned E799 creates only E799 and does not create E704-E798.
13. SP/OAD/OVA/special and multi-episode candidates are skipped by automatic append.
14. TV remains excluded from Watch Insights, Watch Profile, AI recommendation input, and recommendation fingerprints.
15. Same-folder duplicate-copy names such as `502 (1)` or full-width-parenthesis variants attach to E502 when a compatible unknown Season exists.
16. Dotted title-number duplicate-copy names keep the episode number during parsing.
17. Part / disc / trailer duplicate-copy names and multi-episode duplicate-copy names stay out of automatic append / grouping.

## Phase 4.11g - TV Scan Final Acceptance And Closeout

Completed:

- Audited the latest real scan log as the Phase 4.11g acceptance baseline.
- Confirmed AI-on-uncertain runs in at most 3 concurrent batches, all latest batches succeeded, and successful hints were merged through the existing `inputRangeId -> MediaFileIds` apply path.
- Confirmed the post-AI TV retry scope is limited to AI-affected media files.
- Confirmed later-part offset runs after TMDB Series / Season confirmation and maps later part episodes only when previous contiguous sibling evidence is available.
- Confirmed structural `Part` / `S Part` style queries are rejected before TMDB lookup and do not auto-bind.
- Confirmed TV.Parse warnings are deduplicated and excluded from scan error count.
- Confirmed Movie placeholder volume is expected boundary data, not a scan blocker.
- Confirmed library projection keeps recognized Movie, recognized TV, and `Other` separated; orphan video rows and unidentified Seasons are represented in `Other`.
- Confirmed batch select / hide / delete-record paths include grouped placeholders and unidentified Seasons through existing Movie / Season collection services.
- Confirmed Watch Insights and AI recommendation services still load movie-backed data only.

Not done:

- No code changes were made for Phase 4.11g.
- No new scan rules, AI prompt changes, TMDB threshold changes, Movie fallback changes, Episode detail page, multi-source management, active correction UI, or anime-special mapping were added.
- No migration, database update, commit, or push was performed.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No migration diff is present.
3. AI-on-uncertain batch count is at most 3.
4. Successful AI batch hints are applied and failed batches would remain warning / unresolved data.
5. TV retry only processes AI-affected files.
6. Part offset requires confirmed Series / Season and previous contiguous episode evidence.
7. Structural part-only queries are rejected.
8. Orphan files and unidentified Seasons appear in `Other`.
9. Movie category is recognized Movie only and TV category is recognized TV only.
10. Batch delete / hide semantics do not delete local or WebDAV files.
11. TV remains excluded from Watch Insights and AI recommendations.
12. Remaining complex cases are deferred to Phase 4.12 / 4.13.

## Phase 4.11f-fix-9 - Episode Sequence Parsing And Warning Semantics

- Expanded verified episode sequence parsing for fansub bracket forms such as `[Group] Title [01][Quality]` and `Title [01][Quality]`.
- Reused bracket episode segment evidence in TV apply so `[Title][01 - subtitle]` style ranges can parse episodes after AI/TMDB validation instead of falling back to unsupported.
- Added verified leading-number-title support only when the same folder sequence provides strong TV context, such as `01.Title.S01.2022...`; `01: title` / `01：title` remains out of scope.
- Added long-running placeholder grouping support for large numeric ranges with a small gap ratio; missing episode numbers are recorded but no missing Episode rows are created.
- Added a bare-number movie collection guard so short `1..5` style collections without TV evidence stay in Other / unknown handling instead of becoming unidentified TV seasons.
- TV.Parse warnings are now deduplicated by directory/reason and no longer inflate scan `ErrorCount`; run diagnostics log raw and deduplicated warning counts.
- Follow-up scan discovery filtering excludes macOS AppleDouble `._*` resource fork files before media type detection, so `._*.mkv` no longer enters TV/Movie identification or AI candidate ranges.
- Did not change AI prompt, TMDB top1/year-gate strategy, Movie fallback semantics, deletion semantics, migrations, Watch Insights, or TV AI recommendation exclusion.

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

## Phase 4.10.5 - Add Media Library Visibility Actions

Implemented scope:

- Added explicit add-to-library / restore-to-library service paths for Movie and TV Season rows that write `LibraryVisibilityState.Visible`.
- Added TV Series add-to-library behavior that writes `Visible` for all known Seasons, including Season 0 / Specials, after ensuring metadata is available.
- Added a media-library `已移出媒体库` management entry that lists Hidden Movie and TV Season rows.
- Hidden management supports restore to library, view detail, and delete-record actions.
- Discovery Movie and TV Series cards expose add / restore actions when the row is not currently media-library-visible.
- Movie detail, Series overview, and TV Season detail expose add / restore actions when the current item is not media-library-visible.
- Add-to-library does not set want-to-watch, favorite, not-interested, watched, or fake playback sources.
- Add-to-library does not create `MediaFile`, does not restore old `IsDeleted` source rows, and does not execute database update.
- TV remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.5 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.10.5 migration is created.
3. Media library exposes an `已移出媒体库` entry.
4. Hidden Movie rows appear in the removed-library management view.
5. Hidden TV Season rows appear in the removed-library management view.
6. Restoring a Hidden source-backed Movie writes `Visible`, returns it to the media library, and keeps playback sources active.
7. Restoring a Hidden source-less Movie writes `Visible` and shows it as source-less.
8. Restoring a Hidden source-backed Season writes `Visible` and keeps Episode sources playable.
9. Restoring a Hidden source-less Season writes `Visible` and shows it as source-less.
10. Delete record from the removed-library view uses existing software-record deletion and does not delete physical files.
11. View detail from the removed-library view does not change visibility.
12. Discovery Movie rows can be added / restored to the media library.
13. External Movie add-to-library does not set want-to-watch, favorite, not-interested, or watched.
14. Series add-to-library makes all known Seasons visible, including Season 0 / Specials.
15. TV Season detail can add only the current Season.
16. Add-to-library does not create `MediaFile` or fake playback sources.
17. Add-to-library does not affect AI recommendations, Watch Insights, or recommendation fingerprints.
18. TV remains excluded from AI / Watch Insights / recommendation fingerprints.
19. Rescan does not automatically clear `Hidden`.
20. Media-library `全部`, `有播放源`, and `无播放源` lists still exclude Hidden rows until they are restored.
21. Restore writes `Visible` in Phase 4.10.5; Phase 4.10.5b supersedes this with source / state-aware restore.
22. Documents and reports do not include secrets or private media locations.

## Phase 4.10.5b - Restore Visibility And Series-Level Actions

Implemented scope:

- Removed-library restore now uses source / state-aware visibility instead of blindly writing `Visible`.
- Hidden Movie restore writes `Auto` when the Movie has an active source, watched state, favorite state, not-interested / want-to-watch state, or explicit user rating; otherwise it writes `Visible`.
- Hidden TV Season restore writes `Auto` when the Season has an active Episode source or real Season state, including want-to-watch, favorite, not-interested, or any watched Episode; otherwise it writes `Visible`.
- Discovery, Movie detail, TV Season detail, and the removed-library management view use the restore path when the item is Hidden.
- SeriesOverview uses series-level restore when any Season is Hidden and add-to-library when Seasons are merely not visible.
- SeriesOverview keeps a visible series-level action area for one-season and all-visible series; all-visible series display an already-in-library disabled state.
- Movie add-to-library creation explicitly clears default want-to-watch / watched / not-interested flags so visibility-only rows do not become preference state.
- Movie user rating is counted as real current state for media-library visibility.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.5b Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.10.5b migration is created.
3. Auto source-less Season can enter the media library after watched state is set.
4. Auto source-less Season becomes media-library-invisible again after watched state is cleared and no other state remains.
5. Auto automatic invisibility does not enter the removed-library management view.
6. Hidden source-less state-backed Season restores to `Auto`.
7. Hidden source-less state-backed Season can disappear again after its final state is cleared.
8. Hidden source-less no-state Season restores to `Visible`.
9. Hidden source-less no-state Season remains visible after restore.
10. Hidden source-backed Season restores to `Auto`, remains visible, and keeps Episode playback sources active.
11. Movie restore follows the same source / state-aware rule.
12. Visible rows remain visible after state is cleared.
13. The removed-library management view still lists only `Hidden`.
14. One-season SeriesOverview shows a series-level action area.
15. Partial SeriesOverview shows a complete-all-seasons action.
16. SeriesOverview with Hidden Seasons shows restore wording.
17. All-visible SeriesOverview shows an already-in-library disabled state.
18. TV Season detail keeps the current-season add / restore action.
19. Series add / restore still includes Season 0 when metadata is present.
20. Restore and add actions do not create playback sources or fake source rows.
21. TV remains excluded from AI / Watch Insights / recommendation fingerprints.
22. Documents and reports do not include secrets or private media locations.

## Phase 4.10.6 - Progressive TV Metadata Hydration

Implemented scope:

- TV search and TV ranking Series clicks now use a summary-first hydration path before navigation.
- Summary-first hydration writes / updates `TvSeries` and TMDB Season summaries, including Season 0 / Specials, without requiring all Episode metadata first.
- Summary-first hydration only skips when all TMDB Season summaries already exist locally; a Series that previously had only one scanned Season can still be completed before opening `SeriesOverviewPage`.
- `SeriesOverviewPage` still starts the existing full metadata hydration in the background after navigation, so Episode metadata is eventually completed without blocking the Season list.
- `TvSeasonDetailPage` can request current-Season Episode metadata on demand when the Season summary exists but Episode rows are incomplete.
- TV search / ranking navigation uses an `IsTvSeriesNavigating` guard to disable repeated TV card clicks and TV pagination while the detail navigation request is in flight.
- Navigation status messages are request-version guarded so stale hydration tasks cannot overwrite current search / ranking status.
- The deleted-Season reopening path still recreates Series / Season summary metadata from TMDB and opens `SeriesOverviewPage`.
- Hydration still does not create `MediaFile`, fabricate playback sources, write preference state, or route TV into Movie detail / AI surfaces.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.6 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.10.6 migration is created.
3. TV search not-in-library Series click opens `SeriesOverviewPage` after Series + Season summary hydration.
4. TV ranking not-in-library Series click uses the same summary-first path.
5. Full Episode metadata is not required before `SeriesOverviewPage` opens.
6. A previously one-season local Series can receive all TMDB Season summaries before overview navigation.
7. Season 0 / Specials remains included in summary hydration.
8. `SeriesOverviewPage` background hydration can later complete Episode metadata.
9. `TvSeasonDetailPage` can hydrate missing current-Season Episodes on demand.
10. Source-less Episodes remain non-playable until a real active `MediaFile` exists.
11. TV card repeat-click is disabled while TV Series navigation is in progress.
12. TV search previous / next pagination is disabled while TV Series navigation is in progress.
13. TV ranking previous / next pagination is disabled while TV Series navigation is in progress.
14. Failed metadata requests restore UI command availability and show a readable status.
15. Deleting a Season record and reopening the Series from TV search / ranking can recreate summary metadata.
16. Hydration does not create playback sources or fake source rows.
17. Hydration does not write `Visible`, `Hidden`, want-to-watch, favorite, not-interested, or watched state.
18. TV remains excluded from AI / Watch Insights / recommendation fingerprints.
19. Movie search / ranking flows are not changed by the TV navigation guard.
20. Documents and reports do not include secrets or private media locations.

## Phase 4.11 - TV Scan Identification Hardening

Implemented scope:

- Added a scan directory analysis service that builds a sanitized path tree and asks the configured AI service for lightweight TV range hints.
- AI hints are advisory only: they never write DB rows, never call TMDB, and never decide the final Series match.
- Added local TV range fallback for obvious series / season / episode directory structures when AI is unavailable or returns invalid output.
- TV identification now accepts directory analysis hints, supports strong-context bare numeric and title-plus-number episode parsing, and keeps strong TV ranges out of Movie fallback.
- TV search now tries multiple cleaned query candidates and rejects generic season folders, quality-only strings, and codec-only strings before requesting TMDB.
- Scan diagnostics now include TV range analysis, AI success / failure, strong TV context, parser pattern, query rejection, and no-Movie-fallback reasons with sanitized paths.
- Movie scan, media-library visibility, playback, Watch Insights, and AI recommendation semantics are not changed.

## Phase 4.11 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11 migration is created.
3. AI directory hints use sanitized paths only.
4. AI failure or invalid JSON falls back to local rules.
5. Strong TV directories with `01.mp4` / `1.mp4` / `10.mp4` are parsed as episodes only under TV context.
6. Chinese `第01集` / `第01话` episode markers parse.
7. Chinese `第一季` / `第一季全9集` season folders parse.
8. Title-plus-number names in strong TV context can provide episode numbers.
9. Generic queries such as `Season 3`, `S01`, `第一季`, `1080P`, and codec-only strings are rejected before TMDB search.
10. Strong TV range failures no longer silently enter Movie fallback.
11. TV range outside files continue to use existing Movie identification.
12. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11b - Directory-Level Scan AI Schema

Implemented scope:

- Replaced default scan AI `episode-files-v1` with `directory-ranges-v1`.
- The prompt now asks AI for directory-level TV ranges only and explicitly forbids `episodeFiles` / per-file episode mapping.
- The prompt input uses directory summaries with video counts and a capped sample-file list per directory.
- The response parser consumes `seriesFolder`, `seriesTitleHint`, `seasonFolders[].path`, `seasonFolders[].seasonNumberHint`, `confidence`, and compact `reason`.
- Unknown `episodeFiles` fields in a model response are ignored and do not drive default scan logic.
- Accepted high / medium ranges mark files under the returned directory as strong TV context; local parser still resolves actual Season / Episode numbers.
- Low-confidence ranges are rejected for automatic application and logged.
- AI failure and invalid JSON continue to fall back to local rules.
- Long-timeout diagnostic probe, using the same active-video set, returned HTTP 200 with valid JSON:
  - old `episode-files-v1`: about 28.6k prompt chars, 7.1k token estimate, 156.7s duration, 43.3k assistant chars.
  - new `directory-ranges-v1`: about 23.1k prompt chars, 5.8k token estimate, 100.2s duration, 13.8k assistant chars, 24 ranges, 21 accepted high / medium ranges.
- The probe confirms the schema boundary is lighter, but default scan can still exceed the 18s production timeout on large trees.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11b Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11b migration is created.
3. Default scan AI logs `schema=directory-ranges-v1`.
4. Default scan AI prompt no longer requests `episodeFiles`.
5. Default scan AI response parser does not require `episodeFiles`.
6. Local parser remains responsible for Episode numbers.
7. AI failure still falls back to local rules.
8. Low-confidence AI ranges are not automatically applied.
9. TV range parser failures remain protected from silent Movie fallback.
10. Long-timeout probe records prompt size, response size, duration, parsed ranges, and accepted ranges.
11. Diagnostics remain sanitized.
12. No specific title / folder / TMDB ID hardcoding is introduced.
13. TMDB matching thresholds are unchanged.
14. Media-library visibility semantics are unchanged.
15. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11c - Directory Summary Optimization

Implemented scope:

- Kept `directory-ranges-v1`; did not restore `episodeFiles`.
- Changed AI directory summaries so each directory only samples direct video files from that directory.
- Child folders are reported as `childFolders` and are not mixed into `sampleVideoFiles`.
- Sample count is `ceil(directVideoCount * 10%)`, clamped to 1-5; directories with no direct video files use zero samples.
- Representative sampling prefers episode-like, bare numeric, title-plus-number, then ordinary filenames, and spreads samples across beginning / middle / end.
- Replaced natural-language AI reasons with short `evidence` values for diagnostics.
- Response parsing does not require evidence and ignores unexpected `episodeFiles`.
- Diagnostics now include `representedFiles`, `directorySummaryCount`, `sampleFiles`, `maxSamplesPerDirectory`, evidence count / kinds, prompt size, response size, duration, and fallback reason.
- Long-timeout diagnostic probe, using the same active-video set, returned HTTP 200 with valid JSON:
  - Phase 4.11b `directory-ranges-v1`: about 23.1k prompt chars, 5.8k token estimate, 100.2s duration, 26.9k API response chars, 13.8k assistant chars, 24 ranges, 21 accepted high / medium ranges.
  - Phase 4.11c optimized summary: about 20.5k prompt chars, 5.1k token estimate, 92.7s duration, 34.8k API response chars, 13.4k assistant chars, 22 ranges, 22 accepted high / medium ranges.
- Did not add batch / concurrent batch.
- Did not change production timeout.
- Did not change TMDB matching thresholds.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11c Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11c migration is created.
3. Default scan AI still logs `schema=directory-ranges-v1`.
4. Default scan AI does not request or require `episodeFiles`.
5. `sampleVideoFiles` come only from direct video files in the summarized directory.
6. `sampleVideoFiles` do not include child folder names.
7. `sampleVideoFiles` do not recursively sample videos from child folders.
8. Sample count follows 10% with a 1-5 clamp.
9. Direct-video-count zero directories use zero samples.
10. Representative samples prefer episode-like / numeric / title-plus-number filenames.
11. AI output uses short evidence instead of natural-language reasons.
12. Response parsing does not depend on evidence.
13. Unexpected `episodeFiles` remain ignored.
14. Low-confidence ranges are not applied automatically.
15. AI failure still falls back to local rules.
16. Long-timeout probe records duration and prompt / response size.
17. Diagnostics remain sanitized.
18. No specific title / folder / TMDB ID hardcoding is introduced.
19. TMDB matching thresholds are unchanged.
20. Media-library visibility semantics are unchanged.
21. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11d - TV Scan Generalization Tightening And Match Validation

Implemented scope:

- Tightened local strong TV context so one weak signal no longer makes a directory strong by itself.
- Added separate fallback-blocking TV risk state so risky TV folders can avoid silent Movie fallback without enabling broad episode parsing.
- Kept title-plus-number and bare numeric episode parsing behind strong-context evidence.
- Treated Chinese season / episode / count markers as structure hints and rejected structure-only queries before TMDB search.
- Added query source tracking for TV search attempts and bad-query rejection diagnostics.
- Added TMDB candidate conflict downgrade for close top candidates, year conflicts, and weak original-title evidence.
- Added scan diagnostics for strong evidence, weak reason, fallback-risk count, selected query, query source, and candidate conflict reason.
- Kept TMDB thresholds unchanged.
- Did not add specific title / folder / TMDB ID special cases.
- Did not change media-library visibility, playback, Discovery, Watch Insights, or AI recommendation semantics.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11d Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11d migration is created.
3. A single title-plus-number filename no longer makes a directory strong TV by itself.
4. A single bare numeric filename no longer makes a directory strong TV by itself.
5. A single explicit episode-like filename no longer makes a directory strong TV by itself.
6. Multi-evidence TV context can still become strong TV.
7. Chinese structure markers are retained as hints rather than title queries.
8. Pure season / count / range queries are rejected before TMDB search.
9. TV query attempts log query source and selected query.
10. Conflicting top TV candidates downgrade to placeholder / NeedsReview behavior.
11. TV-risk folders can be kept out of Movie fallback without broad auto-bind.
12. Obvious Movie files without TV risk still flow into Movie identification.
13. Diagnostics log strong evidence, weak reason, fallback risk, and conflict reason.
14. No specific title / folder / file prefix / TMDB ID hardcoding is introduced.
15. TMDB thresholds are unchanged.
16. Media-library visibility semantics are unchanged.
17. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-prep - AI-on-Uncertain Candidate Preparation

Implemented scope:

- Disabled default production full AI directory range analysis.
- Kept the directory-range AI implementation available as a diagnostic / optional experiment path.
- Added log-only `aiCandidateRanges` from local TV-risk pre-analysis.
- Candidate ranges are grouped by sanitized directory and include risk tags, direct-video samples, candidate query, suspected Series / Season folder, and blocked Movie fallback counts where available.
- TV candidate conflict / placeholder paths also emit `ai-candidate-range` diagnostics for later AI-on-uncertain processing.
- Did not call AI for the candidate ranges.
- Did not change Phase 4.11d strong TV context, query reject, candidate conflict, or Movie fallback semantics.
- Did not change media-library visibility, playback, Discovery, Watch Insights, or AI recommendation semantics.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11e-prep Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11e-prep migration is created.
3. Default production scan logs `fullAiRangeAnalysis=disabled`.
4. Full AI range code remains available for diagnostics / optional experiments.
5. Local scan identification still runs without AI range calls.
6. `aiCandidateRanges` are emitted as diagnostics only.
7. Candidate range paths are sanitized.
8. Candidate ranges include risk tags and sample direct video files when available.
9. Candidate ranges do not write database rows.
10. Phase 4.11d local scan rules are not rolled back.
11. TMDB thresholds are unchanged.
12. Media-library visibility semantics are unchanged.
13. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-prep-2 - Scan Candidate Range Quality Fixes

Implemented scope:

- Added a Movie identification guard for low-information queries: numeric-only, metadata-only, or extremely weak title queries no longer auto-bind to TMDB Movie results.
- Low-information Movie queries now become Movie placeholders unless TV risk has already removed them from Movie fallback.
- Extended local TV-risk blocking for generic structure evidence such as total-count / season-range hints plus multiple sequential numeric direct video files.
- Kept the Phase 4.11d rule that TV risk blocks silent Movie fallback without turning weak evidence into a confident TV match.
- Reduced `aiCandidateRanges` over-collection: standard high-confidence local TV ranges are no longer emitted solely because they are strong TV.
- Added directory-level merge / de-duplication for `aiCandidateRanges`; repeated risks for the same sanitized directory are merged into one range with combined risk tags, samples, query buckets, conflict counts, and fallback-block counts.
- Split candidate queries into usable / rejected / noisy diagnostic buckets for later AI-on-uncertain input.
- Renamed local directory hints so they no longer appear as AI hints unless they came from an actual AI response.
- Kept default full AI range analysis disabled; no AI-on-uncertain calls are made in this phase.
- Did not change TMDB thresholds, media-library visibility, playback, Discovery, Watch Insights, or AI recommendation semantics.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11e-prep-2 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11e-prep-2 migration is created.
3. Numeric-only / extremely low-information Movie queries do not auto-bind.
4. Low-information Movie queries without TV risk become Movie placeholders.
5. Obvious Movie queries with enough title information still flow through Movie identification.
6. Total-count / season-range hints plus sequential numeric direct videos can block Movie fallback as TV risk.
7. Movie fallback TV-risk blocking remains diagnostic and does not force a confident TV match.
8. `aiCandidateRanges` exclude successful strong TV ranges with no unresolved risk.
9. `aiCandidateRanges` are merged by sanitized directory.
10. `aiCandidateRanges` include risk tags and sanitized paths.
11. Candidate queries are split into usable / rejected / noisy buckets.
12. Local directory hints use local / directory naming, not AI naming.
13. Default full AI range analysis remains disabled.
14. No AI call is made for candidate ranges.
15. Phase 4.11d local tightening is not rolled back.
16. No specific title / directory / file prefix / TMDB ID hardcoding is introduced.
17. TMDB thresholds are unchanged.
18. Media-library visibility semantics are unchanged.
19. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-prep-3 - Finalize Candidate Input And Movie Query Cleanup

Implemented scope:

- Strengthened Movie scan title cleanup with generic release / audio / source metadata handling.
- Movie diagnostics now log raw title, cleaned title, removed noise category, query quality, low-information state, and final decision.
- Kept low-information Movie query protection: numeric-only, release-metadata-only, and otherwise weak queries still become placeholders instead of automatic TMDB binds.
- Tightened TV candidate query diagnostics so usable, noisy, and rejected query buckets remain separate for later AI-on-uncertain prompts.
- TV placeholder / conflict candidate ranges are merged back into the run-level `TvScanDirectoryAnalysisResult`.
- WebDAV and Local scans now emit a final `scan-final-ai-candidate-ranges` summary after TV identification, not only the pre-analysis candidate count.
- Final candidate range entries include sanitized path, risk tags, samples, usable / noisy / rejected queries, conflict counts, fallback-block counts, and Chinese structure hints where available.
- Same-name / version conflicts remain downgraded to placeholder / AI-candidate diagnostics; this phase does not add local special-case adjudication.
- Default full AI range analysis remains disabled and this phase does not call AI for uncertain ranges.
- Did not change TMDB thresholds, media-library visibility, playback, Discovery, Watch Insights, or AI recommendation semantics.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11e-prep-3 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11e-prep-3 migration is created.
3. Movie title cleaner removes generic release / audio / source metadata.
4. Movie title cleaner does not add specific movie title / release group special cases.
5. Obvious Movie titles with clean query information still enter Movie identification.
6. Low-information Movie queries still do not auto-bind.
7. Candidate queries are split into usable / noisy / rejected buckets.
8. Noisy / rejected queries are retained as context, not primary title hints.
9. Final `aiCandidateRanges` summary is emitted after TV identification.
10. Final summary includes TV placeholder / unresolved / conflict ranges produced during TV identification.
11. Final summary is merged and de-duplicated by sanitized directory.
12. Final summary paths are sanitized.
13. Final summary includes risk tags and conflict reasons where available.
14. Same-name / version conflicts remain downgraded to placeholder / AI-candidate behavior.
15. No AI call is made for candidate ranges.
16. Default full AI range analysis remains disabled.
17. Phase 4.11d / 4.11e-prep / 4.11e-prep-2 conservative routing is not rolled back.
18. No specific title / directory / file prefix / TMDB ID hardcoding is introduced.
19. TMDB thresholds are unchanged.
20. Media-library visibility semantics are unchanged.
21. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-prep-4 - Scan Auto-Bind Validation Gates

Implemented scope:

- Movie scan auto-apply now requires a clear `Matched` result. `NeedsReview`, low-confidence, dirty-query, low-information, and conflict-like outcomes remain placeholders / future AI-candidate diagnostics instead of being applied to Movie metadata.
- Movie scan diagnostics now record `movieResultStatus`, `movieAutoApply`, and `movieAutoApplyBlockedReason` around search and apply decisions.
- TV scan auto-apply now requires confidence at the `Matched` threshold. `NeedsReview`-level TV candidates are downgraded to placeholder / AI-candidate diagnostics and are not written as matched Seasons.
- TV localized-title exact matches are checked for generic version qualifier conflicts. If the localized title matches only after dropping a bracketed qualifier while the original title does not support the query, the candidate is downgraded instead of auto-bound.
- TV diagnostics now record localized-title exact-match state, original-title conflict state, auto-apply state, and blocked reason.
- Movie title cleanup received a small generic release/source/audio/subtitle cleanup extension, including spaced source tokens and symbol-heavy trailing release tails.
- Default full AI range analysis remains disabled and this phase does not call AI for uncertain ranges.
- Did not change TMDB thresholds, media-library visibility, playback, Discovery, Watch Insights, or AI recommendation semantics.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11e-prep-4 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11e-prep-4 migration is created.
3. Movie `NeedsReview` results do not auto-apply.
4. Movie `Matched` results still auto-apply.
5. Movie low-confidence / dirty-query / conflict-like results do not auto-apply.
6. TV localized-title exact matches with original-title / version qualifier conflict downgrade to placeholder / AI-candidate diagnostics.
7. TV conflict / NeedsReview candidates do not auto-apply.
8. Movie cleaner only adds generic release/source/audio/subtitle cleanup and no specific title / release-group special cases.
9. Default full AI range analysis remains disabled.
10. No AI call is made for candidate ranges.
11. Final `aiCandidateRanges` summary behavior is preserved.
12. Phase 4.11d / 4.11e-prep / 4.11e-prep-2 / 4.11e-prep-3 conservative routing is not rolled back.
13. No specific title / directory / file prefix / TMDB ID hardcoding is introduced.
14. TMDB thresholds are unchanged.
15. Media-library visibility semantics are unchanged.
16. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e - AI-on-Uncertain Directory Assistance

Implemented scope:

- Added an AI-on-uncertain pass that reads only the final sanitized `aiCandidateRanges` produced by local TV/Movie scan diagnostics.
- Kept default full AI directory range analysis disabled; the new AI pass does not inspect the full scan tree.
- The AI prompt asks for directory / title / season hints only and explicitly rejects `episodeFiles` or per-file Season / Episode mapping.
- AI responses are parsed by stable `inputRangeId`; unknown IDs, low confidence, and `needsReview` hints are ignored and preserve local placeholders.
- Accepted AI hints are written back as `ai-on-uncertain` scan hints only, then re-run through the local TV parser, TMDB search, conflict downgrade, and auto-bind safety gates.
- AI hints do not write database records directly and do not override Movie / TV `NeedsReview` gates.
- Scan diagnostics record AI-on-uncertain attempted / skipped state, input size, duration, success/failure, parsed hints, ignored hints, applied hints, and final validation retry.
- AI failure, timeout, empty response, or invalid JSON falls back to the existing placeholder / candidate range behavior.
- Did not change TMDB thresholds, media-library visibility, playback, Discovery, Watch Insights, or AI recommendation semantics.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11e Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11e migration is created.
3. Default full AI range analysis remains disabled.
4. AI-on-uncertain only processes final `aiCandidateRanges`.
5. Empty `aiCandidateRanges` skips AI.
6. AI input is sanitized and not a full scan tree.
7. AI prompt does not request `episodeFiles`.
8. AI response parser ignores unexpected `episodeFiles`.
9. AI hints never write records directly.
10. AI title hints go through TMDB validation.
11. Local parser still owns Episode parsing.
12. Only `Matched` and no-conflict results can auto-apply.
13. `NeedsReview`, conflict, low-confidence, dirty-query, and weak-source results do not auto-apply.
14. AI failures preserve placeholders / candidate ranges.
15. Movie `NeedsReview` remains blocked from auto-apply.
16. TV localized-title conflicts remain downgraded.
17. No specific title / directory / file prefix / TMDB ID hardcoding is introduced.
18. TMDB thresholds are unchanged.
19. Media-library visibility semantics are unchanged.
20. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-fix-1 - AI Hint Range Mapping

Implemented scope:

- AI-on-uncertain hint application now treats `inputRangeId` as the primary local mapping key.
- `seriesFolderHint` and `seasonFolderHint` are no longer hard requirements for applying a hint once `inputRangeId` maps to a final `aiCandidateRange`.
- Directory hint mismatch is logged as diagnostics (`directoryHintMismatch`, `aiHintAppliedBy`) instead of causing the whole hint to be ignored.
- Fuzzy sanitized-path matching is retained only as a fallback when `inputRangeId` is missing or unknown.
- AI title / season hints still only become scan hints; local parser, TMDB validation, conflict downgrade, and auto-bind safety gates still decide final writes.
- Media-library batch mode adds current-list select-all and clear-selection helpers for scan retest cleanup workflows.
- Delete-record and remove-from-library semantics are unchanged.
- Did not change TMDB thresholds, media-library visibility, playback, Discovery, Watch Insights, or AI recommendation semantics.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11e-fix-1 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11e-fix-1 migration is created.
3. AI-on-uncertain hints prefer `inputRangeId` mapping.
4. Directory hint mismatch does not discard a mapped `inputRangeId` hint.
5. Unknown `inputRangeId` falls back to fuzzy sanitized-path matching before being ignored.
6. AI title hints go through TMDB validation.
7. AI season hints remain context only.
8. AI hints never write records directly.
9. Only `Matched` and no-conflict results can auto-apply.
10. `NeedsReview`, conflict, low-confidence, dirty-query, and weak-source results do not auto-apply.
11. Default full AI range analysis remains disabled.
12. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.
13. Library batch mode exposes current-list select-all.
14. Current-list select-all does not select hidden, filtered-out, or unloaded items.
15. Batch delete-record semantics remain software-record-only and do not delete local or WebDAV files.

## Phase 4.11e-fix-2 - AI Candidate Range File Binding

Implemented scope:

- `aiCandidateRanges` now carry runtime `MediaFileIds` for the files covered by each uncertain range.
- Range merge / dedupe now merges `MediaFileIds` along with risk tags, queries, conflicts, and sample file diagnostics.
- AI-on-uncertain hint application still maps by `inputRangeId` first, but file resolution now uses the range `MediaFileIds` before falling back to sanitized path matching.
- `SanitizedPath` remains for diagnostics, AI prompt context, and fallback only; it is no longer the primary way to recover files for a mapped range.
- Diagnostics now report range file counts, file resolution method, and aggregate counts such as ranges with files, applied-by-media-file-ids, and no-file ignored counts.
- AI hints remain hint-only and still flow through local parser, TMDB validation, conflict downgrade, and auto-bind safety gates.
- The Phase 4.11e-fix-1 media-library current-list select-all helper is preserved.
- Did not change TMDB thresholds, media-library visibility, playback, Discovery, Watch Insights, or AI recommendation semantics.
- Did not add a migration.
- Did not execute database update.

## Phase 4.11e-fix-2 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11e-fix-2 migration is created.
3. Candidate ranges store runtime `MediaFileIds`.
4. `MediaFileIds` are not persisted to the database.
5. Range merge / dedupe preserves merged `MediaFileIds`.
6. AI hints map ranges by `inputRangeId`.
7. Mapped range files resolve by `MediaFileIds` first.
8. Sanitized path matching is fallback only.
9. Directory hint mismatch does not drop a mapped range.
10. AI title hints go through TMDB validation.
11. AI season hints remain context only.
12. AI hints never write records directly.
13. Only `Matched` and no-conflict results can auto-apply.
14. `NeedsReview`, conflict, low-confidence, dirty-query, and weak-source results do not auto-apply.
15. Default full AI range analysis remains disabled.
16. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.
17. Library batch current-list select-all remains available.
18. Batch delete-record semantics remain unchanged.

## Phase 4.11f - AI Refined Title TMDB Lookup

Implemented scope:

- AI-on-uncertain prompt/schema now asks for `refinedSeriesTitle`, optional original title / year / season hints, confidence, `needsReview`, and short evidence.
- AI still only processes final `aiCandidateRanges`; full AI directory range analysis remains disabled.
- AI is not given TMDB top-N candidates and does not choose a TMDB candidate.
- Accepted AI refined title hints are stored as `ai-refined-title` scan hints through the existing `inputRangeId -> MediaFileIds` mapping.
- TV search uses the refined title as a local TMDB TV search query and evaluates TMDB top1 with a lightweight safety gate.
- Lightweight gate checks AI confidence / `needsReview`, refined-title match, optional original title / year hint, and unresolved top-candidate proximity before allowing auto-apply.
- Safety-gate failures stay placeholder / `NeedsReview` / `ai-candidate`; AI still never writes records directly.
- Local parser still owns Episode parsing, and existing Matched / conflict / dirty-query / low-confidence gates remain in force.
- Did not restore full AI, add a second AI call, change TMDB thresholds, change media-library visibility, or add a migration.
- Did not execute database update.

## Phase 4.11f Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f migration is created.
3. Default full AI range analysis remains disabled.
4. AI-on-uncertain only processes final `aiCandidateRanges`.
5. AI prompt does not include TMDB top-N candidates.
6. AI does not choose TMDB candidates.
7. AI returns `refinedSeriesTitle`.
8. `refinedSeriesTitle` is used for local TMDB TV search.
9. TMDB top1 is not written unconditionally.
10. Lightweight safety gate must pass before auto-apply.
11. Safety-gate failures preserve placeholder / `NeedsReview` / `ai-candidate`.
12. AI `needsReview` does not auto-apply.
13. AI low confidence does not auto-apply.
14. TMDB no-result does not auto-apply.
15. Year conflict does not auto-apply.
16. Unresolved version / top-candidate conflict does not auto-apply.
17. `inputRangeId -> MediaFileIds` file binding is preserved.
18. Local parser still owns Episode parsing.
19. Movie `NeedsReview` remains blocked from auto-apply.
20. Library batch current-list select-all remains available.
21. No specific title / directory / file prefix / TMDB ID hardcoding is introduced.
22. TMDB thresholds are unchanged.
23. Media-library visibility semantics are unchanged.
24. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11f-fix-1 - AI refined title top1 apply

Completed:

- Adjusted AI-on-uncertain refined title prompt semantics: uncertain ranges should still return the best clean `refinedSeriesTitle` when possible.
- `needsReview=true` and `low` confidence no longer short-circuit refined title TMDB lookup when a non-empty refined title exists.
- Removed the refined lookup blockers based on original-title, localized-title, year, version, and top1/top2 conflict checks.
- AI refined title lookup now searches TMDB TV and accepts top1 for the AI refined path when a result exists.
- Preserved core boundaries: AI does not receive TMDB top-N, does not choose a TMDB candidate, does not write records directly, and does not return episode-level mapping.
- Preserved `inputRangeId -> MediaFileIds`, local episode parser ownership, full-AI disabled mode, Movie `NeedsReview` write blocking, and media-library visibility semantics.

Acceptance notes:

- Empty refined title and TMDB no-result still preserve placeholder / `ai-candidate`.
- The AI refined top1 path intentionally accepts possible top1 mismatch risk; Phase 4.12 active correction is the follow-up mitigation.

## Phase 4.11f-fix-2 - Original-Language AI Refined Title Lookup

Completed:

- AI-on-uncertain refined title prompt now prioritizes `originalLanguageTitle` and asks for English / localized aliases only as fallback hints.
- The AI response parser accepts `originalLanguageTitle`, `englishTitleHint`, `localizedTitleHint`, `searchTitle`, legacy `refinedSeriesTitle`, year / season hints, confidence, `needsReview`, and short evidence.
- Local TMDB TV refined lookup now selects query title in this order: original-language title, search title, English title, localized title, legacy refined title.
- Diagnostics record the selected AI search title, search title source, original-language-title missing state, English/localized fallback use, and the TMDB query source.
- The previous `inputRangeId -> MediaFileIds` binding, full-AI disabled mode, Movie `NeedsReview` write blocking, local parser ownership, and TMDB top1 product strategy are preserved.
- AI still does not receive TMDB top-N candidates, choose a TMDB candidate, return `episodeFiles`, write records directly, or enter TV AI recommendations / Watch Insights.
- No migration was added and database update was not executed.

## Phase 4.11f-fix-2 Manual Acceptance Matrix

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-2 migration is created.
3. AI prompt asks for original-language series titles.
4. AI response parser supports `originalLanguageTitle`.
5. TMDB search prefers `originalLanguageTitle`.
6. English and localized title hints are fallback only.
7. Fallback use is logged.
8. No specific title / directory / TMDB ID mapping is introduced.
9. AI still does not receive TMDB top-N candidates or choose TMDB candidates.
10. TMDB top1 strategy remains limited to the AI refined lookup path.
11. `inputRangeId -> MediaFileIds` binding is preserved.
12. Local parser still owns Episode parsing.
13. Movie `NeedsReview` remains blocked from auto-apply.
14. Media-library batch current-list select-all remains available.
15. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11f-perf-1 - Limit AI retry scope and cache TMDB searches

Completed:

- The post-AI TV retry no longer reprocesses the full scan set. It now retries only MediaFiles affected by accepted AI-on-uncertain hints.
- When the AI affected file set is empty, the second TV pass is skipped and logged.
- Added a per-scan runtime TMDB search cache for TV and Movie search calls. The cache is not persisted and does not require a migration.
- TV and Movie caches are separated and keyed by media type, normalized query, and relevant search context such as language / page or release year.
- Diagnostics now record first-pass TV counts, AI affected file count, second-pass TV scope, TMDB cache hits / misses, cache entries, and duplicate searches avoided.
- This stage does not change TV parser rules, Movie fallback rules, AI prompt/schema, AI refined top1 behavior, safety gates, media-library visibility, or delete-record semantics.
- Movie AI classification remains background best-effort and TV remains excluded from AI recommendations / Watch Insights.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-perf-1 migration is created.
3. Second-pass TV retry scope is `ai-affected-files`.
4. Second-pass TV retry is skipped when no AI affected file exists.
5. TMDB TV and Movie search caches are separate and per-scan only.
6. Cache hit / miss and duplicate-search-avoided counters are logged.
7. Scan identification behavior and auto-bind gates are unchanged.

## Phase 4.11f-fix-3 - Scan closeout guards and movie placeholder grouping

Completed:

- Added a minimal AI refined top1 year guard. It compares only AI `seriesYearHint` with TMDB Series first-air year, blocks auto-apply only when the difference is greater than two years, and preserves placeholder / `ai-candidate` output.
- `seasonYearHint` is logged but does not participate in the Series year guard.
- Kept AI refined lookup otherwise unchanged: full AI range analysis stays disabled, AI still does not receive TMDB top-N candidates, and AI still does not write records directly.
- Added low-risk Movie title cleanup for HTML entities, trailing dangling punctuation, `3D` quality/source tokens, and conservative release/source/audio/subtitle noise.
- Left TV parser matching rules unchanged. OAD / SP / OVA / special mapping remains out of default scan scope.
- Added log-only grouping for Movie placeholder files that form at least three strictly consecutive episode-like numbers within one direct parent folder.
- Placeholder grouping does not affect matched Movie or matched TV rows, does not cross directories, and does not create Series / Season / Episode rows.
- Diagnostics now expose AI refined year-gate fields and Movie placeholder grouping counts / skipped reasons.
- No migration was added and database update was not executed.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-3 migration is created.
3. AI refined top1 series-year gate blocks only obvious `seriesYearHint` vs TMDB first-air-year conflicts.
4. `seasonYearHint` does not block Series matching.
5. Missing year hints do not block refined lookup.
6. Movie cleaner handles only generic release/source/audio/subtitle noise and trailing dangling punctuation.
7. TV parser rules are not expanded.
8. Movie placeholder grouping only sees Movie placeholder failures.
9. Placeholder grouping requires one direct parent folder and at least three strictly consecutive numbers.
10. Placeholder grouping excludes CD / Disc / Part / sample / trailer / extras-like material.
11. Placeholder grouping is log-only and does not create Season / Episode rows.
12. Full AI range analysis remains disabled.
13. AI-on-uncertain still only processes final `aiCandidateRanges`.
14. `inputRangeId -> MediaFileIds` binding remains in force.
15. Movie AI classification remains background best-effort.
16. Media-library visibility and delete-record semantics are unchanged.
17. TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11f-fix-4 - Persist and surface grouped TV-like placeholders

Completed:

- Movie placeholder grouping is no longer only a scan diagnostic. Consecutive numbered Movie placeholder failures are projected as runtime media-library `Other` / TV-like placeholder ranges.
- Grouped ranges keep their runtime `MediaFileIds`, file count, parent-folder display name, number span, sample filenames, and reason tags for later correction / manual aggregation.
- Grouped Movie placeholder files are hidden from the normal Movie scatter list through query-time read-model projection. Ungrouped Movie placeholders and matched Movie / TV entries keep their existing behavior.
- Grouped ranges do not create `TvSeries`, `TvSeason`, `TvEpisode`, or TMDB bindings and do not mark recognition as successful.
- The AI refined year-gate diagnostic now reports `tvAutoApply=false` when a year conflict blocks the refined top1 path.
- Movie cleaner received a small generic cleanup for leading quality prefixes and conservative edition tails only under release-cleanup context.
- No migration was added and database update was not executed.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-4 migration is created.
3. Movie placeholder grouping persistence is `query-time-read-model`, not `log-only`.
4. Grouped ranges are surfaced as `Other` / TV-like placeholder library rows.
5. Grouped ranges retain runtime `MediaFileIds`.
6. Grouped ranges do not create Series / Season / Episode rows and do not bind TMDB.
7. Grouped Movie placeholders are hidden from the normal Movie scatter list, while ungrouped Movie placeholders remain visible.
8. Grouping rules still require one direct parent folder and strict consecutive numbering.
9. AI refined year-gate blocked logs show `tvAutoApply=false`.
10. Full AI range analysis remains disabled and TV remains excluded from AI recommendations / Watch Insights.

## Phase 4.11f-fix-5 - Complete Other category and grouped placeholder UX

Completed:

- Media-library content categories are now `All / Movie / TV / Other`, with `Other` covering unrecognized, placeholder, NeedsReview, and grouped TV-like placeholder rows.
- Movie category is reserved for recognized Movie rows; ordinary unrecognized Movie placeholders now project as `Other` instead of remaining in the Movie category.
- TV category remains for recognized series / seasons. Grouped TV-like placeholder files are converted into unidentified `TvSeason` / `TvEpisode` rows and stay in `Other` until corrected.
- The visible recognition-status filter was removed from the main library UI. Backend recognition status fields and filtering state remain available for future correction/debug flows.
- Grouped TV-like placeholders now use existing unidentified Season / Episode persistence instead of a temporary grouped read model. The scan creates no-TMDB `TvSeries`, failed `TvSeason`, and failed `TvEpisode` rows, then moves the grouped `MediaFile` rows from failed Movie placeholders to those Episodes.
- Unidentified Episodes use the original source file name for display so unknown items do not show the cleaned Movie query.
- Grouped TV-like placeholder seasons support normal Season detail navigation, playback, watched / unwatched marking, select-current-list, hide / restore, and delete-record operations through existing TV Season semantics.
- Follow-up log analysis showed persisted unidentified Seasons were missing from normal media-library mode because the non-expanded query only loaded Series summaries. The query now appends failed unidentified Seasons into `Other` and suppresses all-failed placeholder Series from recognized TV summaries.
- Movie placeholder grouping now supports a conservative bracketed episode-number segment pattern for continuous failed placeholders where the episode number lives inside a bracket block.
- This stage does not bind TMDB metadata, does not mark recognition successful, and does not create fake playback sources.
- Grouped ranges continue to avoid Watch Insights and AI recommendation inputs.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-5 migration is created.
3. `Other` includes ordinary unrecognized Movie placeholders and unidentified TV-like Seasons.
4. Movie category excludes unrecognized placeholders.
5. Grouped TV-like placeholder files become unidentified Episodes under an unidentified Season.
6. Unidentified Season detail is playable when Episode `MediaFile` rows exist and remains marked unresolved / pending correction.
7. Grouped placeholder conversion does not bind TMDB and does not delete local or WebDAV files.
8. Recognition-status UI is hidden while backend status data remains available.
9. Normal media-library mode includes failed unidentified Seasons in `Other`.
10. Bracketed episode-number grouping remains bounded by same-parent, strict-contiguous, minimum-three-file rules.

## Phase 4.11f-fix-6 - Relax title-number TV candidates and episode sequence parsing

Completed:

- TV preanalysis now treats multi-file title+number sequences as TV-like uncertain ranges before Movie fallback. A single title+number file still does not create strong TV context or auto-bind TV.
- Title+number sequence admission is conservative: same direct parent folder, at least three video files, shared normalized title prefix, strict contiguous numbering, and no cross-directory recursion.
- Final `aiCandidateRanges` diagnostics now include title-number sequence fields: candidate state, prefix, start/end number, file count, and whether the range was added for AI-on-uncertain.
- Multi-episode detection now requires an explicit plausible episode range and rejects common false positives such as years, quality numbers, audio channel numbers, and ordinary title numbers after a single episode marker.
- Unsupported TV diagnostics now include sanitized sample names, match kind, unsupported reason, detected multi-episode range, and detected multi-episode pattern.
- Explicit episode markers now support four-digit episodes (`SxxE####`, `E####`, `EP####`, `Episode ####`, and Chinese episode markers). Bare four-digit numbers remain excluded from global episode parsing.
- Movie placeholder grouping now supports numeric filenames with quality/source/codec tails and can merge same-parent mixed patterns when all parsed episode numbers form one strict contiguous sequence.

Not done:

- `01: title` / leading-number-colon-title parsing remains deferred.
- Movie collections, course folders, theatrical collection grouping, anime-specific SP/OAD/OVA mapping, and multi-episode file splitting remain out of default scan scope.
- No third AI pass, no new migration, and no database update were added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-6 migration is created.
3. Single title+number file does not become strong TV.
4. Strict-contiguous multi-file title+number folders enter TV-like uncertain / AI candidate ranges.
5. Title+number ranges are not auto-bound before AI/TMDB validation.
6. Normal `S01E01 - 1999` / `S01E01.1999` style single episodes are not treated as unsupported multi-episode files.
7. Actual explicit multi-episode ranges remain unsupported and diagnostic-only.
8. Four-digit episode numbers only work under explicit episode markers.
9. Bare `1999`, `2023`, `1080`, and `2160` are not treated as episodes.
10. Movie placeholder grouping can group numeric quality-tail files and same-parent mixed episode patterns.
11. Grouping still requires strict contiguous numbering and at least three files.
12. Grouping does not create recognized Series / Season / Episode rows and does not bind TMDB.

## Phase 4.11f-fix-7 - Apply verified title-number episode sequences

Completed:

- Verified same-parent title+number sequences now participate in TV apply episode parsing, not only AI candidate selection.
- The verified sequence parser remains scoped to prevalidated ranges: same direct parent, at least three strictly contiguous files, and the same sequence key. A single title+number file still does not become global TV evidence.
- Supported verified forms include `Title 01`, `Title.01`, `Title - 01`, `Title_01`, `Title S02 01`, `Title Season 02 01`, and `Title The Final Season - 01`. Final-season wording is not mapped to a numeric TMDB season unless an explicit season number exists.
- Unsupported TV diagnostics now separate true `multi-episode-not-supported` from generic `episode-parse-failed` / `title-number-sequence-not-applied` cases.
- `tv-parse` and `tv-candidate-unsupported` diagnostics now include `verifiedTitleNumberSequenceContext`.
- Scan discovery now records ignored-file summaries by reason and extension for local and WebDAV scans. The log includes sanitized samples, duplicate-path counts, unsupported-extension counts, and current video/subtitle whitelist summaries.
- This phase does not expand the video extension whitelist; potential video extensions found in ignored files remain evidence for a later whitelist decision.

Not done:

- `01: title` / leading-number-colon-title parsing remains deferred.
- Movie collections, course folders, theatrical collections, anime SP/OAD/OVA mapping, and multi-episode file splitting remain out of default scan scope.
- No third AI pass, no new migration, no database update, and no Watch Insights / AI recommendation TV input were added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-7 migration is created.
3. Verified title+number sequence files can parse as episodes during TV apply.
4. Single title+number files remain conservative.
5. `S2 01` / `Season 2 01` can carry explicit season context; Final Season text is not hard-mapped.
6. Generic parse failures no longer claim multi-episode unsupported unless an actual range is detected.
7. True multi-episode ranges remain unsupported and diagnostic-only.
8. Ignored scan files are summarized by reason and extension with sanitized samples.
9. Video extension whitelist is not expanded in this phase.
10. Other category, unidentified Seasons/Episodes, and grouped placeholder behavior are not rolled back.

## Phase 4.11f-fix-8 - Surface orphan media and expand scan candidates

Completed:

- Orphan video `MediaFile` rows now project into the media-library `Other` category when they are active video sources with no Movie binding, no Episode binding, and no grouped unidentified Season binding.
- Orphan rows use the original safe file name as the display title. Movie cleaner output is not used as the primary title for unrecognized files.
- Existing and future unidentified single-source videos under scanned paths are passed through the same conservative grouping helper used by Movie placeholder grouping. Strict same-parent contiguous episode-like runs can become unidentified Season / Episode rows without TMDB binding.
- The scan closeout aggregation runs after Movie identification for the current enabled scan paths, so historical orphan files and newly produced unresolved files share the same grouping path.
- TV preanalysis now recognizes bracketed episode segment sequences, such as bracket-title plus bracketed episode number blocks, and can emit them as TV-like uncertain `aiCandidateRanges` before Movie fallback.
- `.rmvb` is added to the video extension whitelist and `.sup` is added to the subtitle whitelist. `.sup` playback / rendering support remains to be verified separately.
- Ignored-file summaries continue to log reason / extension counts and current whitelist summaries for local and WebDAV scans.

Not done:

- `01: title` / leading-number-colon-title parsing remains deferred.
- Movie collections, course folders, theatrical collections, anime SP/OAD/OVA mapping, multi-episode splitting, and manual regrouping remain deferred.
- No third AI pass, no new migration, no database update, and no TV input to Watch Insights / AI recommendations were added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-8 migration is created.
3. Orphan video files appear in `Other` with original file names.
4. Orphan files already bound to Movie / Episode are not duplicated.
5. Unidentified Season rows and their source files are not duplicated as orphan scatter items.
6. Historical and new unresolved files can be grouped through the same conservative same-parent contiguous rules.
7. Bracketed episode segment directories can enter AI-on-uncertain before Movie placeholder grouping.
8. `.rmvb` is scanned as video and `.sup` is scanned as subtitle candidate.
9. `01: title`, SP/OAD/OVA, course, and theatrical collection handling remain out of default scan scope.
10. Delete / hide semantics, Movie AI background classification, and TV exclusion from Watch Insights / recommendations are unchanged.

## Phase 4.11f-fix-10 - Dotted part title parsing

Completed:

- Verified title-number parsing now distinguishes raw file names from already-trimmed base names, preventing dotted part markers such as `Pt.2` from being treated as file extensions during TV apply.
- Verified sequence parsing now detects generic `Pt.2` / `Pt 2` / `Part.2` / `Part 2` hints after explicit season markers.
- Part-aware parse results preserve `seasonHint`, `partHint`, and `episodeInPart` diagnostics.
- Part hints do not automatically offset episode numbers. A part file such as `Title S3 Pt.2 01` is preserved as unresolved / pending correction unless a later correction workflow can safely map the part offset.
- `tv-parse` and unsupported-candidate diagnostics now report `partHintDetected`, `partHint`, `episodeInPart`, `episodeOffsetApplied=false`, and `episodeOffsetSkippedReason=no-safe-part-offset`.

Not done:

- No automatic `Part 2 -> E13` or similar offset mapping was added.
- Final-season, SP/OAD/OVA, theatrical collection, and manual part-offset correction remain deferred.
- No AI prompt, TMDB top1, year-gate, migration, database update, Watch Insights, or recommendation behavior was changed.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-10 migration is created.
3. `Pt.2` / `Part.2` no longer causes a second extension trim that removes the episode number.
4. Part-aware verified sequences log season / part / episode-in-part hints.
5. Part hint is not treated as an episode number.
6. Episode-in-part is not automatically offset to a TMDB episode number.
7. Unresolved part ranges remain unidentified / pending correction instead of wrong-binding.
8. Existing `Title S3 01` style parsing is not rolled back.
9. TV remains excluded from Watch Insights and AI recommendation inputs.

## Phase 4.11f-fix-11 - Safe sibling part episode offset

Completed:

- The fix-10 dotted part parser path is preserved: verified title-number parsing trims the raw file extension once and keeps `Pt.2` / `Part.2` as part hints instead of treating `.2` as an extension.
- Part-aware parse results continue to expose `seasonHint`, `partHint`, and `episodeInPart` diagnostics before any offset is considered.
- A safe sibling part offset pass now runs after TMDB Series / Season validation and before matched Season apply.
- Offset is generic for `Part 2+`: the current part must have an explicit season number, a `partHint >= 2`, a contiguous episode-in-part sequence starting at 1, a previously bound same-source sibling range for the same TMDB Series and Season, available TMDB season episode count, and no target episode conflict.
- When safe, `episodeInPart + previousPartEndEpisode` becomes the TMDB episode number and logs `episodeOffsetApplied=true`, `episodeOffsetSource=sibling-part-continuation`, `previousPartEndEpisode`, and `mappedEpisodeNumber`.
- When unsafe, the files remain unresolved / pending correction with `episodeOffsetSkippedReason` such as `missing-previous-part`, `previous-part-not-bound`, `tmdb-episode-count-unavailable`, `tmdb-episode-count-insufficient`, or `target-episode-conflict`.

Not done:

- No hard-coded `Part 2 -> E13` rule was added.
- No SP/OAD/OVA, theatrical collection, Final Season mapping, third AI pass, migration, database update, Watch Insights input, or TV recommendation input was added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-11 migration is created.
3. `Pt.2` / `Part.2` still parse as part hints, not extensions.
4. Part offset requires explicit season and part hints.
5. Part offset requires same TMDB Series and Season sibling evidence.
6. Part offset requires a contiguous previous episode range and TMDB season episode count.
7. Target episode conflicts block offset instead of overwriting sources.
8. Unsafe part ranges remain unidentified / pending correction.
9. The logic is generic for later parts and does not special-case a title, folder, or TMDB id.
10. TV remains excluded from Watch Insights and AI recommendation inputs.

## Phase 4.11f-fix-11-hotfix - Disable unsafe part offset apply

Completed:

- Kept the fix-10 dotted part parser path: `Pt.2` / `Part.2` remains a part hint and is not treated as an extension, an episode number, or a TMDB title.
- Added a hard TV query reject for structural part-only queries such as `Part`, `Pt`, `Part 2`, `S3 Part`, and `Season 3 Part 2`.
- Structural part queries now go to rejected candidate-query diagnostics with reason `structural-part-query`, including AI refined title sources; they no longer enter usable queries, TMDB search, or auto-apply.
- Disabled the current automatic sibling part offset apply path for this phase. Existing helper code remains for future redesign, but the active scan path reports `part-offset-apply-disabled` and keeps unresolved part files as unidentified / pending correction.
- Offset diagnostics now distinguish parse-time `not-evaluated` from an actual safety evaluation failure.

Not done:

- No new Part2 / Part3 offset mapping was implemented.
- No AI prompt, TMDB top1, year-gate, SP/OAD/OVA, theatrical collection, migration, database update, Watch Insights, or recommendation behavior was changed.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-11-hotfix migration is created.
3. Structural `Part` / `S Part` queries are rejected before TMDB search.
4. AI refined structural part queries are not bypassed into usable TV queries.
5. Part hints remain logged as diagnostics.
6. Automatic part offset apply is disabled until a safer ordering / evidence model is redesigned.
7. Unresolved part files remain unidentified / pending correction instead of wrong-binding.
8. TV remains excluded from Watch Insights and AI recommendation inputs.

## Phase 4.11f-fix-13 - Batch AI-on-uncertain requests

Completed:

- AI-on-uncertain no longer sends all final candidate ranges in one large request.
- Candidate ranges are deterministically grouped by local folder / suspected title context and split into at most 3 batch prompts without splitting any individual range.
- Up to 3 AI batches run concurrently. Each batch has its own 300 second timeout and can retry once with the same payload.
- Successful batch hints are retained and merged back into the same `inputRangeId -> MediaFileIds` apply path. Failed batches only affect their own ranges and leave those ranges as local placeholders / ai-candidates.
- Final AI summary now reports batch count, successful / failed batches, failed range count, parsed / applied / ignored hint totals, and affected media files.
- Partial AI batch failure is recorded as a warning, not a scan error.

Not done:

- No AI prompt, response schema, TMDB top1, year gate, parser, part-offset, Movie fallback, migration, database update, Watch Insights, or recommendation behavior was changed.
- Failed batches are not recursively split in this phase; that remains a deferred optimization if a single batch is still too large.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-13 migration is created.
3. AI-on-uncertain batch count is at most 3.
4. Each range remains intact inside one batch.
5. Batches can run concurrently and have independent timeout / retry state.
6. Successful batch hints are applied even if another batch fails.
7. Failed batch ranges remain unresolved / pending correction.
8. Partial AI failure increments warning telemetry, not scan error count.
9. TV remains excluded from Watch Insights and AI recommendation inputs.

## Phase 4.11f-fix-14 - Safe part sequence offset after AI batching

Completed:

- Preserved the fix-10 dotted part parser: `Pt.2` / `Part.2` remains a part hint with `episodeInPart`, not a filename extension or search title.
- Re-enabled part offset only after the scan has a confirmed TMDB Series and Season from the normal AI refined title / TMDB validation path.
- Structural part-only queries remain rejected as `structural-part-query`; `Part`, `Pt`, `S Part`, `Season Part`, and season / part / number-only strings cannot search TMDB or auto-apply.
- Candidate processing now orders sibling part directories so earlier same-parent parts are handled before later parts where possible.
- Safe offset can use either current-scan evidence already parsed as contiguous E01..N or safely bound database episodes for the same source, TMDB Series, and Season.
- Offset is generic for later parts: it maps `episodeInPart + previousRangeEndEpisode` only when the previous range starts at E01, is contiguous, TMDB season episodes exist, and target episodes are not occupied.
- If the evidence is insufficient, the part files stay unidentified / pending correction with explicit skipped reasons such as `missing-previous-range`, `previous-part-not-bound`, `target-episode-missing`, or `target-episode-conflict`.

Not done:

- No fixed `Part 2 -> E13` rule was added.
- No AI prompt, TMDB top1, year gate, Final Season mapping, SP/OAD/OVA mapping, theatrical collection handling, migration, database update, Watch Insights input, or TV recommendation input was changed.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-14 migration is created.
3. Structural part-only queries are rejected before TMDB search.
4. Part offset runs only after TMDB Series / Season confirmation.
5. Current-scan E01..N evidence can support same-candidate later part offset.
6. Safely bound database E01..N evidence can support sibling later part offset.
7. Offset does not create missing TMDB episodes.
8. Target episode conflicts block offset instead of overwriting sources.
9. Ordinary non-part TV parsing and AI refined matching are not gated by offset.
10. TV remains excluded from Watch Insights and AI recommendation inputs.

## Phase 4.11f-fix-14-hotfix - AI refined title for unsupported part offset candidates

Completed:

- Unsupported-only part candidates now keep parsed `partHint` / `episodeInPart` files in the search-query source set so an applied AI refined title can confirm the TMDB Series / Season.
- AI original-language / refined title hints are preferred over folder-name queries for those part candidates. Folder-name noise such as subtitles, quality tags, and structural part wording no longer has to be the only TMDB lookup path.
- Part offset still starts only after TMDB Series / Season confirmation and the existing safe sibling offset checks.
- If no AI refined title is available, the refined lookup finds no Series, or the candidate is not safe, part files remain unidentified / pending correction with explicit `episodeOffsetSkippedReason`.
- Diagnostics now log `aiRefinedTitleAvailable`, `aiRefinedSeriesLookupAttempted`, `aiRefinedSeriesLookupQuery`, `aiRefinedSeriesLookupSucceeded`, and concrete not-evaluated reasons.

Not done:

- No AI prompt, batching, parser, TMDB top1, year gate, Final Season mapping, SP/OAD/OVA mapping, migration, database update, Watch Insights input, or TV recommendation input was changed.
- No unsafe `Part 2 -> fixed offset` rule was added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.11f-fix-14-hotfix migration is created.
3. Unsupported-only part candidates can use AI refined / original-language title for TMDB Series lookup.
4. Structural part-only queries remain rejected before TMDB search.
5. Part offset evaluation starts only after confirmed TMDB Series / Season.
6. Offset failure leaves files unidentified / pending correction with explicit skipped reasons.
7. Ordinary non-part TV remains on the existing AI refined / parser path.
8. TV remains excluded from Watch Insights and AI recommendation inputs.

## Phase 4.12b - Episode detail and unknown detail shells

Completed:

- Added an Episode detail navigation route, ViewModel, and page shell.
- Added a TV detail query for Episode basics: Series / Season / Episode labels, identification status, overview / air date / runtime, watched / progress summary, source count, and source summary.
- Episode detail supports both recognized Episodes and failed unidentified Episodes. Missing titles fall back to safe source file names or `E{n}`.
- Season detail now has a non-playback `详情` entry per Episode. The existing Episode play command and playback button behavior were left unchanged.
- Other orphan unknown video items now ensure a failed Movie placeholder before opening and then reuse the existing unidentified Movie detail page as their detail carrier.
- Added a `修正信息` placeholder on Episode detail. It only reports that correction will be supported later and does not call AI, TMDB, or mutate correction state.
- Kept TV outside Watch Insights, AI recommendation inputs, Watch Profile inputs, and recommendation fingerprints.

Not done:

- No full Episode playback-source list, specified-source playback, source deletion, watched/unwatched write action, real correction workflow, AI correction, TMDB candidate search, scan-rule change, migration, database update, commit, or push was added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.12b migration is created.
3. Recognized Episodes can open the Episode detail shell from Season detail.
4. Failed unidentified Episodes can open the same shell with `未识别 / 待修正` status.
5. Season detail Episode play buttons keep their existing behavior.
6. Episode detail tolerates missing overview / air date / runtime / title metadata.
7. Episodes with active sources show a source count; Episodes without active sources show `暂无播放源`.
8. Other orphan unknown video files open through the unidentified Movie detail carrier instead of doing nothing.
9. The `修正信息` action is a placeholder only and does not trigger AI, TMDB, or correction writes.
10. Movie detail and unidentified Movie detail remain the carrier for Movie / orphan unknown items.

## Phase 4.12c - Episode source list and playback

Completed:

- Episode detail now shows a full active source list instead of only a source summary.
- Source rows display safe file names, local / WebDAV source type, masked location text, format, file size, duration, resolution, codecs, bitrate, probe state, recent playback, and progress where available.
- Added a top Episode play button that uses a derived default source. The default derivation prefers an explicit preferred source, then recent / in-progress playable source, then accessible local source, then WebDAV or the first stable source.
- Added per-source `播放此源` actions. The Episode detail ViewModel validates that the source belongs to the current Episode before passing `EpisodeId + MediaFileId` to the existing Episode player service.
- Recognized Episodes and failed unidentified Episodes use the same source list and playback path.
- Episodes without active sources keep the detail shell, display `暂无播放源`, and keep playback disabled.
- Other orphan unknown files still reuse the failed Movie placeholder / unidentified Movie detail carrier; no raw orphan playback path was added.
- Kept the `修正信息` action as a placeholder only.

Not done:

- No source deletion, persistent Episode default source, watched / unwatched write action, real correction workflow, AI correction, TMDB candidate search, batch correction, manual Season aggregation, scan-rule change, online subtitle search, TV Watch Insights input, TV recommendation input, migration, database update, commit, or push was added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.12c migration is created.
3. Recognized Episodes display active source rows.
4. A single-source Episode can use the top play button.
5. A multi-source Episode top play uses the derived default source.
6. A multi-source Episode can play an explicitly selected source.
7. Failed unidentified Episodes display sources and use the same playback path.
8. Source rows do not display full local paths or full WebDAV URLs.
9. Episodes without sources display `暂无播放源` and disable playback.
10. Season detail Episode play buttons keep their existing behavior.
11. Movie detail source list and playback behavior remain unchanged.
12. TV remains excluded from Watch Insights and AI recommendation inputs.

## Phase 4.12c-fix - Episode source display and default playback alignment

Completed:

- Moved Episode source location text onto a shared source display helper. Episode detail now decodes WebDAV / path text for display only, masks locations to safe trailing segments, and does not expose full local paths or full WebDAV URLs.
- Replaced the Episode detail-only safe-location implementation so recognized Episodes and failed unidentified Episodes use the same local / WebDAV display behavior.
- Added a shared Episode default-source selection helper and wired both Episode detail and Episode playback session resolution to it.
- The non-persistent Episode default rule is: preferred active source, accessible local source, recent / in-progress source, WebDAV source, then stable first source.
- `OpenEpisodeAsync(episodeId)` now resolves the same Episode default source as the Episode detail top play button. Passing an explicit `MediaFileId` still plays that selected source.
- Season detail Episode play buttons continue to call the existing Episode player path, but now inherit the aligned playback-session default rule.
- Episode detail source-row playback remains guarded so the selected `MediaFileId` must belong to the current Episode.
- Episode detail top play, Episode source-row play, and Season detail Episode play commands now enter a short opening state so repeated clicks cannot start duplicate playback work.

Not done:

- No WebDAV lazy probe, manual reprobe entry, Movie detail raw-path display change, source deletion, set-default button, persistent Episode default source, watched / unwatched write action, real correction flow, AI correction, TMDB candidate search, scan-rule change, migration, database update, commit, or push was added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.12c-fix migration is created.
3. Episode detail WebDAV locations are decoded for display and do not show full URLs.
4. Episode detail local locations do not show full absolute paths.
5. WebDAV-only, local-only, and mixed WebDAV + local Episode source lists keep all active sources visible.
6. Episode detail top play and `OpenEpisodeAsync(episodeId)` use the same derived default source.
7. Season detail Episode play uses the same playback-session default rule.
8. Explicit source-row playback still plays the selected source.
9. Failed unidentified Episode source list and playback stay on the same path as recognized Episode.
10. Episodes without sources still show `暂无播放源` and keep playback disabled.
11. Movie detail playback behavior remains unchanged.
12. TV remains excluded from Watch Insights and AI recommendation inputs.

## Phase 4.12c-fix-2 - Episode play busy state while player is open

Completed:

- Aligned Episode detail playback buttons with the existing player-window lifecycle semantics: play buttons now remain disabled while `IPlayerWindowService.IsPlayerOpen` is true, not only during the short open call.
- Episode detail subscribes to `PlayerWindowClosed` and refreshes command state / detail data when the player closes.
- Episode detail top play button and per-source play buttons now bind explicit busy text and `CanOpenPlayer`, so the disabled state is visible even when command requery is not enough.
- Season detail Episode play commands now use the same page-level player-open gate and stay disabled while any player window is open.
- Season detail Episode play buttons have explicit XAML `IsEnabled` trigger logic combining `HasPlayableSource` with the page-level `CanOpenEpisodePlayer`.
- The existing selected-source guard and Episode default-source rule were not changed.

Not done:

- No source deletion, set-default action, watched / unwatched write action, default-source rule change, scan-rule change, migration, database update, commit, or push was added.

Manual acceptance matrix:

1. Episode detail top play disables while the player window is open.
2. Episode detail source-row play disables while the player window is open.
3. Season detail Episode play disables while the player window is open.
4. Buttons recover after `PlayerWindowClosed`.
5. Playback failure recovers the buttons because no player window remains open.
6. Episodes without sources remain disabled.
7. Explicit source-row playback still plays the selected source.
8. Episode default-source selection remains unchanged from 4.12c-fix.
9. Movie detail playback behavior remains unchanged.
10. No new Phase 4.12c-fix-2 migration is created.

## Phase 4.12c-fix-3 - Episode media probing restore

Completed:

- Audited the Movie / Episode media-probe path. Detail pages do not start ffprobe; both read technical fields already stored on `MediaFile`.
- Confirmed `MediaProbeService` writes probe results to `MediaFile`, so Movie, Episode, failed Episode placeholders, and orphan / failed Movie placeholders can share the same probe fields.
- WebDAV and local scans now expand the post-scan probe candidate set beyond newly changed videos to include active videos in the scanned paths whose probe status is not successful or whose probe snapshot is stale.
- This catch-up candidate collection covers Episode sources and existing unprobed sources without changing TV identification or Movie fallback rules.
- Added probe queue diagnostics with counts for Movie, Episode, orphan, WebDAV, and local sources.
- Added sanitized probe lifecycle diagnostics for skipped, started, succeeded, failed, and unavailable probe outcomes. Logs include source kind and protocol but not full local paths or full WebDAV URLs.
- Episode detail source rows already mapped duration, resolution, codec, bitrate, probe status, probe error, and probed-at fields from `MediaFile`, so no detail-page lazy probing was added.

Not done:

- No detail-page lazy probe, manual reprobe action, background task center, ffprobe architecture rewrite, scan recognition rule change, default-source rule change, source deletion, set-default action, watched / unwatched write action, TV Watch Insights input, TV recommendation input, migration, database update, commit, or push was added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.12c-fix-3 migration is created.
3. Local Episode sources are included in post-scan probe candidates when they are new, changed, unprobed, or stale.
4. WebDAV Episode sources are included in post-scan probe candidates when they are new, changed, unprobed, or stale.
5. Failed unidentified Episode sources use the same `MediaFile` probe path.
6. Movie source probing remains on the same `MediaFile` path.
7. Probe execution is not filtered by `MovieId`; Episode and orphan sources are not skipped solely because they are not Movie sources.
8. WebDAV probe still uses the existing playback URL / credential construction and remains best-effort.
9. Probe logs are diagnostic and sanitized, with no full local paths or full WebDAV URLs.
10. Episode detail continues to display existing probe fields from `MediaFile`.

Known Issues:

- Blocker: none.
- Deferred: manual reprobe and detail-page lazy probe remain out of scope.
- Noise: WebDAV probe can still fail because of remote server, credential, timeout, or ffprobe limitations; the fix records sanitized failure status instead of fabricating technical metadata.

## Phase 4.12c-fix-4 - Detail lazy media probing

Completed:

- Added a detail-page lazy probe path for Movie detail and Episode detail.
- Movie detail now schedules a background best-effort probe check for the current Movie's active video sources after the detail model has loaded.
- Episode detail now schedules the same check for the current Episode's active video sources, including failed unidentified Episodes.
- The lazy path only receives current detail source ids, then `MediaProbeService` rechecks ownership, active video state, deleted state, source kind, probe status, stale probe snapshots, recent pending probes, and input availability before queueing.
- Each detail page queues at most 10 lazy probe candidates per call and tracks checked `MediaFileId` values for the ViewModel lifetime to avoid repeat enqueue from repeated refreshes.
- Detail lazy probe uses the same `MediaProbeService` background queue as manual queue work. It does not block the first detail render or playback buttons.
- `MediaProbeService` now emits probe status-change notifications when a source enters pending and when it reaches success / failed / unavailable / skipped.
- Current Movie / Episode detail pages listen for those notifications and refresh automatically when one of their displayed sources changes probe state.
- Added sanitized detail-lazy probe diagnostics for check start, candidates, queued work, skipped work, and status-change refresh.
- Probe status text in source rows now uses explicit stage language for waiting, running, completed, failed, unavailable, and skipped states. Episode source rows also show sanitized failure reason text.
- Episode detail source rows now include an `立即探测` action. It validates the source belongs to the current Episode and then runs a force probe for that `MediaFileId` immediately, refreshing the detail view after completion.
- Episode and Movie detail probe actions are disabled while the selected source is probing. Detail-page auto probe temporarily disables checked sources during candidate evaluation, then keeps only queued / pending sources disabled and restores skipped sources.

Not done:

- No app startup probe, scan-time probe enqueue, full-library probe backlog, task center UI, probe architecture rewrite, scan-rule change, Episode default-source change, source deletion, set-default action, watched / unwatched write action, TV Watch Insights input, TV recommendation input, migration, database update, commit, or push was added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new Phase 4.12c-fix-4 migration is created.
3. Opening Episode detail can queue current unprobed / stale Episode sources for probe.
4. Failed unidentified Episode detail uses the same lazy probe path.
5. Opening Movie detail can queue current unprobed / stale Movie sources for probe.
6. Failed Movie placeholders / orphan carriers use Movie detail lazy probe.
7. Sources outside the current Movie / Episode are rejected by ownership checks.
8. Successfully probed and current sources are skipped.
9. Recent pending sources are skipped to avoid duplicate active probe work.
10. Detail pages do not queue all library files.
11. Detail pages remain playable while lazy probe runs.
12. Logs remain sanitized and do not contain full local paths or full WebDAV URLs.
13. Episode source-row `立即探测` starts a force probe for the selected source only.
14. Probe buttons are disabled while the selected source is actively probing, including detail-page lazy probe candidate evaluation and pending state.

Known Issues:

- Blocker: none.
- Deferred: background task-center UI remains deferred.
- Noise: probe status-change refresh is debounced; several near-simultaneous source completions may appear after one combined refresh.

## Phase 4.12d - Episode source reset to unidentified

Completed:

- Audited the initial 4.12d source-delete implementation: it did not call physical file delete, WebDAV delete, hard-delete `MediaFile`, or clear watch history / subtitle bindings / probe fields / metadata, but it did mark `MediaFile.IsDeleted=true`, which prevented the source from returning to Other / unidentified handling.
- Replaced the Episode detail source-row action with `重置为未识别` to align with Movie detail source reset semantics.
- Disabled `重置为未识别` when the Episode already belongs to an unidentified Season, with a matching service-side guard for non-UI calls.
- Active media probing no longer disables `重置为未识别`; probing only disables probe actions.
- Disabled scan-time media-probe enqueue for WebDAV and local scans so large scan runs do not occupy the probe queue ahead of current detail pages.
- Added a confirmation step that states the source is split out from the current Episode, real local / WebDAV files are not deleted, and Episode metadata / watched / progress are not cleared.
- Added a scoped TV reset method that verifies `mediaFileId` belongs to the current Episode, rejects mismatches, and clears `MediaFile.EpisodeId` while keeping the row active.
- Kept Episode, Season, metadata, watched state, Episode progress, watch history records, subtitle bindings, probe fields, and real files intact when a source is reset.
- Episode detail reloads after reset, so remaining sources, derived default source, source count, and play-button enabled state are recalculated.
- Resetting the last source keeps the Episode detail page and Season detail Episode row available; the detail page shows `暂无播放源`.
- The reset `MediaFile` can be picked up by Other / unidentified item handling because it is no longer bound to an Episode or Movie and is not marked deleted.
- Data refresh notifications cover library, playback-history views, and collection surfaces without touching TV Watch Insights or recommendations.
- Reset diagnostics are sanitized and avoid full local paths and full WebDAV URLs.

Not done:

- No physical local-file delete, WebDAV remote delete, `MediaFile` delete, Episode delete, Season delete, Episode-level remove-from-library command, persistent Episode default source, set-default action, watched / unwatched write action, real correction flow, AI correction, TMDB candidate search, scan-rule change, scan-time probe enqueue, TV Watch Insights input, TV recommendation input, migration, database update, commit, or push was added.

Manual acceptance matrix:

1. Recognized Episode multi-source detail can reset one source and remove it from the list.
2. Failed unidentified Episode uses the same reset path.
3. The confirmation copy states real local / WebDAV files are not deleted.
4. A source whose `mediaFileId` does not belong to the current Episode is rejected.
5. Remaining sources can still play after one source is reset.
6. Resetting the current derived default source recalculates the default from remaining active sources.
7. Resetting the last source leaves the Episode visible and shows `暂无播放源`.
8. Season detail still lists the Episode after the last source is reset.
9. Watch history / progress references to the reset source do not crash the detail page.
10. Manual probe, source display, playback, and correction placeholder behavior remain in place.
11. Logs remain sanitized and do not contain full local paths or full WebDAV URLs.
12. No new Phase 4.12d migration is created.

Known Issues:

- Blocker: none.
- Deferred: Episode-level remove-from-library, persistent default source, and watched / unwatched buttons remain future work.
- Noise: reset source rows remain active `MediaFile` records and are expected to reappear through Other / unidentified item handling rather than Episode source lists.

## Phase 4.12d Follow-up - Probe status wording and diagnostics hardening

Completed:

- Updated shared source-row probe status text so a completed ffprobe run with no readable duration / resolution / codec / bitrate is shown as `已探测（未读取到媒体信息）` instead of a normal metadata update.
- Applied the status wording to Movie detail sources, Episode detail sources, and shared playback source read models.
- Added graceful-shutdown diagnostics for abandoned queued probe work and canceled active probe work.
- Added background worker exception diagnostics so a swallowed probe worker exception still leaves a terminal log record.
- Killed the ffprobe process when the linked cancellation token is canceled, reducing the chance of a probe process continuing after shutdown.
- Hardened probe lifecycle and ignored-file sample diagnostics by logging file extension plus a stable hash fingerprint instead of raw sample file names.

Not done:

- No probe retry policy, probe queue scheduling change, manual probe behavior change, lazy detail trigger change, scan-time probe re-enable, source reset semantic change, migration, database update, commit, or push was added.

Manual acceptance matrix:

1. Build succeeds with 0 warnings and 0 errors.
2. No new migration is created.
3. `Success` probe rows with no technical fields show `已探测（未读取到媒体信息）`.
4. Normal successful probe rows still show completed media-info wording.
5. Probe pending / failed / unavailable / skipped wording remains explicit.
6. Detail lazy probe and manual probe entry points remain unchanged.
7. Graceful cancellation / abandoned queue paths emit diagnostics without full paths or URLs.
8. Probe and ignored-file sample logs no longer contain raw sample file names.
9. Episode source reset to unidentified remains independent from active probing.

Known Issues:

- Blocker: none.
- Deferred: deeper ffprobe output analysis for files that complete successfully but expose no readable stream metadata.
- Noise: historical log lines still contain older raw sample names from before this hardening.

## Phase 4.12e - Episode persistent default source

Completed:

- Added nullable `TvEpisode.DefaultMediaFileId` with a `SetNull` relationship to `MediaFiles`, mirroring Movie default-source storage at Episode scope.
- Added the `AddTvEpisodeDefaultMediaFile` migration. The migration adds the column, a unique index, and the foreign key; database update was not executed.
- Added an Episode collection command to set the default source after verifying the `MediaFileId` belongs to the current Episode, is an active video source, and is not deleted.
- Episode detail source rows now expose `设为默认`. The current default row is shown as `当前默认` and keeps the existing default badge.
- Episode detail reloads after setting the default source, so the source list, badge, and top play target refresh immediately.
- Episode detail query now reads the persisted Episode default and marks the effective default source.
- Episode playback sessions now separate effective default from explicit selected source: `OpenEpisodeAsync(episodeId)` uses the effective default, while source-row playback keeps the selected `MediaFileId`.
- Season detail Episode play continues to call `OpenEpisodeAsync(episodeId)`, so it inherits the same persisted-default priority through `PlaybackSourceService`.
- Resetting a source to unidentified clears `TvEpisode.DefaultMediaFileId` when the reset source was the persisted default. The reset still preserves real files, metadata, watched/progress state, watch history, subtitle bindings, and probe fields.
- The shared Episode default-source rule now prefers persisted active usable default, then preferred source for detail resolution, then accessible local, recent / in-progress, WebDAV, and stable first fallback.

Not done:

- No watched / unwatched button, real correction flow, AI correction, TMDB candidate search, batch correction, manual Season aggregation, physical source delete, online subtitles, scan-rule change, TV Watch Insights input, TV recommendation input, database update, commit, or push was added.

Manual acceptance matrix:

1. Episode detail multi-source rows can set one source as default.
2. The selected row shows the default marker and `当前默认`.
3. Episode detail top play uses the persisted default source.
4. Season detail Episode play uses the same persisted default through `OpenEpisodeAsync(episodeId)`.
5. `PlaybackSourceService.GetEpisodePlaybackSessionAsync(episodeId)` returns the same effective default.
6. Source-row playback with an explicit `MediaFileId` still plays the selected source and is not overridden by the persisted default.
7. Resetting the persisted default source to unidentified clears the stored default and falls back to the remaining derived source.
8. Missing / inactive / inaccessible persisted default sources are ignored during automatic selection.
9. Failed unidentified Episodes use the same set-default path as recognized Episodes.
10. No-source Episodes still show no sources and keep playback disabled.
11. Movie default-source behavior is not changed.
12. The migration is present and no database update is executed.

Known Issues:

- Blocker: none.
- Deferred: cross-page real-time refresh for an already-open Season detail page remains deferred; reloading the page resolves the new default for playback.
- Noise: the persisted default can remain stored for an inaccessible local file; playback selection ignores it and falls back until the file becomes accessible again.

## Phase 4.12e Follow-up - Unidentified source reset disabled state

Completed:

- Disabled `重置为未识别` on Episode detail with an explicit page-level enabled binding when the loaded Episode is already in an unidentified Season.
- Disabled `重置为未识别` on Movie detail for failed / unidentified Movie placeholders, including single-source Other / orphan carriers.
- Added a Movie service-side guard so already-unidentified Movie placeholders cannot be reset into another unidentified placeholder through non-UI calls.
- Kept `设为默认`, playback, manual probe, Episode source reset for recognized Episodes, and migration behavior unchanged.

Known Issues:

- Blocker: none.
- Deferred: none for this follow-up.
- Noise: historical logs may still contain earlier reset attempts from before the disabled-state fix.

## Phase 4.12e-fix - Rescan reattach after reset to unidentified

Completed:

- Added a rescan reattach service that runs after TV / Movie identification and before placeholder / orphan grouping.
- Episode reattach now attempts to bind active unbound video sources, or failed Movie placeholder sources, back to an existing matched / manually confirmed Season Episode when the filename parses to a safe single Episode and an existing same-source same-directory or sibling-directory Season context is available.
- Reattached files are appended as Episode playback sources. The flow does not overwrite Episode / Season metadata, does not clear watched / progress state, and does not clear watch history, subtitles, or probe fields.
- Local scanning now records unchanged active unbound videos as restricted reattach candidates, so reset-only files can be considered without sending every unchanged file through full identification again.
- WebDAV scanning applies the same restricted unchanged-unbound candidate rule.
- Added scan classification diagnostics for new, deleted-reappeared, changed, unchanged-unbound, post-process, reattach, and placeholder-fallback counts.
- Added reattach diagnostics for candidate, succeeded, skipped, summary, and error cases. Logs use IDs, counts, source kind, protocol, parsed season / episode, and sanitized directory displays.
- Movie reattach is intentionally conservative in this fix: safe candidates are logged as deferred instead of being automatically attached.

Not done:

- No delete-record history retention, tombstone, ignore-file feature, migration, database update, AI prompt change, TMDB gate change, real correction flow, SP / OAD / OVA / special mapping, Watch Insights input, recommendation input, commit, or push was added.

Manual acceptance matrix:

1. Local reset-only unchanged unbound videos are counted as `unchangedUnboundVideoCount` and passed to restricted reattach.
2. WebDAV reset-only unchanged unbound videos follow the same restricted candidate path.
3. New, changed, and deleted-reappeared videos still enter `postProcessVideoMediaFileIds`.
4. Existing TV first pass, AI-on-uncertain, TV retry, and Movie fallback still run before placeholder grouping.
5. Safe Episode reattach happens before placeholder / orphan grouping.
6. Reattach appends the file as an Episode source and preserves Episode / Season metadata.
7. Failed Movie placeholders can be moved into a safe matched Episode when the Episode reattach conditions pass.
8. Ambiguous, multi-episode, part-hint, SP / OAD / OVA / special, or contextless candidates are skipped and fall back to the existing flow.
9. Movie auto-reattach is not performed in this fix; candidates are logged for a later product decision.
10. Scan diagnostics expose classification and reattach counts without raw paths or full WebDAV URLs.
11. Placeholder grouping remains the fallback for sources that cannot be safely reattached.
12. No new migration is created by this fix.

Known Issues:

- Blocker: none.
- Deferred: automatic Movie reattach remains deferred because title / year matching without a prior-association tombstone can attach the wrong file in ambiguous folders.
- Noise: historical scans before this fix do not contain the new reattach classification counters.

## Phase 4.12e UI Semantics Follow-up - Split source labels

Completed:

- Episode detail source-row reset action is now labeled `从当前集拆分`; the underlying operation still detaches the selected source from the current Episode without deleting real local / WebDAV files or clearing Episode / Season metadata, watched state, progress, history, subtitles, or probe fields.
- Recognized Episodes can still split any active source, including the last source, leaving the Episode visible with no sources.
- Failed / unidentified Episodes now allow `从当前集拆分` whenever the Episode has an active source. A single-source unidentified Episode can be detached back to Other, while the service layer still protects unrelated / deleted / non-video sources.
- Movie detail reset UI is now labeled `从当前电影拆分`; failed / unidentified Movie placeholders and orphan carriers remain disabled.

Not done:

- No service behavior beyond the unidentified multi-source safety gate, migration, database update, real correction flow, physical file delete, Watch Insights input, recommendation input, commit, or push was added.

Known Issues:

- Blocker: none.
- Deferred: none for this wording / enabled-state follow-up.
- Noise: historical docs and logs may still use the earlier `重置为未识别` wording.

## Phase 4.12f - Episode watched / unwatched controls

Completed:

- Added an Episode detail watched toggle that shows `标记已看`, `取消已看`, or `更新中...` while the command is running.
- Reused `ITvSeasonCollectionService.SetEpisodeWatchedAsync` instead of adding a second watched-state implementation.
- Recognized Episodes, failed unidentified Episodes, and no-source Episodes can be marked watched / unwatched from Episode detail.
- After a toggle, Episode detail reloads immediately and playback / collection refresh notifications are emitted so Season detail and library projections use the existing aggregate progress rules on reload.
- The operation updates `TvEpisode.IsWatched` and existing Season state summaries only; it does not create `WatchHistory`, does not alter playback sources, and does not touch Movie watched state.

Not done:

- No TV Watch Insights input, Watch Profile input, recommendation fingerprint input, real correction flow, batch correction, manual Season aggregation, scan-rule change, default-source change, source reset change, migration, database update, commit, or push was added.

Manual acceptance matrix:

1. Recognized Episode detail can mark an unwatched Episode as watched.
2. Recognized Episode detail can cancel watched state.
3. Failed unidentified Episode detail uses the same watched toggle.
4. No-source Episode detail keeps playback disabled but still allows watched / unwatched.
5. The watched button disables and shows a busy label while the operation is running.
6. Episode detail reloads its watched text and progress summary after the operation.
7. Season detail shows the updated Episode state after reload / return.
8. Season watched count and progress summary follow the existing aggregate rules.
9. Source list, playback, default source, manual probe, and split-source behavior remain unchanged.
10. Movie watched / Movie detail behavior remains unchanged.
11. TV still does not feed Watch Insights, Watch Profile, AI recommendations, or recommendation fingerprints.
12. No new migration is created by this phase.

Known Issues:

- Blocker: none.
- Deferred: cross-page real-time refresh for an already-open Season detail page remains best-effort; returning to or refreshing the page reloads the state.
- Noise: historical docs and logs may still describe Episode detail watched / unwatched as deferred.

## Phase 4.12g - Episode detail regression and polish

Completed:

- Regressed Episode detail source-list, playback, persistent default source, split-source, lazy / manual probe, watched / unwatched, no-source, unidentified Episode, and Movie detail boundary behavior.
- Kept the source split wording as `从当前集拆分` / `从当前电影拆分`; this replaces the earlier `重置为未识别` UI wording while keeping the safe detach semantics.
- Episode detail now exposes the same visual disabled state for `从当前集拆分` while an Episode player is opening or open, matching the service / command guard. Media probing still disables only probe actions and does not disable source split.
- Movie split diagnostics now report retained history / progress instead of the old misleading resume-cleared wording. The underlying Movie split behavior still does not delete real files.
- Confirmed Episode detail watched / unwatched uses the existing TV collection service and does not create WatchHistory or feed TV into Watch Insights, Watch Profile, AI recommendations, or recommendation fingerprints.

Not done:

- No real correction entry, AI correction, TMDB candidate search, batch correction, manual Season aggregation, online subtitles, new scan strategy, TV Watch Insights input, TV recommendation input, database update, commit, or push was added.

Manual acceptance matrix:

1. Recognized Episode detail loads from Season detail and keeps metadata fallback safe.
2. Failed unidentified Episode detail uses the same source list, playback, probe, default-source, split-source, and watched toggle surface.
3. No-source Episodes stay visible, show `暂无播放源`, keep playback disabled, and still allow watched / unwatched.
4. Episode source rows keep safe location display and do not expose full local paths or full WebDAV URLs.
5. Top play, source-row play, Season detail play, and `OpenEpisodeAsync(episodeId)` use the shared Episode source-selection path.
6. Persistent default sources remain preferred and fall back when inactive, unbound, deleted, or inaccessible.
7. `从当前集拆分` preserves real files, Episode / Season metadata, watched / progress state, watch history, subtitles, and probe fields.
8. Manual probe and detail lazy probe remain scoped to current detail sources and refresh source rows after status changes.
9. Movie detail split, default-source, source probe, and watched behavior remain within existing Movie semantics.
10. TV still does not enter Watch Insights, Watch Profile, AI recommendations, or recommendation fingerprints.

Known Issues:

- Blocker: none.
- Deferred: cross-page real-time refresh for Season detail after Episode detail changes remains best-effort; reload / return shows the updated state.
- Noise: earlier historical sections may still use `重置为未识别` wording for the same split-source operation.

## Phase 4.12h - Episode detail docs and stage closeout

Scope:

- Documentation closeout only. No business logic, schema, scan strategy, correction workflow, Watch Insights input, recommendation input, database update, commit, or push was added.
- Confirmed the current source split labels are `从当前集拆分` in Episode detail and `从当前电影拆分` in Movie detail.
- Confirmed the Episode persistent default source migration `20260519201559_AddTvEpisodeDefaultMediaFile` is present and tracked, while the current migrations diff is empty.

Completed summary:

- Episode detail supports recognized Episodes, failed unidentified Episodes, no-source Episodes, source list, top play, source-row play, persistent default source, `设为默认`, `从当前集拆分`, detail lazy probe, manual probe, and watched / unwatched.
- Season detail Episode play uses the same `OpenEpisodeAsync(episodeId)` source-selection path as Episode detail top play.
- Source list supports local-only, WebDAV-only, and local + WebDAV mixed sources while keeping location display sanitized.
- Orphan / Other unknown files continue to use the failed Movie detail carrier and inherit Movie source probe / split boundaries where applicable.
- Source split is a safe detach operation. It removes the current Movie / Episode binding and returns the source to unidentified / Other carrying without deleting real files or clearing WatchHistory, progress, probe fields, subtitles, or metadata.
- Failed unidentified Episodes allow `从当前集拆分` whenever they still have at least one active source. This includes single-source unidentified Episodes, so a wrongly auto-grouped source can be detached from the current Episode and returned to Other without deleting the real file.
- Manual watched / unwatched updates Episode state and existing Season aggregates only. It does not create WatchHistory and does not feed TV into Watch Insights, Watch Profile, AI recommendations, or recommendation fingerprints.

Final acceptance matrix:

1. Recognized Episode detail is available from Season detail.
2. Failed unidentified Episode detail uses the same detail shell and source-management surface.
3. Orphan / Other unknown files open through failed Movie detail carrying.
4. Episode source list shows active sources with sanitized display fields.
5. Episode top play uses the effective default source.
6. Episode source-row play uses the selected source.
7. Season detail play uses the shared Episode playback path.
8. WebDAV + local mixed sources stay distinct and playable through their own rows.
9. Full local paths, full WebDAV URLs, credentials, and tokens are not shown in UI or diagnostic docs.
10. Persistent default source and `设为默认` are supported by `TvEpisode.DefaultMediaFileId`.
11. Source split labels are `从当前电影拆分` / `从当前集拆分`.
12. Failed unidentified Episode multi-source split is allowed.
13. Failed unidentified Episode single-source split is allowed and leaves the Episode / Season metadata in place after the last source is detached.
14. Orphan unknown carrier split is disabled.
15. No-source Episode keeps the detail page and disables playback.
16. Detail lazy probe and manual probe remain scoped to current detail sources.
17. Episode watched / unwatched works for recognized, failed unidentified, and no-source Episodes.
18. Movie detail playback, source probe, default source, watched state, and split semantics are not regressed.
19. TV remains excluded from Watch Insights, Watch Profile, AI recommendations, and recommendation fingerprints.
20. Build verification passes with 0 warnings and 0 errors.
21. Migration state is explicit: the Episode default-source migration exists; current migrations diff is empty.
22. Database update was not executed.

Known Issues:

- Blocker: none.
- Deferred: real unified correction entry, AI correction, TMDB candidate search, batch correction, manual Season aggregation, SP / OAD / OVA / special mapping, theatrical / course / collection-specific handling, stronger rescan / reattach hardening, richer probe task-center UI, online subtitles, final UI polish, and any TV Watch Insights / AI recommendation integration.
- Noise: WebDAV probing remains best-effort and can fail because of network, permissions, server behavior, or ffprobe limits; historical docs may still contain earlier `重置为未识别` wording for the split-source operation.

Recommendation:

- Phase 4.12 is ready to close. Next recommended phase is Phase 4.13 for unified correction entry, batch correction, and manual grouping / correction workflows.

## Phase 4.12-post Emergency Safety Fix - Unknown grouping / append stability

Completed:

- Kept the recognized Episode reattach same-directory rule. Sibling folders are still blocked from automatic recognized Episode multi-source append.
- Tightened unknown Series reuse to a strict derived grouping key. Existing no-TMDB Series with mixed derived Series keys is treated as ambiguous and is no longer reused automatically.
- Tightened unknown Season reuse / append to a strict derived Season grouping key. Existing unknown Seasons with mixed derived Season keys are treated as ambiguous and are no longer automatic append targets.
- Removed the old bridge behavior where one compatible source inside an unknown Series or Season could make the whole container compatible with later unrelated directories.
- Preserved existing unknown Series / Season names on reuse. Names are only assigned for new containers or when the existing name is blank.
- Added a conservative skip for special / non-regular TV directories in automatic unknown append and grouped placeholder persistence, including specials, OVA / OAD, theatrical / movie, side-story, course, collection, recap, and remake style folders.
- Kept same-directory duplicate-copy multi-source behavior. Files such as numeric copies or SxxExx copies can still append to the same unknown Episode when the strict Season key matches.
- Automatic append still creates only the actual target Episode when needed and does not create empty intermediate Episodes.

Not done:

- No historical unknown container merge, historical wrong-binding cleanup, manual grouping, unified correction entry, batch correction, AI prompt change, TMDB gate change, database update, migration, commit, or push was added.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors during implementation.
- Current migrations diff remained empty during implementation.

Known Issues:

- Blocker: none in the emergency code path after build verification.
- Deferred: libraries already polluted by the previous aggressive unknown append / grouping logic still require clear-library rescan, manual cleanup, or a later repair tool. This phase prevents future automatic bridge pollution but does not rewrite existing data.
- Noise: the conservative special-directory skip can leave more items in Other / placeholder review until Phase 4.13 manual correction exists.

## Phase 4.12-post Episode split follow-up - Single-source unidentified Episodes

Completed:

- Enabled `从当前集拆分` for single-source failed / unidentified Episodes. This gives users a safe escape hatch when automatic unknown Season grouping puts a file into the wrong Episode.
- Removed the service-side rejection for the last active source of a failed unidentified Episode. The operation still detaches only the selected `MediaFile` binding and does not delete local or WebDAV files.
- Kept the Episode / Season rows, watched state, playback progress, probe fields, subtitles, and metadata intact when the last source is detached.
- Added `lastSourceSplit` to the split diagnostic so logs distinguish multi-source detach from last-source detach without exposing full paths.
- Kept Movie failed placeholders and orphan unknown carriers on the Movie detail boundary unchanged.

Not done:

- No automatic historical ungrouping, manual correction entry, batch correction, data cleanup, database update, migration, commit, or push was added.

Verification:

- `dotnet build MediaLibrary.sln` should be run after this follow-up and must remain 0 warnings / 0 errors.
- Current migrations diff should remain empty.

Known Issues:

- Blocker: none expected in this narrow split path.
- Deferred: the real manual correction / regrouping entry remains Phase 4.13 work.
- Noise: detaching the only source leaves an empty unidentified Episode shell until a refresh, rescan, or later manual cleanup path handles it.

## Phase 4.13a - Single source correction foundation

Completed:

- Added a single-source correction apply service for Movie and TV Episode targets.
- Movie detail source rows now expose a unified `修正信息` entry. The flow selects one source, jumps to the correction tab, searches TMDB, and applies the selected candidate directly.
- Episode detail source rows and the top `修正信息` action now use the same single-source correction flow. Source-row correction scrolls the correction panel into view.
- The correction panel shows only the active target type fields. Movie target shows movie title / year inputs; TV Episode target shows series / season / episode inputs.
- Movie targets bind the selected `MediaFile` to the target Movie and clear `EpisodeId`.
- TV Episode targets bind the selected `MediaFile` to the target Episode and clear `MovieId`.
- Existing target Movie / Episode rows accept the corrected source as an additional playback source.
- The corrected source becomes the target Movie / Episode default source, regardless of whether it is local or WebDAV.
- If the corrected source was the previous Movie / Episode default source, the previous container recalculates its default source with the local-first fallback strategy.
- New TV Episode target binding reuses existing TV metadata / hydration logic and sets the target Episode default source to the corrected source.
- Moving a source away from an Episode now reconciles that Episode's default source if the moved source was default.
- Correction apply keeps the existing transactional manual Movie / TV binding paths and logs `correction-apply-started`, `correction-apply-succeeded`, and `correction-apply-failed` without full paths or credentials.
- Follow-up: candidate-click correction now yields the UI thread, runs the apply path off the WPF dispatcher, and uses a 45-second timeout so a slow TMDB request fails visibly instead of making the app appear frozen.
- Follow-up: TV Episode correction commits the selected source binding first and queues full Series hydration in the background. The selected Episode and source list can refresh without waiting for full-series metadata completion.
- Follow-up: Movie single-source correction skips non-critical OMDb rating fetch during the transactional apply path and logs detail / DB phases for future diagnosis.
- Follow-up: Episode detail correction now uses tabs for `播放源` and `识别修正`; selecting a source switches to the correction tab, and users can switch back to the source list without leaving the page.
- Follow-up: the correction panel is shown only after the target type has been reset to Movie, avoiding the first-entry flicker where TV fields appeared briefly before Movie fields.
- Follow-up: Episode detail source correction now defaults to `修正为电视剧集`, including failed / unidentified Episodes. Movie detail, including orphan carriers, resets to `修正为电影`.

Not done:

- No join-existing unknown Season target, manual Season aggregation, grouped unknown Season to recognized Season correction, batch AI correction, historical wrong-binding cleanup, ignore / blacklist, database update, migration, commit, or push was added.
- TV still does not enter Watch Insights, Watch Profile, AI recommendations, or recommendation fingerprints.
- Existing 4.12-post automatic scan safety gates remain unchanged.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors during implementation.
- Current migrations diff remained empty during implementation.

Known Issues:

- Blocker: none after build verification.
- Deferred: correction to existing unknown Season, manual grouping, batch AI correction, grouped Season correction, richer correction diff, and data cleanup remain Phase 4.13 follow-up work.
- Noise: candidate-click direct apply follows the latest product adjustment and no longer has a separate preview panel.

## Phase 4.13a-fix - Target-kind constrained single-source AI correction

Completed:

- Movie detail and Episode detail now share the same single-source AI assist semantics.
- The correction AI path is constrained by the currently selected target type. `Movie` target calls only the Movie search suggestion path and renders only Movie candidates.
- `TV Episode` target calls only the TV Series search suggestion path and renders only TV candidates plus the existing Season / Episode inputs.
- Episode detail now has the same AI assist entry as Movie detail.
- AI assist only fills the search fields and triggers the existing target-specific candidate search. Candidate click still uses the 4.13a single-source correction apply path.
- AI assist diagnostics record start / succeeded / failed, target kind, media file id, status, and candidate count without full paths or credentials.
- Corrected-source default-source behavior remains unchanged: the corrected source becomes the target Movie / Episode default, and the old container recalculates default source with the local-first fallback if needed.

Not done:

- No batch AI correction, AI target-type auto judgment, manual Season aggregation, join-existing unknown Season target, scan rule change, database update, migration, commit, or push was added.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors during implementation.
- Current migrations diff remained empty during implementation.

Known Issues:

- Blocker: none after build verification.
- Deferred: batch AI correction, manual grouping, unknown Season target selection, grouped Season correction, and correction history / richer preview remain later Phase 4.13 work.
- Noise: TV AI assist currently suggests the Series search query only; Season / Episode numbers still come from the current inputs and remain user-editable before candidate click.

### Follow-up: correction UI stability and TV AI episode hints

Completed:

- Detail page load now resets to the playback-source tab instead of retaining the previous correction tab.
- Starting a new source correction hides the correction panel before resetting target kind, then reopens it after the selected source and default target are ready. This prevents the correction UI from briefly showing the previous Movie / TV target fields.
- The correction target ComboBox ignores mouse-wheel changes while the dropdown is closed, so scrolling the correction form does not accidentally switch Movie / TV target kind.
- TV Episode AI assist now asks for Series title plus season / episode numbers, parses those fields, and fills the Season / Episode inputs automatically when available.
- Local fallback for TV AI also derives Season / Episode from the source file name when the parser can infer a single episode.
- Same-detail refreshes from media probe completion preserve the current correction tab and selected correction source. The playback-source tab reset now applies only when navigating into a different Movie / Episode detail.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Current migrations diff remained empty.
