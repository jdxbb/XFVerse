# Online Subtitles Stage Log

## Phase 5.0a Audit Summary

Completed:

- Audited the player subtitle menu. The current menu is built in the player window code and already separates `None`, embedded subtitles, and external subtitles. A third online submenu can be added as a peer to the embedded and external groups.
- Audited subtitle switching. The current mpv path can dynamically add external subtitles through `sub-add` and select them immediately without reloading playback.
- Audited existing subtitle binding. `SubtitleBinding` is source-level (`MediaFile` to subtitle `MediaFile`) and scan-owned. It is not suitable as the primary online subtitle binding table.
- Audited cache management. Software cache management currently covers poster cache and TMDB/OMDb metadata cache. Subtitle cache is deferred and needs a new managed category.
- Audited settings. The API settings tab currently contains TMDB, OMDb, and AI cards. Online subtitles should be added as another API card.
- Audited metadata availability. Recognized Movie can provide title, original title, year, TMDB ID, IMDb ID, and safe source file name. Recognized Episode can provide series/season/episode fields and safe file name, with Series IMDb ID still requiring hydration or live lookup.
- Audited unidentified playback semantics. Unidentified content may have placeholder Movie/Episode carriers but must still use temporary subtitle relationships, not long-term online bindings.
- Audited OpenSubtitles API docs and current public references. Search, login, download, quota-from-download-response, static language codes, and result display fields are viable enough for infrastructure work, but live contract checks are required.

Not done:

- No player menu implementation.
- No search dialog.
- No download binding UI.
- No database update.
- No commit or push.

## Phase 5.1 Goal And Scope

Phase 5.1 adds the online subtitle infrastructure only:

- Create a dedicated online subtitle binding entity and migration.
- Add a managed subtitle cache directory and base cache service.
- Add an OpenSubtitles client with API-key-only mode, optional login mode, search contract objects, download contract-check method, quota probing, static language list, and sanitized error handling.
- Add an Online Subtitles card to the Settings API tab.
- Save API key, optional username/password, endpoint, enable flag, and default language.
- Protect password and token and never log raw credentials.
- Keep existing embedded subtitles, scanned external subtitles, and scan-time `SubtitleBinding` behavior unchanged.

Out of scope for 5.1:

- No player subtitle menu change.
- No online search dialog.
- No player-side downloaded subtitle binding UI.
- No download-after-select auto switch.
- No delete-binding UI.
- No full-library automatic download.
- No OCR, translation, or subtitle editor.
- No scan-stage integration.

## Later Stage Todo

- 5.2: Player subtitle menu entry, search dialog, pause-on-open, auto search input fill, result list, sorting, and error/quota display.
- 5.3: Download to subtitle cache, create Movie/Episode online binding, temporary unidentified subtitle loading, duplicate handling, player list insertion, and automatic subtitle switch.
- 5.4: Software cache UI for subtitle cache, orphan cache cleanup, final API behavior documentation, and full regression.

## Phase 5.1 - Online Subtitle Infrastructure

Completed:

- Added dedicated `OnlineSubtitleBinding` infrastructure for provider-backed online subtitle files.
- Added migration `20260525184522_AddOnlineSubtitlesInfrastructure`.
- The migration adds OpenSubtitles configuration columns to `ApplicationSettings` and creates `OnlineSubtitleBindings`.
- `OnlineSubtitleBindings` targets Movie or Episode through nullable `MovieId` / `EpisodeId` with a check constraint that requires exactly one target.
- Online subtitle bindings do not use `MediaFileId` and do not reuse scan-owned `SubtitleBinding`.
- Added provider metadata fields: provider subtitle id, provider file id, language code/name, display/release/file name, cache relative path, cache hash, format/extension, downloads, rating/votes, hearing-impaired, machine-translated, AI-translated, trusted uploader, FPS, upload time, timestamps, soft delete flag, and metadata JSON.
- Added unique indexes for Movie/Episode plus provider file id and active/deleted state, plus lookup indexes for subtitle id, cache hash, deleted state, and last-used time.
- Added managed online subtitle cache root through `AppPaths.GetOnlineSubtitleCacheDirectory()`.
- Added `IOnlineSubtitleCacheService` / `OnlineSubtitleCacheService`.
- Subtitle cache saves only `.srt`, `.ass`, `.ssa`, and `.vtt`.
- Subtitle cache write path sanitizes provider/file tokens, hashes content, uses deterministic provider-fileId-hash names, rejects empty/oversized files, validates zip entries, blocks path traversal, and keeps files inside the managed cache root.
- Added base subtitle cache usage and clear methods for later software-cache UI wiring.
- Added `IOpenSubtitlesClientService` / `OpenSubtitlesClientService`.
- OpenSubtitles client supports default endpoint override, `Api-Key` header, `User-Agent`, API-key-only probe/search, optional username/password login, bearer token use, quota probe attempt, search request parameter model, download contract-check method, and sanitized error classification.
- Added a static OpenSubtitles language list generated from the live `/infos/languages` response. Default Chinese code is `zh-cn`.
- Added settings API card for Online Subtitles with endpoint, API key, optional username/password, enable switch, static language dropdown, save, and test/probe actions.
- Password and token are persisted through the existing protected-secret helper. API key follows the existing API settings storage pattern, and logs/status messages avoid raw credentials.
- The settings probe saves a newly returned login token and clears the stored token when an authenticated probe returns Unauthorized.
- Added DI registration for OpenSubtitles client and subtitle cache service.

Live contract check:

- `/infos/languages` was checked without credentials and returned OpenSubtitles language codes including `zh-cn`, `zh-tw`, and `ze`.
- API-key-only search, login, download contract, and quota probing were not executed because this phase does not run database update and no OpenSubtitles credentials were configured through the new settings card in the running app.
- The settings test button now provides the live contract-check path once the user configures the API key and optional username/password.

Not done:

- No player subtitle menu change.
- No online search dialog.
- No download binding UI.
- No download-after-select auto switch.
- No delete-binding UI.
- No full-library automatic download.
- No OCR, translation, subtitle editor, scan-stage integration, or `MediaFile` masquerading.
- No database update.
- No commit or push.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors after implementation.
