# Online Subtitles Known Issues

## Blocker

- No code blocker after Phase 5.4 build validation.
- Database update was intentionally not executed. Runtime use of the new online subtitle settings and bindings requires applying migration `20260525184522_AddOnlineSubtitlesInfrastructure` outside this agent run.
- Phase 5.3b adds migration `20260525211655_AddOnlineSubtitleMediaFileBinding`. Runtime use of unidentified MediaFile-level online subtitle persistence requires applying this migration outside this agent run.

## Deferred

- TV Series IMDb ID hydration for stronger Episode searches.
- OpenSubtitles real-result field drift after live contract checks.
- Search-result pagination beyond the first result page.
- OpenSubtitles live search and download result field drift should be sampled with broader real queries before a final Phase 5.5 closeout.
- Optional cache tooling for deliberately clearing still-bound online subtitle cache files is deferred; Phase 5.4 intentionally clears only orphan cache files.
- Automatic migration from old MediaFile-level online subtitle bindings to Movie / Episode bindings after user correction is intentionally deferred and not part of 5.3b.
- A richer download progress bar is deferred; Phase 5.3 exposes per-row busy state and final status only.

## Noise

- Official OpenSubtitles docs are served through a JS documentation app, so some audit details rely on public OpenSubtitles API references plus live contract checks.
- Existing scan-time external subtitle binding remains `MediaFile` based by design and is not a blocker for online Movie/Episode bindings.
- Unidentified content can have placeholder carriers in the current app, but Phase 5.3b stores unidentified online subtitle bindings on the current video `MediaFile`, not on placeholder Movie / Season / grouped rows.
- Phase 5.3 online subtitle binding deletion is soft-delete only. Physical cache cleanup remains a software-cache responsibility.
- Phase 5.4 orphan cleanup skips still-bound cache files and can leave cache files in place when binding references cannot be read.
- The settings page still has separate save and test actions. Testing uses current input values through a live subtitle search probe, while player search reads the saved configuration.
