# 影片发现实现计划

## 范围

影片发现页包含三个 Tab：影片搜索、榜单、AI 推荐。AI 推荐 Tab 继续复用现有 `RecommendationsViewModel` 与推荐流程。Phase 4.8 在搜索和榜单 Tab 内增加 Movie / TV 二级切换，但不新增顶层 TV Tab。

## FD-2.1 影片搜索

- 数据源使用 TMDB。
- 按影片搜调用 TMDB `search/movie`，请求参数包含 `language=zh-CN`、`include_adult=false`、`page`、`query`，地区筛选非“全部”时补充 `region` 作为发行地区弱约束。
- 按人物搜调用 TMDB `search/person`，取最相关人物后读取 `person/{id}/movie_credits`，合并 cast / crew 并去重。
- 搜索结果按 TMDB ID 合并本地状态：已入库、想看、已看、喜爱、不想看。
- 未入库影片转换为 `AiRecommendationItem` 后复用现有未入库详情入口。
- 未入库详情使用 `MovieDetailViewModel` 的外部影片 AI 标签路径，只在进入详情页时自动生成标签，不在搜索卡片上请求 AI。
- 未入库详情自动生成标签期间，缺失标签字段显示“AI 正在分析影片”，完成后替换为生成结果。
- OMDb 只用于按 IMDb ID 补充评分，不参与 TMDB 搜索召回。

## 搜索分页

- 搜索页使用显式分页，不显示“加载更多”。
- 每个展示页最多 30 部。
- 切换关键词、搜索类型或筛选条件会清空搜索页缓存并回到第 1 页。
- 切换页时优先复用已缓存 TMDB 页，不重复请求同一页。
- `search/movie` 每页通常 20 条，展示页会按需请求多个 TMDB 页后切片。

## 搜索筛选

- 类型、年代、语言、地区、排序和本地观看状态不再只过滤当前可见 30 条，而是在已缓存的 TMDB 结果池上过滤。
- 启用筛选或非相关度排序时，会从第 1 页重新构建结果池，并扫描更多 TMDB 页以覆盖未显示结果。
- 地区筛选优先使用 TMDB `region` 请求参数，随后用 `origin_country` 本地匹配；当 `origin_country` 缺失时，使用 `original_language` 弱映射兜底。
- 本地状态筛选只能在已合并的 TMDB 结果池上过滤，无法作为 TMDB 服务端参数传递。

## FD-2.2 榜单

- 热门榜：TMDB `movie/popular`。
- 高分榜：TMDB `movie/top_rated`。
- 趋势榜：TMDB `trending/movie/{day|week}`。
- 榜单严格保持 TMDB 返回顺序，不按评分、热度、名称或本地状态重排。
- 本地状态合并和 OMDb 补充分数不得改变榜单顺序。
- 去重只删除重复 TMDB ID，不做重排。

## Phase 4.8 TV Discovery

- TV 搜索调用 TMDB `search/tv`，结果显示 Series 卡片。
- TV 榜单调用 TMDB `tv/popular`、`tv/top_rated`、`trending/tv/day`、`trending/tv/week`。
- TV 结果使用独立 `DiscoveryTvSeriesCardViewModel`，不复用电影卡片 ViewModel，也不转换为 `AiRecommendationItem`。
- 已入库 Series 点击进入 `SeriesOverviewPage`。
- 未入库 Series 显示 TV 外部详情后置提示，不跳转 Movie detail。
- TV 评分文案使用 TMDB Series 口径，不显示 OMDb / IMDb 季评分。
- TV 搜索和榜单海报使用现有海报缓存行为。
- TV 不进入 AI 推荐、Watch Insights、画像、观影人格或推荐 fingerprint。

## Phase 4.10 TV Discovery Hydration Update

- TV search and TV ranking not-in-library Series clicks now hydrate TV metadata and navigate to `SeriesOverviewPage`.
- Hydration writes `TvSeries`, all TMDB Seasons including Season 0, and all TMDB Episodes.
- Hydration does not create `MediaFile`, does not fabricate playback sources, and does not convert TV results into `AiRecommendationItem`.
- Metadata-only TV Series remain not-in-library from a playback-source perspective until active Episode sources exist.
- The separate `ExternalTvSeriesDetailPage` is no longer planned for Phase 4.
- Browsing not-in-library TV can accumulate metadata-only rows; cleanup is deferred.
- TV discovery remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.10.1 Metadata-only TV Library Update

- TV discovery can still write metadata-only TV rows when a not-in-library Series is opened.
- Metadata-only TV rows do not count as playback-source library items until active Episode `MediaFile` rows exist.
- A discovery-hydrated Series with no playback source and no Season state stays out of the default media-library list.
- If the user marks a metadata-only Season watched, unwatched, want-to-watch, favorite, or not-interested, that Series / Season can surface in library-related views.
- Phase 4.10.1 originally skipped source-less remove with a no-source message; Phase 4.10.4 supersedes that behavior with `LibraryVisibilityState.Hidden`.
- Batch delete record removes software records / metadata / state only and must not delete local or WebDAV files.
- Metadata-only TV cleanup and refresh policies remain deferred.

## Phase 4.10.3 Library Visibility Schema

- `LibraryVisibilityState` is added as schema-only groundwork for Movie and TV Season user-state rows.
- `Auto = 0` is the default for old and new rows.
- `Visible` will be used later by explicit add-to-library actions.
- `Hidden` will be used later to hide source-less rows from the media library while preserving state and metadata.
- Discovery does not write `Visible` in this phase; opening a not-in-library TV Series continues to hydrate metadata only.
- Discovery add-to-library-specific wording remains deferred until Phase 4.10.5.
- AI recommendations and recommendation fingerprints remain unchanged.

## Phase 4.10.4 Media Library Source Visibility Note

- Media-library source-state filtering now uses `全部`, `有播放源`, and `无播放源`.
- `HasActiveSource` is based on active video `MediaFile` rows, not Discovery's old in-library wording.
- Source-less Movie / TV Season remove writes `LibraryVisibilityState.Hidden` and preserves metadata and state.
- Discovery opening a not-in-library TV Series still hydrates metadata only; it does not write `Visible`.
- Explicit add-to-library actions that write `Visible` remain Phase 4.10.5.
- AI recommendations and recommendation fingerprints remain unchanged.

## Phase 4.10.4f Hide-Only Remove Semantics

- Remove from library now means media-library hide only: Movie and TV Season rows write `LibraryVisibilityState.Hidden`.
- Remove from library no longer marks active `MediaFile` rows deleted and does not disable playback sources.
- Media-library filters still show only visible rows; Hidden rows are excluded from `全部`, `有播放源`, and `无播放源`.
- Discovery remains a search surface: Hidden source-backed items can still resolve as `有播放源`.
- Old source rows already marked `IsDeleted` by earlier remove behavior are not automatically restored.

## Phase 4.10.4d Discovery Visibility Wording

- Movie and TV Discovery source filters use `全部`, `有播放源`, and `无播放源`.
- Movie and TV Discovery cards use `有播放源` / `无播放源` instead of old `已入库` / `未入库` source labels.
- Add-to-library-specific actions are implemented in Phase 4.10.5 and write `LibraryVisibilityState.Visible`.
- Pure visibility-only Movie rows are excluded from movie AI/profile/statistics/recommendation fingerprints and fallback external candidates.
- Real-state source-less Movie rows still represent user preference and remain eligible for movie AI/profile/recommendation inputs.
- TV Discovery still does not create `AiRecommendationItem` and TV remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.

## Phase 4.10.5 Discovery Add-to-Library Actions

- Discovery Movie cards can write `LibraryVisibilityState.Visible` for explicit add-to-library on source-less rows through the user collection service.
- Hidden Discovery Movie restore uses source / state-aware restore semantics from Phase 4.10.5b.
- Discovery TV Series cards can ensure / hydrate TV metadata and write `Visible` for explicit add-to-library across all known Seasons, including Season 0 / Specials.
- Hidden Discovery TV Series restore uses per-Season source / state-aware restore semantics from Phase 4.10.5b.
- Discovery add-to-library does not set want-to-watch, favorite, not-interested, watched, or fake playback sources.
- Discovery add-to-library does not create `MediaFile` and does not restore old `IsDeleted` source rows.
- TV Discovery remains separate from `AiRecommendationItem`, Watch Insights, profile/persona inputs, and recommendation fingerprints.
- Phase 4.10.6 implements summary-first TV navigation: Discovery opens `SeriesOverviewPage` after Series + Season summary hydration, while full Episode metadata completion is deferred to background / Season-detail hydration.

## Phase 4.10.5b Discovery Restore Notes

- Restore-to-library no longer blindly writes `Visible`.
- Hidden rows with active source or real current state restore to `Auto`; source-less no-state rows restore to `Visible`.
- Automatic `Auto` invisibility is not shown in the removed-library management entry.
- TV Discovery navigation / hydration loading is handled by Phase 4.10.6.

## Phase 4.10.6 TV Discovery Navigation Notes

- TV search / ranking Series clicks no longer require full Season Episode hydration before opening `SeriesOverviewPage`.
- Discovery calls a summary-first TV metadata path that ensures `TvSeries` and all TMDB Season summaries, including Season 0 / Specials.
- TV card repeat-click and TV pagination are guarded while a TV Series navigation request is in progress.
- Full Episode metadata is completed by `SeriesOverviewPage` background hydration or by `TvSeasonDetailPage` on-demand Season hydration.
- Discovery still does not create `MediaFile`, fake playback sources, TV AI items, or Watch Insights inputs.

## Phase 4.11 TV Scan Identification Note

- Default scan can use the configured AI service only to suggest sanitized TV directory ranges.
- TV range hints do not affect Movie / TV Discovery search cards and do not create `AiRecommendationItem`.
- Files inside a TV range are protected from silent Movie fallback; files outside TV ranges continue to use the existing Movie identification path.
- Active TV correction and cross-type correction UI moves to Phase 4.12.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11b Default Scan AI Boundary Note

- Default scan AI uses directory-level range hints only, not per-file episode mapping.
- The scan AI schema is `directory-ranges-v1`; `episodeFiles` is not required and is ignored if returned.
- Local TV parser and TMDB verification remain responsible for actual Episode parsing and final match decisions.
- Long-timeout probing shows the new schema is lighter but can still exceed the short production timeout on large libraries, so timeout fallback remains required.
- Discovery search / ranking behavior is unchanged by this scan-only AI helper.
- TV still does not enter `AiRecommendationItem`, Watch Insights, profile/persona inputs, or recommendation fingerprints.

## Phase 4.11c Scan AI Summary Note

- Default scan AI still uses `directory-ranges-v1`; it does not request `episodeFiles`.
- Directory summaries now sample only direct video files from each directory and list child folders separately.
- Sample count follows 10% of direct videos with a 1-5 cap.
- AI output uses short `evidence` values instead of natural-language reasons.
- Discovery search / ranking cards remain unchanged; this is scan-only plumbing.
- TV still does not enter `AiRecommendationItem`, Watch Insights, profile/persona inputs, or recommendation fingerprints.

## Phase 4.11d Scan Generalization Note

- TV scan now requires multi-evidence strong TV context before enabling broad title-plus-number or bare numeric episode parsing.
- TV-risk folders can be blocked from silent Movie fallback without forcing a confident TV auto-match.
- TV query attempts carry query-source diagnostics and reject structure-only / release-only queries before TMDB search.
- TV candidate conflicts downgrade to placeholder / NeedsReview behavior instead of raising bound count.
- Movie Discovery, Movie ranking, and AI recommendation tabs remain unchanged by this scan-only hardening.

## Phase 4.11e-prep Scan AI Candidate Note

- Production default scans no longer call full AI directory range analysis by default.
- The scan-only AI range implementation remains available for diagnostics / optional experiments.
- Local TV-risk analysis emits log-only `aiCandidateRanges` for later AI-on-uncertain processing.
- `aiCandidateRanges` do not affect Movie / TV Discovery cards, ranking tabs, or AI recommendation items.
- No AI call is made for uncertain ranges in this phase.
- TV still does not enter `AiRecommendationItem`, Watch Insights, profile/persona inputs, or recommendation fingerprints.

## Phase 4.11e-prep-2 Scan Candidate Quality Note

- Movie scan identification now treats numeric-only / low-information title queries as placeholders instead of automatic matches.
- TV-risk analysis now blocks Movie fallback for generic total-count / season-range structures with multiple sequential numeric direct videos.
- `aiCandidateRanges` are merged by sanitized directory and focused on unresolved / conflict / weak-risk / fallback-blocked ranges.
- Candidate queries are separated into usable, rejected, and noisy diagnostic buckets for future AI-on-uncertain prompts.
- Local directory hints no longer use AI naming unless the source is an actual AI response.
- Discovery search / ranking cards remain unchanged; this is scan identification plumbing only.
- TV still does not enter `AiRecommendationItem`, Watch Insights, profile/persona inputs, or recommendation fingerprints.

## Phase 4.11e-prep-3 Scan Candidate Input Note

- Movie scan title cleanup now removes broader generic release / audio / source metadata before Movie TMDB search.
- Numeric-only and otherwise low-information Movie queries still become placeholders rather than automatic matches.
- Candidate queries are split into usable / noisy / rejected buckets for later AI-on-uncertain prompts.
- Final `aiCandidateRanges` summaries are emitted after TV identification so placeholder / conflict ranges produced during identification are included.
- Final candidate ranges remain log-only and sanitized; no AI call is made in this phase.
- Discovery search / ranking cards remain unchanged; this is scan identification plumbing only.
- TV still does not enter `AiRecommendationItem`, Watch Insights, profile/persona inputs, or recommendation fingerprints.

## Phase 4.11e-prep-4 Scan Auto-Bind Gate Note

- Movie scan auto-binding now requires a clear `Matched` result; `NeedsReview` remains a placeholder / future AI-candidate path.
- Movie scan logs expose `movieResultStatus`, `movieAutoApply`, and `movieAutoApplyBlockedReason`.
- TV scan applies the same rule: `NeedsReview` TV candidates are not written as matched Seasons.
- TV localized-title exact matches are downgraded when generic version qualifier / original-title conflict evidence is present.
- Movie title cleanup only receives small generic release/source/audio/subtitle improvements.
- Discovery search / ranking cards remain unchanged; this is scan identification plumbing only.
- TV still does not enter `AiRecommendationItem`, Watch Insights, profile/persona inputs, or recommendation fingerprints.

## Phase 4.11e AI-on-Uncertain Scan Note

- Scan AI now runs only for final sanitized `aiCandidateRanges`, not the full media tree.
- AI-on-uncertain returns directory / title / season hints only; it does not return `episodeFiles`, classify every file, write records, or bypass TMDB validation.
- AI hints re-enter the local TV parser, TMDB matching, conflict downgrade, and auto-bind safety gates.
- Discovery search / ranking cards remain unchanged; this is scan identification plumbing only.
- TV still does not enter `AiRecommendationItem`, Watch Insights, profile/persona inputs, or recommendation fingerprints.

## Phase 4.11e-fix-1 Scan AI Mapping Note

- AI-on-uncertain hint mapping now uses `inputRangeId` as the primary key back to final `aiCandidateRanges`.
- AI folder hints are auxiliary and no longer discard a hint when their sanitized path text differs from the local range path.
- Fuzzy sanitized-path matching remains a fallback for missing / unknown IDs only.
- Media-library batch current-list select-all is a library workflow helper and does not change Discovery behavior.
- Discovery search / ranking cards remain unchanged; this is scan identification and media-library batch plumbing only.
- TV still does not enter `AiRecommendationItem`, Watch Insights, profile/persona inputs, or recommendation fingerprints.

## Phase 4.11e-fix-2 Scan AI File Binding Note

- AI candidate ranges now carry runtime `MediaFileIds` for the files covered by each range.
- AI-on-uncertain maps by `inputRangeId` and resolves files by those `MediaFileIds`; sanitized paths remain prompt / diagnostic / fallback data.
- Range merge / dedupe preserves the file ID set while still avoiding database persistence or migration.
- AI hints remain hint-only and still pass through local parser, TMDB validation, and safety gates.
- Discovery search / ranking cards remain unchanged; this is scan identification plumbing only.
- TV still does not enter `AiRecommendationItem`, Watch Insights, profile/persona inputs, or recommendation fingerprints.

## Phase 4.11f Scan AI Refined Title Note

- AI-on-uncertain now returns refined TV title lookup hints for uncertain scan ranges.
- The refined title is used only by local TV identification for TMDB TV search; AI does not receive TMDB top-N candidates and does not choose a TMDB candidate.
- TMDB top1 still has to pass a lightweight local safety gate before any automatic TV binding.
- Discovery search / ranking cards, Movie AI recommendation inputs, Watch Insights, and media-library visibility are unchanged.

## Phase 4.11f-fix-1 Scan AI Refined Title Update

- AI-on-uncertain refined TV title lookup now searches TMDB TV with non-empty `refinedSeriesTitle` even when AI returns `needsReview=true` or low confidence.
- For the AI refined lookup path, TMDB top1 is accepted directly when a result exists; original/year/version/top-candidate conflict checks are left to later active correction.
- TMDB no-result and empty refined title still preserve placeholder / `ai-candidate`.
- This remains scan-identification plumbing only: Movie Discovery UI, Movie recommendation AI, Watch Insights, and media-library visibility semantics are unchanged.

## Phase 4.11f-fix-2 Scan AI Original-Language Title Update

- AI-on-uncertain scan refined lookup now asks for original-language TV titles and uses them before English / localized fallback titles for TMDB TV search.
- English and localized title hints remain auxiliary fallback aliases; no specific title mapping table is added.
- This remains scan-identification plumbing only: Movie Discovery UI, Movie recommendation AI, Watch Insights, and media-library visibility semantics are unchanged.

## Phase 4.11f-perf-1 Scan Performance Note

- The second TV identification pass after AI-on-uncertain is limited to AI affected `MediaFileIds`.
- Same-run TMDB search caching deduplicates repeated TV and Movie search queries without persisting cache data.
- The optimization does not change Movie Discovery UI, recommendation AI inputs, Watch Insights, media-library visibility, scan matching thresholds, or AI refined top1 behavior.

## Phase 4.11f-fix-3 Scan Closeout Note

- AI refined TV lookup now blocks only obvious `seriesYearHint` vs TMDB Series first-air-year conflicts; `seasonYearHint` is not used for this gate.
- Movie scan title cleanup receives small generic release/source/audio/subtitle cleanup, including HTML entity normalization, trailing dangling punctuation, and `3D` quality tokens.
- Movie placeholder files that form at least three strictly consecutive episode-like numbers in one direct parent folder are grouped as log-only TV-like placeholder ranges.
- This grouping does not auto-bind TV, does not create Series / Season / Episode rows, and does not affect matched Movie or TV items.
- Anime specials, folk season numbering differences, course / extras folders, and multi-source Episode cases remain deferred to active correction / Episode management.
- Movie Discovery UI, ranking cards, recommendation AI inputs, Watch Insights, and media-library visibility semantics are unchanged.

## Phase 4.11f-fix-4 Scan Placeholder Projection Note

- Consecutive numbered Movie placeholder failures are no longer only logged; they are projected as media-library `Other` / TV-like placeholder ranges at query time.

## Phase 4.11f-fix-5 Other Category Note

- The media-library `Other` category now includes ordinary unrecognized Movie placeholders, grouped TV-like placeholders, NeedsReview, and type-uncertain rows.
- Movie category is reserved for recognized Movie rows.
- Grouped TV-like placeholders are persisted as unidentified `TvSeason` / `TvEpisode` rows, are selectable, can be hidden/restored/deleted as software records, and open the existing Season detail in unidentified / pending-correction mode.
- Normal media-library mode appends those failed unidentified Seasons to `Other`, while all-failed placeholder Series are not treated as recognized TV.
- The grouped Episodes keep real playback sources and use original file names as their display titles.
- Grouping also supports conservative bracketed episode-number segments, but still requires one direct parent folder, strict contiguous numbering, and at least three failed placeholders.
- This does not change Movie Discovery ranking, recommendation, Watch Insights, scan binding, or delete-file semantics.
- Grouped placeholder files are moved out of failed Movie placeholders, while ungrouped Movie placeholders remain visible in `Other`.
- The unidentified TV rows do not bind TMDB, do not count as successful recognition, and do not change Discovery rankings, recommendation AI inputs, or media-library delete / visibility semantics.

## Phase 4.11f-fix-6 Scan Parsing Note

- Repeated title+number episode folders can now become TV-like uncertain scan candidates before Movie fallback, which reduces cases where obvious episode sequences only appear later as Movie placeholder grouping.
- Movie placeholder grouping now supports numeric quality-tail names and same-parent mixed episode patterns when the episode numbers are strictly contiguous.
- The change does not alter Movie Discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, delete-record semantics, or visibility semantics.
- `01: title`, movie collections, courses, theatrical collections, and anime-special mapping remain out of default scan scope.

## Phase 4.11f-fix-7 Scan Apply / Ignored Diagnostics Note

- Verified title+number sequence context now reaches the TV apply parser, while single title+number files remain conservative.
- Unsupported TV parse warnings now distinguish real multi-episode ranges from ordinary unsupported patterns.
- Scan discovery now emits ignored-file reason and extension summaries for local and WebDAV sources.
- Ignored extension evidence does not change the video whitelist in this phase.
- Movie Discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, delete-record semantics, and visibility semantics remain unchanged.

## Phase 4.11f-fix-8 Orphan Media Note

- Orphan video sources with no Movie / Episode binding now surface in media-library `Other` with original file-name titles.
- The scan closeout pass can group historical and newly unresolved orphan sources into unidentified Seasons / Episodes when they pass conservative same-parent contiguous episode-like rules.
- Bracketed episode segment directories can enter scan AI-on-uncertain before Movie fallback.
- `.rmvb` is now a scan video extension and `.sup` is now a subtitle extension candidate.
- Discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, delete-record semantics, visibility semantics, and ordinary Movie matching thresholds remain unchanged.

## Phase 4.11f-fix-10 TV Part Parsing Note

- Dotted TV part markers are handled inside the TV verified sequence parser, not Movie discovery.
- `Pt.2` / `Part.2` hints remain unresolved correction context and do not change Movie fallback thresholds, ranking, search, recommendation AI inputs, Watch Insights, or delete / visibility semantics.

## Phase 4.11f-fix-11 TV Part Offset Boundary

- Safe sibling part offsets are handled in TV identification only after TMDB Series / Season validation.
- The offset does not relax Movie matching, does not create Movie fallback exceptions, and does not feed TV content into Movie discovery recommendations.
- If part evidence is unsafe, the media remains in unresolved TV / Other correction surfaces rather than being treated as a Movie.

## Phase 4.11f-fix-11-hotfix TV Part Query Boundary

- Structural part-only strings are rejected as TV search queries before TMDB lookup.
- This includes AI refined title inputs when the cleaned query contains only season / part / number structure.
- Automatic TV part offset apply is temporarily disabled; unresolved part files stay in unidentified / pending-correction surfaces.
- Movie Discovery behavior is unchanged: part hints are not Movie title evidence, not Movie collection evidence, and not recommendation / Watch Insights input.

## Phase 4.11f-fix-13 AI-on-uncertain Batching

- TV scan AI-on-uncertain now uses at most 3 concurrent batches with one retry per failed batch.
- Successful batch hints still go through local TV parser / TMDB validation; failed batch ranges remain unresolved.
- Partial AI failure is warning telemetry and does not change Movie Discovery error semantics.
- Movie Discovery ranking/search, Movie recommendation AI inputs, Watch Insights, Movie fallback thresholds, and media-library visibility / delete semantics are unchanged.

## Phase 4.11f-fix-14 TV Part Offset Note

- TV part sequence offset is restored only after confirmed TMDB Series / Season validation and previous contiguous episode evidence.
- The rule does not create Movie collection handling, Movie fallback exceptions, or Movie recommendation input.
- Structural part-only strings remain rejected as TV queries and are not used as Movie title hints.
- Unsafe part ranges stay unresolved / pending correction instead of being treated as Movie matches.

## Phase 4.11f-fix-14-hotfix TV Part Lookup Note

- Unsupported-only TV part ranges can use applied AI refined / original-language titles to confirm the TV Series before safe offset evaluation.
- This does not add Movie title exceptions, Movie collection rules, Movie recommendation inputs, or Watch Insights inputs.
- If TV part offset remains unsafe, the range stays in unresolved TV / Other correction surfaces rather than Movie discovery.

## Phase 4.11g TV Scan Closeout Boundary

- Phase 4.11g accepts the default TV scan support baseline and does not change Movie Discovery ranking, search, Movie AI recommendation, or Watch Insights behavior.
- TV scan improvements remain scan / media-library plumbing: AI-on-uncertain batching, safe part offset, orphan `Other` projection, unidentified Seasons, warning/error semantics, and ignored-file diagnostics.
- Recognized Movie rows stay in the Movie category; unrecognized Movie placeholders and type-uncertain rows now belong to media-library `Other`.
- TV rows still do not feed Movie Discovery recommendation inputs, Movie discovery rankings, Watch Insights, Watch Profile, persona inputs, or recommendation fingerprints.
- Remaining cross-type correction, Episode detail, multi-source management, manual regrouping, and anime-special workflows move to Phase 4.12 / 4.13.

## Phase 4.8 Bugfix TV Parity

- TV 榜单布局与电影榜单一致：第 1 名为大卡，第 2 名后为两列。
- TV 榜单分页与电影榜单一致：第一页 21 部，后续每页 20 部，最多前 200 名。
- TV 榜单加载中禁用上一页 / 下一页，失败后恢复可用并显示错误状态。
- TV 搜索筛选区仿照电影搜索布局，提供类型、地区、入库 / Season 状态、排序、顺序、年代和语言。
- TV 类型筛选使用 TMDB TV 类型映射，不使用电影类型表。
- TV 搜索和榜单卡片按需读取 TMDB Series detail 显示总季数，不在发现页写库或补全 Season metadata。
- 未入库 TV 详情、全季 metadata 补全、无播放源 Season 展示和 TV 修正入口仍不属于本阶段。

## 榜单分页

- 榜单使用显式分页，不使用滚动加载。
- 榜单主内容区加载态和空态使用单一状态覆盖层，避免首次加载时多层提示重叠。
- 最多展示前 200 名。
- 第 1 页展示 21 部：全榜第 1 名为大卡，第 2-21 名为普通双列。
- 第 2 页起每页展示 20 部，全部为普通双列，不再放大当前页第一项。
- 排名连续切片：第 1 页 1-21，第 2 页 22-41，第 3 页 42-61，依此类推。
- 切换榜单类型或趋势时间会清空榜单页缓存并回到第 1 页。

## 复用与边界

- `+ 想看` 继续复用 `UserCollectionService`，`changeSource` 使用 `Discovery`。
- 状态合并继续复用 `DiscoveryMovieStatusResolver`。
- 未入库详情继续复用 `DiscoveryExternalMovieAdapter` 到 `AiRecommendationItem`。
- 不新增 DB 字段，不新增 migration。
- 不改推荐算法，不改 AI 推荐 Tab，不改媒体库、观影洞察、设置页或播放器。

## Phase 4.12c-fix-4 Movie Detail Lazy Probe Note

- Movie detail now performs a non-blocking lazy probe check for only the current Movie's active playback sources.
- The check reuses `MediaProbeService`, caps candidates at 10, skips current successful probe snapshots, and does not queue the full library.
- Movie detail refreshes automatically when a currently displayed source changes probe state, using the shared probe status-change notification.
- Movie source-row probe status text now uses explicit stage labels for waiting, running, completed, failed, unavailable, and skipped states.
- Movie detail source rows include an `立即探测` button that force-probes the selected current source and refreshes the detail page.
- The `立即探测` button is disabled while that source is probing. Detail-page auto probe temporarily disables checked sources during candidate evaluation, then keeps only queued / pending sources disabled and restores skipped sources.
- Scan-time media-probe enqueue is disabled; Movie probing now enters through Movie detail lazy probe or the source-row manual probe action.
- Failed Movie placeholders / orphan carriers benefit through the existing unidentified Movie detail carrier.
- This does not change Movie Discovery search, ranking, recommendation inputs, Watch Insights inputs, correction flows, delete-record semantics, visibility semantics, schema, or migrations.

## Phase 4.12d Probe Diagnostics Boundary

- Movie source rows share the updated probe wording: a successful ffprobe run with no readable media fields is shown as `已探测（未读取到媒体信息）`.
- Probe lifecycle logs now include graceful cancellation / abandoned-queue diagnostics and use extension plus stable hash fingerprints for sample file identifiers.
- This does not change Movie detail lazy probe eligibility, manual probe behavior, scan-time probe disablement, Movie Discovery search/ranking/recommendation inputs, Watch Insights inputs, correction flows, schema, or migrations.
# Phase 4.11f-fix-9 Scan Boundary Note

Movie discovery semantics are unchanged. Scan closeout now routes additional verified TV-like sequence patterns away from Movie placeholder scatter when there is same-parent sequence evidence, while short bare-number collection-like folders without TV evidence stay in Other / unknown collection handling instead of unidentified TV season projection. TV.Parse scan warnings are warning telemetry and no longer count as scan errors.
