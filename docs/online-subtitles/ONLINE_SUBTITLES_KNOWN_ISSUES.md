# Online Subtitles Known Issues

## Blocker

- No code blocker after Phase 5.2 build validation.
- Database update was intentionally not executed. Runtime use of the new online subtitle settings and bindings requires applying migration `20260525184522_AddOnlineSubtitlesInfrastructure` outside this agent run.

## Deferred

- Download binding UI and delete-binding UI.
- Download completion auto-switch in the player.
- Subtitle cache clearing UI in software cache management.
- TV Series IMDb ID hydration for stronger Episode searches.
- OpenSubtitles real-result field drift after live contract checks.
- Search-result pagination beyond the first result page.
- Download quota display from real download responses.
- OpenSubtitles live search result field drift should be sampled with real queries before Phase 5.3 final UI decisions.
- Click-to-switch for existing downloaded online bindings in the player menu.
- Orphan-cache cleanup policy after bindings are removed.

## Noise

- Official OpenSubtitles docs are served through a JS documentation app, so some audit details rely on public OpenSubtitles API references plus live contract checks.
- Existing scan-time external subtitle binding remains `MediaFile` based by design and is not a blocker for online Movie/Episode bindings.
- Unidentified content can have placeholder carriers in the current app, but Phase 5 treats those subtitle relationships as temporary unless the content is recognized.
- The Phase 5.2 online binding menu list is read-only. Actual downloaded-subtitle switching, deletion, and binding lifecycle belong to Phase 5.3.
- The settings page still has separate save and test actions. Testing uses current input values through a live subtitle search probe, while player search reads the saved configuration.
