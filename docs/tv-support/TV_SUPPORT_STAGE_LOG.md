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
