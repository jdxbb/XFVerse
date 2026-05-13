# Local Folder Source Plan

## Phase 3 Goal

Phase 3 adds Local Folder Source support beside the existing WebDAV media source. The finished MVP boundary is: manage local media directories, scan local video and subtitle files, add them to the media library as Local sources, play local media files directly without WebDAV or video cache, filter the library by source, and show Local/WebDAV source identity in the library and detail pages.

## Stage Breakdown

- Phase 3.1: Local source and local folder configuration foundation.
- Phase 3.2: Local folder scan, local `MediaFile` upsert, missing-file handling, and subtitle binding reuse.
- Phase 3.3: Local file playback integration.
- Phase 3.4: Real source filtering and source display in library/detail views.
- Phase 3.5: Regression, documentation, and known issues closeout.

## Phase 3.1 Scope

Phase 3.1 only lets the application express Local media sources and manage local folder configuration from the scan tasks page.

Included:

- Add `ProtocolType.Local = 2` while keeping `ProtocolType.WebDav = 1`.
- Reuse `SourceConnection` for the Local source.
- Reuse `ScanPath` for Local folders.
- Use a fixed non-sensitive Local `SourceConnection.BaseUrl` sentinel.
- Save Local folder paths in `ScanPath.Path`.
- Default Local folders to recursive scanning.
- Keep Local folder persistence separate from WebDAV URL and virtual-path validation.
- Add Local folder configuration UI on the scan tasks page.

Not included:

- Local folder scanning.
- Local `MediaFile` creation.
- Local file playback.
- Media library source filtering.
- Detail page source display changes.
- Local folder removal syncing existing `MediaFile` rows.

## Data Model Decision

Phase 3 reuses the existing source model:

- `SourceConnection` identifies a source provider.
- `ScanPath` stores source-specific scan roots.
- `MediaFile` stores files discovered by WebDAV and Local scans.
- `ScanTaskLog` is reused for WebDAV and Local scan records.

No dedicated `LocalMediaFolder` table is introduced in Phase 3. Local support is represented by `ProtocolType.Local = 2`, a Local `SourceConnection`, Local `ScanPath` rows, and Local `MediaFile` rows.

## Migration Decision

Phase 3 does not change database schema and does not add a migration. `ProtocolType` is already stored as an integer, so adding a new enum value is a code-level change.

## Privacy Decision

Local absolute paths are allowed in the database because they are needed to resolve and play local media. Logs, docs, stage reports, and diagnostic summaries must not include full local media paths. UI may display a path that the user typed or selected.

## Deferred Boundaries

- Local scanning and ingestion are implemented in Phase 3.2.
- Local playback is implemented in Phase 3.3.
- Real source filtering is Phase 3.4.
- Final UI polish is outside the Local Folder MVP.

## Phase 3.2 Scope

Phase 3.2 scans configured Local folders manually from the scan tasks page and writes local video/subtitle files into the existing `MediaFile` table.

Included:

- Add `ILocalMediaScanService` and `LocalMediaScanService`.
- Scan all enabled Local folders or one selected Local folder.
- Respect `ScanPath.IsRecursive`.
- Reuse the same `MediaFileRules` video/subtitle extension rules used by WebDAV scanning.
- Skip hidden and system directories/files.
- Upsert Local `MediaFile` rows by the existing `(SourceConnectionId, FilePath)` uniqueness model.
- Store Local file absolute paths in `MediaFile.FilePath`.
- Store `RemoteUri = null` for Local files.
- Mark missing Local files as `IsDeleted = true` only after a directory scan completes without access errors.
- Reuse movie identification, AI classification, media probe, and subtitle binding services.
- Reuse `ScanTaskLog` for Local scan records.
- Remove a Local folder configuration by marking its Local `MediaFile` rows as deleted, then removing the configuration.
- Prevent overlapping Local folder configuration under the same Local source.

Not included:

- Local file playback.
- Source filtering in the media library.
- Detail page source display changes.
- File system watching.
- Online subtitle search.
- New video extension lists.

## Phase 3.2 Privacy Notes

UI may display configured Local folder paths. Logs, docs, and stage reports must use folder display names, counts, exception types, or placeholders instead of full local paths.

## Phase 3.2 Closeout UI Alignment

- The scan progress card is the single manual scan entry area.
- The scan progress card provides separate actions for WebDAV scanning and Local file scanning.
- Local folder configuration is limited to add, edit, enable/disable, remove, and recursive options.
- WebDAV and Local scan records are shown together in the scan records card, with source text such as `网盘` or `本地`.
- Local direct playback, Local source default playback priority, and missing-file playback prompts are handled in Phase 3.3.

## Phase 3.3 Scope

Phase 3.3 lets Local `MediaFile` rows play directly from their local file path.

Included:

- `PlaybackSourceService` builds Local playback input from `MediaFile.FilePath`.
- Local playback does not build WebDAV URLs.
- Local playback does not attach WebDAV username, password, or authorization headers.
- Local playback bypasses the video cache acquire path and hides WebDAV cache status UI for Local sources.
- Playback checks that a Local file exists before loading it into the player.
- Missing Local files show a friendly unavailable-file state and are not automatically deleted or marked `IsDeleted`.
- When no explicit playback source is requested, active playable Local video sources are preferred over WebDAV sources.
- After Local scan identification, accessible Local video sources are promoted to `Movie.DefaultMediaFileId` so rescanning a Local source can restore it as the stored default source.
- Manual or automatic re-identification also promotes an accessible Local video source, so a previously stored WebDAV default does not keep the detail page out of sync with playback.
- Explicitly requested `preferredMediaFileId` still wins over the Local default rule.
- Continue watching, watch history, and automatic watched state use the existing `MovieId` / `MediaFileId` flow, with cross-source resume allowed when durations differ by up to 5 minutes or 5%.
- Local seek may still show the normal player buffering state briefly; no special Local seek UI suppression is applied.

Default source decision:

- Phase 3.3 uses both stored and playback-time Local priority.
- Local scan updates `Movie.DefaultMediaFileId` to an accessible Local video source after identification.
- Detail page source display resolves the same effective Local default used by playback.
- Playback and detail display respect a valid stored default source, including a user-selected WebDAV default.
- If no valid stored default exists, playback and detail display prefer an active playable Local source.
- If the stored default Local source is unavailable, playback falls back to another active playable source.
- Explicit source selection is respected and does not get forced back to Local.

Not included:

- Media library source filtering.
- Detail page source display refinement.
- Complex multi-version priority UI.
- Local file health-check cleanup outside manual rescan.

## Phase 3.4 Scope

Phase 3.4 turns source filtering and source display into real Local/WebDAV behavior in the media library and movie detail views.

Included:

- Media library source filters are real:
  - `全部来源`: movies with any active video source.
  - `本地`: movies with at least one active Local video source.
  - `网盘`: movies with at least one active WebDAV video source.
- Movies with both Local and WebDAV sources appear in both `本地` and `网盘` filters while still rendering as one movie row/card.
- Media library cards and list rows show a source summary:
  - `本地`
  - `网盘`
  - `本地 + 网盘`
- Movie detail playback sources display source type as `本地` or `网盘`.
- Default source display includes the source type so Local/WebDAV sources with the same file name are easier to distinguish.
- Remove/delete wording continues to state that software operations do not delete Local physical files or WebDAV files.

Not included:

- No playback source priority UI.
- No playback logic changes.
- No scanning rule changes.
- No final UI polish.

Privacy notes:

- UI may show user-configured local paths in the detail page source list.
- Docs, logs, and stage reports must not include full local media paths, full WebDAV URLs, credentials, or tokens.

## Delete And Remove Semantics

- Removing a movie from the media library marks software records as removed and does not delete Local physical files or WebDAV files.
- Deleting a movie record removes software records and does not delete Local physical files or WebDAV files.
- Removing a Local folder configuration marks related Local `MediaFile` rows as deleted in software and does not delete physical files.
- Missing Local files are only marked `IsDeleted = true` after a successful manual rescan of the relevant Local folder.
- If a Local folder is inaccessible, existing library records are kept.

## Out Of Scope

- TV series support.
- Online subtitle search.
- File system watching.
- Automatic background scanning.
- Advanced duplicate source merging.
- Complex multi-source priority UI.
- Local path migration for drive-letter or removable-disk remapping.
- Final UI visual polish.

## Phase 3.5 Scope

Phase 3.5 closes the Local Folder Source work with regression review, documentation cleanup, build validation, migration checks, and commit/push readiness.

Included:

- Audit Local source configuration, scanning, playback, media library source filtering, and detail source display boundaries.
- Confirm no schema or migration changes were introduced.
- Confirm docs and reports do not contain full local media paths, WebDAV URLs, credentials, tokens, or API keys.
- Record known deferred work and acceptance status.
- Build and Git checks before committing.

Not included:

- New Local Folder Source functionality.
- Watch Insights or recommendation changes.
- Discovery, poster cache, software cache, or video cache feature changes.

## Rating Copy Note

- User-facing rating source labels should show `IMDb` when the value comes from OMDb-returned IMDb fields.
- `OMDb` remains the API integration and settings concept.
- This copy-only rule does not change rating calculations, weighting, caching, or API calls.
