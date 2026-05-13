# Local Folder Known Issues

## Blocker

- None known for Phase 3.1.

## Deferred

- Local playback.
- Real library source filtering.
- Detail page Local/WebDAV source display refinement.
- Local source priority in playback.
- File system watcher.
- Automatic background scan.
- Advanced duplicate source merging.

## Noise

- UI may display local paths entered by the user, but logs, documentation, and stage reports must stay path-redacted.
- Phase 3.1 intentionally has no folder picker dependency; manual path input is enough for the foundation stage.
- Phase 3.1 does not add a large-directory warning or confirmation dialog.
- Phase 3.2 uses simple scan status text rather than per-file progress.
- Inaccessible Local folders produce a safe failure and keep existing library records.
