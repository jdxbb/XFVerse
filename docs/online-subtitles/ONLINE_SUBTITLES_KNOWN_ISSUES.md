# Online Subtitles Known Issues

## Blocker

- No code blocker after Phase 5.1 build validation.
- Database update was intentionally not executed. Runtime use of the new online subtitle settings and bindings requires applying migration `20260525184522_AddOnlineSubtitlesInfrastructure` outside this agent run.
- OpenSubtitles API-key-only, login, quota, and download behavior still require user credential configuration and live testing through the new settings probe.

## Deferred

- Player subtitle menu integration and `Online downloaded subtitles` submenu.
- Online subtitle search dialog.
- Download binding UI and delete-binding UI.
- Download completion auto-switch in the player.
- Subtitle cache clearing UI in software cache management.
- TV Series IMDb ID hydration for stronger Episode searches.
- OpenSubtitles real-result field drift after live contract checks.
- Final local composite ranking and UI match-score display.
- Orphan-cache cleanup policy after bindings are removed.

## Noise

- Official OpenSubtitles docs are served through a JS documentation app, so some audit details rely on public OpenSubtitles API references plus live contract checks.
- Existing scan-time external subtitle binding remains `MediaFile` based by design and is not a blocker for online Movie/Episode bindings.
- Unidentified content can have placeholder carriers in the current app, but Phase 5 treats those subtitle relationships as temporary unless the content is recognized.
