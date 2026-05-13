# Poster Cache Phase 1 Known Issues

## Deferred To Phase 2

- Poster cache capacity limit.
- Poster cache size statistics.
- Unified software cache management UI.
- Manual cache cleanup entry.
- File-level failed-download markers that survive app restart.
- Bulk refresh and full cache rebuild.

## Phase 1 Limitations

- Failed-download cooldown is in-memory only and resets when the app restarts.
- A changed remote URL, including query string changes, creates a different hash and cache file.
- First load still depends on network availability until a poster has been cached.
- Download failure falls back to the original remote URL or existing placeholder behavior.
- Phase 1 intentionally does not write poster cache diagnostic logs, to avoid leaking remote poster URLs or query strings.
