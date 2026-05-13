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
