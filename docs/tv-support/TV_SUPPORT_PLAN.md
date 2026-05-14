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
- Batch operations act on Movie or Season items. Mixed Movie + Season selections are rejected in the MVP to avoid unclear cross-type semantics.
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

- TV discovery search / ranking UI
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
