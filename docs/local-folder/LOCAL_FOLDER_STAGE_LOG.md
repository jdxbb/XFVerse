# Local Folder Stage Log

## 2026-06-17 - Scan Log History Snapshot Migration Follow-up

Goal:

- Keep scan history cards from changing their displayed WebDAV BaseUrl / username / scan path after the user edits current scan settings.
- Check whether scan path history had true historical data.

Completed:

- Added formal nullable snapshot columns on `ScanTaskLogs`: `SourceBaseUrlSnapshot`, `SourceUsernameSnapshot`, `ScanPathSnapshot`, and `ScanPathDisplayNameSnapshot`.
- Added migration `20260617073620_AddScanTaskLogHistorySnapshots`.
- The migration backfills snapshot columns from existing `ReasonSummaryJson` snapshots when present, then falls back to the currently related `SourceConnection` / `ScanPath` values.
- New WebDAV scan logs now write BaseUrl, username, scan path, and scan path display name to formal snapshot columns at scan start.
- New Local scan logs now write scan path and display name to formal snapshot columns at scan start.
- WebDAV and Local scan overview read models now prefer the formal snapshot columns, then fall back to current `SourceConnection` / `ScanPath` data, then JSON snapshot compatibility data.
- Scan-log cards now prefer per-log BaseUrl / username values instead of always using the current scan settings.
- Existing JSON snapshot compatibility remains as a fallback, but formal columns are now the primary history source.

Not done:

- No commit or push was added.
- Existing old logs without a previous JSON snapshot cannot recover a prior BaseUrl / username / path if the old value was already overwritten before this migration; they are backfilled with the relation values available during migration.
- No scan matching, media-file creation, local file deletion, WebDAV deletion, or library visibility semantics were changed.

Validation:

- `dotnet build MediaLibrary.sln -m:1 -v:minimal -p:OutDir="%TEMP%\\XFVerseCodexBuildScanLogSnapshotMigration\\"` passed with 0 warnings and 0 errors.
- `dotnet ef database update --project src/MediaLibrary.Core/MediaLibrary.Core.csproj --startup-project src/MediaLibrary.Core/MediaLibrary.Core.csproj --context AppDbContext` succeeded and applied `20260617073620_AddScanTaskLogHistorySnapshots`.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` includes the new migration and updated model snapshot.

Known Issues:

- Blocker: none confirmed by build.
- Deferred: manually run a new WebDAV scan, edit BaseUrl / username, and verify the new history card keeps the original values.
- Deferred: manually run a new Local scan, edit that local scan path, and verify the new history card keeps the original path text.
- Noise: old scan logs created before the snapshot existed can only be backfilled with the best available relation data from migration time.

## Phase 3.1

### Modified Files

- `src/MediaLibrary.Core/Models/Enums/ProtocolType.cs`
- `src/MediaLibrary.Core/Services/Interfaces/ISettingsService.cs`
- `src/MediaLibrary.Core/Services/Implementations/SettingsService.cs`
- `src/MediaLibrary.App/ViewModels/Pages/ScanTasksViewModel.cs`
- `src/MediaLibrary.App/Views/Pages/ScanTasksPage.xaml`

### Added Files

- `docs/local-folder/LOCAL_FOLDER_PLAN.md`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`
- `docs/local-folder/LOCAL_FOLDER_KNOWN_ISSUES.md`

### Deleted Files

- None.

### Added Migration

- None.

### Completed

- Added Local protocol support.
- Added Local source creation through the existing `SourceConnection` model.
- Added Local folder persistence through the existing `ScanPath` model.
- Kept Local folder path handling separate from WebDAV URL and virtual-path handling.
- Added Local folder configuration UI on the scan tasks page.
- Kept WebDAV configuration and scan-path management separate.
- Added Phase 3 documentation skeleton.

### Not Done In This Stage

- No Local scan execution.
- No Local `MediaFile` creation.
- No Local playback.
- No media library source filtering.
- No detail page source display changes.
- No Local directory removal syncing to existing media rows.

### Build Result

- `dotnet build MediaLibrary.sln`: passed with 0 warnings and 0 errors.

### Manual Acceptance Matrix

- Start the application.
- Open the scan tasks page.
- Confirm the WebDAV configuration area still appears.
- Confirm WebDAV scan-path management still appears.
- Confirm the Local folder configuration area appears.
- Add a Local folder configuration.
- Confirm recursive mode is enabled by default.
- Edit display name, path, recursive mode, and enabled state.
- Remove a Local folder configuration.
- Restart the application and confirm Local folder configuration persists.
- Confirm this stage does not scan Local folders.
- Confirm WebDAV save/load behavior does not regress.
- Confirm logs and documentation do not contain full local media paths.
- Run `dotnet build MediaLibrary.sln` and confirm 0 warnings and 0 errors.
- Confirm no migration files were added.

## Phase 3.2 Closeout Fixes

### Modified Files

- `src/MediaLibrary.App/ViewModels/Pages/ScanTasksViewModel.cs`
- `src/MediaLibrary.App/Views/Pages/ScanTasksPage.xaml`
- `docs/local-folder/LOCAL_FOLDER_PLAN.md`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`
- `docs/local-folder/LOCAL_FOLDER_KNOWN_ISSUES.md`

### Added Files

- None.

### Deleted Files

- None.

### Added Migration

- None.

### Completed

- Moved manual scan actions into the scan progress card.
- Split the scan progress actions into WebDAV and Local file scan buttons.
- Kept the Local folder area focused on configuration management only.
- Removed the separate recent Local scan display.
- Unified WebDAV and Local scan records in the scan records card with source text.

### Not Done In This Stage

- No Local playback.
- No Local source default playback priority.
- No missing Local file playback prompt.
- No media library source filtering.
- No detail page source display changes.

### Build Result

- Pending validation in this closeout pass.

### Manual Acceptance Matrix

- Open the scan tasks page.
- Confirm the scan progress card shows separate WebDAV and Local scan actions.
- Confirm the WebDAV action still runs WebDAV scanning.
- Confirm the Local action runs Local folder scanning.
- Confirm the Local folder configuration area still supports add, edit, enable/disable, remove, and recursion changes.
- Confirm there is no separate recent Local scan area.
- Confirm WebDAV and Local scan records appear together in the scan records card.
- Confirm scan records distinguish source as WebDAV or Local.
- Confirm logs and documentation do not include full local media paths.
- Confirm no migration files were added.

## Rating UI Copy Fix

### Modified Files

- `src/MediaLibrary.App/ViewModels/Pages/MovieDetailViewModel.cs`
- `src/MediaLibrary.Core/Services/Implementations/LibraryQueryService.cs`
- `src/MediaLibrary.App/Views/Pages/SettingsPage.xaml`
- `DesignDraft/page-spec/movie-detail-page.md`
- `DesignDraft/DESIGN.md`
- `docs/local-folder/LOCAL_FOLDER_PLAN.md`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`
- `docs/local-folder/LOCAL_FOLDER_KNOWN_ISSUES.md`

### Added Files

- None.

### Deleted Files

- None.

### Added Migration

- None.

### Completed

- Changed user-facing rating source labels from `OMDb` to `IMDb` where they describe the rating source.
- Clarified that OMDb is the API source that returns IMDb rating fields.
- Kept TMDB rating text unchanged.
- Kept OMDb service names, settings field names, API calls, and rating calculations unchanged.

### Build Result

- Pending final validation in this closeout pass.

## Phase 3.2

### Modified Files

- `src/MediaLibrary.Core/Services/Interfaces/ISettingsService.cs`
- `src/MediaLibrary.Core/Services/Implementations/SettingsService.cs`
- `src/MediaLibrary.App/ViewModels/Pages/ScanTasksViewModel.cs`
- `src/MediaLibrary.App/Views/Pages/ScanTasksPage.xaml`
- `src/MediaLibrary.App/Services/AppServiceProvider.cs`
- `docs/local-folder/LOCAL_FOLDER_PLAN.md`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`
- `docs/local-folder/LOCAL_FOLDER_KNOWN_ISSUES.md`

### Added Files

- `src/MediaLibrary.Core/Services/Interfaces/ILocalMediaScanService.cs`
- `src/MediaLibrary.Core/Services/Implementations/LocalMediaScanService.cs`

### Deleted Files

- None.

### Added Migration

- None.

### Completed

- Added a Local scanning service for enabled Local folders.
- Added all-enabled and single-folder Local scan entry points.
- Reused current `MediaFileRules` video/subtitle extension handling.
- Upserted Local video and subtitle files into `MediaFile`.
- Reused movie identification, AI classification, media probe, and subtitle binding services.
- Recorded Local scans with `ScanTaskLog`.
- Skipped hidden and system directories/files.
- Marked missing Local files as `IsDeleted` only after a clean directory scan.
- Kept inaccessible Local folders from clearing existing library records.
- Added overlapping Local folder protection.
- Changed Local folder removal to software-remove related Local `MediaFile` rows without touching physical files.

### Not Done In This Stage

- No Local playback.
- No media library source filter implementation.
- No detail page source display changes.
- No file system watcher.
- No automatic background scan.
- No online subtitle search.
- No new video extension list.

### Build Result

- `dotnet build MediaLibrary.sln`: passed with 0 warnings and 0 errors.

### Manual Acceptance Matrix

- Start the application.
- Open the scan tasks page.
- Confirm the WebDAV scan entry still appears.
- Confirm the Local folder configuration area still appears.
- Add an empty Local folder and scan it.
- Add a Local folder containing supported video files and scan it.
- Add non-video files and confirm they are skipped.
- Toggle recursion and confirm nested folders follow the setting.
- Confirm hidden/system folders are skipped or handled safely.
- Add external subtitles and confirm subtitle binding follows existing rules.
- Repeat a scan and confirm duplicate `MediaFile` rows are not created.
- Move or delete a local file, rescan, and confirm the corresponding Local `MediaFile` is marked deleted.
- Scan an inaccessible Local folder and confirm existing records are not cleared.
- Remove a Local folder configuration and confirm matching Local `MediaFile` rows are software-removed only.
- Confirm WebDAV scanning does not regress.
- Confirm Local playback is not implemented in this stage.
- Confirm media library source filtering is not implemented in this stage.
- Confirm logs/docs/stage reports do not contain full local paths.
- Run `dotnet build MediaLibrary.sln` and confirm 0 warnings and 0 errors.
- Confirm no migration files were added.

## Phase 3.3

### Modified Files

- `src/MediaLibrary.Core/Services/Implementations/PlaybackSourceService.cs`
- `src/MediaLibrary.App/ViewModels/Player/PlayerWindowViewModel.cs`
- `src/MediaLibrary.App/Views/Player/PlayerWindow.xaml.cs`
- `docs/local-folder/LOCAL_FOLDER_PLAN.md`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`
- `docs/local-folder/LOCAL_FOLDER_KNOWN_ISSUES.md`

### Added Files

- None.

### Deleted Files

- None.

### Added Migration

- None.

### Completed

- Added Local playback input generation from `MediaFile.FilePath`.
- Kept Local playback out of WebDAV URL construction.
- Kept Local playback out of WebDAV credentials and authorization headers.
- Kept Local playback out of video cache acquisition.
- Hid WebDAV cache status UI for Local sources.
- Added Local file existence validation before player load.
- Added friendly unavailable-file playback state without deleting or marking records.
- Added playback-time dynamic Local default priority when no explicit source is requested.
- Preserved explicit `preferredMediaFileId` selection.
- Preserved watch history, resume, and automatic watched behavior on the existing identifiers.

### Local Default Strategy

- The player dynamically selects an active playable Local video source before WebDAV when no explicit source is requested.
- `Movie.DefaultMediaFileId` is not updated in this stage.
- If the stored Local default is missing or unavailable, playback falls back to another active playable source.

### Not Done In This Stage

- No media library source filtering.
- No detail page Local/WebDAV source display refinement.
- No complex multi-version playback priority UI.
- No Local file health-check cleanup beyond manual rescan behavior.
- No file system watcher.

### Build Result

- `dotnet build MediaLibrary.sln`: passed with 0 warnings and 0 errors.

### Manual Acceptance Matrix

- Start the application.
- Confirm a Local folder has been scanned into the library.
- Open a movie with only a Local playback source.
- Click play and confirm the Local file plays directly.
- Confirm Local playback does not show WebDAV cache status.
- Confirm Local playback does not require WebDAV credentials.
- Pause and close the player, then reopen and confirm resume works.
- Play enough progress and confirm automatic watched logic still works.
- Confirm watch history records the expected movie and media source.
- For a movie with WebDAV and Local sources, confirm default playback chooses Local when no source is specified.
- If a WebDAV source is explicitly selected, confirm playback honors that selection.
- Temporarily move or remove a Local file and confirm playback shows an unavailable-file message.
- Confirm unavailable Local files are not deleted and not automatically marked `IsDeleted`.
- Confirm WebDAV playback still works.
- Confirm WebDAV cache playback still works.
- Confirm this stage does not add media library source filtering.
- Confirm logs and documentation do not include full local media paths.
- Run `dotnet build MediaLibrary.sln` and confirm 0 warnings and 0 errors.
- Confirm no migration files were added.

## Phase 3.3 Closeout Fixes

### Modified Files

- `src/MediaLibrary.Core/Services/Implementations/LocalMediaScanService.cs`
- `src/MediaLibrary.Core/Services/Implementations/PlaybackSourceService.cs`
- `src/MediaLibrary.App/ViewModels/Player/PlayerWindowViewModel.cs`
- `docs/local-folder/LOCAL_FOLDER_PLAN.md`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`
- `docs/local-folder/LOCAL_FOLDER_KNOWN_ISSUES.md`

### Added Files

- None.

### Deleted Files

- None.

### Added Migration

- None.

### Completed

- Promoted accessible Local video sources to `Movie.DefaultMediaFileId` after Local scan identification.
- Promoted accessible Local video sources during manual or automatic re-identification.
- Aligned detail page source-list default display with the playback effective default source.
- Kept valid stored defaults authoritative, so a user-selected WebDAV default remains selectable as the default.
- Kept playback-time Local priority as a fallback only when no valid stored default exists.
- Preserved explicit source selection.
- Relaxed cross-source resume duration compatibility to 5 minutes or 5%.
- Reverted Local seek buffering overlay suppression; Local seek uses the normal player buffering state.
- Ordered playback source menu data so the actual selected/default source appears first.
- Added source type to the player source button text to disambiguate Local and WebDAV files with the same name.
- Confirmed Local playback diagnostics use `local-file` mode and do not use the WebDAV video cache path.

### Not Done In This Stage

- No media library source filtering.
- No detail page source display refinement.
- No health-check cleanup for Local files missing outside manual rescan.
- No player core rewrite.

### Build Result

- `dotnet build MediaLibrary.sln`: passed with 0 warnings and 0 errors.

### Manual Acceptance Matrix

- Scan a Local source after a WebDAV source has been matched to the same movie and confirm the Local source becomes the stored default.
- Reset a Local source to unrecognized, identify it back into an existing WebDAV movie, and confirm the detail page marks Local as default.
- With both Local and WebDAV sources present, manually set WebDAV as default and confirm detail display and playback default follow WebDAV.
- Play a movie that has both Local and WebDAV sources and confirm no explicit source selection defaults to Local.
- Explicitly select a WebDAV source and confirm it is respected.
- Play progress on one source, switch to the other source with a small duration difference, and confirm resume/progress is shared.
- Seek repeatedly during Local playback and confirm behavior follows the normal player buffering state.
- Open the source menu and confirm the actual selected/default Local source appears first and is checked.
- Confirm startup/opening loading UI can still appear normally.
- Confirm no full local paths, WebDAV URLs, credentials, or tokens are written to docs or stage reports.
- Run `dotnet build MediaLibrary.sln` and confirm 0 warnings and 0 errors.
- Confirm no migration files were added.

## Phase 3.4

### Modified Files

- `src/MediaLibrary.Core/Models/ReadModels/LibraryMovieListItem.cs`
- `src/MediaLibrary.Core/Models/ReadModels/MediaSourceDisplayText.cs`
- `src/MediaLibrary.Core/Services/Implementations/LibraryQueryService.cs`
- `src/MediaLibrary.App/ViewModels/Pages/LibraryMovieItemViewModel.cs`
- `src/MediaLibrary.App/ViewModels/Pages/LibraryViewModel.cs`
- `src/MediaLibrary.App/Views/Pages/LibraryPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/MovieDetailViewModel.cs`
- `src/MediaLibrary.App/Views/Pages/MovieDetailPage.xaml`
- `docs/local-folder/LOCAL_FOLDER_PLAN.md`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`
- `docs/local-folder/LOCAL_FOLDER_KNOWN_ISSUES.md`

### Added Files

- None.

### Deleted Files

- None.

### Added Migration

- None.

### Completed

- Added Local/WebDAV source flags and source summary text to media library list items.
- Made the media library source filter real instead of a placeholder.
- Kept mixed-source movies merged into one movie card/list row.
- Made mixed-source movies appear under both `本地` and `网盘` source filters.
- Added source summary display to media library poster cards and list rows.
- Displayed movie detail source type as `本地` or `网盘`.
- Added source type to the detail page default-source text and source-switch status.
- Kept remove/delete wording explicit that files are not physically deleted.

### Source Filter Semantics

- `全部来源`: all movies with active video sources.
- `本地`: movies with at least one active Local video source.
- `网盘`: movies with at least one active WebDAV video source.
- Mixed Local + WebDAV movies match both source-specific filters.

### Delete / Remove Semantics

- Media library remove marks software records out of the library and does not delete Local physical files or WebDAV files.
- Movie record deletion removes software records and does not delete Local physical files or WebDAV files.

### Not Done In This Stage

- No playback source priority UI.
- No playback logic changes.
- No scanning rule changes.
- No final UI polish.
- No file system watcher.

### Build Result

- `dotnet build MediaLibrary.sln`: passed with 0 warnings and 0 errors.

### Manual Acceptance Matrix

- Start the application.
- Open the media library.
- Confirm `全部来源` shows WebDAV, Local, and mixed-source movies.
- Switch to `本地` and confirm movies with at least one active Local source appear.
- Switch to `网盘` and confirm movies with at least one active WebDAV source appear.
- Confirm a Local-only movie appears under `全部来源` and `本地`, not `网盘`.
- Confirm a WebDAV-only movie appears under `全部来源` and `网盘`, not `本地`.
- Confirm a mixed-source movie appears under all three source filters.
- Confirm media library cards/list rows show `本地`, `网盘`, or `本地 + 网盘`.
- Confirm search, sorting, status filters, and batch operations still work.
- Open a Local-only movie detail page and confirm the source shows `本地`.
- Open a WebDAV-only movie detail page and confirm the source shows `网盘`.
- Open a mixed-source movie detail page and confirm both sources are distinguishable.
- Confirm default playback still follows Phase 3.3 rules.
- Remove or delete library records and confirm no Local physical files or WebDAV files are physically deleted.
- Confirm logs/docs/stage reports do not contain full local media paths.
- Run `dotnet build MediaLibrary.sln` and confirm 0 warnings and 0 errors.
- Confirm no migration files were added.

## Phase 3.5

### Modified Files

- `docs/local-folder/LOCAL_FOLDER_PLAN.md`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`
- `docs/local-folder/LOCAL_FOLDER_KNOWN_ISSUES.md`

### Added Files

- None.

### Deleted Files

- None.

### Added Migration

- None.

### Completed

- Reviewed Phase 3 Local source configuration, scanning, playback, library filtering, and detail source display boundaries.
- Confirmed `ProtocolType.WebDav = 1` and `ProtocolType.Local = 2`.
- Confirmed Phase 3 reuses `SourceConnection`, `ScanPath`, `MediaFile`, and `ScanTaskLog`.
- Confirmed Local scanning reuses `MediaFileRules` video/subtitle classification.
- Confirmed Local scan removal/missing-file handling is software-only and does not delete physical files.
- Confirmed Local playback uses local paths and avoids WebDAV URL construction, WebDAV credentials, and video cache acquisition.
- Confirmed library source filtering uses active video sources and mixed-source movies match both Local and WebDAV filters.
- Confirmed detail page source display uses `本地` / `网盘` source labels.
- Confirmed docs use placeholders and policy wording rather than real local paths, full WebDAV URLs, credentials, tokens, or API keys.

### Not Done In This Stage

- No new feature implementation.
- No TV series support.
- No online subtitle search.
- No file system watcher.
- No advanced duplicate source or multi-version priority UI.
- No final UI visual polish.

### Build Result

- `dotnet build MediaLibrary.sln`: passed with 0 warnings and 0 errors.

### Manual Acceptance Status

- User-confirmed by observation during Phase 3:
  - Local playback works.
  - Local source can become the default after Local scan/re-identification.
  - A manually selected WebDAV default can still be respected.
  - Source menu/default display issues were corrected.
  - Local seek uses the normal player buffering state.
- Pending user confirmation:
  - Full media library source filter matrix.
  - Full detail-page source display matrix.
  - Full Local folder inaccessible/removable-drive matrix.
  - Full subtitle binding matrix across Local and WebDAV mixed sources.

### Suggested Commit Message

- `feat: add local folder media source support`

## Scan Task UI Follow-up

### Modified Files

- `src/MediaLibrary.App/Services/Implementations/ScanPathPickerService.cs`
- `src/MediaLibrary.App/ViewModels/Pages/ScanTasksViewModel.cs`
- `src/MediaLibrary.App/Views/Pages/ScanTasksPage.xaml`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`

### Added Files

- None.

### Deleted Files

- None.

### Added Migration

- None.

### Completed

- Kept Local folder overlap protection: a selected folder is still skipped when it has a parent/child containment relationship with an existing Local scan path.
- Moved the Local configuration status text into the card header, immediately to the left of the path picker button.
- Made skipped Local folder additions show a compact reason such as duplicate path, containment relationship, or invalid path format.
- Combined `FolderNames` and `FolderName` from the Windows folder picker so a valid returned selection is not lost.
- Changed the Local path list to two equal responsive columns with a vertical divider between the left and right path boxes.
- Reduced scan metric card width, changed the scanned icon to a centered check mark, and moved the third metric column left to leave more room before scan action buttons.

### Not Done In This Stage

- No change to Local folder overlap scanning semantics.
- No WebDAV picker behavior changes.
- No database schema change.
- No migration.

### Build Result

- `dotnet build MediaLibrary.sln`: passed with 0 warnings and 0 errors.

### Manual Acceptance Matrix

- Open the scan tasks page.
- In Local configuration, confirm the status text appears left of `选择路径`.
- Select a new Local folder and confirm the list refreshes.
- Select an already configured Local folder and confirm the status reports a duplicate reason.
- Select a child or parent folder of an existing Local folder and confirm the status reports a containment reason.
- Confirm the skipped containment folder is not added to the list.
- Confirm long Local path names truncate with tooltip rather than stretching the row.
- Confirm Local paths render as two equal-width columns.
- Confirm the vertical divider appears between the two Local path columns.
- Collapse the navigation bar and confirm both Local path columns expand equally.
- Confirm the scan progress `已扫描` metric uses a vertically centered check icon.
- Confirm scan metric cards are narrower and leave a larger gap before the scan action buttons on wider layouts.

### Known Issues

- Blocker: None.
- Deferred: WebDAV path picker had no concrete follow-up item in this pass.
- Noise: Full visual QA still needs in-app confirmation on the user's real display scale.

## Scan Task UI Follow-up 2

### Completed

- Local path add status now keeps the full reason in tooltip while showing a shortened display value for very long directory names.
- The containment-skip semantic remains explicit in the status message; long private path names are truncated only for the visible header text.
- WebDAV configuration card now has a vertical divider between the two equal-width path/config boxes.
- Local path cards stay top-aligned inside the two-column list to avoid large vertical gaps when only a few paths are configured.
- Scan progress metric icons now share the same centered vector-icon structure, and the gap before the right-side scan buttons was increased.
- WebDAV path picker gained compact dialog styling, icon-only parent/forward/refresh controls, shorter directory rows, and two evenly distributed action buttons.

### Not Done

- No Local folder overlap scanning semantic was changed.
- No database schema, migration, database update, commit, or push was added.

### Verification

- `dotnet build MediaLibrary.sln`: passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`: no output.

### Known Issues

- Blocker: None.
- Deferred: Manual visual QA is still needed on the real scan page for sidebar collapsed/expanded widths.
- Noise: Full private Local/WebDAV paths are not recorded in documentation.

## Scan Task Picker And Progress Alignment Follow-up

### Completed

- Local configuration status display now truncates both long status segments and the whole visible header value; the full reason remains available through the tooltip.
- WebDAV path picker now uses a single preference-style glass panel on a transparent borderless window, avoiding the previous double rounded-border look.
- WebDAV picker close action uses the Segoe MDL2 close glyph instead of a text fallback.
- WebDAV directory loading now shows a centered spinner and loading text inside the directory content area.
- WebDAV picker forward navigation now keeps a stack so consecutive parent navigation can be undone with consecutive forward clicks until the user switches into a selected directory.
- WebDAV directory list height was reduced, uses a modern auto-reveal scrollbar, and supports mouse-wheel scrolling while the pointer is over the list.
- WebDAV picker footer actions now use auto-width buttons distributed across two equal columns.
- Scan progress actions now sit between equal flexible gaps after the fixed metric-card cluster, so the gap from the third metric card to the buttons matches the gap from the buttons to the progress card right edge across sidebar states.

### Not Done

- No scan recognition, overlap, deletion, or media-library semantics were changed.
- No database schema, migration, database update, commit, or push was added.

### Verification

- `dotnet build MediaLibrary.sln -p:BaseOutputPath=<temp-build-output>` passed with 0 warnings and 0 errors.

### Known Issues

- Blocker: None.
- Deferred: Manual visual QA is still needed in the real WPF window for exact sidebar collapsed/expanded spacing and WebDAV row density.
- Noise: Existing adjacent scan-task and UI follow-up changes remain in the working tree.
