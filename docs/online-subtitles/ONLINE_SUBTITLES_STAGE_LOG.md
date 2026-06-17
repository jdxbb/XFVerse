# Online Subtitles Stage Log

## 2026-06-17 - Search Dialog Scrollbar Polish

Completed:

- Replaced the online subtitle search result list scrollbar template with the app's modern scrollbar styling and auto-reveal behavior.
- Kept result list selection, virtualization, and search/download behavior unchanged.

Not done:

- No subtitle provider API, binding model, database schema, migration, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: none.
- Deferred: manual search dialog verification is still needed with enough results to force scrolling.
- Noise: none.

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
- 5.4: Software cache UI for subtitle cache, orphan cache cleanup, API/error wording polish, and full regression.

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

## Phase 5.2 - Player Menu Entry And Search Dialog

Completed:

- Added the player subtitle-menu peer submenu `Online downloaded subtitles` beside the existing `None`, embedded, and external subtitle entries.
- The online submenu now exposes `Search online subtitles...`.
- The online submenu reads current Movie / Episode online subtitle bindings through a read-only query path and shows them as non-switching menu rows. If none exist, it shows an empty placeholder.
- Existing embedded subtitle, external subtitle, and `None` switching behavior remains unchanged.
- Opening the online subtitle search dialog pauses active playback and does not auto-resume on close.
- Added a search dialog with the three Phase 5 primary inputs: static OpenSubtitles language dropdown, Movie / TV Episode type dropdown, and editable keyword input.
- The dialog defaults to Chinese (`zh-cn`), infers Movie vs TV Episode from the current playback session, fills an initial safe keyword, and automatically runs the first search on open.
- Movie search can carry IMDb ID, TMDB ID, title, original title, year, and file size where available. The current safe file name is kept as local release / filename ranking context.
- Episode search can carry Series TMDB ID, series title, original name, Season / Episode numbers, episode title, and file size where available. The current safe file name is kept as local release / filename ranking context. Series IMDb ID remains unavailable in the current TV data model.
- Unidentified Movie/orphan playback falls back to Movie search with the safe file name. Unidentified Episode/Season playback falls back to TV Episode search with the safe file name and any available Season / Episode numbers.
- Search uses the Phase 5.1 `OpenSubtitlesClientService` and settings-backed API key / optional token configuration.
- Search results display language, release / file name, download count, rating / votes, hearing-impaired flag, machine / AI translation flags, trusted uploader flag, FPS, upload date, matched feature title/year, and Season / Episode information when returned.
- Added sorting options for composite ranking, download count, rating, upload date, and match score. Composite ranking remains local so current identity, Season / Episode, language, filename/release, download/rating/upload/trust signals can be prioritized.
- Search has loading, empty, unconfigured API, auth/rate-limit/server/network-style error messages without exposing credentials.
- Download quota is not forced in search. The dialog only notes that quota will be reported from download responses when OpenSubtitles provides it.

Not done:

- No subtitle download.
- No cache write from search results.
- No Movie / Episode online subtitle binding write.
- No delete-binding UI.
- No downloaded subtitle auto-switch.
- No subtitle cache management UI.
- No OCR, translation, editor, full-library automatic download, scan-stage integration, `MediaFile` masquerading, or playback-source-level long-term binding.
- No migration, database update, commit, or push.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Follow-up fix:

- OpenSubtitles API key probe now validates the API key without reusing a previously stored bearer token, so an invalid API key is not masked by a still-valid old login token.
- Settings connection test now uses the same `/subtitles` search endpoint shape as the search dialog for the API-key acceptance check, instead of relying on public info or quota endpoints.
- OpenSubtitles search now uses API-key-only authentication and no stored bearer token, matching the Phase 5.2 search-only contract and making invalid API keys fail immediately.
- Settings clears the stored OpenSubtitles token when endpoint, API key, username, or password changes before saving or testing, and clears it on `401` / `403` probe failures.
- Search maps provider `403 Forbidden` responses to a bounded user-facing API-key/access error.
- Settings and search failure messages were tightened so invalid key, forbidden access, login failure, rate limiting, server error, network error, and invalid response are distinguishable to the user.
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors after the fix.

Manual validation scope:

1. Existing `None`, embedded, and external subtitle menu entries still display and switch as before.
2. The player subtitle menu shows the new online downloaded subtitles submenu.
3. The online submenu shows the search action and an empty placeholder when no online bindings exist.
4. Existing online bindings, if present, are listed read-only and do not attempt Phase 5.3 switching.
5. Opening search during playback pauses the player and does not auto-resume after close.
6. The dialog defaults to Chinese and auto-selects Movie or TV Episode from the current playback session.
7. Recognized Movie search auto-fills title/year and carries available IMDb/TMDB metadata.
8. Recognized Episode search auto-fills Series plus Season/Episode and carries available Series TMDB metadata.
9. Unidentified Movie/orphan and unidentified Episode/Season searches use safe file names only.
10. Manual language/type/keyword edits can be searched again without losing the typed keyword.
11. Loading, empty, unconfigured API, auth/rate-limit/server/network failures show bounded user messages.
12. Sorting can switch between composite, downloads, rating, upload date, and match score without crashing on missing fields.

## Phase 5.3 - Download, Binding, Delete, And Auto Switch

Completed:

- Added download actions to online subtitle search results with per-row busy state and bounded download status text.
- Added `OpenSubtitlesClientService.DownloadAsync`, which calls the provider download contract, fetches the returned file link without logging the URL, bounds download size, and carries download quota fields from the provider response.
- Downloaded files are saved through the managed online subtitle cache service, so extension allow-list, zip validation, path traversal protection, hash calculation, and stable cache naming remain centralized.
- Added an online subtitle binding write service on top of the Phase 5.1 table. It upserts Movie / Episode bindings, updates provider metadata, marks bindings used, and soft-deletes bindings without touching cache files.
- Recognized Movie downloads write `OnlineSubtitleBindings.MovieId`; recognized Episode downloads write `OnlineSubtitleBindings.EpisodeId`. No `MediaFileId` binding is added.
- Unidentified Movie / Episode-like playback downloads are cached and loaded only into the current player session as temporary subtitles. They do not create long-term Movie / Episode bindings.
- Duplicate handling first reuses an existing active binding with the same provider file id / subtitle id and available cache file. The upsert path also deduplicates by provider file id, subtitle id, and cache hash for the current Movie / Episode target.
- Download success adds the subtitle to the online subtitle menu and attempts immediate mpv `sub-add` selection without reloading playback.
- The player online subtitle submenu now supports selecting cached online subtitles and deleting bindings. Deleting a binding soft-deletes the binding only and keeps the cache file.
- Deleting the currently selected online subtitle switches to `None` before refreshing the menu.
- Download quota / remaining / reset values are shown when the provider returns them; otherwise the UI states that no displayable quota was returned.
- Download errors distinguish missing file id, unconfigured API, auth / forbidden, rate-limit or quota, server error, network failure, invalid download response, unsupported format, oversized file, empty file, invalid zip, and cache path rejection.

Not done:

- No full-library automatic download.
- No OCR, translation, external subtitle editor, scan-stage integration, `MediaFile` masquerading, or playback-source-level long-term binding.
- No physical subtitle cache deletion from the player menu.
- No subtitle cache management UI; cache clearing remains Phase 5.4.
- No new migration, database update, commit, or push.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

## Phase 5.3b - Persist Unidentified Online Subtitles

Completed:

- Updated `OnlineSubtitleBindings` so exactly one of `MovieId`, `EpisodeId`, or `MediaFileId` is required.
- Added migration `20260525211655_AddOnlineSubtitleMediaFileBinding`.
- Recognized Movie downloads still bind to `MovieId`; recognized Episode downloads still bind to `EpisodeId`.
- Unidentified orphan / unidentified Episode-like playback downloads now bind to the current video `MediaFileId` instead of being session-only temporary subtitles.
- The player online subtitle menu now queries current Movie/Episode bindings and current MediaFile bindings together.
- When the same subtitle appears through both entity-level and MediaFile-level bindings, the list keeps one row and prefers Movie/Episode-level rows.
- Selecting and deleting online subtitle bindings now uses the binding row's own target, so Movie, Episode, and MediaFile bindings can all be switched or deleted correctly.
- Delete binding remains soft-delete only and does not physically delete cache files.
- Delete-record paths for Movie records, Season records, grouped placeholders, and unassociated media files soft-delete affected MediaFile-level online subtitle bindings before clearing software records.
- Remove-from-library / hide-only paths were not changed, so they do not clear MediaFile-level online subtitle bindings.
- Correction / reattach flows were not changed, so MediaFile-level online subtitle bindings are not automatically migrated after a source is corrected.

Not done:

- No automatic migration from MediaFile-level bindings to Movie/Episode-level bindings after correction.
- No subtitle cache management UI.
- No physical subtitle cache deletion.
- No full-library automatic download, OCR, translation, editor, scan-stage integration, database update, commit, or push.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff contains only the 5.3b OnlineSubtitleBinding target expansion.

## Phase 5.4 - Online Subtitle Cache Management And Polish

Completed:

- Added online subtitle cache as a user-visible software-cache category in Settings > General.
- The cache overview now reports total online subtitle cache size and file count.
- The online subtitle cache service now reads active `OnlineSubtitleBindings` and separates referenced cache files from orphan cache files.
- Orphan cleanup deletes only supported subtitle files under the managed online subtitle cache root that are not referenced by active binding `CacheRelativePath` or `CacheHash`.
- Active Movie / Episode / MediaFile online subtitle bindings protect their referenced cache files from physical deletion.
- If binding references cannot be read, cleanup is disabled instead of risking deletion of still-bound subtitle files.
- Delete-binding remains soft-delete only; physical cleanup is handled later by the software-cache orphan cleanup action.
- Download quota and download failure wording was tightened so unknown quota, expired token/API key, forbidden download, rate limit/quota, server, network, and invalid response states are clearer.
- Existing search, download, player subtitle switching, Movie/Episode binding, and MediaFile-level unidentified binding semantics were not changed.

Not done:

- No cache management UI for deleting still-bound subtitle cache files.
- No automatic migration from MediaFile-level bindings to Movie / Episode bindings.
- No full-library automatic download, OCR, translation, editor, scan-stage integration, database update, commit, or push.
- No new migration was added for 5.4.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained limited to the existing 5.1 and 5.3b online-subtitle migrations already present in the worktree; 5.4 added no migration.

## Phase 5.5 - Full Regression And Closeout

Completed:

- Audited Phase 5 documentation against the current implementation for settings, OpenSubtitles client behavior, player subtitle menu, search dialog, download/cache/binding flow, Movie / Episode / MediaFile targets, delete-record boundaries, and software-cache cleanup.
- Confirmed the settings probe uses a real OpenSubtitles subtitle search request for API-key acceptance checks, with username/password login remaining optional.
- Confirmed player search still pauses active playback, uses the static language table, fills safe file-name based queries, and does not upload full local paths or WebDAV URLs.
- Confirmed downloads save through the managed online subtitle cache service, enforce the `.srt` / `.ass` / `.ssa` / `.vtt` allow-list, bound sizes, safe zip extraction, hash naming, and path traversal checks.
- Confirmed recognized Movie downloads bind to `MovieId`, recognized Episode downloads bind to `EpisodeId`, and unidentified playback downloads bind to `MediaFileId`.
- Confirmed the player online subtitle menu merges current Movie / Episode bindings with current MediaFile bindings, can select cached entries, and soft-deletes bindings without physically deleting cache files.
- Confirmed remove-from-library / hide-only paths do not clear online subtitle bindings, while delete-record paths clear affected online subtitle bindings and leave cache cleanup to software-cache management.
- Confirmed Settings > General reports online subtitle cache usage and clears only orphan files not referenced by active `OnlineSubtitleBindings`.
- Fixed one closeout safety issue: binding menu cache-file availability now uses a strict cache-root boundary check before reporting a managed cache file as available.

Not done:

- No new online subtitle feature scope was added.
- No full-library automatic download, OCR, translation, external subtitle editor, scan-stage integration, MediaFile masquerading, still-bound cache deletion, final UI redesign, database update, commit, or push.
- No live OpenSubtitles download regression was executed during closeout to avoid consuming user quota or exposing credentials outside the running app.

Verification:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- No migration was added in Phase 5.5.
- Migration diff remained limited to the already-existing Phase 5 online-subtitle migrations in the repository baseline.
