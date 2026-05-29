# TV Support Known Issues

## Phase 4.18 Full Regression Known Issues

Blocker:

- None after the Phase 4.18 full-regression audit, documentation closeout, migration check, and build validation.

Deferred:

- Phase 5 online subtitle search remains a separate stage. Do not fold subtitle provider lookup, online matching, or subtitle download UX into Phase 4 closeout.
- Final UI redesign, release packaging, and installer work remain later stages.
- Future TV Watch Insights, TV AI recommendations, TV recommendation fingerprints, and TV-specific recommendation UX require TV-only inputs, TV-only fingerprint/cache namespace, TV-specific prompt/candidate/filter design, and an independent acceptance matrix.
- Mixed Movie + TV profiles remain out of scope. Do not reuse Movie Watch Profile semantics to carry TV signals.
- Automatic cleanup and stale-data policy for metadata-only TV Series / Seasons / Episodes created by Discovery browsing remains deferred.
- Dedicated hidden-state badges on TV Discovery cards remain UI polish; current restore behavior uses the existing add / restore action.
- TV Season rating persistence / Season-level rating-source normalization remains a later product/data-model decision.
- Specialty automatic mapping for SP/OAD/OVA/special, theatrical, course, derivative-series, collection, and complex multi-episode/cross-season files remains manual-correction or later specialty work unless a future phase defines conservative gates.
- No automated test project is currently present in `MediaLibrary.sln`; broad regression is audit/build/manual-matrix based until a test harness is added.

Noise:

- Movie Watch Statistics source fingerprinting can still invalidate more Movie-only cache rows than the final displayed Movie statistics require. This does not read TV tables or import TV data.
- Metadata-only TV detail can show source-less Episode rows by design. These rows are non-playable until a real active Episode source exists.
- Asynchronous TV Discovery Season-count enrichment can finish after a UI page/filter switch; request-version guards prevent stale values from overwriting the current visible result set.

## Phase 4.17 Closure Regression Known Issues

Blocker:

- None after the Phase 4.17a exclusion audit, closure regression review, and build validation.

Deferred:

- Future TV Watch Insights, TV AI recommendations, TV recommendation fingerprints, and TV-specific recommendation UX require a separate TV-only input model, TV-only fingerprint/cache namespace, and independent acceptance matrix.
- Mixed Movie + TV watch profiles remain out of scope. Do not reuse Movie Watch Profile semantics to carry TV signals without a separate product design.
- Automatic cleanup and stale-data policy for metadata-only TV Series / Seasons / Episodes created by Discovery browsing remains deferred.

Noise:

- Movie Watch Statistics source fingerprinting can still be broader than the final displayed Movie statistics in some Movie-only row-lifecycle cases. This may cause extra Movie statistics cache invalidation, but it does not read TV tables or import TV data.

## Phase 4.16 Closure Regression Known Issues

Blocker:

- None after closure audit and build validation.

Deferred:

- A dedicated hidden-state badge on TV Discovery cards remains UI polish. Current restore behavior is functional through the existing add / restore action and is not a 4.16 blocker.
- Automatic cleanup and stale-data policy for metadata-only TV Series / Seasons / Episodes created by Discovery browsing remains deferred.
- TV AI recommendation, TV Watch Insights, TV recommendation fingerprints, and TV-specific recommendation UX remain later product decisions and should not be folded into Phase 4.

Noise:

- TV search / ranking Season counts are supplemented with per-visible-card TMDB Series detail requests. These requests are asynchronous, use existing TMDB cache / throttle behavior, and do not block the first result render.
- In-flight Season-count detail requests may finish after a page or filter switch, but request-version checks prevent stale results from overwriting the current visible TV result set.
- Metadata-only TV detail can expose Episode rows without playback sources by design; those Episodes remain non-playable until a real active `MediaFile` is attached.

## Phase 4.13c Manual Aggregation Known Issues

Blocker:

- None after build validation.

Deferred:

- `聚合后识别`, AI top1, batch AI correction, grouped unknown Season to recognized Season correction, and historical wrong-binding cleanup remain later Phase 4.13 work.
- Manual aggregation creates a new unknown Series / Season and does not try to merge into existing unknown containers; existing-container repair remains a separate correction path.

Noise:

- Dialog source context uses hashed hints instead of readable paths to avoid exposing full local paths or WebDAV URLs.
- Episode-number prefill remains parser-based; unusual SP / OVA / special naming can still require manual number edits or later specialty correction.

## Phase 4.13c-fix Manual Aggregation Duplicate Guard Known Issues

Blocker:

- None after build validation.

Deferred:

- Joining an existing unknown Season or recognized Season remains a correction workflow, not manual aggregation.
- Same-title unknown-to-recognized migration is not automatic and remains later Season-level correction work.

Noise:

- The duplicate guard is normalized-equality based. It catches whitespace, case, wrapping-symbol, and common full-width / half-width differences, but it does not infer aliases or semantic title equivalence.

## Phase 4.12-post-fix-follow-up Known Issues

Blocker:

- None after build validation.

Deferred:

- Existing wrong recognized Episode source bindings are not automatically removed by the same-directory reattach gate.
- Cross-directory source correction for recognized TV remains manual-correction / batch-correction work for Phase 4.13 or later.
- SP/OAD/OVA/special, theatrical, derivative-series, course, and collection-specific mapping remain outside automatic reattach.

Noise:

- Recognized Episode reattach can now leave more sibling-folder numeric files in placeholder / Other flows by design.
- Directory diagnostics for this gate use hashes, so logs can prove same/different directory decisions without exposing private directory text.

## Phase 4.12-post-fix Known Issues

Blocker:

- None after build validation.

Deferred:

- Historical duplicate unknown Series / Season containers are not bulk-merged automatically.
- Manual aggregate-to-Season, manual correction into an existing unknown Season episode number, batch button rule changes, AI-assisted correction strategy changes, and unified Movie / TV correction entry remain Phase 4.13 work.
- SP/OAD/OVA/special, theatrical, course, collection, folk season numbering, and multi-episode expansion remain specialty or manual-correction work.

Noise:

- Unknown grouping keys are derived at scan time from current source context and are not stored as schema fields.
- When source/root/title context conflicts or more than one compatible unknown Season is found, automatic append skips and leaves the file for existing placeholder / Other handling.
- Duplicate-copy suffix normalization is scoped to TV-like parsing / grouping and does not imply broad duplicate-file cleanup outside unknown Season append.
- Existing placeholder / Other counts may remain high for ambiguous folders; this phase prevents more unsafe duplicates instead of claiming broad identification.

## Phase 4.12h Closeout Known Issues

Blocker:

- None for Phase 4.12 Episode detail closeout.

Deferred:

- Real unified correction entry remains Phase 4.13 work.
- AI correction, TMDB candidate search, batch correction, and manual Season grouping remain deferred.
- SP / OAD / OVA / special mapping, theatrical / course / collection-specific handling, folk season splits, and multi-episode file splitting remain outside Phase 4.12.
- More complete scan / rescan hardening can build on the existing reset-only reattach logs and conservative Episode reattach behavior.
- Richer probe task-center UI, manual retry policy controls, and broader live progress surfaces remain deferred; current detail lazy probe and manual probe are best-effort.
- TV Watch Insights, Watch Profile, AI recommendations, persona inputs, and recommendation fingerprints remain excluded and need a separate design if product direction changes.
- Final UI visual polish remains deferred.

Noise:

- WebDAV probe can fail because of network instability, server behavior, permissions, ffprobe limits, or unavailable stream metadata. It is not a Phase 4.12 blocker.
- Movie placeholders / Other counts can remain high in ambiguous libraries; 4.12 provides detail carrying and source management, not full correction.
- Historical docs and logs may still use `重置为未识别`; current UI wording is `从当前集拆分` / `从当前电影拆分`.
- The current worktree may contain cumulative Phase 4.12 changes; this closeout only records current status and does not execute database update.

## Blocker

- No known Phase 4.3 code blocker after service-layer build validation.
- Real database migration validation for Phase 4.1 remains required before broad manual scanning against a user library.
- No known Phase 4.4 build blocker after hidden route and page validation.
- No known Phase 4.5 build blocker after Episode playback integration.
- No known Phase 4.6 build blocker after media-library, home, history, and favorites integration.
- No known Phase 4.6 Bugfix blocker after validation fixes are applied; build verification remains the gate.
- No known Phase 4.6 Bugfix 2 blocker after Season state and poster-cache fixes; build verification remains the gate.
- No known Phase 4.7 blocker after rating display and mixed batch closeout; build verification remains the gate.
- No known Phase 4.8 blocker after TV discovery search and rankings integration; build verification remains the gate.
- No known Phase 4.8 Bugfix blocker after TV discovery UI / filtering / ranking parity fixes; build verification remains the gate.
- No known Phase 4.10 blocker after TV metadata hydration and unavailable Season display; build verification remains the gate.
- No known Phase 4.10.1 blocker after metadata-only TV library visibility and batch-rule refinement; build verification remains the gate.
- No known Phase 4.10.3 schema blocker after adding the visibility-state fields; build verification remains the gate.
- No known Phase 4.10.4 blocker after connecting visibility-state query and remove semantics; build verification remains the gate.
- No known Phase 4.10.4d blocker after visibility wording, Hidden restore, and movie-only AI input cleanup; build verification remains the gate.
- No known Phase 4.10.4f blocker after changing remove-from-library to hide-only semantics; build verification remains the gate.
- No known Phase 4.10.5 blocker after adding explicit add / restore visibility actions; build verification remains the gate.
- No known Phase 4.10.5b blocker after source / state-aware restore and SeriesOverview action consistency fixes; build verification remains the gate.
- No known Phase 4.10.6 blocker after summary-first TV Discovery navigation and Season-level Episode hydration; build verification remains the gate.
- No known Phase 4.11 blocker after TV scan identification hardening; build verification remains the gate.

## Deferred

- Phase 4.11f-fix-9 keeps `01: title` / `01：title`, SP/OAD/OVA/special mapping, theater collections, course/extras classification, and active correction outside default scan. These still belong to manual correction, Episode detail, or later specialty phases.
- Long-running numeric unknown range grouping allows small gaps but does not create missing Episode rows. Large gaps, duplicate episode numbers, and ambiguous short movie collections remain unresolved / Other items.
- Existing `._*` rows created before the AppleDouble filter may require rescan missing-file cleanup or manual delete-record cleanup; new scans should ignore those files before media type detection.
- `TvSeasonRatingSources` is deferred until a later rating persistence decision.
- Season-level IMDb rating display is deferred unless OMDb responses can be verified as stable and unambiguous.
- Additional TV detail refinements are deferred until the library and discovery phases expose their entry points.
- Complex anime season models remain deferred.
- Multi-episode file expansion remains deferred.
- Final correction dialogs / UI entry points remain deferred.
- Per-episode independent collection state remains deferred.
- Full not-in-library TV external detail pages are replaced by Phase 4.10 metadata hydration and `SeriesOverviewPage` reuse.
- Full advanced TV discovery filters remain deferred; Phase 4.8 Bugfix adds basic client-side filters without forcing Movie person search or Movie genre semantics onto TV.
- Full TV feature-gap audit and stage reorder remains deferred to the next phase.
- Not-in-library TV metadata cleanup remains deferred.
- TV correction entry points and cross-type correction UI remain deferred.
- Full Phase 4 regression and documentation closeout is deferred to Phase 4.9.
- Full TV status filtering in normal Series mode remains deferred; Season state is surfaced in batch mode and favorites.
- Final TV UI polish remains deferred.
- Cleanup for metadata-only TV Series / Seasons / Episodes remains deferred after Phase 4.10.
- Persistent TV metadata refresh / stale-data policy remains deferred after Phase 4.10.
- TV correction entry UI remains Phase 4.11.
- TV scan / rescan / history-location hardening remains Phase 4.12.
- TV AI support remains a Phase 5 candidate and should not be folded into Phase 4.
- Automatic cleanup for metadata-only TV rows created by discovery browsing remains deferred after Phase 4.10.1.
- More granular metadata-only TV library filters remain deferred; Phase 4.10.1 keeps default visibility conservative.
- A future explicit remove-source action may be needed if users need to detach playback sources without deleting full software records.
- Complex mixed Movie files inside AI-detected TV folders can become TV placeholders by design in Phase 4.11; Phase 4.12 should provide AI-assisted correction.
- Default scan AI only provides TV range hints. It is not a TV recommendation feature and does not enter Watch Insights or recommendation fingerprints.
- Phase 4.11b reduced the default scan AI schema from episode-file mapping to directory ranges, but large directory trees can still exceed the current short production timeout. Budget-driven directory batching / stronger summarization remains deferred.
- Phase 4.11c further reduces prompt size with direct-video directory summaries and short evidence fields, but large scans can still exceed the short production timeout. Batch remains deferred until needed by real scan results.
- Phase 4.11d tightens strong TV context and candidate conflict handling, but it still relies on default local/TMDB evidence. Uncertain mixed folders should remain candidates for the next local pre-analysis / AI-on-uncertain phase rather than being forced into automatic matches.
- Phase 4.11e-prep disables default full AI range analysis in production scans. The AI range implementation is retained for diagnostics, while log-only `aiCandidateRanges` prepare uncertain directories for a later AI-on-uncertain phase.
- Phase 4.11e-prep-2 blocks low-information Movie auto-binds and improves `aiCandidateRanges` quality, but another real scan log audit is still required before enabling AI-on-uncertain.
- Movie title cleaning may still leave some release/audio/source metadata in candidate queries; broad Movie cleaner refinement should be driven by additional real Movie samples, not by one-off title special cases.
- Phase 4.11e-prep-3 improves generic Movie release/audio/source cleanup and emits final run-level `aiCandidateRanges`; another real scan should verify whether Movie placeholders now reflect real ambiguity instead of leftover metadata noise.
- Formal AI-on-uncertain is still not enabled after Phase 4.11e-prep-3; uncertain ranges remain diagnostics until the next scan phase.
- Phase 4.11e-prep-4 blocks Movie / TV `NeedsReview` scan results from automatic binding. A real rescan should verify that wrong auto-bind count drops before enabling AI-on-uncertain.
- TV localized-title version conflicts are downgraded by generic qualifier/original-title checks, but deeper country / remake / same-name adjudication remains for AI-on-uncertain or manual correction.

## Noise

- TMDB season and episode rating fields are response-dependent and should be treated as optional.
- OMDb season responses can expose episode ratings without proving a stable Season-level rating.
- External metadata cache cleanup already targets the managed TMDB / OMDb cache provider and cache types used by TV metadata.
- Phase 4.3 TV scan grouping is intentionally conservative so ordinary movie folders continue through the movie flow.
- Scan counters still mostly reflect file scanning rather than separate Movie / TV identification subtotals.
- Unsupported multi-episode files are handled as unsupported sources, not as fully modeled Season contents.
- Phase 4.5 Episode playback exists, but before Phase 4.6 users may still lack a natural media-library click path into TV pages.
- Previous / next and auto-next intentionally stay inside one Season.
- Missing adjacent Episode sources show a friendly notice instead of falling back across seasons.
- Phase 4.6 uses existing card layouts with minimal TV fields; final visual polish remains deferred.
- Batch favorite, want-to-watch, and not-interested are intentionally not in the batch toolbar. Use Season detail or favorites card actions instead.
- Watch Insights and Watch Profile stay Movie-only; Episode watch history is available in history UI but excluded from statistics, profile, persona, and recommendation fingerprints.
- Existing matched Seasons with only in-library Episode rows need a rescan or TV correction refresh to populate missing TMDB Episode metadata rows.
- Phase 4.8 TV rankings are Series-level because TMDB TV ranking endpoints return Series, not Seasons.
- TV discovery does not request OMDb / IMDb ratings; cards show TMDB Series rating or no rating.
- Not-in-library TV Series clicks no longer show the old future-detail placeholder; Phase 4.10 hydrates metadata and enters `SeriesOverviewPage`.
- Phase 4.10 changes not-in-library TV Series clicks to write metadata-only TV rows and navigate to `SeriesOverviewPage`; these rows are not playback sources and should not be counted as in-library sources.
- Source-less Episodes can be marked watched / unwatched by design, but they remain non-playable until a real active `MediaFile` is attached.
- Metadata-only TV Series with no source and no user state are intentionally hidden from the default media-library list.
- Phase 4.10.1 batch-remove skip behavior is superseded by Phase 4.10.4; source-less remove now hides rows with `Hidden` while preserving state and metadata.
- Batch delete record removes software records and source rows from the app database only; it must not delete local or WebDAV files.
- Phase 4.10.4d keeps recommendation algorithm semantics unchanged; it only filters pure visibility-only Movie rows out of movie AI/profile/statistics/recommendation input surfaces.
- Phase 4.10.4f does not restore old `MediaFile.IsDeleted` rows created by earlier remove-from-library behavior, because those rows cannot be safely separated from missing-file, removed-path, or delete-record history. If files still exist, rescan recovery should be verified in Phase 4.13.
- Phase 4.10.5 add-to-library writes `Visible` as media-library visibility only; it intentionally does not create playback sources, set preference state, restore old `IsDeleted` rows, or include TV in AI / Watch Insights.
- Phase 4.10.5b supersedes Phase 4.10.5 restore behavior: removed-library restore writes `Auto` when active source or real current state exists, and writes `Visible` only for source-less no-state rows.
- Phase 4.10.6 changes TV Discovery navigation to summary-first hydration. Full Episode metadata can complete later in `SeriesOverviewPage` background hydration or on demand in `TvSeasonDetailPage`.
- Phase 4.11 scan diagnostics intentionally keep only sanitized tail paths and filenames; they are for rule tuning, not full private path reconstruction.
- Phase 4.11b long-timeout probing is diagnostic only; production default scan still uses the configured short AI range timeout and falls back to local rules on timeout.
- Phase 4.11c does not set a production timeout. Long-timeout probing remains diagnostic and must not be treated as a user-facing scan wait target.
- Phase 4.11d intentionally prioritizes lower wrong auto-bind risk over higher bound count. Manual scan validation should track wrong auto-bind count and candidate conflicts, not only the number of bound TV seasons.
- Phase 4.11e-prep does not run AI against uncertain ranges yet. `aiCandidateRanges` are diagnostic hints only and should not be interpreted as persisted scan results.
- Phase 4.11e-prep-2 keeps `aiCandidateRanges` diagnostic-only. Candidate range counts should be read as review workload, not as successful identification counts.
- Phase 4.11e-prep-3 final `aiCandidateRanges` summaries are still diagnostic-only. They are input candidates for later AI-on-uncertain, not successful scan bindings.
- Phase 4.11e AI-on-uncertain can improve uncertain range title / season hints, but it remains hint-only. Any unresolved, conflicting, low-confidence, or dirty result must stay placeholder / `NeedsReview` until active correction.
- Phase 4.11e-fix-1 changes AI-on-uncertain mapping from directory-hint hard matching to `inputRangeId`-first mapping. A fresh scan should verify that hints are no longer dropped primarily as `no-matching-sanitized-directory`.
- Phase 4.11e-fix-2 changes candidate range file recovery from sanitized-path matching to runtime `MediaFileIds`. A fresh scan should verify that mapped hints are no longer dropped primarily as `no-files-in-input-range`.
- Phase 4.11f uses AI refined title hints for local TMDB top1 lookup, but it still does not ask AI to choose among TMDB candidates. Ambiguous same-title / remake / version cases that fail the lightweight safety gate remain placeholder / `NeedsReview` / `ai-candidate` until Phase 4.12 active correction.
- Phase 4.11f-fix-1 removes the AI refined lookup original/year/version safety gate and accepts TMDB top1 when `refinedSeriesTitle` returns a result. This may increase wrong top1 risk by product choice; Phase 4.12 active correction / manual confirmation is the intended mitigation.
- Phase 4.11f-fix-2 prefers AI-provided original-language titles for refined TMDB lookup. If AI cannot infer an original-language title and falls back to English / localized aliases, wrong top1 matches can still occur and remain Phase 4.12 active correction work.
- Current-list batch select-all is intentionally scoped to loaded / filtered media-library items. It is not a hidden global delete helper and should not select removed-library or unloaded items.
- Phase 4.11f-perf-1 reduces duplicated scan work by limiting post-AI TV retry to AI affected files and caching TMDB searches for one scan run only. It is not a recognition quality change, and any remaining wrong top1 matches remain Phase 4.12 active correction / manual review work.
- Phase 4.11f-fix-3 blocks only obvious AI refined top1 Series-year conflicts. It intentionally does not solve same-title / remake / folk-season-numbering conflicts without a clear `seriesYearHint`.
- Phase 4.11f-fix-3 Movie placeholder grouping is log-only. It identifies consecutive episode-like failed Movie placeholders as TV-like ranges for later correction, but it does not create Series / Season / Episode rows or remove all such items from current UI surfaces yet.
- Phase 4.11f-fix-5 stores grouped TV-like placeholders as unidentified `TvSeason` / `TvEpisode` rows so they can play, be batch marked, and use Season detail. They remain unresolved / pending correction and are not TMDB-bound successful TV matches.
- Follow-up fixes route failed unidentified Seasons into `Other` in normal media-library mode and add conservative bracketed episode-number grouping. Remaining ungrouped files are expected when they fail the same-parent, strict-contiguous, minimum-three-file, or excluded-token rules.
- Active correction, manual regrouping, full Episode detail management, and multi-source episode handling remain deferred.
- Anime OAD / SP / OVA / special mapping, course / extras classification, folk season numbering vs TMDB season numbering, and multi-source Episode handling remain deferred to active correction, Episode management, or future anime-specialty work.

## Phase 4.11f-fix-6 Known Issues

Blocker:

- None.

Deferred:

- `01: title` / leading-number-colon-title episode parsing remains deferred because it has higher movie / course false-positive risk.
- Movie collections, theatrical collections, course folders, extras, anime SP/OAD/OVA/special mapping, multi-episode file splitting, and manual regrouping remain active-correction / Episode-management work.
- Title+number sequence candidates are intentionally uncertain. They enter AI-on-uncertain or unresolved placeholders, but they do not directly auto-bind TV.

Noise:

- Unsupported sample diagnostics are sanitized and truncated. They are sufficient for pattern review but not a full private path reconstruction tool.
- Four-digit episode numbers are supported only under explicit episode markers. Bare four-digit filenames remain conservative by design.

## Phase 4.11f-fix-7 Known Issues

Blocker:

- None.

Deferred:

- `01: title` / leading-number-colon-title parsing remains deferred.
- Movie collections, course folders, theatrical collections, anime SP/OAD/OVA mapping, and multi-episode file splitting remain active-correction / Episode-management work.
- Ignored scan file extension logs are evidence only. Potential video formats such as `.m4v`, `.webm`, `.ts`, `.m2ts`, `.wmv`, `.flv`, `.rmvb`, `.mpg`, or `.mpeg` should be reviewed before any whitelist change.

Noise:

- Verified title+number parsing is scoped to prevalidated same-parent sequences. Standalone title+number files can still remain unresolved by design.
- Ignored-file samples are sanitized and capped per extension; they are not a full inventory of skipped files.

## Phase 4.11f-fix-8 Known Issues

Blocker:

- None.

Deferred:

- `.sup` files are now accepted as subtitle candidates, but actual playback / renderer support still needs validation.
- `01: title` / leading-number-colon-title parsing remains deferred because it has higher movie / course false-positive risk.
- Movie collections, course folders, theatrical collections, anime SP/OAD/OVA mapping, multi-episode file splitting, and manual regrouping remain active-correction / Episode-management work.
- Orphan scatter items are a visibility fallback. Correcting them into known Movie / TV metadata still belongs to active correction or later regrouping workflows.

Noise:

- Orphan grouping remains conservative: same direct parent, strict contiguous episode-like numbers, and no cross-directory merge.
- `.rmvb` scan admission does not guarantee every file is playable; playback capability depends on the player stack.

## Phase 4.11f-fix-10 Known Issues

Blocker:

- None.

Deferred:

- Automatic part offset mapping remains deferred. `S3 Part 2 01` is not assumed to mean any specific TMDB episode number without stronger evidence or user confirmation.
- Final-season wording, SP/OAD/OVA, theatrical collection mapping, and manual part-offset correction remain active-correction / Episode-management work.

Noise:

- Part hints are diagnostics and grouping context, not successful recognition by themselves.
- Unresolved part ranges can still appear under unidentified / pending-correction surfaces until a correction workflow maps them to exact TMDB episodes.

## Phase 4.11f-fix-11 Known Issues

Blocker:

- None.

Deferred:

- Part offsets are applied only when previous sibling part evidence is already safely bound to the same TMDB Series and Season. Missing or unbound previous parts still require active correction / manual confirmation.
- Final Season wording, SP/OAD/OVA, theatrical collection mapping, and explicit user-controlled part-offset correction remain deferred.
- Multi-source conflict resolution remains conservative; an occupied target Episode blocks offset unless a later multi-source workflow can safely merge it.

Noise:

- Part offset diagnostics can report `missing-previous-part`, `previous-part-not-bound`, or `target-episode-conflict` for files that are probably correct but lack enough safe evidence.
- The offset rule intentionally favors unresolved / pending correction over guessing a folk-season or part-to-TMDB mapping.

## Phase 4.11f-fix-11-hotfix Known Issues

Blocker:

- None.

Deferred:

- Automatic sibling part offset apply is disabled pending a safer redesign. The redesign should run before low-confidence placeholder decisions, carry parsed part context into candidate search, and only then apply offsets with same-Series / same-Season evidence.
- Part ranges with valid `partHint` / `episodeInPart` but no safe offset remain unidentified / pending correction.
- Final Season wording, SP/OAD/OVA, theatrical collection mapping, and explicit user-controlled part-offset correction remain deferred.

Noise:

- Structural part-only queries are rejected aggressively. Real-title queries that include part wording remain eligible when they contain a title token beyond season / part / number structure.

## Phase 4.11f-fix-13 Known Issues

Blocker:

- None.

Deferred:

- If one AI batch is still too large, retry-time sub-batching can be considered later. This phase keeps the batching model simple and bounded.
- AI provider-side concurrency or rate limits may still cause one batch to fail; successful sibling batches should still apply.
- AI timeout tuning remains operational and provider-dependent.

Noise:

- Batch grouping is deterministic and local. It keeps related ranges together where possible, but it is not a semantic clustering model.
- Partial AI failures can still leave some folders unresolved even when nearby batches succeed.

## Phase 4.11f-fix-14 Known Issues

Blocker:

- None.

Deferred:

- Part offsets still require a confirmed TMDB Series / Season and a previous contiguous episode range. Missing Part1 evidence, missing TMDB episode metadata, or occupied target episodes leave later parts unresolved.
- Final Season wording, SP/OAD/OVA, theatrical collection mapping, and explicit user-controlled part-offset correction remain deferred.
- More advanced cross-directory part reconciliation can be revisited in the active correction flow if safe automatic evidence is not available during scan.

Noise:

- Later parts can remain in `Other` / unidentified season surfaces even when the title is correct, because the offset is intentionally blocked without E01..N evidence.
- Current-scan ordering helps same-parent part folders, but it is still a conservative local ordering rule rather than a semantic season planner.

## Phase 4.11f-fix-14-hotfix Known Issues

Blocker:

- None.

Deferred:

- Later-part offset still depends on confirmed TMDB Series / Season plus previous contiguous episode evidence. If that evidence is missing, the range remains unidentified / pending correction.
- Final Season wording, SP/OAD/OVA, theatrical collection mapping, and explicit user-controlled part-offset correction remain deferred.
- Scan duration summary accuracy remains a separate deferred issue.

Noise:

- Part candidates can now attempt AI refined Series lookup before offset evaluation, but a successful title lookup alone does not guarantee offset; target episode availability and conflict checks still decide apply.
- Folder-name part queries may still appear as rejected/noisy diagnostics, but structural-only part queries remain blocked from TMDB lookup.

## Phase 4.11g Known Issues

Blocker:

- None for the default TV scan closeout baseline.

Deferred:

- Episode detail page and per-episode multi-source management remain Phase 4.12 work.
- Active correction, cross-type correction, manual regrouping into Seasons, and user-confirmed part offsets remain Phase 4.12 / 4.13 work.
- SP/OAD/OVA, specials, theatrical collections, course / extras classification, and Final Season folk-to-TMDB mapping remain outside default scan.
- TV discovery expansion, TV recommendation inputs, TV Watch Insights, Watch Profile TV fingerprints, and TV persona inputs remain deferred.
- `.sup` scan admission is complete, but actual renderer / playback support still needs validation.
- Media-library performance and large `Other` / unidentified-season list ergonomics remain later optimization work.

Noise:

- Movie placeholders can remain high in mixed anime / movie / course folders; this is expected unresolved data, not a scan failure.
- AI refined title top1 can still require human correction when TMDB naming, folk season splits, or source folders disagree.
- AppleDouble `._*` files from older scans may require delete-record cleanup if they were inserted before the ignore rule existed.

## Phase 4.12-post Emergency Safety Fix Known Issues

Blocker:

- None for the tightened unknown grouping / append safety path after build verification.

Deferred:

- Existing unknown Series / Season containers that were already polluted by the previous aggressive reuse logic are not automatically repaired. Use clear-library rescan, manual cleanup, or a later repair tool.
- Special, recap, remake, OVA / OAD, theatrical / movie, side-story, course, and collection directories are intentionally not auto-appended into regular unknown Seasons. Phase 4.13 should provide user-controlled correction / grouping for these cases.
- Broader TV recognition quality work remains separate: TMDB candidate conflict handling, year-gate tuning, AI prompt changes, and folk-season mapping are not part of this emergency fix.

Noise:

- The conservative skip may leave more sources visible as Other / placeholder items until manual correction exists.
- Strict-key mismatch logs are expected when files share episode numbers but are in different directories or already mixed unknown containers.

## Phase 4.13d Known Issues

Blocker:

- None after build verification.

Deferred:

- Partial unknown Season correction is not implemented; Phase 4.13d moves the whole source Season.
- SP / OVA / OAD / specials / theatrical mappings remain manual future work and are not inferred during Season correction.
- Historical wrong-binding cleanup and automatic top1 / batch AI correction remain separate phases.

Noise:

- The target Season number is user-entered. Folk-season naming that differs from TMDB numbering still depends on the user choosing the correct TMDB Season.
- If TMDB lacks metadata for a moved Episode number, the local Episode is still created with minimal metadata so the source is not discarded.

## Phase 4.14b Known Issues

Blocker:

- None after build verification.

Deferred:

- Historical rows that were previously marked `MediaFile.IsDeleted=true` by older remove-from-library behavior are not auto-restored, because those rows cannot be safely distinguished from missing files, removed scan paths, or delete-record retention.
- Hidden failed Movie placeholders are intentionally excluded from automatic scan recovery. Users must restore them before they can reappear in the media library or participate in later manual correction workflows.
- `MediaFile` still has no dedicated visibility column. If future phases need hide-only state for raw source rows without a Movie / Season carrier, that requires a separately approved migration.
- Scan summary UI, history positioning, probe / subtitle boundary cleanup, online subtitle search, TV Discovery closeout, and media-library performance work remain later phases.

Noise:

- Unchanged unbound files now re-enter the full TV / Movie identification input, so scan logs may show larger requested identification counts without implying broader matching rules.
- Existing Episode bindings skipped by automatic TV scan can leave wrongly bound sources in place until the user uses an explicit correction or split workflow.

## Phase 4.14c Known Issues

Blocker:

- None after build verification.

Deferred:

- Reason summaries are task-level aggregates only. Per-file reason history, reason click-through, and history / calendar positioning remain future work.
- The reason summary intentionally stores counts and stable reason keys, not detailed diagnostic payloads. Detailed low-level scan diagnostics remain log-only.
- Historical scan logs created before migration `20260524213322_AddScanTaskReasonSummary` have no reason summary until they are naturally superseded by new scan logs.
- No data backfill is included because this phase does not execute database update or rebuild old scan records.

Noise:

- Recent scan cards may show the same task-level reason summary on multiple path logs when one scan run processes multiple enabled paths.
- Scan progress file text is throttled and safe-name-only, so very fast scans may skip over intermediate file names by design.

## Phase 4.14d Known Issues

Blocker:

- None after build verification.

Deferred:

- Watch History date positioning is date-level only. Per-history-item deep links, scan reason click-through, and richer timeline navigation remain future work.
- Deleted or orphaned history rows are not cleaned up automatically; they remain visible where enough display metadata exists, with playback/detail actions bounded by current target availability.
- Online subtitle search, subtitle download, subtitle version management, playback-source-level subtitle binding, full-library probe scheduling, and probe task-center UI remain later dedicated work.
- If future manual online subtitles bind to Movie / Episode entities, automatic subtitle rebuild needs a separate preservation rule so it cannot overwrite manual choices.

Noise:

- Target-date and target-Episode highlights are intentionally short-lived visual hints. They are not persisted and may disappear after refresh or navigation.
- Probe skip diagnostics for deleted source rows include only ids, source kind, protocol kind, and a safe file fingerprint.

## Phase 4.14 Closure Known Issues

Blocker:

- None after closure build verification.

Deferred:

- Historical `MediaFile.IsDeleted=true` rows created by older remove-from-library behavior are still not auto-restored.
- Historical scan logs created before task-level reason summary support still have no reason summary unless superseded by later scans.
- Watch History still does not provide per-item deep links, scan reason click-through, or data-health cleanup for orphaned history rows.
- Online subtitle search / download, playback-source-level subtitle binding, full-library probe scheduling, probe task-center UI, TV Discovery closure, and media-library performance work remain later phases.

Noise:

- WebDAV scan log errors now intentionally show generic operation text plus exception type; detailed raw exception text is not persisted to avoid leaking URLs, paths, or credentials.
- Reason summaries remain aggregate task explanations and are not used as scan matching rules.

## Phase 4.15b Known Issues

Blocker:

- None after build verification.

Deferred:

- The refresh query still uses the existing full `ILibraryQueryService.GetLibraryItemsAsync` path. Query split, projection reuse, pre-aggregation, and N+1 cleanup remain Phase 4.15c candidates and must be driven by the new timing logs.
- The media library item controls and poster view are unchanged. Virtualization, image decode tuning, first-screen poster prioritization, and ObservableCollection differential updates remain later performance work.
- Database index additions, if later justified by query timings, require a separately approved migration and were not added in this phase.
- Already-running refreshes continue to completion after page deactivation. The active dirty gate prevents new inactive DataChanged refreshes but does not cancel in-flight UI application.

Noise:

- Debounced DataChanged bursts now produce request / debounced / completed diagnostics with merged reason names. Multiple request events in the same burst are expected; they should collapse to one completed refresh unless another request arrives during the refresh.
- Refresh completion logs intentionally report aggregate counts and elapsed milliseconds only. They do not include item titles, local paths, WebDAV URLs, account names, tokens, passwords, or API keys.

## Phase 4.15c Known Issues

Blocker:

- None after build verification.

Deferred:

- Query / projection optimization is still deferred until the new `library-query-*` diagnostics are collected on real media-library operations. This phase adds observability and removes a confirmed duplicate-refresh pattern, but does not rewrite LINQ or projection logic.
- If service-level logs show repeated expensive source / state aggregation, Phase 4.15d may need targeted projection reuse or query splitting. Any database index work still requires a separately approved migration.
- UI virtualization, poster decode prioritization, image cache behavior, and ObservableCollection differential updates remain later work because current refresh logs still show UI apply and filter / sort as negligible in the sampled library.

Noise:

- Operation-local refresh reasons now appear in `mergedReasons` together with DataChanged reasons, for example operation remove / delete reasons plus library / collection changes. This is expected and indicates coalescing.
- `library-query-*` logs can appear before each `library-refresh-completed` event. They are aggregate timings only and do not log item names, local paths, WebDAV URLs, credentials, tokens, passwords, or API keys.

## Phase 4.15d Known Issues

Blocker:

- None after build verification.

Deferred:

- The media library still performs a full query refresh for the active page. Phase 4.15d reduces TV projection materialization cost but does not add projection caching or differential refresh.
- Movie-side query time, orphan / Other projection time, and external no-source collection projection remain unchanged. Revisit only if the new `library-query-*` logs show them as the next bottleneck.
- If the new Season aggregate source query remains slow on a larger real library, index work may be needed, but that requires a separately approved migration and was not added in this phase.
- UI virtualization, poster decode prioritization, image cache behavior, and ObservableCollection differential updates remain later work unless real logs show UI / image cost overtaking query cost.

Noise:

- `library-query-tv-series-completed` and `library-query-tv-season-completed` now include `episodeAggregateRowsMs`, `sourceAggregateRowsMs`, `episodeAggregateRows`, and `sourceAggregateRows`. The `episodeRows` and `sourceRows` fields are aggregate totals for comparison, not materialized row-list sizes.
- Hidden-library TV projection also uses the aggregate path. Removed-library counts should remain semantically unchanged even though the internal query shape changed.

## Phase 4.15d-fix Known Issues

Blocker:

- None after build verification.

Deferred:

- The flat source-row query must be re-sampled on the user library. If `sourceAggregateRowsMs` does not drop near the previous flat `sourceRowsMs` range, the next step is query-plan / index audit rather than UI work.
- Full media-library refresh, projection caching, differential refresh, UI virtualization, poster decode prioritization, and image cache behavior remain unchanged.

Noise:

- `sourceAggregateRowsMs` now measures a flat minimal source read plus in-memory grouping. It should be compared against both the first 4.15d aggregate query and the older 4.15c `sourceRowsMs` values.

## Phase 4.15e Known Issues

Blocker:

- None after build verification.

Deferred:

- The poster card grid still uses `ScrollViewer + ItemsControl + WrapPanel`, so it creates every visible card instead of virtualizing the first viewport. If background thumbnail decode is still not enough, the next performance phase should evaluate a virtualized poster panel or a list-first rendering strategy.
- Poster downloads and cache misses are still handled by the existing poster cache service. This phase improves cached local decode pressure but does not add first-screen prioritization or a new download scheduler.
- WPF render / layout duration is still not directly logged. If perceived latency persists, add render-priority dispatcher diagnostics before further UI structural changes.

Noise:

- Media-library card posters now request thumbnail-sized decode through `DecodePixelWidth=436`; detail pages that use the same image behavior keep their existing full decode behavior unless they opt in separately.

## Phase 4.15d Frontend Poster Virtualization Known Issues

Blocker:

- None after build verification.

Deferred:

- The new virtualized poster panel should be sampled on the user's real library. If `library-render-ready` remains high while `library-poster-virtualization realized` is much smaller than `items`, the next step is targeted WPF template/layout profiling rather than another database query rewrite.
- The panel keeps a fixed card width and an adaptive global row height to preserve wrap-grid virtualization. A later final UI phase can revisit fully responsive card sizing if visual polish needs it.
- Poster cache misses and remote downloads still use the existing poster cache service. First-screen prioritization and a dedicated poster request scheduler remain deferred unless logs show poster request bursts as the next bottleneck.
- Query caching, differential refresh, and index work remain separate decisions driven by real logs and require separate approval if they touch schema.

Noise:

- `library-poster-virtualization` diagnostics are aggregate layout evidence. They may repeat when the viewport size, realized count, or filtered item count changes.
- The virtualized panel reports realized item containers, not business items selected by batch operations. Batch select-all continues to use the full current filtered collection.
- Phase 4.15d follow-up reduces poster-view mouse-wheel speed and rate-limits virtualization diagnostics; users should retest scroll feel on mouse wheel and touchpad separately.

## Phase 4.15 Closure Regression Known Issues

Blocker:

- None after closure build verification.

Deferred:

- First media-library entry after full application startup can still include one-time cold-start cost from process / WPF / EF / image-cache warmup. Phase 4.15 intentionally does not add cold-start prewarm.
- If real-library samples later show `library-render-ready` remains high while `library-refresh-completed` stays low and `library-poster-virtualization realized` remains much smaller than `items`, the next step is targeted WPF template / layout profiling rather than another query rewrite.
- If large-library query logs later show a new dominant `library-query-*` segment, query-plan / index work should be proposed separately; any index change still requires an approved migration.
- First-screen poster prioritization and a dedicated poster request scheduler remain deferred unless cache-miss / remote-poster bursts become the next measured bottleneck.

Noise:

- `library-poster-virtualization` events are intentionally rate-limited aggregate layout evidence. A small number of repeated entries during viewport settle or scroll is expected.
- `library-render-ready` measures dispatcher render readiness after refresh completion; it can include normal WPF layout / composition scheduling time and is not a database query duration.
- Current Phase 4.15 logs are aggregate count / duration diagnostics and should not be treated as item-level audit logs.

## TV Scan Continuation Anchor Known Issues

Blocker:

- None after build verification.

Deferred:

- The continuation anchor only applies to new unbound TV season candidates. It deliberately does not rewrite already attached Episode bindings, including previously mis-grouped unknown Seasons; delete-record or manual correction remains the remediation path before rescan.
- The anchor requires a unique already matched / manually confirmed sibling Season context from the same source connection. Ambiguous roots with multiple recognized Series, non-sibling layouts, Season 0 / specials, absolute episode numbering, and cross-season ranges still need dedicated correction or future mapping work.
- The TMDB target Season detail must confirm the parsed Episode numbers. Missing TMDB Season detail or Episode-number mismatch still falls back to placeholder / review instead of auto-binding.

Noise:

- `tv-continuation-anchor-*` diagnostics are decision evidence only. Skipped anchor logs are expected for low-confidence TV candidates that do not have a unique recognized sibling Series context.

## TV / Media Library Follow-up Known Issues

Blocker:

- None after build verification.

Deferred:

- If future scans still reshuffle unchanged TV / Movie cards in "recently updated" order, audit whether binding helpers are updating `MediaFile.UpdatedAt` for association-only or metadata-only changes. If so, split source-content recency from metadata / binding recency in a later scoped phase.
- Home recently-added now shows TV seasons as season-level entries. Episode-level recently-added cards remain deferred unless the home page product spec explicitly asks for episode granularity.

Noise:

- The visible media-library sort intentionally does not use raw Movie / TV Series / TV Season metadata `UpdatedAt`, because those fields may be touched by scan metadata refresh and are not reliable evidence of a user-visible library update.
