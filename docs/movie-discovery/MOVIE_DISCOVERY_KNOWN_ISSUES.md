# 影片发现 Known Issues

## Phase 4.12h Movie Boundary

Blocker:

- None for Movie Discovery from the Episode detail closeout.

Deferred:

- Cross-type correction, Movie / TV candidate correction, batch correction, manual grouping, and unidentified carrier correction remain outside Movie Discovery.

Noise:

- Movie detail now uses `从当前电影拆分` for the old reset-to-unidentified source action. The logic still detaches the source from the current Movie into unidentified carrying and does not delete real files.
- Failed Movie placeholders and orphan unknown carriers remain unidentified detail carriers; their split action stays disabled.
- Phase 4.12 does not change Movie Discovery ranking, search, recommendation inputs, Watch Insights, visibility semantics, schema, or migrations.

## Phase 4.10 TV Notes

- TV search / ranking not-in-library Series clicks now write metadata-only TV rows and enter `SeriesOverviewPage`.
- Metadata-only TV rows are not playback sources; they should remain not-in-library in discovery status until active Episode `MediaFile` rows exist.
- TV discovery still does not create `MediaFile`, fake `RemoteUri`, `AiRecommendationItem`, or Watch Insights input.
- Cleanup for metadata-only TV rows remains deferred.
- `ExternalTvSeriesDetailPage` is no longer planned for Phase 4.

## Phase 4.10.1 TV Library Notes

- Discovery browsing may create metadata-only TV rows, but rows with no active Episode source and no Season state are intentionally hidden from the default media-library list.
- Source-backed Series expand to all known Seasons in batch mode, including source-less Seasons and Season 0.
- Metadata-only Season watched / unwatched batch operations update TV Episode state only and do not create `WatchHistory`, `MediaFile`, or fake sources.
- Phase 4.10.1 originally skipped source-less TV Seasons and not-in-library Movies; Phase 4.10.4 supersedes this with `LibraryVisibilityState.Hidden`, while batch delete record remains the software-record removal path.
- Automatic cleanup and stale-refresh strategy for metadata-only TV rows remains deferred.

## Phase 4.10.3 Visibility Schema Notes

- `LibraryVisibilityState` exists after Phase 4.10.3 but Discovery does not use it yet.
- Source-less TV rows opened from Discovery remain metadata-only unless explicit add-to-library actions write `Visible`.
- Discovery add-to-library-specific labels are available after Phase 4.10.5, while fuller added / hidden status design can still be refined later.

## Phase 4.10.4 Visibility Semantics Notes

- Source-less media-library remove now writes `Hidden` and preserves state / metadata.
- Media-library source-state filters use active source presence instead of old in-library wording.
- Discovery metadata browsing still does not write `Visible`; only explicit add-to-library actions do.
- Phase 4.10.4d changes source-status labels to `有播放源` / `无播放源`; explicit add / restore actions are available after Phase 4.10.5.
- Pure visibility-only Movie rows should stay out of movie AI/profile/statistics/recommendation fingerprints and fallback external candidates.
- Phase 4.10.4f changes remove-from-library to hide-only semantics; it no longer disables active playback sources.
- Old `MediaFile.IsDeleted` rows created by earlier remove-from-library behavior are not restored automatically because they cannot be safely distinguished from missing files, removed paths, or delete-record cleanup; rescan recovery is a Phase 4.13 validation item.

## Phase 4.10.5 Add-to-Library Notes

- Add-to-library writes `LibraryVisibilityState.Visible` only.
- Restore-to-library is superseded by Phase 4.10.5b and now writes `Auto` when active source or real current state exists, falling back to `Visible` only for source-less no-state rows.
- Movie add-to-library does not set want-to-watch, favorite, not-interested, watched, or `MediaFile`.
- TV Series add-to-library writes `Visible` for all known Seasons, including Season 0 / Specials, after metadata is available.
- Add-to-library does not restore old `IsDeleted` source rows.
- TV Discovery remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.
- Phase 4.10.6 handles TV Discovery navigation / hydration contention with summary-first navigation and TV pagination guards.
- Phase 4.11 default scan AI is limited to sanitized TV range hints and does not affect Discovery AI recommendation semantics.
- Phase 4.11 protects strong TV ranges from silent Movie fallback; mixed-folder corrections are deferred to Phase 4.12.
- Phase 4.11b changes default scan AI to directory-range output only. It no longer asks for `episodeFiles`, but large scan trees may still need budget-driven summary batching if the short production timeout is exceeded.
- Phase 4.11c optimizes scan AI summaries with direct-video samples and short evidence fields. Discovery behavior is unchanged, and batch is still deferred unless real scans require it.
- Phase 4.11d tightens TV scan generalization: strong TV context now requires multiple evidence signals, weak TV risk blocks silent Movie fallback only when appropriate, and TMDB candidate conflicts should downgrade to placeholder / review instead of chasing bound counts.
- Phase 4.11e-prep disables default production full AI range calls and only emits log-only `aiCandidateRanges`; Discovery behavior remains unchanged.
- Phase 4.11e-prep-2 keeps scan candidate range fixes out of Discovery surfaces: Movie low-information query blocking and TV-risk fallback protection only affect scan identification plumbing.
- Numeric-only / low-information Movie scan queries now become placeholders instead of automatic TMDB binds; users can still correct them through later manual / AI-assisted correction flows.
- Phase 4.11e-prep-3 broadens generic Movie release/audio/source cleanup and emits final scan candidate summaries, but it still does not enable AI-on-uncertain or change Discovery surfaces.
- Phase 4.11e-prep-4 blocks Movie `NeedsReview` scan candidates from automatic binding and keeps them as placeholders / future AI-candidate diagnostics.
- Phase 4.11e-prep-4 also downgrades TV localized-title version conflicts and still does not enable AI-on-uncertain or change Discovery surfaces.
- Phase 4.11e enables scan AI-on-uncertain only for final sanitized candidate ranges. It is still scan plumbing and does not change Discovery ranking cards, Movie AI recommendation inputs, Watch Insights, or TV exclusion from AI surfaces.
- Phase 4.11e-fix-1 changes scan AI hint application to `inputRangeId`-first mapping. It should reduce dropped AI hints caused by sanitized directory text mismatch, but Discovery surfaces remain unchanged.
- Phase 4.11e-fix-2 binds scan AI candidate ranges to runtime `MediaFileIds`, so mapped AI hints no longer depend primarily on sanitized path parent/child matching to recover files. Discovery surfaces remain unchanged.
- Phase 4.11f uses AI refined TV title hints for local scan TMDB lookup. It remains scan plumbing only and does not change Discovery cards, Movie AI recommendation inputs, Watch Insights, or TV exclusion from AI surfaces.
- Phase 4.11f-fix-2 prefers original-language AI title hints for scan refined TV lookup, but English / localized fallback titles can still produce wrong TMDB top1 matches. These remain Phase 4.12 active correction / manual review work.
- Phase 4.11f-perf-1 adds same-run TMDB search caching and limits post-AI TV retry scope. It does not change Discovery behavior or scan matching quality; remaining wrong top1 matches still require Phase 4.12 active correction / manual review.
- Phase 4.11f-fix-3 adds only a narrow AI refined Series-year gate. Wrong top1 matches without a clear `seriesYearHint` conflict still remain Phase 4.12 active correction / manual review work.
- Phase 4.11f-fix-3 groups consecutive numbered Movie placeholders as TV-like ranges for later correction. Phase 4.11f-fix-4 / fix-5 surface those ranges through `Other`, but they remain unresolved data.
- Phase 4.11f-fix-5 moves ordinary unrecognized Movie placeholders into `Other` and stores grouped TV-like placeholders as unidentified `TvSeason` / `TvEpisode` rows. They can play and use Season detail, but remain unresolved and unbound to TMDB.
- Follow-up fixes ensure those failed unidentified Seasons are included in `Other` during normal media-library mode. Bracketed episode-number segments are grouped only under the existing conservative same-parent / strict-contiguous / minimum-three-file rules.
- Active correction, manual regrouping, full Episode detail management, and multi-source episode handling remain deferred.
- Anime specials, folk season splits, course / extras folders, and multi-source Episode cases are intentionally not solved in default scan.

## Phase 4.11f-fix-6 Scan Known Issues

Blocker:

- None.

Deferred:

- `01: title` / leading-number-colon-title, movie collections, courses, theatrical collections, anime specials, SP/OAD/OVA mapping, and multi-episode file splitting remain outside default scan.
- Title+number sequence candidates are TV-like uncertain inputs, not successful bindings. Active correction or manual grouping still owns ambiguous results.

Noise:

- Movie placeholder grouping now accepts numeric quality tails and mixed episode patterns, but only for already failed Movie placeholders in one direct parent folder with strict contiguous numbering.
- Bare four-digit numbers are intentionally not treated as episodes unless an explicit episode marker exists.
- Phase 4.11f-fix-7 ignored-file extension summaries are diagnostic evidence only; they do not imply that skipped extensions should immediately enter the video whitelist.

## Phase 4.11f-fix-8 Scan Known Issues

Blocker:

- None.

Deferred:

- `.sup` is accepted as a subtitle candidate, but playback / renderer support still needs validation.
- `.rmvb` is accepted by scan discovery; playback quality depends on the player stack and codec support.
- Orphan `Other` rows remain unresolved until active correction, manual regrouping, or later metadata binding.
- `01: title`, movie collections, courses, theatrical collections, anime specials, SP/OAD/OVA mapping, and multi-episode splitting remain outside default scan.

Noise:

- Orphan and placeholder grouping remains strict and may leave scatter items when numbering has gaps, crosses directories, or includes excluded extras / trailer tokens.

## Phase 4.10.6 TV Discovery Notes

- TV search / ranking Series clicks only require Series + Season summary metadata before navigation.
- Full Episode metadata is completed later by Series overview background hydration or by Season detail on-demand hydration.
- TV pagination and repeated TV card clicks are guarded while a TV Series navigation request is active.
- Discovery still does not create playback sources or TV AI / Watch Insights input.

## Phase 4.11f-fix-1 Scan AI Refined Title Risk

- AI refined title lookup now accepts TMDB TV top1 directly for uncertain scan ranges when `refinedSeriesTitle` is non-empty and TMDB returns a result.
- This intentionally removes the previous original/year/version refined safety gate for this path; wrong top1 matches should be handled by Phase 4.12 active correction / manual confirmation.
- Movie Discovery, Movie recommendation AI, Watch Insights, and media-library visibility are unchanged.

## 搜索

- 人物中文搜索依赖 TMDB alias / 本地化数据，不保证稳定；无结果时仍建议尝试英文名或原名。
- 影片搜索仍优先使用 TMDB `search/movie` 保留相关度召回，不切换为全文 `discover/movie`。
- 类型、年代、语言、片名排序和本地观看状态筛选是在扩展后的搜索结果池上处理，不是 TMDB 全库级精确筛选。
- 本地状态筛选无法传给 TMDB，只能在已合并状态的 TMDB 结果池内过滤。
- 片名排序是当前结果池内的本地排序，不代表 TMDB 全量搜索结果的全局片名排序。
- 地区筛选准确性受 TMDB 字段限制：
  - `search/movie` 的 `region` 更接近发行地区，不等同出品地区。
  - 本地优先使用 `origin_country`。
  - 当 `origin_country` 缺失时，使用 `original_language` 弱映射，可能把同语言不同地区影片纳入结果。
  - 中国大陆 / 香港 / 台湾在缺少 `origin_country` 时都会使用中文语言兜底，因此无法精确区分三地。
- OMDb 请求失败会降级为 TMDB 分或暂无评分，不阻塞搜索结果展示。
- “切布局”仍是占位按钮，只提示后续接入。

## 榜单

- 榜单最多展示前 200 名。
- 榜单严格保持 TMDB 返回顺序；如果 TMDB 当前榜单第一名变化，UI 会随 API 返回变化，不硬编码标题。
- 热门榜和高分榜当前人工核查结果均为 `奇幻变身大冒险` / `Swapped`，TMDB ID `1007757`，但该结果随 TMDB API 变化可能失效。
- 榜单加载态和空态已收敛为单一主内容状态覆盖层；如果后续新增状态文案，应继续避免在同一区域放置多个居中文案层。
- 情绪标签仅已入库影片或已完成未入库详情 AI 标签后可显示；榜单卡片本身不请求 AI 标签。
- OMDb 请求失败会降级为 TMDB 分或暂无评分，不阻塞榜单展示。
- 发现页海报已接入现有海报缓存行为；后续新增远程图片仍应继续使用同一行为。
- Phase 4.8 TV 榜单使用 TMDB Series 级结果；不提供 Season 榜单。
- Phase 4.8 TV 搜索 / 榜单不请求 OMDb / IMDb 评分，只显示 TMDB Series 评分或暂无评分。
- Phase 4.8 Bugfix 已补齐 TV 基础筛选，但筛选仍是当前结果池内的客户端筛选，不代表 TMDB 全量 TV 库级精确筛选。
- TV 搜索 / 榜单总季数通过 TMDB Series detail 按需加载；加载失败时显示未知，不在发现页补全 Series / Season metadata。
- 未入库 TV Series 外部详情仍是后置项，当前仅显示提示并保留卡片简介。
- TV 修正入口、跨类型修正 UI、无播放源 Season 展示和 TV 功能缺口大审查仍在后续阶段处理。

## 未入库详情

- 未入库详情会在进入详情页时自动请求 AI 标签，不在搜索或榜单卡片上提前请求。
- 自动请求期间缺失标签字段显示“AI 正在分析影片”，避免生成中误显示为“未提供”。
- 详情页使用内存级 TMDB ID 标签缓存避免同一运行会话内重复请求；该缓存不落库，重启应用后不会保留。
- AI 标签请求失败只更新状态提示，不阻塞详情页基础信息展示。

## 边界

- AI 推荐 Tab 未在本轮修改。
- 推荐算法、画像 AI、媒体库、观影洞察、首页、设置页和播放器未在本轮修改。
- 未新增 DB 字段，未新增 migration。
## Phase 4.11f-fix-9 Notes

- Short bare-number folders that look like movie collections and have no TV evidence are intentionally kept out of unidentified TV season grouping.
- Long-running unknown numeric ranges may still stay split when gaps are too large, duplicate episode numbers exist, or folder evidence is too ambiguous.
- TV.Parse warnings are no longer scan errors; real scan errors should still come from enumeration, save, access, or provider failures.
- Existing `._*` MediaFile rows from older scans may remain until covered by missing-file cleanup or manual delete-record cleanup. New scans ignore macOS AppleDouble files before Movie identification.

## Phase 4.11f-fix-10 Notes

- TV part hints such as `Pt.2` / `Part.2` are preserved for correction, but they are not Movie collection evidence and do not change Movie discovery behavior.
- Automatic part offset mapping remains deferred; unresolved part ranges should stay pending correction rather than becoming wrong Movie or TV bindings.

## Phase 4.11f-fix-11 Notes

- Safe part offset is TV-only and requires same-Series / same-Season sibling evidence before any episode remapping.
- Movie discovery does not use part hints as Movie title evidence, collection evidence, ranking input, recommendation input, or Watch Insights input.
- Part files without enough sibling evidence remain unresolved / pending correction in `Other`.

## Phase 4.11f-fix-11-hotfix Notes

- Automatic part offset apply is disabled until the TV-side evidence model is redesigned.
- Structural part-only queries are rejected before TMDB search and cannot become Movie or TV matches.
- Part hints remain correction context only; Movie Discovery, Movie recommendation AI, Watch Insights, and delete / visibility semantics are unchanged.

## Phase 4.11f-fix-13 Notes

- AI-on-uncertain batching is TV scan plumbing. Partial batch failure can leave TV ranges unresolved, but it should not change Movie discovery or Movie recommendation behavior.
- Provider rate limits or a single oversized batch can still leave some ranges unresolved; retry-time sub-batching is deferred.

## Phase 4.11f-fix-14 Notes

- Safe TV part offset can reduce unresolved later-part episodes only when previous same-Series / same-Season episode evidence is available.
- If part evidence is unsafe, the range remains in TV / Other correction surfaces; Movie discovery should not reinterpret it as a Movie title or collection.
- Movie Discovery thresholds, recommendation AI inputs, Watch Insights, and delete / visibility semantics remain unchanged.

## Phase 4.11f-fix-14-hotfix Notes

- AI refined title lookup for unsupported TV part candidates is TV-side confirmation only.
- Structural part-only strings remain rejected and should not be treated as Movie titles, Movie collection hints, recommendation input, or Watch Insights input.
- Later-part files without safe offset evidence remain unresolved / pending correction in TV / Other surfaces.

## Phase 4.11g Notes

Blocker:

- None for Movie Discovery from the TV scan closeout.

Deferred:

- Cross-type active correction, manual regrouping, Episode detail, multi-source management, TV recommendation inputs, TV Watch Insights, and anime-special mapping remain outside Movie Discovery.
- Large mixed folders can still create many unresolved `Other` rows; UI ergonomics and performance tuning remain later work.
- `.sup` is accepted as a subtitle candidate, but playback / renderer behavior still needs validation.

Noise:

- Unrecognized Movie placeholders being visible in `Other` is expected and does not mean Movie Discovery ranking/search changed.
- TV scan logs may mention Movie fallback blocking or placeholder grouping; those are scan diagnostics and are not Movie Discovery recommendation inputs.
