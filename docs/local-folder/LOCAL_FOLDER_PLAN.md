# Local Folder Source Plan

## Phase 3 Goal

Phase 3 adds Local Folder Source support beside the existing WebDAV media source. The first usable boundary is: manage local media directories, scan local video and subtitle files in a later stage, add them to the media library as Local sources, and play local media files directly without WebDAV or video cache.

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
- `MediaFile` will store files discovered by later scans.
- `ScanTaskLog` will be reused by later scan stages.

No dedicated `LocalMediaFolder` table is introduced in Phase 3.1.

## Migration Decision

Phase 3.1 does not change database schema and does not add a migration. `ProtocolType` is already stored as an integer, so adding a new enum value is a code-level change.

## Privacy Decision

Local absolute paths are allowed in the database because they are needed to resolve and play local media. Logs, docs, stage reports, and diagnostic summaries must not include full local media paths. UI may display a path that the user typed or selected.

## Deferred Boundaries

- Local scanning and ingestion are implemented in Phase 3.2.
- Local playback is Phase 3.3.
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
- Local direct playback, Local source default playback priority, and missing-file playback prompts remain Phase 3.3 work.

## Rating Copy Note

- User-facing rating source labels should show `IMDb` when the value comes from OMDb-returned IMDb fields.
- `OMDb` remains the API integration and settings concept.
- This copy-only rule does not change rating calculations, weighting, caching, or API calls.
