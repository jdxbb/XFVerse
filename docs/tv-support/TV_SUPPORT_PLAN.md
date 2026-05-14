# TV Support Plan

## Phase 4.2 Goal

Phase 4.2 adds TV metadata service capabilities only. It does not connect TV data to scanning, UI, playback, discovery pages, recommendations, Watch Insights, user profiles, or fingerprints.

## TMDB TV Endpoints

The TV service layer adds methods for:

- `search/tv`
- `tv/{series_id}`
- `tv/{series_id}/season/{season_number}`
- `tv/{series_id}/external_ids`
- `tv/popular`
- `tv/top_rated`
- `trending/tv/{day|week}`

TV search and TV ranking results are Series-level unless TMDB explicitly returns a different type. Series results must not be expanded into Season results or converted into movie discovery models.

## Read Model Separation

TV metadata uses dedicated read models:

- TV series search page and item
- TV series detail
- TV season summary
- TV season detail
- TV episode metadata item
- TV series external ids

These models are separate from movie discovery models and must not be converted into `AiRecommendationItem`.

## Rating Policy

- TMDB TV series may expose `vote_average` and `vote_count`.
- TMDB season and episode rating fields are parsed only when present in the response.
- Episode ratings must not be treated as Season ratings.
- OMDb is the API source; IMDb is the rating source.
- UI copy must use "IMDb rating" or "IMDb series rating", not "OMDb rating".
- If only Series-level IMDb rating is stable, later UI should label it as "IMDb series rating".
- If Season-level IMDb rating cannot be verified safely, do not show a Season IMDb rating.
- `TvSeasonRatingSources` remains deferred and is not part of Phase 4.2.

## Out Of Scope

- Scanning and file name parsing
- `SeriesOverviewPage`
- `TvSeasonDetailPage`
- Episode playback
- Previous / next episode controls
- Auto-next
- Library Series cards
- Favorites Season integration
- TV discovery UI
- AI recommendations
- Watch Insights
- Database migrations

## Phase 4.3 Goal

Phase 4.3 adds service-layer TV season scanning and correction. WebDAV and Local scans can split clear TV season candidates before the movie identification stage, create or update `TvSeries`, `TvSeason`, `TvEpisode`, and attach each episode video as a normal `MediaFile` with `EpisodeId`.

Phase 4.3 still does not add TV pages, playback, discovery UI, recommendations, Watch Insights, user profiles, or database migrations.

## TV Season Scan Flow

1. Enumerate video files through the existing WebDAV or Local scan path.
2. Upsert `MediaFile` rows through the existing scan logic.
3. Before movie identification, group changed video files by parent folder.
4. Treat a folder as a TV season candidate only when it has clear episode-like files or already-known episode sources.
5. Prefer the folder name as the Series / Season search candidate.
6. If the folder name is generic, use episode file name candidates or common prefixes.
7. Search TMDB TV Series metadata.
8. Fetch TV Series detail and Season detail when a Series candidate is usable.
9. Create or update `TvSeries`, `TvSeason`, and in-library `TvEpisode` rows.
10. Attach episode video files by setting `MediaFile.EpisodeId` and clearing `MovieId`.
11. Pass remaining non-TV files to the existing movie identification flow.

TV scan does not automatically convert existing Movie media files into Episode media files. Cross-type source movement is handled only by the correction service.

## Episode File Name Parsing

Supported episode patterns include:

- `S01E01`
- `S1E1`
- `01x01`
- `第1季第1集`
- `第01季 第01集`
- `Season 1 Episode 1`
- Season-context-only forms such as `第01集`, `E01`, `EP01`, and `Episode 01`

When no season number is present in a valid season context, Season 1 is used.

Unsupported multi-episode files include:

- `S01E01-E02`
- `S01E01E02`
- `第1-2集`

These files must not crash scanning and must not be expanded into multiple episodes during Phase 4.3.

## Unidentified TV Seasons

Unidentified TV seasons are represented by `TvSeason` itself with an identification status, not by a shared unknown-media table. The UI copy for later phases should use `未识别电视剧季`.

Movie unidentified handling remains separate and continues to use the existing movie mechanism.

## Correction Policy

Correction remains split into two service-layer directions:

- Correct to movie: an Episode media file can be moved to a Movie by clearing `EpisodeId` and setting `MovieId`.
- Correct to TV: a Movie media file can be moved to a `TvEpisode` by clearing `MovieId` and setting `EpisodeId`.

Correction moves only the selected media source. It does not migrate watched state, favorites, want-to-watch, not-interested state, WatchHistory, or recommendations. It never deletes local or WebDAV files.

## Phase 4.3 Out Of Scope

- `SeriesOverviewPage`
- `TvSeasonDetailPage`
- Episode playback
- Previous / next episode controls
- Auto-next
- Library Series cards
- Batch mode Season expansion
- Home continue watching Episode UI
- Watch history Episode UI
- Favorites Season integration
- TV discovery UI
- AI recommendations
- Watch Insights
- Database migrations
- `EpisodeMediaFile`
- Unified Unknown / UnidentifiedMedia table
- `TvSeasonRatingSources`

## Phase 4.4 Goal

Phase 4.4 adds hidden TV detail routes and visible pages for data already created by TV scanning:

- `SeriesOverviewPage` for Series packaging and Season list display.
- `TvSeasonDetailPage` for Season metadata, aggregate progress, source summary, and Episode list display.

Series remains a packaging layer and does not expose favorite, want-to-watch, not-interested, or watched actions. Season remains the core management unit, but Phase 4.4 does not add Season collection actions yet. Episode remains the playback unit, but Phase 4.4 only shows play placeholders; real Episode playback is deferred to Phase 4.5.

## Phase 4.4 Navigation

The app adds hidden routes:

- `SeriesOverview`
- `TvSeasonDetail`

These routes do not appear in the fixed main navigation. They are activated through navigation state IDs and will get natural media-library entry points in Phase 4.6.

## Phase 4.4 Query Rules

- Series poster fallback uses Series poster first, then the latest Season poster.
- Season total episode count uses TMDB total first and falls back to known `TvEpisode` count.
- Watched progress counts `TvEpisode.IsWatched`.
- In-library count requires at least one active Episode `MediaFile`.
- Source summary is aggregated from active Episode sources as local, cloud, local + cloud, or no source.
- Unidentified seasons display `未识别电视剧季` and do not pretend to have TMDB metadata.

## Phase 4.4 Out Of Scope

- Episode playback
- Previous / next episode controls
- Auto-next
- Home continue watching Episode UI
- Watch history Episode UI
- Full media-library Series cards
- Batch mode Season expansion
- Favorites Season integration
- TV discovery UI
- AI recommendations
- Watch Insights
- Database migrations

## Phase 4.5 Goal

Phase 4.5 connects `TvEpisode` to the existing player. Episode playback reuses the same player window, playback engine, WebDAV handling, local-file handling, subtitles, video cache, and `MediaFile` source model as movie playback.

Phase 4.5 still does not add media-library Series cards, home continue-watching Episode UI, watch-history Episode UI, Season favorites, TV discovery UI, recommendations, Watch Insights, user profiles, fingerprints, or database migrations.

## Phase 4.5 Playback Rules

- Movie playback keeps the existing `OpenAsync(movieId, mediaFileId)` path.
- Episode playback uses a dedicated Episode entry point and never pretends an Episode is a Movie.
- If a media file is explicitly selected, it is used only when it belongs to the target Episode and is active.
- Without an explicit source, active Local sources are selected before active WebDAV sources.
- Local Episode sources play directly from the local file and do not enter the WebDAV video cache.
- WebDAV Episode sources reuse the existing WebDAV playback and video-cache path.
- Subtitle binding remains `MediaFile` based.

## Phase 4.5 Previous / Next And Auto-Next

- Movie sessions disable previous / next Episode controls.
- Episode sessions calculate previous / next only inside the same `TvSeason`.
- Navigation uses adjacent `EpisodeNumber - 1` and `EpisodeNumber + 1`.
- The first Episode disables previous.
- The last Episode disables next.
- Auto-next runs only after the current Episode progress and history have been saved.
- Auto-next never crosses Season boundaries.
- If the next Episode is missing or has no active source, playback stops and shows a friendly notice.

## Phase 4.5 Watch History And Episode Summary

- Episode playback writes `WatchHistory.EpisodeId` with `MovieId` left empty.
- Movie playback continues to write `WatchHistory.MovieId`.
- `TvEpisode` stores the lightweight playback summary:
  - `IsWatched`
  - `LastPlayedAt`
  - `LastPlayPositionSeconds`
  - `DurationWatchedSeconds`
- Season progress remains derived from Episode rows.
- Watch Insights, AI recommendation inputs, profile inputs, personality inputs, and recommendation fingerprints remain Movie-only.

## Phase 4.5 Out Of Scope

- Media-library Series cards
- Batch mode Season expansion
- Season collection and favorites integration
- Home continue-watching Episode UI
- Watch-history Episode UI
- TV discovery UI
- Cross-season auto-next
- Online subtitle search
- Database migrations
- `EpisodeMediaFile`
- Unified Unknown / UnidentifiedMedia table
- `TvSeasonRatingSources`

## Phase 4.6 Goal

Phase 4.6 exposes TV Seasons in the main user experience while keeping Series, Season, and Episode responsibilities separate.

- Normal media-library mode shows Series aggregate cards.
- Batch media-library mode expands Series into Season cards.
- Season is the stateful collection and batch-operation unit.
- Episode appears in continue watching and watch history as the playback unit.
- Favorites show Season cards only, not Series or Episode cards.

Phase 4.6 still does not add TV discovery search / rankings, AI recommendations, Watch Insights TV statistics, user profile inputs, personality inputs, recommendation fingerprints, database migrations, `EpisodeMediaFile`, Unknown tables, or `TvSeasonRatingSources`.

## Phase 4.6 Library Rules

- Movie items remain Movie items.
- Series items are not disguised as Movies and navigate to `SeriesOverviewPage`.
- Episode items do not appear as first-level library cards.
- Entering batch mode reloads TV cards as Season items.
- Batch operations act on Movie or Season items. Phase 4.7 supports mixed Movie + Season selections for the four unified actions: watched, unwatched, remove from library, and delete record.
- Content type filtering supports all, Movie, and TV.
- Source filtering for Series / Season aggregates active Episode `MediaFile` sources.

## Phase 4.6 Season State Rules

- Season favorite, want-to-watch, and not-interested state is stored in `UserTvSeasonCollectionItem`.
- State changes are recorded in `UserTvSeasonStateChangeHistory`.
- Season favorite, want-to-watch, and not-interested are single-item actions on Season detail or favorites cards.
- Batch operations do not include favorite, want-to-watch, or not-interested.
- The final batch toolbar scope is watched, unwatched, remove from library, and delete record.
- Batch watched / unwatched updates all TMDB Season Episodes once metadata has been populated, including Episodes without playable sources.
- Batch watched / unwatched does not create `WatchHistory`.
- Season progress uses watched Episode count over TMDB total Episode count.

## Phase 4.6 Home, History And Favorites

- Home continue watching can show Episode entries with Series / Season / Episode text and resume playback through `OpenEpisodeAsync`.
- Watch history includes Episode entries with `WatchHistory.EpisodeId` and navigates to `TvSeasonDetailPage`.
- Favorites merge Movie cards with Season cards in favorite and want-to-watch tabs.
- Favorites do not display Series cards or Episode cards.
- TV remains excluded from AI, Watch Insights, profiles, personalities, and recommendation fingerprints.

## Phase 4.6 Bugfix / Validation Fixes

Phase 4.6 validation fixes keep the project inside the library / home / history / favorites scope and do not start Phase 4.7.

- Auto-next now uses the same adjacent-Episode switching path as the manual next button and stays on the UI dispatcher.
- `TvSeasonDetailPage` exposes Season-level favorite, want-to-watch, and not-interested toggle buttons.
- Episode rows expose watched / unwatched buttons that update only `TvEpisode` lightweight summary fields and do not create `WatchHistory`.
- The media-library batch toolbar only exposes watched, unwatched, remove from library, and delete record.
- Mixed Movie + Season batch operation semantics remain deferred.
- `SeriesOverviewPage` Season list is structurally scrollable when content exceeds available height.
- Watch Insights, Watch Profile, persona inputs, and recommendation fingerprint remain Movie-only. Existing statistics queries filter `WatchHistory.MovieId` and never consume Episode history.

## Phase 4.6 Bugfix 2 / Season State And Poster Cache Fixes

Phase 4.6 Bugfix 2 keeps the project inside the TV library integration scope and does not start Phase 4.7.

- TV poster image bindings now use the existing poster cache behavior for Series, Season, favorites, home continue watching, watch history, and TV detail pages.
- Season favorite / want-to-watch / not-interested rules follow the movie rules: only fully watched Seasons can be favorite, only unwatched Seasons can be want-to-watch, and not-interested is mutually exclusive with favorite / want-to-watch.
- Marking not-interested does not change Episode watched state.
- Marking a Season watched updates all known TMDB Season Episode metadata rows, cancels want-to-watch, and does not create `WatchHistory`.
- Marking a Season unwatched updates all known TMDB Season Episode metadata rows, cancels favorite, and does not create `WatchHistory`.
- TV identification and manual TV correction upsert the full TMDB Season detail Episode metadata so missing Episodes without media sources can still participate in Season watched state.
- Season progress is displayed as watched Episodes over TMDB total Episodes, for example `3 / 7` when only three of seven TMDB Episodes are watched.
- Episode watched / unwatched actions remain single-Episode actions and refresh aggregate Season state.
- Episodes without active media remain non-playable even when their metadata exists and their watched state can be edited.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.6 Delete / Remove Policy

- Remove Season marks Episode media files as removed in software only.
- Delete Season record removes software records only and mirrors the existing Movie record deletion class of behavior.
- Local files and WebDAV files are never deleted.

## Phase 4.6 Out Of Scope

- TV discovery search / ranking UI, moved to Phase 4.8
- AI recommendations for TV
- Watch Insights TV statistics
- Final UI polish
- Complex anime season models
- Multi-episode file expansion
- Online subtitle search
- Database migrations
- `EpisodeMediaFile`
- Unified Unknown / UnidentifiedMedia table
- `TvSeasonRatingSources`

## Phase 4.7 Goal

Phase 4.7 closes the main TV experience before discovery work. The original TV search / ranking phase moves to Phase 4.8, and the full Phase 4 regression and documentation closeout moves to Phase 4.9.

- `TvSeasonDetailPage` displays Season ratings without adding a migration.
- Season detail metadata is not blocked by rating requests; only the rating area shows loading / unavailable text.
- TMDB Season detail is already requested during TV scan / identification, and detail rating display reads TMDB Season rating through that metadata cache path.
- TMDB Season `vote_average` / `vote_count` is shown as `TMDB 季评分` when available.
- Series-level IMDb data from OMDb loads after opening Season detail and is shown only as `IMDb 剧集评分`.
- OMDb Season audit responses are not treated as stable Season-level IMDb ratings; Season-level IMDb is not shown unless it becomes stable and unambiguous in a later phase.
- Missing rating data shows an empty / `暂无评分` state and does not block Season state actions.
- `TvSeasonRatingSources` remains deferred.

## Phase 4.7 Mixed Batch Rules

- Media-library batch mode may select Movie and Season cards together.
- Mixed batch watched keeps Movie behavior on Movies and uses Season whole-season watched on Seasons.
- Mixed batch unwatched keeps Movie behavior on Movies and uses Season whole-season unwatched on Seasons.
- Mixed batch remove marks records / media sources removed in software only and never deletes physical files.
- Mixed batch delete record removes software records only and never deletes physical files.
- Series and Episode are not batch-operation units.
- Batch favorite, want-to-watch, and not-interested remain unsupported.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.7 Deferred

- TV search / ranking UI is now Phase 4.8.
- Full Phase 4 regression and documentation closeout is now Phase 4.9.
- Persistent Season rating source tables, including `TvSeasonRatingSources`, remain deferred.
- Final TV visual polish remains deferred.

## Phase 4.8 Goal

Phase 4.8 adds TV discovery search and TV rankings inside the existing movie discovery page. The page still has exactly three top-level tabs: movie search, rankings, and AI recommendations.

- Search tab adds a second-level Movie / TV switch.
- Movie search keeps the existing movie and person-search paths.
- TV search calls TMDB TV search and displays Series cards through a dedicated TV view model.
- Ranking tab adds a second-level Movie / TV switch.
- Movie rankings keep the existing popular, top-rated, and trending paths.
- TV rankings call TMDB TV popular, top-rated, trending day, and trending week.
- TV ranking results are Series-level results only; Season rankings are not fabricated.
- TV discovery cards show Series title, original name, first-air year, genres, overview, TMDB Series rating, poster, library state, and Season state summary when available.
- TV discovery poster bindings use the existing poster cache behavior.
- In-library Series navigate to `SeriesOverviewPage`.
- Not-in-library Series show a low-risk TV external-detail placeholder instead of navigating to Movie detail or becoming `AiRecommendationItem`.
- TV discovery remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.8 Bugfix Goal

Phase 4.8 Bugfix aligns TV discovery with the Movie discovery experience without expanding into later TV feature-gap work.

- TV ranking layout mirrors Movie ranking: rank 1 is a full-width large card, rank 2 onward uses two-column rows.
- TV ranking pagination mirrors Movie ranking: page 1 displays 21 entries, later pages display 20 entries, with the 200-entry cap preserved.
- TV ranking navigation is disabled while TV ranking requests are loading and restored after success or failure.
- TV search has basic filters for TV genre, region, library / Season state summary, sort direction, sort key, first-air decade, and language.
- TV genre filtering uses TMDB TV genre labels rather than Movie genre labels.
- TV search and ranking cards show total Season count from TMDB Series detail when available, with loading and unknown states.
- TV discovery copy distinguishes shared Movie / TV search from Movie-only person search.
- TV discovery remains Series-level and read-only; it does not create Season metadata, write database state, show Episodes, or fabricate Season rankings.
- TV discovery remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.8 Deferred

- Full not-in-library TV external detail page remains deferred.
- TV search filters remain MVP-level; Phase 4.8 Bugfix adds basic client-side filters, while fuller TV-specific filtering remains deferred.
- TV rankings remain Series-level because TMDB returns Series for TV ranking endpoints.
- Full TV feature-gap audit and stage reorder remains deferred to the next phase.
- Full Phase 4 regression and documentation closeout remains Phase 4.9.
- Final TV visual polish remains deferred.

## Phase 4.10 Goal

Phase 4.10 adds centralized TV metadata hydration and unavailable Season / Episode display. It reuses the existing Series overview, Season detail, `TvSeries`, `TvSeason`, and `TvEpisode` model instead of adding an external TV detail page.

- A TMDB Series ID can be hydrated into local `TvSeries`, all TMDB `TvSeason` rows, and all TMDB `TvEpisode` rows.
- Hydration does not create `MediaFile`, does not fabricate playback sources, does not modify existing source rows, and does not write collection state.
- Existing Episode playback progress and watched state are preserved while metadata fields can be refreshed.
- Season 0 is displayed as `特别篇` and is not hidden.
- Series overview displays all known Seasons, including Seasons without active Episode sources.
- Season detail displays all known Episodes, including Episodes without active sources.
- Episodes without active sources show `暂无播放源`; their play button is disabled while watched / unwatched actions remain available.
- TV discovery not-in-library Series clicks hydrate metadata and navigate to `SeriesOverviewPage` instead of showing the old external-detail placeholder.
- Browsing not-in-library TV can accumulate TV metadata rows; cleanup is deferred.

## Phase 4.10 Boundaries

- No migration is added.
- No database update is executed.
- No `ExternalTvSeriesDetailPage` is added.
- No `MediaFile` or fake `RemoteUri` is created for metadata-only TV.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.10 Roadmap Rebase

- Phase 4.11: TV correction entry UI.
- Phase 4.12: TV scan / rescan / history-location hardening.
- Phase 4.13: Full Phase 4 regression and documentation closeout.

## Phase 4.10 Deferred

- Cleanup strategy for metadata-only TV Series / Seasons / Episodes.
- Persistent TV metadata refresh and stale-data policy.
- TV correction entry UI and cross-type correction UI.
- Scan / rescan edge-case hardening.
- TV AI support as a Phase 5 candidate.

## Phase 4.10.1 Goal

Phase 4.10.1 refines metadata-only TV visibility in the media library and locks the batch-operation rules for source-less Seasons and not-in-library Movies.

- Metadata-only TV created by discovery browsing does not appear in the default media-library list unless it has playback context or user state.
- A Series appears in normal library mode when at least one Episode has an active `MediaFile`, or when at least one Season has user state.
- User state for metadata-only TV includes Season collection flags and explicit watched / unwatched history recorded through Season state changes.
- Batch mode expands a source-backed Series into all known Seasons, including source-less Seasons and Season 0.
- A metadata-only Series with no playback source only exposes Seasons that have user state in batch mode.
- Episodes remain playback/detail units only; they are not media-library top-level items and are not batch items.
- Batch watched / unwatched is allowed for metadata-only Seasons and updates all known Episodes without writing `WatchHistory`, `MediaFile`, or fake sources.
- Batch remove skips source-less Seasons and not-in-library Movies with a readable `暂无播放源可移出` result instead of deleting metadata or state.
- Batch delete record removes software records / metadata / state according to the existing record-deletion path and does not delete local or WebDAV files.

## Phase 4.10.1 Boundaries

- No migration is added.
- No database update is executed.
- No TV correction UI is added.
- No scan / rescan hardening is included.
- No metadata-only auto-cleanup or stale-refresh policy is added.
- TV state and metadata remain excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.10.1 Deferred

- Cleanup policy for metadata-only TV rows created by discovery browsing.
- Persistent TV metadata refresh / stale-data policy.
- Phase 4.11 TV correction entry UI.
- Phase 4.12 TV scan / rescan / history-location hardening.
- TV AI support remains a Phase 5 candidate.
