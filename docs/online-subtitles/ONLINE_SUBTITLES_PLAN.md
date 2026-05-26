# Online Subtitles Plan

## Phase 5 Goal

Phase 5 adds online subtitle search and download support centered on the existing player subtitle menu. The feature must keep existing embedded subtitle and scanned external subtitle behavior intact, keep scan-time `SubtitleBinding` semantics unchanged, and store online downloaded subtitles as managed software-cache artifacts instead of media-library `MediaFile` rows.

## Confirmed Product Semantics

- The online subtitle entry lives in the player subtitle menu.
- The subtitle menu adds an `Online downloaded subtitles` submenu beside `Embedded subtitles` and `External subtitles`.
- The online submenu shows subtitles already downloaded and bound to the current Movie / Episode, and exposes delete-binding actions.
- The online search dialog is opened by a `Search online subtitles` action from the subtitle menu or online submenu; it is not the submenu itself.
- Opening the search dialog pauses playback automatically when playback is active.
- The search dialog has three primary inputs: language dropdown, type dropdown, and search keyword input.
- The language dropdown uses a static OpenSubtitles language list. It does not dynamically request `/infos/languages`.
- The default language is Chinese, using the OpenSubtitles official language code mapping.
- The type dropdown supports Movie and TV Episode. The default is inferred from the current playback item and can be changed by the user.
- Recognized Movie search prefers IMDb ID, TMDB ID, title, original title, year, and the current safe playback file name.
- Recognized Episode search prefers Series IMDb/TMDB ID, series name, original name, season number, episode number, episode title, and the current safe playback file name.
- If OpenSubtitles ID parameters are unsupported or unsuitable, search falls back to title or series title plus season and episode numbers.
- The current safe playback file name is always kept as a release / filename hint, even when IMDb/TMDB identifiers are used.
- Phase 5 first-pass filename handling is lightweight only: strip path, strip extension, length-limit, and sanitize. It does not do complex keyword extraction.
- Full local paths and full WebDAV URLs must never be uploaded.
- Unidentified orphan playback defaults to Movie search.
- Episodes inside unidentified episodes / unidentified seasons default to TV Episode search.
- Unidentified orphan / unidentified Episode-like playback may search and download subtitles, but it must not create long-term Movie / Episode online subtitle bindings.
- Unidentified playback online subtitles bind long term to the current video `MediaFile` only. They do not bind to unidentified Movie placeholders, unknown Seasons, or grouped placeholders.
- MediaFile-level online subtitles survive closing the player and are shown again when the same playback source is opened.
- If that playback source is later corrected to a recognized Movie or Episode, the old MediaFile-level binding is not automatically migrated. New downloads after correction bind to the recognized Movie / Episode target.
- A recognized current playback source may show both Movie/Episode-level online subtitles and current MediaFile-level online subtitles.
- Downloaded subtitles for recognized content bind to Movie / Episode, not to a concrete playback source.
- Movie subtitles bind to Movie.
- Episode subtitles bind to Episode.
- Playback sources are used to determine the active Movie / Episode. `MediaFile` is also allowed as the fallback long-term target for unidentified playback and as a source-following legacy binding after later correction.
- One Movie / Episode can bind multiple online subtitles.
- A successful download saves the file into the subtitle cache, adds it to the player subtitle list, and switches to the downloaded subtitle.
- Deduplication should prefer OpenSubtitles file id, subtitle id, cache file hash, and Movie/Episode/MediaFile binding relationship where available.
- Deleting a binding is supported, but the player menu must not physically delete cache files.
- Physical subtitle cache deletion belongs to software cache management.
- Settings > API configuration adds an Online Subtitles card.
- OpenSubtitles API key is required for configuration.
- Username/password are optional and must not block API-key-only mode.
- API-key-only mode can search and attempt download; quota and permission behavior must be determined by live contract checks.
- If username/password are configured, the app attempts login and uses the returned token.
- Password and token use protected storage and must never be logged in plain text.
- API key may follow the existing API configuration storage pattern, but logs must mask it.
- Supported online subtitle formats are `.srt`, `.ass`, `.ssa`, and `.vtt`.
- Default result ranking is a local composite of identity match, season/episode match, language, filename/release match, downloads, rating, and upload time.
- Sorting options are composite ranking, downloads, rating, upload time, and match score, bounded by fields returned by OpenSubtitles.
- Result rows show language, release / file name, download count, rating, hearing-impaired flag, machine / AI translation flags, trusted uploader, FPS, upload time, and matched movie / episode labels when available.
- Download quota should be shown when OpenSubtitles exposes it; otherwise the app reports quota from download responses.
- Subtitle cache is part of software cache management.

## Explicit Non-Goals

- No full-library automatic subtitle download.
- No OCR.
- No subtitle translation.
- No external subtitle editor.
- No scan-stage integration.
- No online subtitle rows masquerading as `MediaFile`.
- No playback-source-level long-term online subtitle binding for recognized Movie / Episode downloads. MediaFile-level binding is allowed only for unidentified fallback and source-following legacy subtitles.

## Suggested Stage Split

- 5.1 Online subtitle infrastructure: model, migration, subtitle cache service, OpenSubtitles client, settings card, credential protection, and live contract check plumbing.
- 5.2 Player menu plus search dialog: add menu entry, pause-on-open, auto-fill search inputs, static language dropdown, result list, sorting, error and quota display.
- 5.3 Download / bind / delete binding / auto switch: save downloaded files, create Movie/Episode bindings, temporary unidentified playback subtitles, duplicate checks, add to player list, and select downloaded subtitle.
- 5.3b Unidentified persistence: add MediaFile as the third online-subtitle target, bind unidentified downloads to the current MediaFile, merge Movie/Episode and MediaFile bindings in the player menu, and keep correction from automatically migrating MediaFile-level bindings.
- 5.4 Cache management and polish: expose subtitle cache usage and orphan-cache cleanup in software cache management, keep bound cache files protected, polish download quota/error wording, and run player/settings/cache regression.

## Current Implementation Status

- 5.1 is implemented in the current codebase: dedicated online subtitle binding model/migration, managed subtitle cache service, OpenSubtitles client, static language list, and Settings API card.
- 5.2 is implemented as search-only UI: the player subtitle menu has the online downloaded subtitles submenu, the search action pauses playback and opens the search dialog, and results can be searched and sorted through OpenSubtitles.
- 5.3 is implemented: search results can download subtitles, save them through the managed online subtitle cache, upsert Movie / Episode bindings for recognized playback, refresh the player online subtitle menu, select cached online subtitles, soft-delete bindings, and auto-switch after successful download.
- 5.3b changes unidentified playback from session-only temporary subtitles to MediaFile-level persistent online subtitle bindings through migration `20260525211655_AddOnlineSubtitleMediaFileBinding`.
- 5.4 is implemented: Settings > General software-cache management now includes online subtitle cache usage, bound/orphan breakdown, and an orphan-cache cleanup action that refuses to delete files referenced by active `OnlineSubtitleBindings`.

## Data Safety, Credentials, And Privacy

- Do not upload full local paths or full WebDAV URLs.
- Only use a sanitized safe file name as release / filename hint.
- Mask API key, password, and bearer token in logs and user-visible failures.
- Store password and token through protected storage. The current project-level `SecretProtector` is the minimum local protection mechanism already used for WebDAV passwords.
- Store downloaded subtitles only under the managed subtitle cache directory.
- Enforce extension allow-list, size limit, safe file names, safe zip extraction, and path traversal checks.
- Deleting bindings must not delete local media files, WebDAV media files, or subtitle cache files.
- Software-cache cleanup may physically delete only orphaned files under the online subtitle cache root. Active Movie / Episode / MediaFile `OnlineSubtitleBinding` rows protect their `CacheRelativePath` and `CacheHash` from deletion.
- Online subtitles must not change scan rules, `SubtitleBindingService.RebuildBindingsAsync`, or the existing scan-time external subtitle binding table.

## OpenSubtitles API Audit Summary

- Search endpoint: `GET /api/v1/subtitles`.
- Download endpoint: `POST /api/v1/download`, keyed by `file_id`.
- Login endpoint: `POST /api/v1/login`, returning a token when username/password are supplied.
- Requests require `Api-Key` and `User-Agent`; token use is optional and depends on live endpoint behavior.
- Search supports query/title-style text, IMDb/TMDB IDs, parent TV IDs, season and episode numbers, languages, page, type, and provider sorting fields where available.
- IMDb IDs should be normalized to the provider expectation, typically without the `tt` prefix.
- TMDB fields are documented, but implementation must keep title/series fallback because provider coverage can vary.
- Download responses may expose quota information such as remaining requests and reset time.
- Static language data should be generated from OpenSubtitles language codes and kept in source.
- Phase 5.1 must run a live contract check only when credentials are configured; reports must stay credential-free.

## Why Migration Is Required

The existing `SubtitleBinding` table is source-level and scan-owned: it binds `MediaFileId` to subtitle `MediaFileId` and can be rebuilt by scan logic. Phase 5 requires Movie/Episode-level online subtitle bindings to managed cache files with provider metadata and without creating fake `MediaFile` rows. A dedicated online subtitle binding table is therefore required to avoid corrupting scan subtitle behavior and to support provider/file/hash deduplication.

Phase 5.3b adds a second migration because the product semantics changed after 5.3: unidentified playback now needs persistent MediaFile-level online subtitle bindings. `OnlineSubtitleBindings` therefore has exactly one target among `MovieId`, `EpisodeId`, and `MediaFileId`.
