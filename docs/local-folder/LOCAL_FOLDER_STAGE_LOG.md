# Local Folder Stage Log

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
