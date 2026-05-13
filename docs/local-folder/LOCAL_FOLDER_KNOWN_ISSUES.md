# Local Folder Known Issues

## Blocker

- None known for Phase 3.4.

## Deferred

- File system watcher for real-time Local folder changes.
- TV series support.
- Online subtitle search.
- Local source health-check cleanup for files missing outside manual rescan.
- Local path migration for drive-letter changes, removable-disk remapping, or moved folders.
- Automatic background scan.
- Advanced duplicate source merging and multi-version playback-source priority UI.
- Final UI polish.

## Noise

- UI may display local paths entered by the user, but logs, documentation, and stage reports must stay path-redacted.
- SQLite/database rows store Local absolute paths by product decision; this is required for direct Local playback.
- Phase 3.1 intentionally has no folder picker dependency; manual path input is enough for the foundation stage.
- Phase 3.1 does not add a large-directory warning or confirmation dialog.
- Phase 3.2 uses simple scan status text rather than per-file progress.
- Inaccessible Local folders produce a safe failure and keep existing library records.
- Missing Local folders or moved removable drives do not automatically clean existing library records.
- A physically deleted Local file is reported at playback time until the relevant Local folder is rescanned.
- The MVP uses manual Local scans only and does not automatically monitor folders.
- Local seek can still show the normal player buffering state briefly; no special Local seek UI suppression is applied.
- OMDb remains the API configuration name; user-facing rating labels use IMDb for rating-source wording.
- Mixed-source movies intentionally appear in both the `本地` and `网盘` media library filters.
