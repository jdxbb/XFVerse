# TV Support Plan

## Phase 4.12-post-fix Scope

Phase 4.12-post-fix is an automatic-scan legacy fix before Phase 4.13. It keeps the existing schema and derives non-persistent unknown TV grouping keys from active source context only.

The phase changes normal media-library `Other` projection so persisted all-failed no-TMDB TV-like content is shown as an unknown Series container. Batch mode continues to show Season / grouped item granularity.

Automatic grouping and append use conservative safety gates:

- Same source connection.
- Same scan path.
- Same series root or same carrying directory when no explicit series root exists.
- Compatible normalized parent/title context.
- Unique compatible failed no-TMDB Series / Season candidate.
- Single normal Episode number for append.
- Trailing duplicate-copy suffixes such as `(1)` or full-width parenthesis variants are normalized only in TV-like context, so same-folder duplicate files can attach as additional sources.
- No SP/OAD/OVA/special, multi-episode, course, collection, or theatrical mapping.

If any condition is ambiguous, automatic reuse / append is skipped and existing placeholder grouping remains the fallback. The phase never creates empty Episode rows; it creates or updates only Episodes backed by real sources.

Out of scope remains Phase 4.13 work: manual aggregate-to-Season, manual correction to an existing unknown Season episode number, batch button rule changes, AI-assisted correction strategy changes, unified Movie / TV correction entry, and historical bulk merge of duplicate unknown containers.

## Phase 4.11f-fix-9 Scope

Phase 4.11f-fix-9 is a scan closeout parser and telemetry pass. It broadens only verified sequence patterns that already have folder-level evidence: fansub bracket episode numbers, bracket episode segments reused at TV apply time, and leading-number-title files with explicit season context. It also allows long-running numeric unknown ranges to contain small gaps without creating missing episodes.

The phase keeps default scan conservative: no `01: title` parsing, no SP/OAD/OVA mapping, no theater-collection automation, no extra AI pass, no TMDB threshold changes, and no TV entry into Watch Insights or AI recommendation inputs. TV.Parse warnings are scan warnings, not scan failures.

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
- Phase 4.10.1 originally skipped source-less remove with a no-source result; Phase 4.10.4 supersedes that rule with `LibraryVisibilityState.Hidden`, preserving metadata and state while hiding source-less rows from the media library.
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

## Phase 4.10.3 Goal

Phase 4.10.3 adds the data-model foundation for explicit media-library visibility without changing runtime library behavior.

- Add `LibraryVisibilityState` with `Auto = 0`, `Visible = 1`, and `Hidden = 2`.
- Add `LibraryVisibilityState` to `UserMovieCollectionItem` and `UserTvSeasonCollectionItem`.
- Keep existing `UserMovieCollectionItem.IsInLibrary` unchanged; it is not redefined or reused as visibility state.
- Default old and new rows to `Auto`.
- Add only the required EF mapping and migration columns; do not backfill, delete, rename, or clean existing data.
- Do not change media-library filters, remove behavior, Discovery labels, detail buttons, Favorites, Home, Watch History, AI recommendations, Watch Insights, or recommendation fingerprints in this phase.

## Phase 4.10.3 Deferred

- Phase 4.10.4 will connect visibility state to media-library filters and source-less remove / hide behavior.
- Phase 4.10.5 will add explicit add-to-library actions that write `Visible`.
- `Hidden` will affect media-library visibility only after Phase 4.10.4 and must not hide status-based Favorites entries.

## Phase 4.10.4 Goal

Phase 4.10.4 connects `LibraryVisibilityState` to media-library query and remove semantics without adding another migration.

- Media-library source-state filter labels are `全部`, `有播放源`, and `无播放源`.
- `HasActiveSource` means at least one active video `MediaFile`.
- `IsVisibleInLibrary` is separate from active source state and from the legacy `UserMovieCollectionItem.IsInLibrary` field.
- Movie and TV Season `Hidden` hides rows from the media-library list while preserving active sources, state, history, and metadata.
- Movie / Season remove writes `Hidden`; it no longer skips source-less rows and no longer disables source-backed rows.
- `MediaFile.IsDeleted` remains the app-level source-unavailable / source-record cleanup marker, not the media-library hide marker.
- Delete record remains the path that clears app metadata / state records; it still does not delete physical files.
- Favorites remain status views: `Hidden` must not hide favorite / want-to-watch / not-interested entries there.
- TV remains excluded from Watch Insights, AI recommendations, profile/persona inputs, and recommendation fingerprints.

## Phase 4.10.4 Deferred

- Phase 4.10.5 will add explicit add-to-library actions that write `Visible`.
- Add-to-library-specific actions are implemented in Phase 4.10.5 and write `LibraryVisibilityState.Visible`.
- TV metadata hydration loading optimization is handled by Phase 4.10.6.
- TV correction entry UI remains Phase 4.11.

## Phase 4.10.4d Goal

Phase 4.10.4d closes the remaining visibility wording, Hidden restore, and movie-only AI input gaps without adding a migration.

- Media-library cards and TV detail source labels use source semantics: `有播放源` / `无播放源` / `暂无播放源`, not `已入库` / `未入库`.
- Discovery search and ranking labels use `有播放源` / `无播放源`; add-to-library-specific wording remains Phase 4.10.5.
- Positive user state operations clear `Hidden` back to `Auto`: want-to-watch true, favorite true, not-interested true, and watched true.
- Mark-unwatched and cancel-state operations do not clear `Hidden`.
- Pure visibility-only Movie rows are excluded from movie AI/profile/statistics/recommendation fingerprints and fallback candidates.
- Favorites remain state views; `Hidden` does not hide favorite / want-to-watch / not-interested rows.
- Favorites Movie rows with a local `MovieId` navigate to `MovieDetail`; only pure external metadata rows use external detail.
- TV remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.
- No database update, no new migration, no commit, and no push are part of this phase.

## Phase 4.10.4f Goal

Phase 4.10.4f changes remove-from-library to hide-only semantics without adding a migration.

- Remove from library writes `LibraryVisibilityState.Hidden` for Movie and TV Season regardless of active source state.
- Remove from library does not mark active `MediaFile` rows deleted, does not reset default playback source, and does not disable Episode sources.
- `Hidden` has priority over active source in media-library queries, so hidden source-backed rows disappear from `全部`, `有播放源`, and `无播放源`.
- Favorites, Watch History, detail pages, playback source selection, scanning, and Discovery continue to use active `MediaFile` semantics instead of `Hidden`.
- Old `MediaFile.IsDeleted` rows created by earlier remove behavior are not automatically restored because old hide operations cannot be safely distinguished from missing files, path removals, or delete-record cleanup.
- Phase 4.10.5 remains the explicit add-to-library action that writes `Visible`.

## Phase 4.10.5 Goal

Phase 4.10.5 adds explicit media-library visibility actions without changing playback-source, AI, or Watch Insights semantics.

- Add-to-library writes `LibraryVisibilityState.Visible`.
- Movie add-to-library creates or updates the Movie user-state row only for visibility; it does not set want-to-watch, favorite, not-interested, watched, or `IsInLibrary`, and it does not create `MediaFile`.
- TV Season add-to-library writes `Visible` on `UserTvSeasonCollectionItem` only; it does not create Episode sources or set Season state flags.
- TV Series add-to-library acts on all known Seasons, including Season 0 / Specials, after ensuring TV metadata exists.
- The media-library page exposes an `已移出媒体库` management entry for Hidden Movie / Season rows.
- Hidden management supports restore, detail navigation, and delete-record operations while preserving the existing delete-record semantics.
- Discovery and detail pages can expose add / restore actions for source-less or Hidden Movie / TV rows.
- `Visible` is media-library visibility only and is not an AI preference signal.
- TV remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.

## Phase 4.10.5b Goal

Phase 4.10.5b refines restore semantics and SeriesOverview series-level actions without adding a migration.

- Restore-to-library no longer blindly writes `Visible`.
- Hidden Movie / Season restore writes `Auto` when the row has active source or real current state.
- Hidden Movie / Season restore writes `Visible` only when the row has no active source and no real current state.
- Real current state includes want-to-watch, favorite, not-interested, watched, and explicit Movie user rating when present; unwatched and metadata-only rows are not real current state.
- Auto rows can become media-library-invisible again after the final state is cleared.
- Visible rows remain visible after state is cleared because they represent explicit add-to-library.
- The removed-library management list still shows only `Hidden`; automatic Auto invisibility does not enter that list.
- SeriesOverview always exposes a series-level library action area: join whole series, complete all seasons, restore hidden seasons, or show an already-in-library disabled state.
- One-season SeriesOverview pages still show the series-level action area.
- TV Season detail keeps the current-season add / restore action.

## Phase 4.10.5 Deferred

- TV correction entry UI remains Phase 4.11.
- Scan / rescan / history-location hardening remains Phase 4.12.
- Old `MediaFile.IsDeleted` rows from earlier remove behavior are still not automatically restored.

## Phase 4.10.6 Goal

Phase 4.10.6 optimizes TV Discovery navigation and metadata hydration without changing media-library visibility, playback-source, AI, or Watch Insights semantics.

- TV search / ranking Series clicks use a summary-first metadata path: ensure `TvSeries` plus TMDB Season summaries, then navigate to `SeriesOverviewPage`.
- Summary-first hydration only skips when every TMDB Season summary already exists locally, so previously partial Series can still be completed before navigation.
- Full Episode metadata hydration is no longer required before opening `SeriesOverviewPage`.
- `SeriesOverviewPage` keeps the existing background full hydration refresh and can show metadata completion / partial failure status without blocking the Season list.
- `TvSeasonDetailPage` can request current-Season Episode metadata on demand when the Season summary exists but Episode rows are incomplete.
- TV search / ranking pagination and repeated TV card clicks are guarded while a TV Series detail navigation is in progress.
- Deleting a TV Season record and reopening the Series from TV search / ranking must still recreate the Series / Season summary and open `SeriesOverviewPage`.
- Hydration still does not create `MediaFile`, fabricate playback sources, write collection state, or include TV in AI / Watch Insights / fingerprints.
- Phase 4.11 is rebased to TV scan identification hardening before correction UI.
- Phase 4.12 becomes the AI-assisted correction / cross-type correction entry phase.

## Phase 4.11 Goal

Phase 4.11 hardens TV scan identification and adds lightweight AI directory hints for default scan.

- Default scan can call the configured AI service with a sanitized path tree, but AI only suggests likely TV ranges.
- AI results do not write the database, do not request TMDB, and do not decide the final match.
- Local parser and TMDB confidence checks remain the final gate.
- AI failure, timeout, or invalid JSON falls back to local rules.
- Files inside an AI or local strong TV range go through TV identification first; if TV cannot match safely, they stay in TV placeholder / unrecognized TV handling and are not silently sent to Movie fallback.
- Files outside TV ranges keep the existing Movie identification path.
- Parser support is extended for strong TV contexts: bare numeric episodes, Chinese episode markers, Chinese season folders, and title-plus-number filenames.
- TV query generation now tries multiple cleaned candidates and rejects generic season / quality / codec-only queries.
- Diagnostic scan logs record sanitized paths, TV range source, parser pattern, candidate source, query attempts, reject reason, and final decision.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11 Boundaries

- No migration is added.
- No database update is executed.
- Default scan AI does not perform full Movie / TV correction.
- Complex mixed folders and cross-type corrections move to Phase 4.12.
- TV AI recommendation remains a Phase 5 candidate, not part of Phase 4.

## Phase 4.11b Goal

Phase 4.11b corrects the default-scan AI responsibility boundary after diagnostics showed the old `episode-files-v1` prompt was too heavy.

- Default scan AI now uses `directory-ranges-v1`.
- AI returns Series / Season directory ranges only: `seriesFolder`, `seriesTitleHint`, `seasonFolders`, `seasonNumberHint`, `confidence`, and a compact reason.
- AI is explicitly instructed not to return `episodeFiles` and not to map files to episode numbers.
- Local TV parsing remains responsible for Season / Episode numbers inside AI TV ranges.
- AI range results still do not write database rows, do not call TMDB, and do not decide final Series matches.
- Low-confidence ranges are logged but not applied.
- Invalid JSON, timeout, or AI failure still falls back to local rules.
- Diagnostics record schema, prompt size, token estimate, sample file count, response size, duration, parsed range count, applied range count, ignored low-confidence count, and fallback reason.
- A long-timeout probe against the same active-video set showed the new schema reduced assistant output from about 43.3k chars to about 13.8k chars and duration from about 156.7s to about 100.2s, but the request can still exceed the production 18s timeout.
- Budget-driven directory summary / batch remains a follow-up if default scan AI still times out.
- Phase 4.12 remains the active AI-assisted correction / cross-type correction entry phase.

## Phase 4.11c Goal

Phase 4.11c optimizes the `directory-ranges-v1` prompt without changing scan matching rules or adding batching.

- Directory summaries now describe direct video files only.
- `sampleVideoFiles` are selected only from the current directory's direct videos; child folders are listed separately as `childFolders`.
- Sample count is `ceil(directVideoCount * 10%)`, clamped to 1-5; directories with no direct videos use zero samples.
- Sample selection favors episode-like names, bare numeric names, title-plus-number names, then ordinary videos, and spreads samples across the directory instead of taking only adjacent files.
- AI output uses short `evidence` values instead of natural-language reasons.
- The parser does not require evidence and ignores unexpected `episodeFiles`.
- Long-timeout probing against the same active-video set showed the prompt was reduced from about 23.1k chars to about 20.5k chars and duration from about 100.2s to about 92.7s.
- The result is acceptable for one-off large-directory diagnosis, but it can still exceed the production short timeout; batch remains deferred until real scans prove it is required.
- No production timeout decision is made in this phase.

## Phase 4.11d Goal

Phase 4.11d tightens TV scan generalization and match validation without adding a migration.

- Strong TV context now requires multiple independent evidence signals instead of a single weak hint.
- Single title-plus-number, bare numeric, or explicit episode-like filenames are treated as weak TV context unless supported by directory / sibling / sequential evidence.
- TV risk can block silent Movie fallback without enabling broad title-plus-number parsing.
- Chinese season / episode / count structures are retained as structure hints and rejected as standalone title queries.
- TV query attempts carry a `querySource`, and rejected queries log a concrete bad-query reason.
- TV TMDB search logs selected query source and downgrades close / conflicting top candidates to placeholder instead of auto-binding.
- Diagnostics record strong evidence, weak reason, fallback-risk count, selected query, query source, and candidate conflict reason.
- This phase optimizes for lower wrong auto-bind risk, not higher bound count.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11d Deferred

- Disabling default full AI and introducing local pre-analysis / `aiCandidateRanges` / AI-on-uncertain remains the next scan phase.
- Active AI-assisted correction and cross-type correction UI remains Phase 4.12.
- Episode detail pages and Episode multi-source management remain deferred.

## Phase 4.11e-prep Goal

Phase 4.11e-prep disables default production full AI range analysis and prepares local `aiCandidateRanges` diagnostics for a later AI-on-uncertain scan phase.

- Default WebDAV and Local scans no longer call the full AI directory range request by default.
- `TvScanDirectoryAnalysisService` and the directory-range AI code remain available as a diagnostic / optional experiment path.
- Local scan pre-analysis now emits `aiCandidateRanges` from TV risk signals such as weak TV context, title-number / numeric uncertainty, generic season queries, fallback-blocked directories, placeholders, and candidate conflicts.
- `aiCandidateRanges` are log-only and are not persisted to the database.
- `aiCandidateRanges` include sanitized path, risk tags, sample direct video files, suspected Series / Season folder, candidate query, blocked Movie fallback count, and conflict count where available.
- This phase does not actually call AI for uncertain ranges.
- Phase 4.11d local tightening, query rejection, candidate conflict downgrade, and Movie fallback risk interception remain in place.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-prep Deferred

- AI-on-uncertain directory assistance remains the next scan phase.
- Active AI-assisted correction and cross-type correction UI remains Phase 4.12.
- No production long timeout, batch, or concurrent batch policy is introduced in this phase.

## Phase 4.11e-prep-2 Goal

Phase 4.11e-prep-2 improves the quality of scan candidate ranges before AI-on-uncertain is enabled.

- Movie identification rejects numeric-only, metadata-only, and otherwise low-information queries from automatic binding.
- Low-information Movie queries become placeholders instead of high-confidence wrong auto-binds.
- Generic TV structure risk is extended to total-count / season-range hints with multiple sequential numeric direct videos.
- TV-risk folders can still block Movie fallback without becoming automatic TV matches.
- `aiCandidateRanges` only represent unresolved, conflicting, weak-risk, or fallback-blocked ranges; successful strong TV matches without unresolved risk are not emitted as AI candidates.
- Candidate ranges are merged by sanitized directory and keep combined risk tags, direct-video samples, query buckets, conflict counts, and fallback-block counts.
- Candidate queries are classified as usable, rejected, or noisy for later AI-on-uncertain prompts.
- Local directory hints use local / directory diagnostic names; AI names are reserved for real AI responses.
- Default full AI range analysis remains disabled and this phase does not call AI for candidate ranges.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-prep-2 Deferred

- Formal AI-on-uncertain directory assistance remains the next scan phase after another real scan log audit.
- Active AI-assisted correction and cross-type correction UI remains Phase 4.12.
- Movie title cleaning may still need broader release-token refinement after real Movie scan samples are reviewed.

## Phase 4.11e-prep-3 Goal

Phase 4.11e-prep-3 finalizes scan candidate input before enabling AI-on-uncertain.

- Movie scan title cleaning removes generic release / audio / source metadata before TMDB Movie search.
- Movie cleanup stays pattern-based and does not add specific movie, release group, folder, or TMDB ID special cases.
- Candidate query diagnostics are split into usable, noisy, and rejected buckets so later AI prompts do not treat dirty strings as primary title hints.
- TV placeholder, conflict, dirty-query, low-confidence, and fallback-blocked paths are merged into a final run-level `aiCandidateRanges` summary.
- Final candidate range summaries are log-only, sanitized, merged by directory, and do not write database rows.
- Same-name / version conflicts remain downgrade-only; local scan does not force a version decision.
- Default full AI range analysis remains disabled and no AI-on-uncertain call is made in this phase.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-prep-3 Deferred

- Formal AI-on-uncertain directory assistance remains the next scan phase.
- Active AI-assisted correction and cross-type correction UI remains Phase 4.12.
- Episode detail pages and Episode multi-source management remain deferred.

## Phase 4.11e-prep-4 Goal

Phase 4.11e-prep-4 adds final scan auto-bind validation gates before enabling AI-on-uncertain.

- Movie scan auto-binding is limited to clear `Matched` results. `NeedsReview` and other uncertain Movie results become placeholders / future AI-candidate diagnostics.
- TV scan auto-binding is limited to clear `Matched` results. `NeedsReview`-level TV candidates are not written as matched Seasons.
- Localized-title exact matches are no longer enough by themselves when a generic version qualifier / original-title conflict is detected.
- Movie title cleanup gets only small generic release/source/audio/subtitle improvements; no specific movie title, release group, directory, or TMDB ID special cases are introduced.
- Diagnostics expose `movieAutoApply`, `tvAutoApply`, and blocked reasons so scan logs can prove when uncertain candidates were not written.
- Default full AI range analysis remains disabled and no AI-on-uncertain call is made in this phase.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-prep-4 Deferred

- Formal AI-on-uncertain directory assistance remains the next scan phase if real scan logs show no remaining wrong auto-bind blocker.
- Active AI-assisted correction and cross-type correction UI remains Phase 4.12.
- Episode detail pages and Episode multi-source management remain deferred.

## Phase 4.11e Goal

Phase 4.11e enables AI-on-uncertain scan assistance for final `aiCandidateRanges` only.

- Default full AI range analysis remains disabled.
- AI-on-uncertain input is built only from the final sanitized `aiCandidateRanges` summary.
- AI returns directory / title / season hints only. It does not return `episodeFiles` and does not map individual files to Season / Episode numbers.
- AI hints are diagnostic inputs to the existing TV parser, TMDB query, conflict downgrade, and auto-bind safety gates.
- AI never writes database records directly. Only local validation can apply clear `Matched` and no-conflict results.
- `NeedsReview`, conflict, low confidence, dirty query, and weak-source results remain placeholders / AI-candidate diagnostics.
- AI failure, timeout, empty response, or invalid JSON falls back to the pre-existing placeholders / candidate ranges.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e Deferred

- Active AI-assisted correction and cross-type correction UI remains Phase 4.12.
- Episode detail pages and Episode multi-source management remain deferred.
- Complex mixed Movie / TV folders that still cannot be resolved safely remain `NeedsReview` / placeholder until active correction.

## Phase 4.11e-fix-1 Goal

Phase 4.11e-fix-1 fixes AI-on-uncertain hint mapping and adds a small media-library batch selection helper.

- AI-on-uncertain maps hints back to local candidate ranges by `inputRangeId` first.
- AI-returned `seriesFolderHint` / `seasonFolderHint` values are auxiliary hints, not hard path selectors.
- Directory hint mismatch is logged as a warning-style diagnostic and does not discard an otherwise mapped hint.
- Fuzzy sanitized-path matching remains only as fallback for missing / unknown `inputRangeId`.
- AI hints remain hint-only and still flow through local parser, TMDB validation, candidate conflict downgrade, and scan auto-bind gates.
- Media-library batch mode can select the current filtered / loaded list and clear the current selection.
- Remove-from-library and delete-record semantics are unchanged.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-fix-1 Deferred

- Full active AI-assisted correction and cross-type correction UI remains Phase 4.12.
- Further scan match-quality tuning should be driven by a fresh rescan log after the mapping fix.
- Episode detail pages and Episode multi-source management remain deferred.

## Phase 4.11e-fix-2 Goal

Phase 4.11e-fix-2 fixes AI-on-uncertain candidate range file binding.

- Candidate ranges carry runtime `MediaFileIds` for the files covered by each uncertain directory range.
- Candidate range merge / dedupe preserves and merges `MediaFileIds`.
- AI hint application maps by `inputRangeId`, then resolves files by `MediaFileIds`.
- `SanitizedPath` is retained for logs, prompt context, and fallback only.
- Diagnostics expose range file counts, file resolution method, and aggregate no-file counters.
- AI hints remain hint-only and still require local parser, TMDB validation, and safety gates.
- Phase 4.11e-fix-1 current-list batch select-all remains available.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11e-fix-2 Deferred

- Fresh real scan validation should verify that `no-files-in-input-range` drops and applied hints increase.
- Active AI-assisted correction and cross-type correction UI remains Phase 4.12.
- Episode detail pages and Episode multi-source management remain deferred.

## Phase 4.11f Goal

Phase 4.11f upgrades AI-on-uncertain from generic directory/title hints to refined title lookup hints while keeping local validation in control.

- Default full AI range analysis remains disabled.
- AI-on-uncertain still processes only final `aiCandidateRanges`.
- AI returns `refinedSeriesTitle`, optional `originalTitleHint`, `yearHint`, `seasonNumberHint`, confidence, `needsReview`, and short evidence.
- AI does not receive TMDB top-N candidates, does not choose TMDB candidates, does not return `episodeFiles`, and does not write records.
- Local TV identification uses `refinedSeriesTitle` as an `ai-refined-title` TMDB TV search query and takes the TMDB top1 only after a lightweight safety gate.
- The lightweight safety gate rejects low confidence, `needsReview`, no-result, title mismatch, year conflict, original-title conflict, and unresolved top-candidate conflict.
- Safety-gate failures preserve placeholder / `NeedsReview` / `ai-candidate` results.
- `inputRangeId -> MediaFileIds`, local parser ownership of Episode parsing, and all auto-bind safety gates remain in force.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11f Deferred

- Active AI-assisted correction and cross-type correction UI remains Phase 4.12.
- AI-based TMDB candidate selection / second-pass disambiguation is not introduced in Phase 4.11f.
- Episode detail pages and Episode multi-source management remain deferred.

## Phase 4.11f-fix-1 Update

Phase 4.11f-fix-1 supersedes the Phase 4.11f refined lookup safety-gate behavior for AI-on-uncertain ranges.

- AI-on-uncertain still processes only final `aiCandidateRanges`; full AI range analysis remains disabled.
- AI is asked to return the cleanest likely `refinedSeriesTitle` for local TMDB TV search, even when the range is uncertain.
- `needsReview=true` and low confidence no longer block TMDB lookup when `refinedSeriesTitle` is present.
- Local code searches TMDB TV with `refinedSeriesTitle` and accepts top1 for the AI refined lookup path.
- Original-title, localized-title, year, version, and top-candidate conflict gates no longer block this AI refined top1 path.
- AI still does not receive TMDB top-N candidates, does not choose TMDB candidates, does not return `episodeFiles`, and does not write records directly.
- Empty `refinedSeriesTitle`, no TMDB result, missing `inputRangeId`, missing `MediaFileIds`, or unparseable episode files still preserve placeholder / `ai-candidate` behavior.
- Possible wrong top1 matches are a known product tradeoff and should be corrected through Phase 4.12 active AI-assisted correction / manual confirmation.

## Phase 4.11f-fix-2 Update

Phase 4.11f-fix-2 changes AI refined lookup title priority to prefer original-language titles.

- AI-on-uncertain prompt/schema now asks for `originalLanguageTitle`, `englishTitleHint`, `localizedTitleHint`, and `searchTitle`.
- For non-English series, the AI is instructed to return the original-language title as the primary TMDB search query instead of the English/international title when it can infer one.
- Local refined lookup query priority is `originalLanguageTitle`, then `searchTitle`, then `englishTitleHint`, then localized / legacy refined fallback.
- English and localized titles are retained as fallback / auxiliary aliases only.
- The existing product choice remains: TMDB top1 from the selected refined query is accepted for the AI refined path when TMDB returns a result.
- No title-specific alias table, TMDB top-N AI selection, second AI request, migration, or database update is introduced.
- Phase 4.12 remains responsible for active AI-assisted correction / manual confirmation of any remaining wrong top1 matches.

## Phase 4.11f-perf-1 Update

Phase 4.11f-perf-1 is a scan performance pass with no recognition-policy change.

- The second TV identification pass after AI-on-uncertain is scoped to AI affected `MediaFileIds` instead of the full scan set.
- If AI affects no files, the second TV pass is skipped.
- A per-scan TMDB search cache deduplicates repeated TV and Movie search queries during the same scan run.
- TV and Movie search caches stay separate and are keyed with media type and relevant search context.
- The cache is runtime-only, not persisted to the database, and does not require migration.
- This phase does not change AI prompt/schema, refined-title top1 behavior, parser rules, fallback rules, safety gates, media-library visibility, Movie AI classification behavior, or Discovery surfaces.

## Phase 4.11f-fix-3 Update

Phase 4.11f-fix-3 is a scan closeout guard pass, not a broader recognition expansion.

- AI refined TMDB top1 now has a minimal series-year gate: only `seriesYearHint` is compared with the TMDB Series first-air year, and only a difference greater than two years blocks auto-apply.
- `seasonYearHint` is not used for Series year gating because later seasons naturally air years after the Series first-air year.
- Missing AI series year or missing TMDB first-air year does not block the refined lookup; it is only logged.
- Blocked refined matches remain placeholder / `ai-candidate`; no second AI request, top-N AI selection, or local special-case adjudication is added.
- Movie title cleanup receives small generic release/source/audio/subtitle cleanup for HTML entities, trailing dangling punctuation, `3D` quality tokens, and conservative source / edition tokens.
- TV parser rules are not expanded in this phase; unsupported OAD / SP / OVA / special mapping remains deferred.
- Movie placeholders with at least three strictly consecutive episode-like numbers in the same direct parent folder are grouped as log-only TV-like placeholder ranges. The grouping does not auto-bind TV and does not create Series, Season, or Episode rows.
- Complex anime season mapping, folk season numbering vs TMDB season numbering, multi-source episode management, course / extras classification, and manual aggregation remain Phase 4.12 / Phase 4.13 or future anime-specialty work.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11f-fix-4 Update

Phase 4.11f-fix-4 turns grouped TV-like placeholders into media-library read-model data without adding schema.

- Consecutive numbered Movie placeholder failures are projected as `Other` / TV-like placeholder ranges at query time.
- Grouped ranges retain runtime `MediaFileIds`, file count, parent-folder display, number span, sample filenames, and reason tags.
- Grouped placeholder files are hidden from the regular Movie scatter list, but ungrouped Movie placeholders continue to display normally.
- Grouped ranges remain unresolved: they do not bind TMDB, create Series / Season / Episode rows, or count as successful recognition.
- The projection provides the data base for the `Other` category, batch AI correction, and manual aggregation into seasons.

## Phase 4.11f-fix-5 Update

Phase 4.11f-fix-5 completes the early `Other` category surface without adding schema.

- `Other` now covers unrecognized / placeholder / NeedsReview / type-uncertain media-library rows.
- Recognized Movie and recognized TV remain in their own categories; grouped TV-like placeholders are stored as unidentified `TvSeason` / `TvEpisode` rows and remain in `Other` until corrected.
- The recognition-status filter is hidden from the main UI, while backend status data remains available for future correction and diagnostics.
- Grouped TV-like placeholders use existing Season / Episode infrastructure: the scan creates no-TMDB `TvSeries`, failed `TvSeason`, and failed `TvEpisode` rows, then attaches grouped `MediaFile` rows to those Episodes.
- Normal media-library mode now appends unidentified failed Seasons into `Other`; all-failed placeholder Series are not shown as recognized TV summaries.
- Unidentified Episode display titles use original file names, so unknown rows remain traceable to source files.
- The detail page is the normal Season detail in unidentified / pending-correction mode. It can play Episode sources and mark Season / Episode watched state, but it does not bind TMDB or mark recognition successful.
- Movie placeholder grouping also recognizes conservative bracketed episode-number segments, such as bracket blocks that start with an episode number and are grouped only when the same direct parent has at least three strictly consecutive failures.
- Phase 4.12 / 4.13 continue to own active correction, episode details, multi-source management, and manual aggregation into seasons.
- AI refined year-gate logs are made consistent with blocked results, and Movie cleaner receives only a small generic release-context cleanup.
- No migration, database update, full AI range restoration, or TV recommendation / Watch Insights integration is introduced.

## Phase 4.11f-fix-6 Update

Phase 4.11f-fix-6 relaxes only the uncertain-candidate boundary for repeated title+number episode directories.

- Same-parent title+number files can enter TV-like uncertain / `aiCandidateRanges` when there are at least three strictly contiguous numbers and a shared normalized title prefix.
- A single title+number file still does not create strong TV context or an automatic TV bind.
- The candidate is emitted before Movie fallback so AI-on-uncertain can see these directories instead of waiting for Movie placeholder grouping.
- Multi-episode detection is tightened to explicit plausible ranges only; years, quality numbers, audio channel numbers, and ordinary title numbers after `SxxEyy` no longer count as multi-episode ranges.
- Explicit episode markers now allow four-digit episode numbers, while bare four-digit numbers remain conservative and are not global TV evidence.
- Movie placeholder grouping can group numeric quality-tail failures and merge same-parent mixed episode patterns when the parsed episode numbers form one strict contiguous run.
- `01: title`, movie collections, course folders, theatrical collections, anime-specific special mapping, and performance optimization remain deferred.
- TV stays excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11f-fix-7 Update

Phase 4.11f-fix-7 applies already-verified title+number sequence context during TV episode parsing and adds ignored-file scan diagnostics.

- Title+number sequences admitted by preanalysis can now parse as Episodes during TV apply when the file belongs to the same verified range.
- The rule is still range-scoped and conservative: no single-file title+number auto-bind, no cross-directory sequence reuse, and no recursive child-directory mixing.
- Explicit `S2` / `S02` / `Season 2` tokens can supply season number in the verified context. Final-season wording is logged as context but is not hard-mapped to a numeric season.
- Unsupported TV logs now distinguish true multi-episode ranges from ordinary parse failures.
- Local and WebDAV scan discovery now logs ignored file counts by reason and extension with sanitized samples, so future whitelist changes can be evidence-based.
- The video whitelist is not expanded in this phase, and TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11f-fix-8 Update

Phase 4.11f-fix-8 closes the media-library visibility gap for unresolved scan sources.

- Active orphan video `MediaFile` rows with no Movie binding and no Episode binding are projected into `Other` as unrecognized file items.
- Orphan display titles use original safe file names, not cleaned Movie query titles.
- After Movie identification, scanned paths run a conservative unresolved-source grouping pass. Historical orphan rows and newly produced unresolved rows can become unidentified `TvSeason` / `TvEpisode` rows when they form strict same-parent contiguous episode-like sequences.
- Bracketed episode segment sequences are admitted to TV-like uncertain `aiCandidateRanges` before Movie fallback.
- `.rmvb` is included in the video whitelist and `.sup` is included in the subtitle whitelist. `.sup` rendering remains a validation item.
- No third AI pass, no schema migration, no database update, no Watch Insights TV input, and no TV recommendation input are introduced.

## Phase 4.11f-fix-10 Update

Phase 4.11f-fix-10 fixes a verified title-number parsing edge case around dotted part markers.

- TV apply parsing now avoids applying `Path.GetFileNameWithoutExtension` twice to already-trimmed base names, so `Pt.2` / `Part.2` does not truncate the following episode token.
- Verified part-title patterns preserve `seasonHint`, `partHint`, and `episodeInPart` for diagnostics and future correction.
- Part hints are not converted into TMDB episode offsets by default. `Part 2` ranges remain unidentified / pending correction unless a later user-confirmed correction flow can safely map offsets.
- This phase does not change AI prompts, TMDB top1 behavior, year gating, default full-AI settings, Watch Insights, or TV recommendation exclusion.

## Phase 4.11f-fix-11 Update

Phase 4.11f-fix-11 adds a conservative automatic offset only when sibling part evidence is strong enough.

- Dotted part parsing from fix-10 remains the baseline: `Pt.2` / `Part.2` is preserved as a part hint and the filename extension is removed only once.
- A part range can be offset only after AI / TMDB has resolved the same Series and Season, and only when a previous sibling part is already safely bound or safely offset into a contiguous episode range.
- The rule is generic for `Part 2`, `Part 3`, `Part 4`, and later parts; it does not encode a fixed offset or a specific title.
- The offset uses `episodeInPart + previousPartEndEpisode` and still checks TMDB season episode count plus target episode conflicts before apply.
- If any required evidence is missing, the files remain unidentified / pending correction and keep their part hints for later active correction.
- This phase does not add Final Season mapping, SP/OAD/OVA mapping, theatrical collection handling, a third AI pass, Watch Insights input, or TV recommendation input.

## Phase 4.11f-fix-11-hotfix Update

Phase 4.11f-fix-11-hotfix temporarily narrows the part-offset rollout to prevent unsafe auto-binding.

- The fix-10 parser remains in place: dotted part markers are preserved as `partHint` / `episodeInPart` diagnostics.
- `Part`, `Pt`, `Part 2`, `S3 Part`, `Season 3 Part`, and other queries composed only of season / part / number structure are rejected as `structural-part-query`.
- Structural part queries are rejected even when they come from AI refined title input; they do not search TMDB and cannot auto-apply.
- Automatic sibling part offset apply is disabled for now. Part ranges without a safe mapping stay unidentified / pending correction in `Other`.
- A future safe offset redesign must evaluate part evidence before low-confidence placeholder decisions, confirm the TMDB Series / Season, and then decide whether a sibling part continuation is safe.
- TV remains excluded from Watch Insights, AI recommendation inputs, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11f-fix-13 Update

Phase 4.11f-fix-13 reduces AI-on-uncertain timeout blast radius.

- Final `aiCandidateRanges` are split into at most 3 local deterministic batches.
- Grouping keeps related ranges together where possible, using suspected folder / title / parent context, while preserving each range as an indivisible unit.
- The batches run concurrently with a 300 second per-batch timeout and one retry per failed batch.
- Batch successes are merged and applied through the existing `inputRangeId -> MediaFileIds` path.
- Batch failures preserve local placeholder / ai-candidate state for only the failed ranges.
- Partial AI failure is a scan warning, not a scan error.
- The AI prompt / schema, TMDB top1 strategy, year gate, parser behavior, part-offset boundary, Movie fallback, and Watch Insights / recommendation exclusion are unchanged.

## Phase 4.11f-fix-14 Update

Phase 4.11f-fix-14 reintroduces part offset through the safe path rather than the earlier unsafe fallback.

- `Part` / `Pt` / season-part-only strings remain rejected as structural TV search queries and cannot trigger TMDB lookup.
- Part offset is evaluated only after a TMDB Series and Season have been selected by the normal TV identification path.
- Offset evidence can come from the current scan's already parsed contiguous E01..N files or from previously bound same-source database episodes for the same TMDB Series and Season.
- Later parts use `episodeInPart + previousRangeEndEpisode`; no fixed offset or title-specific rule is encoded.
- If TMDB episode metadata is missing, the previous range is not contiguous, or the target episode is occupied, the part range remains unidentified / pending correction.
- The change does not alter AI batching, AI prompt/schema, TMDB top1 policy, year gate, Movie fallback, Watch Insights exclusion, or TV recommendation exclusion.

## Phase 4.11f-fix-14-hotfix Update

Phase 4.11f-fix-14-hotfix fixes the ordering gap for unsupported-only later-part candidates.

- Part candidates with parsed `partHint >= 2` and `episodeInPart` now carry applied AI refined / original-language titles into TMDB Series lookup even when normal episode files are still unsupported.
- The scan no longer relies on folder-name queries with part / subtitle / quality noise as the only lookup path for those candidates.
- The existing safe part offset helper remains the only place that can map `episodeInPart` to a TMDB episode number; it still requires Series / Season confirmation and previous contiguous episode evidence.
- If AI refined Series lookup is unavailable or unsafe, the part range stays unidentified / pending correction with a concrete skipped reason.
- No AI prompt/schema, batching, parser, TMDB top1 policy, year gate, Movie fallback, Watch Insights exclusion, or TV recommendation exclusion changed.

## Phase 4.11g Closeout

Phase 4.11g closes the default TV scan support pass and moves remaining work out of default scanning.

- Latest accepted scan completed with AI-on-uncertain batching enabled, 3 successful batches, 58 parsed hints, 57 applied hints, and 701 AI-affected files retried through the TV parser / TMDB path.
- The second TV pass was limited to AI-affected files, not a full rescan.
- Safe part offset is verified for later sibling parts when the previous same-Series / same-Season episode range is contiguous and TMDB episode metadata is available.
- Structural part-only queries remain rejected and cannot trigger TMDB search or auto-apply.
- TV.Parse warnings are deduplicated and no longer count as scan errors.
- Orphan video files, Movie placeholders, TV placeholders, AI candidates, NeedsReview rows, and unidentified Seasons belong to `Other`; recognized Movies and recognized TV Seasons remain separated.
- Unidentified files use original safe file names; unidentified Seasons / Episodes remain pending correction and do not count as successful TMDB recognition.
- macOS AppleDouble `._*` files are ignored before media type detection.
- `.rmvb` is accepted as a video scan candidate and `.sup` as a subtitle candidate.
- TV remains excluded from Watch Insights, AI recommendation inputs, Watch Profile, persona inputs, recommendation fingerprints, and TV recommendation surfaces in Phase 4.
- Remaining complex cases move to Phase 4.12 / 4.13: Episode detail, multi-source management, active correction, manual regrouping, SP/OAD/OVA, theatrical collections, course / extras classification, Final Season mapping, TV discovery expansion, and media-library performance tuning.

## Phase 4.12b Update

Phase 4.12b adds the first detail shells for Episode-level and unknown-file detail handling.

- Added an Episode detail route and shell page for recognized and failed unidentified Episodes.
- Episode detail currently loads Series / Season / Episode basics, identification status, overview / air date / runtime when present, watched / progress summary, source count, and source summary.
- Missing Episode metadata falls back to safe source file names or `E{n}` display text. The shell does not display full local paths or WebDAV URLs.
- Season detail now exposes a non-playback `详情` entry for each Episode. Existing Episode play buttons remain unchanged.
- Failed unidentified Seasons can still use Season detail, and their Episodes can open the Episode detail shell in `未识别 / 待修正` mode.
- Pure orphan unknown video files in `Other` now open through an ensured failed Movie placeholder and reuse the existing unidentified Movie detail carrier, preventing the click from being a dead end and avoiding duplicate orphan projection for the same source afterward.
- Episode detail includes a `修正信息` placeholder action. Phase 4.12b does not call TMDB, AI, or write correction results.
- No source list, specified-source playback, source deletion, watched/unwatched write operation, schema migration, database update, scan-rule change, Watch Insights TV input, or TV recommendation input is introduced.

## Phase 4.12c Update

Phase 4.12c turns the Episode detail source summary into a playable source list.

- Episode detail now shows active Episode video sources with safe file names, local / WebDAV source type labels, masked location text, format, file size, duration, resolution, codecs, bitrate, probe state, recent playback time, and per-source progress when present.
- The top Episode play button uses a derived default source. The rule is: explicit preferred source when supplied, then recent / in-progress playable source, then accessible local source, then WebDAV or first stable source.
- Each source row can open the existing Episode player with its own `MediaFileId`. The ViewModel verifies that the selected source belongs to the currently loaded Episode before calling the player service.
- Recognized Episodes and failed unidentified Episodes share the same source list and playback behavior.
- Episodes without active sources keep the detail shell and show `暂无播放源`; the play button is disabled.
- Other orphan unknown files continue to use the failed Movie placeholder / unidentified Movie detail carrier. No raw MediaFile playback path is introduced.
- Phase 4.12c does not add source deletion, persistent Episode default source, watched / unwatched writes, real correction, AI / TMDB correction, scan rule changes, TV Watch Insights input, TV recommendation input, online subtitles, migration, or database update.

## Phase 4.12c-fix Update

Phase 4.12c-fix aligns Episode source display and default playback behavior before source deletion / watched-state work begins.

- Episode source location display now uses a shared source display helper instead of the Episode-only location formatter.
- WebDAV and local location text is decoded for display only and reduced to safe trailing path segments, so the UI does not expose full local paths or full WebDAV URLs.
- Episode detail, Season detail playback, and Episode playback-session resolution now share one non-persistent default-source rule.
- The aligned rule is: preferred active source, accessible local source, recent / in-progress source, WebDAV source, then stable first source.
- Episode detail top play and `OpenEpisodeAsync(episodeId)` should now select the same source. Explicit source-row playback still passes `EpisodeId + MediaFileId` and keeps the selected-source guard.
- Episode detail top/source play and Season detail Episode play commands enter an opening state to prevent repeated clicks while the player is starting.
- This fix does not add WebDAV lazy probing, manual reprobe, source deletion, set-default, persistent Episode defaults, watched / unwatched writes, real correction, AI / TMDB correction, scan-rule changes, Watch Insights TV input, TV recommendation input, migration, or database update.

## Phase 4.12c-fix-2 Update

Phase 4.12c-fix-2 tightens the Episode playback busy state to cover the whole player-window lifetime.

- Episode detail top play and per-source play now stay disabled while `IPlayerWindowService.IsPlayerOpen` is true.
- Episode detail listens for `PlayerWindowClosed` to restore play buttons and refresh detail data after playback closes.
- Episode detail buttons bind explicit busy text and `CanOpenPlayer`, so the disabled state does not depend only on command requery.
- Season detail Episode play uses the same page-level player-open gate and explicit XAML `IsEnabled` triggers.
- Season detail keeps `详情` navigation available while playback is open, but Episode play buttons remain disabled until the player window closes.
- The change does not alter default-source selection, source-row ownership validation, source deletion, set-default, watched / unwatched writes, scan rules, migration, or database update.

## Phase 4.12c-fix-3 Update

Phase 4.12c-fix-3 restores Episode media-probe coverage while keeping detail pages read-only for probe data.

- Movie detail and Episode detail both remain read-only consumers of `MediaFile` technical metadata; neither page starts ffprobe.
- The scanner now sends a broader post-scan probe candidate set: changed videos plus active videos in the scanned paths whose probe status is not successful or whose stored probe snapshot is stale.
- The candidate expansion applies to both WebDAV and local scans, so recognized Episode, failed unidentified Episode, Movie, and orphan / failed Movie placeholder sources can all reach the same `MediaProbeService`.
- `MediaProbeService` remains `MediaFile`-level. It no longer relies on Movie-only enqueue diagnostics, and it logs Movie / Episode / orphan plus WebDAV / local counts for queued work.
- Probe lifecycle diagnostics now record sanitized skipped / started / succeeded / failed outcomes without logging full local paths or full WebDAV URLs.
- WebDAV probe remains best-effort through the existing playback URL / credential construction. Manual reprobe, detail-page lazy probing, and a background task center remain deferred.
- The change does not alter scanning identification rules, Episode default-source selection, playback behavior, source deletion, watched / unwatched writes, Watch Insights TV input, TV recommendation input, migration, or database update.

## Phase 4.12c-fix-4 Update

Phase 4.12c-fix-4 adds current-detail lazy probing instead of app startup or full-library probe catch-up.

- Movie detail and Episode detail now trigger a non-blocking best-effort lazy probe check after their source lists load.
- The ViewModels only pass the current page source ids. `MediaProbeService` rechecks that each source belongs to the current Movie or Episode before it can be queued.
- Candidate rules remain conservative: active video source, not deleted, not ignored system file, input information present, not successful with a current probe snapshot, and not a recent pending probe.
- Each detail page queues at most 10 candidates and avoids repeating the same `MediaFileId` within the ViewModel lifetime.
- The same background probe queue handles local and WebDAV sources; detail pages do not block initial rendering or playback.
- Probe status changes now notify the current detail page when a displayed source enters pending or reaches a terminal result, and the page refreshes automatically if the user is still viewing it.
- Source row probe status text now spells out the current stage: waiting for background probe, probing in background, completed, failed, unavailable, or skipped. Episode source rows also display sanitized failure reason text.
- Episode source rows include an `立即探测` button. It runs a force probe for the selected source after verifying the source still belongs to the current Episode, then refreshes the detail view.
- The `立即探测` button is disabled while that source is probing. Detail-page auto probe temporarily disables checked sources during candidate evaluation, then keeps only queued / pending sources disabled and restores skipped sources.
- Detail lazy diagnostics are sanitized and report content kind, source count, candidate count, queued count, skip reasons, protocol counts, limit, and refresh behavior without logging full local paths or full WebDAV URLs.
- Scan-time probe enqueue is now disabled so large scan runs cannot block current detail-page probe work. Probe entry points are detail-page lazy probe and source-row manual probe only.
- This change does not add startup probing, does not queue the full library, and does not change scan identification, Episode default source selection, playback, source deletion, watched / unwatched writes, migration, or database update.

## Phase 4.12d Update

Phase 4.12d adds Episode source reset-to-unidentified, aligned with Movie detail's source reset semantics.

- Episode detail source rows now include `重置为未识别`.
- Episodes already in an unidentified Season keep the same source row visible, but `重置为未识别` is disabled because the source is already in the unidentified review flow.
- Active media probing no longer disables `重置为未识别`; probing only disables probe actions.
- The action confirms before writing and states that real local / WebDAV files are not deleted and Episode metadata / watched / progress are not cleared.
- The TV collection service resets by clearing the current Episode's active video `MediaFile.EpisodeId`, after verifying that `mediaFileId` belongs to the loaded Episode.
- Reset sources stop appearing in Episode detail and Season detail, then return to Other / unidentified item handling because the `MediaFile` row remains active and unbound.
- Resetting the current derived default source causes Episode detail to reload and derive a new default from remaining active sources. If no sources remain, the default source becomes empty.
- Resetting the last source keeps the Episode and Season visible. Episode detail shows `暂无播放源`, the play button is disabled, and the correction placeholder remains available.
- Watch history, subtitle bindings, Episode watched/progress fields, Season metadata, Episode metadata, probe fields, and real files are not cleared by this action.
- Reset diagnostics are sanitized and log identifiers / protocol / remaining count only, without full local paths or full WebDAV URLs.
- This change does not add Episode-level remove-from-library semantics, persistent defaults, watched / unwatched writes, real correction, AI / TMDB correction, scan-rule changes, Watch Insights TV input, TV recommendation input, migration, or database update.

## Phase 4.12d Probe Diagnostics Follow-up

- Probe status text now distinguishes `Success` with no readable technical fields from a normal successful metadata update. Source rows show `已探测（未读取到媒体信息）` when ffprobe completes but duration / resolution / codec / bitrate remain empty.
- Probe lifecycle diagnostics now include cancellation / abandoned-queue records for graceful shutdown and worker exception records for background failures.
- Probe and ignored-file sample diagnostics use extension plus a stable hash fingerprint instead of raw sample file names. Full local paths and full WebDAV URLs remain excluded.
- This follow-up does not change probe queue eligibility, manual probe behavior, lazy detail probe triggering, Episode reset semantics, schema, migrations, or database update.

## Phase 4.12e Update

Phase 4.12e adds a persistent Episode default-source setting aligned with Movie detail.

- `TvEpisode.DefaultMediaFileId` stores the user-selected default source. The field is nullable and uses a `SetNull` foreign key to `MediaFiles`.
- Episode detail source rows now include `设为默认`; the current effective default row shows the existing default badge and the button reads `当前默认`.
- Episode detail top play, Season detail Episode play, and `OpenEpisodeAsync(episodeId)` all resolve the same effective default source through the shared Episode source-selection helper.
- The default-source priority is: persisted Episode default if it is still an active usable source, explicit preferred source for detail queries, accessible local source, recent / in-progress source, WebDAV source, then stable first source.
- Explicit source-row playback still passes `EpisodeId + MediaFileId` and plays that selected source; it is not overridden by the persisted default.
- If the persisted default is reset to unidentified, deleted, unbound, no longer active, or a local file is not accessible, playback falls back to the derived default rule.
- Resetting an Episode source to unidentified clears `TvEpisode.DefaultMediaFileId` when that source was the persisted default, while preserving Episode / Season metadata, watched state, progress, watch history, probe fields, and real files.
- This phase adds a migration but does not execute database update. It does not add watched / unwatched writes, real correction, AI / TMDB matching, scan-rule changes, TV Watch Insights input, TV recommendation input, online subtitles, commit, or push.

## Phase 4.12e-fix Update

Phase 4.12e-fix hardens rescan behavior after `reset to unidentified`.

- Reset-to-unidentified is not a blacklist. A reset source may be considered again on a later scan.
- WebDAV and local scans now run a shared rescan reattach step after TV / Movie identification and before placeholder / orphan grouping.
- Local and WebDAV unchanged active unbound videos are treated as restricted reattach candidates; they are not pushed wholesale through full identification.
- Episode reattach is allowed only for active unbound video or failed Movie placeholder video, safe single-Episode parsing, matched / manually confirmed existing Season context, same source, and same or sibling directory context.
- Reattach appends the file as another Episode playback source and preserves real files, Episode / Season metadata, watched / progress state, watch history, subtitle bindings, and probe fields.
- New scan diagnostics record new, deleted-reappeared, changed, unchanged-unbound, post-process, reattach, and placeholder-fallback counts without raw paths or full WebDAV URLs.
- Movie reattach is logged but not automatically applied in this fix; automatic Movie matching remains deferred until a safer product rule exists.
- Delete-record semantics are unchanged: deleting records clears XFVerse software history, and a later scan treats the file as new or deleted-reappeared.

## Phase 4.12f Update

Phase 4.12f adds Episode detail watched / unwatched controls without changing playback-source or scan semantics.

- Episode detail now exposes a watched toggle beside the primary playback action. The button reads `标记已看`, `取消已看`, or `更新中...` while the operation is running.
- The toggle uses the existing `SetEpisodeWatchedAsync` TV collection service, so recognized Episodes, failed unidentified Episodes, and no-source metadata Episodes share the same state rules as Season detail.
- Toggling an Episode watched state refreshes the current Episode detail and notifies playback / collection listeners so Season detail and library projections reload with the existing Season aggregate progress rules.
- Manual Episode watched / unwatched does not create `WatchHistory`, does not affect Movie watched state, and does not add TV inputs to Watch Insights, Watch Profile, AI recommendations, or recommendation fingerprints.
- This update does not change source lists, playback, default-source selection, manual probe, split-source behavior, correction placeholders, scan rules, migrations, or database update behavior.

## Phase 4.12g Update

Phase 4.12g is the Episode detail regression / polish pass for the completed 4.12 surface.

- Recognized Episode, failed unidentified Episode, no-source Episode, source list, playback, persistent default source, split-source, lazy / manual probe, and watched / unwatched flows were rechecked together.
- Source split UI keeps the newer wording: `从当前集拆分` for Episode detail and `从当前电影拆分` for Movie detail. The operation remains a safe detach from the current container, not a physical file delete.
- Episode detail visibly disables `从当前集拆分` while an Episode player is opening or open, matching the command guard. Probe work continues to disable only probe buttons.
- Movie split diagnostics now describe retained history / progress instead of implying resume state was cleared.
- No real correction flow, scan-strategy change, Watch Insights TV input, recommendation TV input, new migration, database update, commit, or push is part of this pass.

## Phase 4.12h Closeout

Phase 4.12 is closed as the Episode detail and source-management stage.

Completed surface:

- Episode detail exists for recognized Episodes and failed unidentified Episodes.
- Orphan / Other unknown files are carried by the existing failed Movie detail shell.
- Episode detail shows basic Episode / Season / Series metadata, identification status, source count, progress, watched state, and no-source fallback.
- Episode source rows show safe file name, local / WebDAV source type, sanitized location, size / format / duration / resolution / codec / bitrate where available, probe stage, failure text, playback history, and default-source state.
- Top play, source-row play, Season detail Episode play, and `OpenEpisodeAsync(episodeId)` use the shared Episode source-selection path.
- WebDAV-only, local-only, and WebDAV + local mixed Episode sources are supported without changing Movie source behavior.
- `TvEpisode.DefaultMediaFileId` stores the user-selected persistent default source through migration `20260519201559_AddTvEpisodeDefaultMediaFile`; database update is not executed by the agent.
- Source split wording is finalized as `从当前集拆分` for Episode detail and `从当前电影拆分` for Movie detail.
- Source split detaches the source from the current Episode / Movie only. It does not delete real local / WebDAV files and does not clear WatchHistory, progress, subtitle bindings, probe fields, or metadata.
- Failed unidentified Episodes allow `从当前集拆分` whenever at least one active source exists. Single-source unidentified Episodes can be detached so wrongly auto-grouped sources can return to Other; orphan unknown carriers on the Movie detail boundary stay disabled.
- Detail-page lazy probe and source-row manual probe are the active probe entry points. Scan-time probe enqueue remains disabled.
- Episode detail watched / unwatched is supported for recognized, failed unidentified, and no-source Episodes through existing TV collection state logic.

Closed boundaries:

- Phase 4.12 does not implement real correction, AI correction, TMDB candidate search, batch correction, manual Season aggregation, online subtitles, new scan strategy, final UI redesign, TV Watch Insights input, TV recommendation input, database update, commit, or push.
- Phase 4.13 should start from the unified correction entry and batch / manual correction scope.

## Phase 4.12-post Emergency Safety Boundary

- Automatic unknown TV Series / Season reuse now requires strict derived grouping keys and refuses ambiguous existing unknown containers.
- Unknown append now targets an existing unknown Season only when the strict Season grouping key matches; same-directory duplicate-copy multi-source remains supported.
- Special / non-regular TV directories are skipped by automatic unknown append / grouped placeholder persistence and are left for Phase 4.13 manual correction.
- Reusing an unknown Series / Season no longer overwrites its existing display name.
- Recognized Episode reattach remains same-directory only; sibling-directory recognized reattach is still disabled.
- Historical polluted unknown containers are not repaired by this emergency fix.

## Phase 4.13a Update

Phase 4.13a starts the active correction work with a narrow single-source foundation.

- Movie detail and Episode detail now expose a per-source `修正信息` flow.
- The flow supports two target types only: Movie and TV Episode.
- The user selects one source, jumps to the correction interface, searches a TMDB target, and clicks a candidate to apply the correction directly.
- The correction interface shows only the selected target type fields: Movie title / year for Movie, and Series / Season / Episode for TV Episode.
- Apply uses existing transactional Movie / TV manual binding paths. A failed apply rolls back through those transactions.
- Correcting to Movie sets `MediaFile.MovieId` and clears `EpisodeId`.
- Correcting to TV Episode sets `MediaFile.EpisodeId` and clears `MovieId`; TV remains excluded from Watch Insights, Watch Profile, AI recommendations, and recommendation fingerprints.
- The corrected source becomes the target Movie / Episode default source. If it was the previous container's default source, that previous Movie / Episode recalculates its default source with the local-first fallback strategy.
- Target Movie / Episode rows can receive the corrected source as an additional playback source.
- No join-existing unknown Season target, manual Season aggregation, grouped Season correction, batch AI correction, ignore / blacklist, migration, database update, commit, or push is part of this update.

## Phase 4.13a-fix AI Assist Boundary

- Single-source correction AI assist is target-kind constrained.
- When the user selects Movie, AI only generates a Movie search query, searches Movie candidates, and applies only the Movie correction path after candidate click.
- When the user selects TV Episode, AI only generates a TV Series search query, searches TV candidates, and applies only the TV Episode correction path after candidate click.
- AI does not decide Movie vs TV and cannot switch the selected target kind.
- Movie detail and Episode detail expose the same AI assist behavior.
- Detail page load returns to the playback-source tab; the correction tab is entered only after the user chooses a source to correct.
- Same-detail refreshes, including media probe completion refreshes, preserve the current correction tab and selected correction source.
- The correction target selector must not change by mouse-wheel scrolling while the dropdown is closed.
- TV Episode AI assist fills the Series search query and, when available, the Season / Episode number inputs.
- Batch AI correction, manual grouping, join-existing unknown Season, grouped Season correction, schema changes, migrations, and scan-safety gate changes remain out of scope.

## Phase 4.13b Join-existing Unknown Season Boundary

- Single-source correction now supports `加入已有未识别季`.
- The target picker is a modal dialog limited to no-TMDB unknown Seasons and does not show recognized Seasons, Movies, or orphan single files.
- The picker groups Seasons under their unknown Series rows. Expanding a Series reveals Seasons sorted by Season number with separate visual treatment for Series and Season rows.
- The user must provide a positive integer episode number. `0`, negative values, empty input, non-numeric values, SP / OVA / OAD / special mappings, and multi-episode mapping remain out of scope.
- Existing target Episodes receive the corrected source as an additional playback source.
- Missing target Episodes are created one at a time. Intermediate empty Episodes are not created.
- The corrected source becomes the target Episode default source. The old Movie / Episode recalculates default source with the local-first fallback only when the moved source was its default.
- The path preserves the physical source file, probe fields, subtitle bindings, and existing user states. Cross-type user state migration is still not performed.
- Picker diagnostics and UI context hints remain sanitized; full local paths and full WebDAV URLs are not displayed.
- This phase does not add manual Season aggregation, batch AI correction, grouped unknown Season to recognized Season correction, recognized Season target selection, historical data cleanup, schema changes, migrations, or scan-safety changes.
