# 影片发现阶段日志

## FD-2.1 影片搜索基础版

- 接入影片搜索 Tab：TMDB 影片搜索、人物搜索、筛选区、结果卡片、空状态和加载状态。
- 新增 TMDB discovery read model、发现页卡片 ViewModel、状态合并 resolver、发现结果到 `AiRecommendationItem` 的适配器。
- 搜索结果按 TMDB ID 合并本地已入库、想看、已看、喜爱、不想看状态。
- `+ 想看` 复用 `UserCollectionService`，change source 使用 `Discovery`。
- 未入库详情复用现有 `RequestExternalMovieDetail`。
- AI 推荐 Tab 未改动，继续绑定现有 AI 推荐 ViewModel。
- 构建验证：`dotnet build MediaLibrary.sln`，0 warning，0 error。
- 未新增 DB 字段，未新增 migration。

## FD-2.2 榜单基础版

- 接入榜单 Tab：热门榜、高分榜、趋势榜。
- 扩展 TMDB 服务：
  - `GetPopularMoviesAsync`
  - `GetTopRatedMoviesAsync`
  - `GetTrendingMoviesAsync`
- 榜单卡片复用发现页状态合并、未入库详情、想看操作和评分展示。
- 初版使用滚动加载，最多展示前 200 名。
- AI 推荐 Tab 未改动。
- 构建验证：`dotnet build MediaLibrary.sln`，0 warning，0 error。
- 未新增 DB 字段，未新增 migration。

## FD-2.1 / FD-2.2 Bugfix

目标：只修影片搜索和榜单已知 Bug，不做最终 UI，不改 AI 推荐 Tab，不新增 DB 字段或 migration。

完成内容：

- 未入库详情自动 AI 标签：
  - 复用 `MovieDetailViewModel.LoadExternalRecommendationAsync` 的外部影片自动分类路径。
  - Discovery 未入库影片仍通过 `DiscoveryExternalMovieAdapter` 转为 `AiRecommendationItem`。
  - 增加未入库详情页内存级 TMDB ID 标签缓存，已有完整标签时不重复请求。
  - AI 标签请求失败不阻塞未入库详情展示。
  - 搜索结果卡片和榜单卡片不请求 AI 标签。

- 地区筛选：
  - 根因是地区硬依赖 `origin_country`，而 TMDB 搜索结果中该字段可能缺失，导致非“全部”地区被误过滤为空。
  - 按影片搜在非“全部”地区时给 `search/movie` 追加 `region` 请求参数。
  - 本地过滤优先使用 `origin_country`，字段缺失时使用 `original_language` 弱映射兜底。
  - “其它”按已知地区代码和已知语言集合排除。

- 搜索分页：
  - 移除“加载更多”按钮。
  - 搜索改为上一页 / 下一页 / 当前页总页数。
  - 每页最多显示 30 部。
  - 切换搜索关键词、搜索类型或筛选条件会清空分页缓存并回到第 1 页。
  - 内部缓存 TMDB 页，避免重复请求同一页。

- 搜索筛选范围：
  - 筛选不再只作用于当前已显示结果。
  - 启用筛选或非相关度排序时，从第 1 页重建搜索结果池，并按需扫描更多 TMDB 页。
  - 人物搜索仍基于人物作品集结果池做本地过滤和分页。
  - 本地观看状态筛选在状态合并后的结果池上处理。

- 榜单分页：
  - 移除 `ScrollViewer.ScrollChanged` 滚动加载。
  - 榜单改为上一页 / 下一页 / 当前页。
  - 第 1 页显示 21 部，第 1 名大卡，第 2-21 名双列。
  - 第 2 页起每页 20 部，全部普通双列。
  - 仍保留前 200 名上限。
  - 状态合并、去重和 OMDb 补分不改变 TMDB 原始顺序。

- 榜单第一名核查：
  - 当前 TMDB `movie/popular?page=1&language=zh-CN` 第一名：`奇幻变身大冒险` / `Swapped`，TMDB ID `1007757`。
  - 当前 TMDB `movie/top_rated?page=1&language=zh-CN` 第一名：`奇幻变身大冒险` / `Swapped`，TMDB ID `1007757`。
  - 未做标题硬编码。

构建验证：

- `dotnet build MediaLibrary.sln`
- 0 warning
- 0 error

## Phase 4.12-post-fix Unknown TV-Like Other Projection

- Media-library normal mode now keeps recognized Movie, recognized TV, and unknown TV-like containers separated by item kind: all-failed no-TMDB TV-like containers are projected as `Other` Series items instead of loose unknown Season items.
- Batch mode still expands to Season / grouped item granularity so unresolved unknown ranges remain selectable for future correction.
- Failed Movie placeholders and orphan single files remain `Other` and are not converted into TV containers by display projection alone.
- TV remains excluded from Movie Discovery AI recommendation input, Watch Insights, Watch Profile, persona input, and recommendation fingerprints.
- No migration was added and database update was not executed.

## Phase 4.8 TV Discovery Extension

- 影片发现页仍保持三个顶层 Tab：影片搜索、榜单、AI 推荐。
- 影片搜索 Tab 增加 Movie / TV 二级切换；默认仍是 Movie。
- Movie 搜索和人物搜索路径保持原逻辑。
- TV 搜索调用 TMDB TV search，并显示独立 TV Series 卡片。
- 榜单 Tab 增加 Movie / TV 二级切换；默认仍是 Movie。
- Movie 榜单路径保持原逻辑。
- TV 榜单接入 TMDB TV popular、top-rated、trending day、trending week。
- TV 榜单显示 Series 级排名，不伪造 Season 榜单。
- TV Series 卡片海报使用现有海报缓存行为。
- 已入库 Series 跳转 `SeriesOverviewPage`；未入库 Series 仅显示 TV 外部详情后置提示。
- TV 不进入 AI 推荐、Watch Insights、画像、观影人格或推荐 fingerprint。
- 未新增 DB 字段，未新增 migration。

## Phase 4.8 Bugfix TV Discovery Parity

- TV 榜单 UI 对齐电影榜单：第一页第 1 名为一行大卡，第 2-21 名为两列；后续页每页 20 部两列展示。
- TV 榜单上一页 / 下一页在加载中禁用，失败后恢复可用并保留错误提示。
- TV 搜索增加基础筛选区：TV 类型、地区、入库 / Season 状态、顺序、排序、年代和语言。
- TV 类型筛选使用 `TmdbTvGenreMapper`，不复用电影类型表。
- TV 搜索 / 榜单卡片通过 TMDB Series detail 显示 `共 N 季`，加载中显示季数加载状态，失败显示季数未知。
- TV Discovery 页面文案已补充电视剧搜索语义，人物搜索仍限定在 Movie 模式。
- 未入库 TV 详情页、Series 全季 metadata 补全、无播放源 Season 展示、TV 修正入口和 TV 功能缺口大审查仅记录为后续事项。
- 未新增 DB 字段，未新增 migration。

## Phase 4.10 TV Discovery Hydration Update

- TV search / ranking Series clicks now call the TV metadata hydration path before opening `SeriesOverviewPage`.
- Not-in-library TV Series no longer use the deferred external-detail placeholder.
- Hydration creates or updates TV metadata only: `TvSeries`, all TMDB Seasons including Season 0, and all TMDB Episodes.
- Hydration does not create `MediaFile`, does not fabricate playback sources, and does not route TV into Movie detail.
- Metadata-only TV Series remain not-in-library from a playback-source perspective until active Episode sources exist.
- Movie search, Movie rankings, and AI recommendations remain separate from TV.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.1 Metadata-only TV Library Visibility Note

- Discovery-hydrated TV metadata remains allowed, but pure metadata-only Series with no active Episode source and no Season state no longer pollute the default media-library list.
- Source-backed Series still appear normally and expand into all known Seasons in batch mode.
- Metadata-only Seasons with explicit user state can be surfaced in library-related views and participate in watched / unwatched batch operations.
- Phase 4.10.1 originally skipped source-less TV Seasons and not-in-library Movies with a no-source message; Phase 4.10.4 supersedes this with `LibraryVisibilityState.Hidden`.
- Batch delete record remains software-record deletion only and does not delete local or WebDAV files.
- Movie search, Movie rankings, TV search, TV rankings, and AI recommendations remain separated by media type.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.3 Library Visibility Schema

- Added `LibraryVisibilityState` as schema-only groundwork for source-less media-library visibility.
- Added the field to Movie and TV Season user-state rows with `Auto = 0` default.
- Discovery behavior is unchanged: opening not-in-library TV hydrates metadata but does not mark rows `Visible`.
- Discovery labels and filters are unchanged in this phase.
- AI recommendations and recommendation fingerprints are unchanged.
- Database update was not executed.

## Phase 4.10.4 Media Library Visibility Semantics

- Connected `LibraryVisibilityState` to media-library query and remove behavior.
- Media-library filters now separate visible rows by active source state: all visible, with active source, without active source.
- Source-less Movie / TV Season remove writes `Hidden` instead of skipping.
- Source-backed remove still removed app source rows in Phase 4.10.4; Phase 4.10.4f supersedes this with hide-only behavior.
- Discovery remains metadata-only for not-in-library TV clicks and does not write `Visible`.
- AI recommendations, Watch Insights, and recommendation fingerprints are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.10.4d Visibility Tail Bugfix

- Discovery search / ranking filters now use source wording: `全部`, `有播放源`, and `无播放源`.
- Discovery Movie and TV cards now show `有播放源` / `无播放源` as source status.
- Add-to-library-specific Discovery wording remains deferred until Phase 4.10.5.
- Pure visibility-only Movie rows are filtered out of movie AI/profile/statistics/recommendation fingerprints and fallback external candidates.
- Real-state source-less Movie rows remain eligible movie preference inputs.
- TV remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.

## Phase 4.10.4f Hide Library Items Without Removing Sources

- Movie and TV Season remove-from-library now writes `LibraryVisibilityState.Hidden` only.
- Active Movie and Episode `MediaFile` rows are not marked deleted by remove-from-library.
- Hidden source-backed rows are excluded from media-library `全部`, `有播放源`, and `无播放源`.
- Discovery remains source-oriented and can still show Hidden source-backed items as `有播放源`.
- Old `IsDeleted` source rows are not automatically restored; rescanning existing files remains the safe recovery path to verify later.

## Phase 4.10.5 Add-to-Library Actions

- Discovery Movie rows now expose add / restore actions when they are not media-library-visible.
- Discovery TV Series rows now expose add / restore actions and write `Visible` for all known Seasons after metadata is available.
- Add-to-library writes media-library visibility only; it does not set want-to-watch, favorite, not-interested, watched, or source rows.
- Hidden source-backed rows can be restored without changing active playback sources.
- TV Discovery remains excluded from `AiRecommendationItem`, Watch Insights, profile/persona inputs, and recommendation fingerprints.
- No new migration was added.
- Database update was not executed.

## Phase 4.10.5b Restore Strategy And Series Actions

- Discovery restore actions now use source / state-aware restore instead of blindly writing `Visible`.
- Hidden Movie / TV rows with active source or real current state restore to `Auto`; source-less no-state rows restore to `Visible`.
- Explicit add-to-library remains `Visible` and remains separate from preference state.
- SeriesOverview now keeps a series-level action area visible for one-season, partial, hidden, and all-visible series states.
- TV Discovery navigation and hydration loading contention is superseded by Phase 4.10.6.
- No new migration was added.
- Database update was not executed.

## Phase 4.10.6 TV Discovery Navigation And Hydration

- TV search / ranking Series clicks now use summary-first metadata hydration before opening `SeriesOverviewPage`.
- Summary-first hydration ensures `TvSeries` and TMDB Season summaries, including Season 0 / Specials, without blocking on every Season detail / Episode metadata request.
- The summary path verifies all TMDB Season summaries exist locally before skipping, so stale one-season local Series can still be completed before navigation.
- TV card repeat-click, TV search pagination, TV ranking pagination, and TV trend-time switching are disabled while a TV Series navigation request is active.
- `SeriesOverviewPage` keeps full background hydration for eventual Episode metadata completion.
- `TvSeasonDetailPage` can hydrate missing current-Season Episodes on demand and refresh the Episode list.
- The deleted-Season reopen case from TV search / ranking is handled by recreating Series / Season summary metadata.
- Discovery still does not write playback sources, preference state, TV AI rows, Watch Insights rows, or recommendation fingerprint input.
- No new migration was added.
- Database update was not executed.

## Phase 4.11 TV Scan Identification Note

- Default scan can use the configured AI service only for sanitized TV directory range hints.
- TV range hints are not Discovery results, do not create `AiRecommendationItem`, and do not change TV search / ranking cards.
- Strong TV ranges are protected from silent Movie fallback; files outside TV ranges continue to use the Movie identification path.
- Active cross-type correction UI is deferred to Phase 4.12.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11b TV Scan AI Schema Boundary

- Default scan AI range schema is reduced from `episode-files-v1` to `directory-ranges-v1`.
- AI returns Series / Season directory ranges only and does not return per-file `episodeFiles`.
- Local TV parser continues to parse Episode numbers inside accepted TV ranges.
- Long-timeout probing against the same active-video set showed valid JSON with a much smaller assistant response, but the request can still exceed the production short timeout on large trees.
- Discovery surfaces remain unchanged; this is scan identification plumbing only.
- No new migration was added.
- Database update was not executed.

## Phase 4.11c TV Scan AI Summary Optimization

- Default scan AI directory summaries now use direct-video-only samples.
- Child folder names are emitted separately from sample video files.
- Sample count is 10% of direct videos, clamped to 1-5.
- AI output uses short evidence values instead of natural-language reasons.
- Long-timeout probing showed a smaller prompt and shorter duration than Phase 4.11b, while preserving valid JSON output.
- Discovery surfaces remain unchanged; this is scan identification plumbing only.
- No new migration was added.
- Database update was not executed.

## Phase 4.11d TV Scan Generalization And Match Validation

- TV scan strong context now requires multiple independent directory / episode / season evidence signals.
- Single title-number, bare numeric, or explicit episode-like evidence is treated as weak until directory context supports it.
- TV query generation records query source and rejects generic season / count / release-only queries before TMDB matching.
- TV candidate conflicts can downgrade automatic matches to placeholder / review instead of relying only on top title similarity.
- Movie fallback is blocked only when TV risk evidence is present; obvious Movie paths without TV risk continue through Movie identification.
- Discovery UI, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-prep AI-on-Uncertain Candidate Preparation

- Default production scan no longer calls the full AI directory range request.
- The scan-only AI directory range code remains available for diagnostic / optional experiment use.
- Local TV-risk analysis emits log-only `aiCandidateRanges` for future AI-on-uncertain processing.
- TV candidate conflict and placeholder paths emit candidate-range diagnostics without calling AI.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-prep-2 Scan Candidate Range Quality Fixes

- Movie scan identification now blocks numeric-only / low-information queries from automatic TMDB binding and records a Movie placeholder instead.
- TV-risk pre-analysis now catches total-count / season-range hints combined with multiple sequential numeric direct videos so those files do not silently fall into Movie fallback.
- `aiCandidateRanges` are refined for later AI-on-uncertain processing: successful strong TV ranges are not emitted just because they are strong, repeated ranges are merged by sanitized directory, and query diagnostics are split into usable / rejected / noisy buckets.
- Local directory hints no longer use AI-oriented names unless the hint came from an actual AI response.
- Full AI directory range analysis remains disabled by default.
- No AI call is made for uncertain ranges in this phase.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-prep-3 Scan Candidate Input Finalization

- Movie scan identification now applies broader generic release / audio / source cleanup before Movie TMDB search.
- Movie cleanup remains pattern-based and does not add specific movie title, release group, folder, or TMDB ID special cases.
- Movie diagnostics record raw title, cleaned title, removed noise category, query quality, and low-information blocking state.
- TV candidate query diagnostics keep usable, noisy, and rejected buckets separate for later AI-on-uncertain input.
- TV placeholder / conflict / unresolved ranges emitted during TV identification are merged into a final run-level `aiCandidateRanges` summary.
- The final summary is sanitized, de-duplicated by directory, and includes risk tags, query buckets, conflict counts, and fallback-block counts.
- Same-name / version conflicts remain downgrade-only and are not locally forced into a specific match.
- Full AI directory range analysis remains disabled by default, and this phase does not call AI for uncertain ranges.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-prep-4 Scan Auto-Bind Validation Gates

- Movie scan identification no longer applies `NeedsReview` candidates to Movie metadata. Only clear `Matched` results auto-bind during default scan.
- Movie low-confidence, dirty-query, low-information, and conflict-like outcomes remain placeholders / future AI-candidate diagnostics.
- Movie diagnostics record `movieResultStatus`, `movieAutoApply`, and `movieAutoApplyBlockedReason`.
- Movie title cleanup received a small generic release/source/audio/subtitle extension, including spaced source tokens and symbol-heavy trailing release tails.
- TV scan identification now applies the same auto-bind gate: `NeedsReview`-level candidates become placeholders / AI-candidate diagnostics instead of matched Seasons.
- TV localized-title exact-match candidates are downgraded when a generic version qualifier / original-title conflict is detected.
- Full AI directory range analysis remains disabled by default, and this phase does not call AI for uncertain ranges.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e AI-on-Uncertain Directory Assistance

- Scan AI-on-uncertain now uses only final sanitized `aiCandidateRanges`; full AI directory range analysis remains disabled.
- The AI prompt returns directory / title / season hints keyed by `inputRangeId`, not `episodeFiles` or per-file Season / Episode decisions.
- Low-confidence, `needsReview`, unknown-range, malformed, empty, timeout, or provider-error AI results preserve local placeholders / candidate ranges.
- Accepted AI hints are added as `ai-on-uncertain` scan hints and then validated through the existing local TV parser, TMDB query, candidate conflict downgrade, and auto-bind gates.
- AI does not write records directly and does not change Movie `NeedsReview` or TV conflict behavior.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-fix-1 Scan AI Hint Mapping

- AI-on-uncertain now applies accepted hints primarily by `inputRangeId`.
- AI-returned directory hints are auxiliary diagnostics and no longer need to exactly match sanitized local directory strings.
- Directory mismatch is logged without discarding the mapped range; fuzzy path matching remains only as a fallback for unknown IDs.
- AI hints still do not write records directly and still pass through local parser, TMDB validation, and safety gates.
- Media-library batch mode adds current-list select-all and clear-selection helpers for scan retesting.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-fix-2 Scan AI Candidate Range File Binding

- AI candidate ranges now carry runtime `MediaFileIds` so accepted AI-on-uncertain hints can recover their covered files without relying on sanitized path parent/child matching.
- Candidate range merge / dedupe now merges `MediaFileIds` with risk tags, queries, conflict reasons, and sample diagnostics.
- AI hint application resolves files by `inputRangeId -> MediaFileIds` first and uses sanitized path matching only as fallback.
- Diagnostics report `rangeMediaFileCount`, file resolution method, and aggregate counts for ranges with / without files and applied-by-media-file-ids.
- AI hints still do not write records directly and still pass through local parser, TMDB validation, and safety gates.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11f Scan AI Refined Title Lookup

- AI-on-uncertain now requests refined TV lookup titles for uncertain scan ranges instead of generic directory title hints.
- AI still does not receive TMDB top-N candidates, select TMDB candidates, return episodeFiles, or write records.
- Local TV identification searches TMDB TV with the refined title and evaluates top1 with a lightweight safety gate before automatic binding.
- Safety-gate failures remain placeholder / NeedsReview / ai-candidate and do not affect Movie Discovery, AI recommendations, Watch Insights, or media-library visibility.
- No new migration was added.
- Database update was not executed.

## FD-2.1 / FD-2.2 Final Log Audit

收尾日志与运行侧检查：

- `dotnet test MediaLibrary.sln --no-build --verbosity normal` 执行成功，VSTest 目标 0 warning / 0 error；当前解决方案未发现独立测试项目输出。
- 日志落点检查：
  - 工作区 `logs\` 存在 `ai-perf-debug.log`、`ai-pool-debug.log`、`mpv-playback.log`、`video-cache-debug.log`、`watch-completion.log`。
  - `%LocalAppData%\MediaLibrary\logs` 不存在。
  - `src\MediaLibrary.App\bin\Debug\net8.0-windows\logs` 不存在。
  - `src\MediaLibrary.Core\bin\Debug\net8.0\logs` 不存在。
- 近窗口日志检查：
  - `2026-05-13 04:00:00` 后 `ai-pool-debug.log`、`mpv-playback.log`、`video-cache-debug.log`、`watch-completion.log` 未发现 error / exception / failed / fatal / 失败 / 错误 / 异常关键词。
  - `ai-perf-debug.log` 仅发现两条 AI 推荐 preview `TaskCanceledException` cancellation 记录，属于前台刷新/预览被取消的协调噪声，不属于影片发现搜索或榜单路径。
- Windows Application 事件日志检查：
  - 最近 6 小时未发现 `MediaLibrary`、`.NET Runtime` 或 `Application Error` 相关应用崩溃事件。
- XAML / ViewModel 绑定回查：
  - 未发现旧的 `LoadMoreSearch`、`LoadMoreRankings`、`IsRankingLoadingMore`、`ScrollChanged` 榜单滚动加载绑定残留。
  - 新的搜索 / 榜单状态覆盖层绑定均能在 `MovieDiscoveryViewModel` 找到对应属性。

数据库：

- 未新增 DB 字段。
- 未新增 migration。

## Phase 4.11f-fix-1 Scan AI Refined Title Update

- AI-on-uncertain prompt now asks for the best clean TV `refinedSeriesTitle` instead of using `needsReview` as an auto-apply judgement.
- Non-empty AI refined titles proceed to local TMDB TV lookup even when AI marks the range as low confidence or `needsReview=true`.
- The AI refined lookup path accepts TMDB top1 directly when a result exists and still keeps AI out of direct database writes.
- Original-title, localized-title, year, version, and top-candidate conflict checks are no longer hard blockers for this AI refined path.
- Empty refined title and TMDB no-result preserve placeholder / `ai-candidate`.

## Phase 4.11f-fix-2 Scan AI Original-Language Title Update

- AI-on-uncertain prompt now asks for original-language TV titles first, with English and localized titles as fallback aliases.
- Local scan refined lookup uses the AI title priority `originalLanguageTitle -> searchTitle -> englishTitleHint -> localizedTitleHint -> legacy refined title`.
- Diagnostics record the selected search title, title source, missing original-language title state, and English / localized fallback use.
- AI still does not receive TMDB top-N candidates, select TMDB candidates, return episodeFiles, or write records directly.
- This is scan plumbing only; Movie Discovery, AI recommendations, Watch Insights, and media-library visibility are unchanged.

## Phase 4.11f-perf-1 Scan Performance Update

- The post-AI TV retry now runs only for AI affected `MediaFileIds`, not the full scan set.
- A per-scan TMDB search cache deduplicates repeated TV and Movie search queries during scan identification.
- TV and Movie caches are separate, runtime-only, and not persisted.
- This is performance plumbing only. Movie Discovery UI, ranking cards, recommendation AI inputs, Watch Insights, and media-library visibility semantics are unchanged.

## Phase 4.11f-fix-3 Scan Closeout Guards

- AI refined TV top1 auto-apply now checks only AI `seriesYearHint` against TMDB Series first-air year and blocks only differences greater than two years.
- `seasonYearHint` is logged but does not block Series matching.
- Movie title parsing now normalizes HTML entities, trims dangling trailing punctuation, and removes only generic release/source/audio/subtitle noise such as `3D` quality tokens.
- Movie placeholder failures are analyzed after Movie identification. Files in the same direct parent folder with at least three strictly consecutive episode-like numbers are logged as TV-like placeholder ranges.
- The placeholder grouping is diagnostic / future-correction data only. It does not create Series, Season, Episode, or new schema.
- Movie Discovery UI, ranking cards, recommendation AI inputs, Watch Insights, and media-library visibility semantics are unchanged.
- No new migration was added and database update was not executed.

## Phase 4.11f-fix-4 Grouped Placeholder Projection

- Consecutive numbered Movie placeholder failures are now projected as media-library `Other` / TV-like placeholder ranges through runtime read models.
- The grouped range keeps `MediaFileIds`, file count, direct parent display, number span, samples, and reason tags for later correction or manual aggregation.
- Grouped Movie placeholder files are filtered out of the normal Movie scatter list; ungrouped Movie placeholders remain unchanged.
- The grouped range is unresolved data only. It does not create Series, Season, Episode, TMDB binding, or new schema.
- Movie placeholder grouping diagnostics now report query-time read-model persistence instead of `log-only`.
- AI refined year-gate logging is made consistent when the gate blocks auto-apply, and Movie cleaner receives only a tiny generic release-context cleanup.
- Movie Discovery rankings, recommendation AI inputs, Watch Insights, and media-library delete / visibility semantics are unchanged.

## Phase 4.11f-fix-5 Other Category Completion

- Media-library `Other` is now the display category for unrecognized / placeholder / NeedsReview / type-uncertain rows, not only grouped TV-like placeholders.
- Recognized Movie rows stay in Movie; ordinary failed Movie placeholders are projected into `Other`.
- Grouped TV-like placeholder ranges now persist through existing unidentified TV rows: a no-TMDB `TvSeries`, failed `TvSeason`, and failed `TvEpisode` rows are created without binding TMDB or marking recognition successful.
- The grouped files are moved from failed Movie placeholders to the unidentified Episodes, so playback, watched / unwatched marking, detail-page marking, hide / restore, and delete-record operations reuse the normal Season / Episode paths.
- Normal media-library mode now appends failed unidentified Seasons into `Other`; all-failed placeholder Series are not surfaced as recognized TV summaries.
- Movie placeholder grouping now recognizes conservative bracketed episode-number segments, still limited to the same direct parent folder, strict contiguous numbering, and at least three failed placeholders.
- Unidentified Episode titles use the original source file name, not the cleaned Movie query.
- The visible recognition-status filter is hidden from the main library UI; backend status data is retained for future correction workflows.
- Movie Discovery rankings, recommendation AI inputs, Watch Insights, and scan binding rules are unchanged.

## Phase 4.11f-fix-6 Scan Sequence Parsing Update

- TV scan preanalysis now admits repeated title+number folders into TV-like uncertain candidate ranges before Movie fallback, but still does not auto-bind a single title+number file.
- Multi-episode false positives are reduced by requiring plausible explicit ranges; years and quality numbers after a normal episode marker are not treated as second episodes.
- Unsupported TV parsing diagnostics now include sanitized sample names, match kind, detected range, and pattern where available.
- Explicit TV episode markers support four-digit episode numbers; bare four-digit filenames remain excluded from global TV parsing.
- Movie placeholder grouping now recognizes numeric filenames with quality/source/codec tails and can merge same-parent mixed episode patterns into one strict contiguous run.
- This is scan plumbing only. Movie Discovery rankings, recommendation AI inputs, Watch Insights, media-library delete / visibility semantics, and Movie AI classification behavior are unchanged.

## Phase 4.11f-fix-7 Scan Apply / Ignored Diagnostics Update

- Verified title+number TV-like sequences now apply during TV episode parsing after AI / TMDB validation, so those ranges no longer stop at candidate admission only.
- Generic TV parse failures no longer reuse the multi-episode unsupported reason unless an explicit multi-episode range is detected.
- Local and WebDAV scan discovery now logs ignored files by reason and extension with sanitized samples.
- Ignored extension diagnostics are evidence-only; this phase does not add formats to the video whitelist.
- Movie Discovery rankings, recommendation AI inputs, Watch Insights, media-library delete / visibility semantics, and Movie AI classification behavior are unchanged.

## Phase 4.11f-fix-8 Orphan / Other Category Update

- Orphan video `MediaFile` rows that have no Movie binding and no Episode binding now appear as `Other` unrecognized file items instead of disappearing from every media-library category.
- Unrecognized file items use their original safe file names as display titles; Movie cleaner output remains search-only and is not shown as the primary unresolved title.
- The scan closeout grouping pass now also considers historical orphan rows and newly unresolved single-source rows under the enabled scan paths. Strict same-parent contiguous episode-like runs can become unidentified TV Seasons / Episodes without TMDB binding.
- Bracketed episode segment sequences are promoted to TV-like uncertain candidates before Movie fallback, so they can be reviewed by AI-on-uncertain instead of only appearing after Movie placeholder grouping.
- `.rmvb` is accepted as a video extension and `.sup` as a subtitle extension. Playback / rendering support for those formats remains separate from scan admission.
- Movie Discovery rankings, recommendation AI inputs, Watch Insights, media-library delete / visibility semantics, Movie AI classification behavior, and TMDB thresholds are unchanged.

## FD-2.1 / FD-2.2 Closeout

收尾检查与人工验收反馈修复：

- 未入库详情 AI 标签生成中占位：
  - 当外部影片缺少 AI / 情绪 / 场景标签并触发自动生成时，详情页缺失字段显示“AI 正在分析影片”。
  - 已有标签或缓存标签仍直接展示，不重复请求 AI。
  - 生成失败不阻塞详情页，缺失字段回落为“尚未分类”。

- 搜索 / 榜单加载状态：
  - 根因是主内容区同时存在加载 TextBlock 与空态 TextBlock，且 `IsSearchLoading` / `IsRankingLoading` 变化时未同步通知空态属性刷新。
  - 搜索和榜单主内容区改为单一状态覆盖层，加载态和空态共用一个 TextBlock。
  - 加载时折叠结果 ScrollViewer 和分页控件，避免旧结果层、分页层与加载提示重叠。
  - 补齐 `ShowSearchStatusOverlay`、`SearchStatusOverlayText`、`ShowRankingStatusOverlay`、`RankingStatusOverlayText` 的状态通知。

构建验证：

- `dotnet build MediaLibrary.sln`
- 0 warning
- 0 error
# Phase 4.11f-fix-9 Scan Closeout Notes

- Placeholder / orphan grouping now understands additional verified TV-like sequence shapes before they reach Movie fallback: fansub bracket numbers, bracket episode segments reused at apply time, and leading-number-title files with explicit season context.
- Long-running numeric unknown ranges can be grouped with a small gap ratio, but missing numbers are diagnostics only and do not fabricate Episode rows.
- A bare-number movie collection guard keeps short `1..5` collection-like folders without TV evidence out of unidentified TV season projection.
- Scan warnings from TV.Parse no longer inflate scan `ErrorCount`; final scan diagnostics log raw warning count, deduplicated warning count, and `warningsIncludedInErrorCount=false`.
- Scan discovery now ignores macOS AppleDouble `._*` resource fork files before media type detection, preventing `._*.mkv` from becoming Movie placeholders or AI candidates.
- Movie discovery, Movie AI recommendation inputs, Watch Insights, and normal Movie fallback thresholds are unchanged.

# Phase 4.11f-fix-10 TV Part Parsing Note

- TV verified title-number parsing now preserves dotted part markers such as `Pt.2` instead of truncating them as file extensions.
- Part hints are recorded for unresolved TV correction but are not converted into Movie fallback rules or Movie discovery behavior.
- Movie discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, delete-record semantics, and visibility semantics remain unchanged.

# Phase 4.11f-fix-11 Safe Part Offset Note

- TV part offset is now allowed only after the same TMDB Series and Season are established and a previous sibling part has already produced a safe contiguous episode range.
- The rule is generic and does not add Movie-side title, folder, or TMDB exceptions.
- Unsafe part ranges remain unresolved / pending correction instead of falling back into Movie matching.
- Movie discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, delete-record semantics, and visibility semantics remain unchanged.

# Phase 4.11f-fix-11-hotfix Part Query Boundary

- TV structural part-only queries are now rejected before TMDB search, including AI refined title sources.
- `Part`, `Pt`, `S Part`, `Part 2`, and season / part / number-only variants are not Movie or TV title evidence.
- The automatic sibling part offset apply path is disabled until a safer TV-side redesign is implemented.
- Dotted part parsing remains TV correction context only and does not change Movie fallback thresholds, Movie Discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, delete-record semantics, or visibility semantics.

# Phase 4.11f-fix-13 AI-on-uncertain Batching

- Scan AI-on-uncertain requests are split into at most 3 concurrent batches with one retry per failed batch.
- Successful TV AI batches still feed the existing TMDB validation path; failed batches keep their ranges unresolved.
- Partial AI batch failure is a scan warning instead of a scan error.
- Movie Discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, Movie fallback thresholds, delete-record semantics, and visibility semantics remain unchanged.

# Phase 4.11f-fix-14 Safe TV Part Offset Boundary

- Safe part sequence offset remains a TV identification concern and does not change Movie discovery or Movie fallback thresholds.
- Structural part-only strings are rejected before TV TMDB lookup and are not Movie title evidence.
- Later TV parts can bind only after TMDB Series / Season confirmation plus previous contiguous episode evidence; otherwise they stay unresolved / pending correction.
- No Movie ranking/search, Movie recommendation AI, Watch Insights, delete-record, or visibility semantics changed.

# Phase 4.11f-fix-14-hotfix Part Candidate Lookup Boundary

- Unsupported-only TV part candidates can now use applied AI refined / original-language titles for TMDB Series confirmation before safe offset evaluation.
- This remains TV-only plumbing. It does not change Movie discovery thresholds, Movie fallback behavior, Movie recommendation inputs, Watch Insights, delete-record semantics, or visibility semantics.
- Structural part-only strings remain rejected and are not Movie title evidence.

# Phase 4.11g TV Scan Closeout Boundary

- TV scan final acceptance completed as a scan and media-library closeout, not a Movie Discovery feature change.
- AI-on-uncertain batching, TV retry scoping, safe part offset, orphan `Other` projection, unidentified Seasons, warning/error semantics, `.rmvb` / `.sup` scan admission, and macOS `._*` ignores are accepted for the TV scan baseline.
- Movie Discovery search, ranking, Movie AI recommendation inputs, Watch Insights, Movie fallback thresholds, delete-record semantics, and visibility semantics are unchanged.
- Unrecognized Movie placeholders remain unresolved data under `Other`; recognized Movie rows remain the only rows in the Movie category.
- Phase 4.12 / 4.13 will own correction UI, Episode detail, multi-source handling, manual regrouping, and complex anime / special content workflows.

# Phase 4.12c-fix-4 Movie Detail Lazy Probe Boundary

- Movie detail now queues a background best-effort media probe check for the current Movie's active sources after the detail page loads.
- The lazy probe path is scoped to the current Movie detail source ids and capped at 10 candidates; it does not scan or enqueue the full library.
- Movie detail now refreshes automatically from probe status-change notifications when one of the currently displayed sources changes probe state.
- Movie source-row probe status copy now uses explicit stage language for waiting, running, completed, failed, unavailable, and skipped states.
- Movie detail source rows now include an `立即探测` action. It validates the source belongs to the current Movie and force-probes only that selected source.
- Movie detail disables the source-row `立即探测` action while the selected source is probing. Detail-page auto probe temporarily disables checked sources during candidate evaluation, then keeps only queued / pending sources disabled and restores skipped sources.
- Scan-time media-probe enqueue is disabled so large scan runs do not occupy the probe queue ahead of the current Movie detail page. Movie probing now enters through detail lazy probe or manual source-row probe.
- Failed Movie placeholders and orphan carriers reuse the same Movie detail behavior.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights, Movie fallback thresholds, delete-record semantics, visibility semantics, and correction flows are unchanged.

# Phase 4.12d Probe Diagnostics Boundary

- Movie detail source rows now distinguish successful probes with no readable technical fields from normal successful metadata updates.
- Probe lifecycle diagnostics now include graceful cancellation / abandoned-queue and worker exception records.
- Probe and ignored-file sample diagnostics now use extension plus stable hash fingerprints instead of raw sample file names.
- Movie detail lazy probe eligibility, manual probe actions, scan-time probe disablement, Movie Discovery ranking/search/recommendation inputs, Watch Insights, correction flows, visibility semantics, schema, and migrations are unchanged.

# Phase 4.12e Unidentified Movie Reset Guard

- Movie detail now disables `重置为未识别` when the loaded Movie is already a failed / unidentified placeholder.
- This covers Other / orphan single-source carriers that reuse Movie detail.
- `MovieManagementService.ResetMediaFileToUnidentifiedAsync` also rejects already-unidentified Movies, so non-UI calls cannot create another placeholder for the same already-unidentified source.

# Phase 4.12e-fix Rescan Reattach Boundary

- Rescan reattach now runs before placeholder / orphan grouping so reset-to-unidentified TV sources can safely return to an existing matched Episode when the same-source directory context is unambiguous.
- Movie reattach candidates are logged but not automatically attached in this fix; automatic Movie source reattach remains deferred because title / year matching without a prior-association tombstone can be ambiguous.
- Delete-record semantics are unchanged: deleting records clears XFVerse software history, and later scans treat the physical file as new or deleted-reappeared.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights, Movie fallback thresholds, visibility semantics, schema, and migrations are unchanged.

# Phase 4.12e UI Semantics Follow-up

- Movie detail now labels the source reset action as `从当前电影拆分`; the underlying operation still moves the selected source out of the current Movie into unidentified carrying without deleting real files.
- Failed / unidentified Movie placeholders, including orphan carriers shown through Movie detail, remain disabled for this action.
- Episode detail uses the parallel `从当前集拆分` wording. Failed / unidentified Episodes now also allow single-source detach so wrongly auto-grouped TV sources can return to Other; Movie failed placeholders and orphan carriers remain disabled for Movie-side split.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights, Movie fallback thresholds, visibility semantics, schema, and migrations are unchanged.

# Phase 4.12g Movie Detail Regression Boundary

- Movie detail source split semantics remain unchanged: `从当前电影拆分` moves the selected source into unidentified carrying without deleting real files.
- The diagnostic message for Movie source split now reports retained history / progress instead of the older resume-cleared wording.
- Movie detail playback, default source, source probe, watched state, Movie Discovery ranking / search / recommendation inputs, Watch Insights, visibility semantics, schema, and migrations are unchanged.

# Phase 4.12h Movie Detail Closeout Boundary

- Phase 4.12 closes with Movie detail source split wording set to `从当前电影拆分`.
- Failed Movie placeholders and orphan unknown carriers remain the Movie-side unidentified detail carrier. The split action stays disabled there because an orphan carrier has no multi-source current Movie boundary to split from.
- The underlying Movie split operation still detaches the selected source from the current Movie into unidentified carrying without deleting real files or changing Movie Discovery, Watch Insights, recommendation inputs, ranking, search, schema, or migration behavior.

# Phase 4.12-post Emergency Unknown TV Grouping Boundary

- TV-like failed Movie placeholder grouping now uses strict unknown TV Series / Season grouping keys before reusing existing no-TMDB unknown containers.
- Special / non-regular TV directories are skipped by automatic unknown append and grouped placeholder persistence instead of being absorbed into regular numbered unknown Seasons.
- Reused unknown TV containers preserve existing names; later placeholder ranges no longer overwrite the Series / Season display names.
- Movie Discovery ranking, Movie matching, Movie recommendations, Watch Insights inputs, Movie detail source split semantics, schema, and migrations are unchanged.
- Existing failed Movie placeholders may remain visible under Other when the conservative TV grouping safety gate skips them; Phase 4.13 correction workflows should handle manual grouping / correction.

# Phase 4.12-post Episode Split Boundary Follow-up

- TV Episode detail now allows `从当前集拆分` for single-source failed / unidentified Episodes, so an incorrectly auto-grouped TV source can be detached back to Other without deleting the real file.
- This is a TV Episode source-management boundary change only. Movie failed placeholders, orphan unknown carriers, Movie Discovery ranking / search, Movie recommendations, Watch Insights inputs, Movie matching, schema, and migrations are unchanged.

# Phase 4.13a Single Source Correction Boundary

- Movie detail now routes per-source `修正信息` through the unified single-source correction flow. Selecting a source switches to the correction tab, and clicking a Movie candidate applies the correction directly.
- The Movie target path still uses the existing Movie metadata binding logic; when the target Movie already has sources, the corrected source is appended as an additional playback source.
- The corrected source becomes the target Movie default source. If it was the old Movie default source, the old Movie recalculates default source with the local-first fallback strategy.
- Candidate-click correction now yields the UI thread, runs the apply path off the WPF dispatcher, and uses a 45-second timeout so slow TMDB calls return an error state instead of leaving the app apparently frozen.
- Movie single-source correction no longer waits for non-critical OMDb rating fetches inside the transactional apply path. TMDB metadata remains the source of the corrected Movie identity.
- Additional correction phase logs record Movie detail load, DB transaction start, DB apply, and commit without full paths or credentials.
- The correction panel now resets the target type before exposing the selected source, preventing first-entry UI flicker between TV and Movie target fields.
- Movie detail source correction resets to `修正为电影`, including failed Movie placeholders and orphan carriers. Episode detail owns the default `修正为电视剧集` behavior for unidentified Episodes.
- The TV Episode target path clears `MovieId`, binds `EpisodeId`, preserves the selected `MediaFile`, probe fields, and subtitle bindings, and does not migrate Movie collection states to TV.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights inputs, Movie fallback thresholds, delete-record semantics, schema, and migrations are unchanged.
- Phase 4.13a does not add batch AI correction, manual grouping, unknown Season target selection, ignore / blacklist, or historical data cleanup.

# Phase 4.13a-fix Target-kind Constrained AI Correction Boundary

- Movie detail single-source correction AI assist now reads the selected target kind before generating search terms.
- Movie target uses only the Movie search suggestion and Movie correction path; TV candidates are not shown or applied from that target.
- TV Episode target uses only the TV Series search suggestion and TV Episode correction path; Movie candidates are not shown or applied from that target.
- Episode detail now exposes the same target-kind constrained AI assist behavior as Movie detail.
- Detail page load returns to playback sources instead of retaining the correction tab.
- Same-detail refreshes from media probe completion preserve the user's current tab and selected correction source.
- The correction target selector ignores mouse-wheel changes while closed, avoiding accidental Movie / TV target switches during form scrolling.
- TV Episode AI assist can fill Season / Episode inputs when the AI response or local filename fallback provides a safe single-episode hint.
- Corrected-source default-source rules from Phase 4.13a are unchanged.
- Movie Discovery ranking / search / recommendation inputs, Watch Insights inputs, Movie fallback thresholds, scan identification rules, schema, migrations, and batch AI correction are unchanged.

# Phase 4.13b Join-existing Unknown Season Boundary

- Movie detail single-source correction now includes `加入已有未识别季` as an explicit target for the selected source.
- Choosing this target hides Movie and recognized-TV candidate search and shows a positive episode-number input plus a modal unknown Season picker.
- The picker groups target Seasons under expandable unknown Series rows and sorts Seasons by Season number.
- Applying it clears the source's Movie binding and binds the source to the selected unknown TV Episode.
- If the source was the old Movie default source, the old Movie recalculates default source with the local-first fallback.
- Failed Movie placeholders with no remaining source can be cleaned up by the existing safe orphan cleanup semantics; real files, probe fields, subtitle bindings, and Movie user states are not deleted or migrated.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights inputs, Movie fallback thresholds, scan identification rules, schema, migrations, and batch AI correction are unchanged.

# Phase 4.13b-fix No-source Movie Detail Semantics

- Movie detail now distinguishes three states: external not-in-library candidate, in-library Movie with no active sources, and failed / unidentified placeholder.
- Follow-up semantic tightening: Movie detail no longer keeps a separate not-in-library details section. The page has only source-backed and no-source detail states; library membership is exposed through labels and available actions.
- External TMDB candidates now use the same detail tabs and no-source source-list empty state as local no-source Movies. They remain non-playable, and `Add to library` remains an action rather than a separate page mode.
- Search and ranking cards preserve local `MovieId` status separately from active source status. Clicking a local Movie with zero active sources now opens the local Movie detail instead of the external not-in-library detail.
- Search and ranking card clicks refresh the current local Movie status before routing, so cached TMDB cards do not keep opening the external not-in-library detail after a source correction changes local state.
- Discovery cards now label local Movies with zero active sources as `暂无播放源`; true external candidates remain `未加入媒体库`.
- Movie detail shows `已入库 / 暂无播放源`, disables playback, keeps the library tabs visible, and displays an empty source-list state instead of the external not-in-library explanation.
- Media-library movie queries keep recognized local Movies visible even when active source count is zero. Hidden rows still respect `LibraryVisibilityState.Hidden`.
- Single-source correction and manual correction cleanup now preserve recognized Movies after their last source moves away. The old safe cleanup remains limited to failed / unidentified placeholders with no remaining sources.
- Movie correction clears or recalculates the old Movie default source before the moved source becomes the target Movie default source, avoiding duplicate default-source ownership during cross-Movie correction.
- Movie single-source correction now materializes a newly created target Movie before moving watch-history / collection foreign keys to it, avoiding SQLite foreign-key failure when correcting into a Movie that did not already exist locally.
- External no-source Movie candidates keep the previous not-in-library detail `Add to library` write semantics: the action writes user collection visibility state, not a fake playback source. Movie detail carries that visible-in-library state so reopening shows the `no source / in library` label and hides the add button.
- Media-library projection includes visible external no-source Movie collection rows even without a local Movie record, including rows written by the previous not-in-library detail flow.
- No-source Movie detail hides the correction tab because there is no selected playback source to correct; source-backed failed placeholders still keep single-source correction.
- Recommendations and home / collection entry clicks now open local Movie detail whenever a local `MovieId` exists, even when the item has no active source.
- Failed Movie placeholders and orphan carriers keep the unidentified / 待修正 semantics and are not reclassified as no-source recognized Movies.
- Verification: `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors; migrations diff remained empty.

# Phase 4.13c Manual Unknown Season Aggregation Cross-impact

- Media-library batch mode can now move selected failed Movie placeholder sources into a newly created no-TMDB unknown TV Season.
- This path clears `MediaFile.MovieId`, binds the same `MediaFile` to an unknown Episode, preserves real local / WebDAV files, probe fields, subtitle bindings, and Movie user states, and recalculates old Movie default source with the existing local-first fallback when needed.
- Empty failed Movie placeholders continue to use the existing safe placeholder cleanup semantics. Recognized Movies and no-source Movies are not valid inputs for manual Season aggregation.
- Movie Discovery ranking / search, Watch Insights, recommendations, Movie metadata matching, schema, migrations, and batch AI correction are unchanged.

# Phase 4.13c-fix Manual Aggregation Duplicate Guard Cross-impact

- Manual aggregation now blocks creating a new unknown Series when the entered Series title normalized-equals an existing TV Series title.
- This keeps the Movie-side failed placeholder aggregation path from silently creating duplicate same-name TV containers. Users should use correction to join existing unknown / recognized Seasons.
- Movie Discovery ranking / search, Watch Insights, recommendations, Movie metadata matching, schema, migrations, and batch AI correction are unchanged.

# Phase 4.13d Unknown Season Correction Cross-impact

- Unknown TV Season detail can now move an entire failed / no-TMDB Season into a recognized TMDB Series / Season.
- This path only moves TV Episode sources already inside the unknown Season and does not change Movie discovery ranking, search, recommendations, Watch Insights, Movie metadata matching, Movie source correction, schema, or migrations.
- The operation preserves physical local / WebDAV files, probe fields, subtitle bindings, and Movie / TV user states; no Movie source is moved unless it was already part of the unknown TV Season.

# Phase 4.13e Batch AI Correction Cross-impact

- Media-library batch AI can now move selected Movie / failed Movie placeholder sources through the same single-source correction service used by Movie detail.
- AI can choose Movie or TV Episode for single-source units, but no-source Movies are skipped and no metadata-only correction is performed.
- Movie targets still use TMDB Movie search / apply; TV targets use TV Episode correction and remain outside Watch Insights / AI recommendation inputs.
- The batch path does not change Movie discovery ranking, search card status, recommendation inputs, Movie fallback thresholds, scan identification rules, schema, or migrations.
- SP / OVA / OAD / special / theatrical content is not hard-skipped by local token filters; AI can classify it as Movie or supported TV when confident, while unsupported special mapping remains a later manual special-mapping concern.

# Phase 4.13e Follow-up Source-path Weighted AI Context

- Movie detail and batch single-source AI correction now pass a sanitized playback source path hint together with the file name.
- AI prompts treat the current Movie / TV title and existing Season / Episode metadata as weak hints during correction, while prioritizing source path hints and file names.
- Season-level batch AI samples up to 18 source rows evenly across the Season, covering low, middle, and high Episode numbers when available.
- Batch AI correction now allows up to 3 concurrent AI unit requests; database apply remains serialized to avoid concurrent SQLite write conflicts.
- Batch AI no longer hard-skips locally based on special-content tokens in source file names or folders, avoiding false skips before AI can classify the item.
- Batch AI disables batch controls, exits selection mode as soon as the operation starts, and refreshes back to the normal media-library projection before AI requests run. This keeps the UI behavior aligned with clicking the batch "Done" action first, after selected rows are snapshotted.
- Movie and TV correction AI prompts now require TMDB `original_title` / `original_name` semantics: the official original title/name. This can still be English when the official original title/name is English; translated/localized/marketing aliases are not accepted as substitutes.
- Batch AI result logs now include sanitized returned Movie / TV title fields so title compliance with TMDB original-title/original-name semantics can be audited after a run.
- Batch AI now shows a preparation/progress message immediately after the action starts, before the first AI unit completes.
- Batch AI no longer asks for or trusts AI-returned TMDB ids. Movie and TV corrections now use AI-returned original-title/original-name style titles only, then resolve the final TMDB id through local TMDB search.
- Batch Movie title resolution now ranks TMDB movie candidates by local title/year confidence instead of applying the first result blindly.
- Batch TV title fallback initially required TMDB `OriginalName` equality, but a later follow-up relaxed this local filter to improve correction hit rate while keeping the prompt-level official-name instruction.
- Detail-page and batch TV prompts now explicitly reject English/international aliases for non-English-original TV series. Final-season wording can be used as a season-number clue only when the target TMDB season number is known confidently.
- Batch AI no longer local-skips solely because source context mentions SP / OVA / OAD / special / theatrical wording; AI may classify those as Movie or supported TV when confident, otherwise it returns Skip.
- Path hints are tail-only and sanitized; full local paths, full WebDAV URLs, query strings, and credentials are not sent to the prompt.
- Movie discovery ranking, search card status, recommendation inputs, Watch Insights, scan identification rules, schema, and migrations are unchanged.

# Phase 4.13e Follow-up Batch Delete / Removed Library Cleanup

- Batch delete-record now dispatches by persisted `MovieId` / `SeasonId` before display-state labels, so no-source local Movies and no-source Seasons with existing records can be deleted from batch mode.
- External no-source Movie rows without a local `MovieId` still use the existing collection-record delete path.
- The removed-library panel no longer renders Movie rows inside expandable groups. Only TV Series groups remain collapsible and keep group-level restore / delete actions.
- Latest batch AI log review remained read-only for prompt and recognition logic; no Movie discovery prompt, TMDB matching threshold, scan rule, schema, migration, database update, commit, or push was changed.

# Phase 4.13e Follow-up AI Correction Prompt Tightening

- Movie detail and batch single-source Movie correction prompts now emphasize source file / path evidence over current metadata, and prefer the specific work title from the source over collection, franchise, pack, or parent-folder names.
- Movie AI prompts now explicitly require TMDB `original_title` semantics and tell AI not to return TMDB ids; the app continues to resolve final ids through local TMDB search and validation.
- Romanized / transliterated Movie titles are treated as aliases unless they are the official TMDB original title.
- This follow-up does not change Movie discovery ranking, Movie search card status, recommendation inputs, Watch Insights, scan identification rules, TMDB matching thresholds, schema, migrations, database update, commit, or push.

# Phase 4.13e Follow-up Split No-source Shell Consistency

- Splitting a source from a recognized TMDB Movie now preserves the old Movie record when it becomes no-source, matching single-source correction semantics.
- Empty failed Movie placeholders can still be cleaned up when they have no sources, no history, and no retained user state.
- This keeps "split from current Movie" and "correction moved the last source away" consistent: recognized metadata and user state are retained; real local / WebDAV files are not deleted.
- TV Episode split already preserves Episode / Season metadata; this follow-up did not change TV cleanup or progress rules.

# Phase 4.13e Follow-up Movie Scan Prompt Alignment

- Movie scan / identification AI search prompt now uses the same TMDB `original_title` semantics as Movie detail and batch Movie correction prompts.
- The scan prompt now tells AI not to return TMDB ids, not to substitute translated / localized / marketing aliases, and not to use collection, franchise, pack, or parent-folder titles when a specific work title is present in the source.
- Movie scan AI input now sends the selected source file name plus a sanitized tail-only source path hint instead of a full local path / WebDAV URL.
- This follow-up does not change Movie discovery ranking, Movie search card status, recommendations, Watch Insights, TMDB matching thresholds, scan binding safety gates, schema, migrations, database update, commit, or push.

# Phase 4.13e Follow-up Source Language Prompt Tuning

- Movie scan, Movie detail correction, and batch Movie correction prompts now clarify that source file/path text can identify the work, but the language or script used by the file name must not be treated as proof of TMDB `original_title` language.
- English, localized, or romanized file names are explicitly allowed to point to non-English TMDB original titles; AI should return the actual TMDB `original_title` or skip when it cannot know it.
- This is prompt-only: no alias conversion, TMDB resolver fallback, ranking threshold, schema, migration, database update, commit, or push was changed.

# AI model routing cleanup

- Legacy DeepSeek model names `deepseek-chat` and `deepseek-reasoner` are no longer added as defaults or selectable options; if an old saved setting still uses either name, runtime routing resolves it to `deepseek-v4-flash`.
- Movie scan / tagging and AI recommendation requests explicitly stay on `deepseek-v4-flash` for DeepSeek endpoints with deep thinking disabled.
- Movie detail correction AI and batch AI correction now explicitly use `deepseek-v4-pro` for DeepSeek endpoints without enabling deep thinking / high thinking.
- Watch Profile keeps `deepseek-v4-pro` plus the existing high-thinking behavior.
- Central AI routing diagnostics now record sanitized requested / resolved model, provider kind, request purpose, thinking mode, and override reason. No endpoint credential, API key, local path, or WebDAV URL is logged.
- This routing cleanup does not change prompts, TMDB matching thresholds, scan binding safety gates, correction safety gates, schema, migration, database update, commit, or push.

# Phase 4.13e Follow-up Batch TV Search Hit-rate Relaxation

- Batch AI TV correction still asks AI to return TMDB `original_name` semantics, but no longer rejects a TMDB search result solely because the returned AI series title differs from the top result's `OriginalName`.
- When TMDB TV search returns results, batch AI now accepts the top search result and logs whether the AI series title matched `OriginalName`.
- Empty TMDB TV search results still skip the unit. Existing requirements for target kind, Season number, Episode number, no empty Episode creation, and correction safety gates remain unchanged.

# Phase 4.13e Follow-up Detail TV Correction Candidate Grouping

- Movie detail single-source correction to TV Episode now uses the same collapsible TMDB Series / Season candidate list as Episode detail.
- Applying at the Series row keeps the manually entered Season/Episode numbers; applying at a Season row overwrites only the Season number and keeps the manually entered Episode number.
- Detail-page TV AI assist now clears missing Season/Episode values and asks the user to enter them manually or select a Season, instead of silently reusing stale defaults.
- This is a UI/search refinement only. Movie correction apply semantics, no-source Movie semantics, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, and push were not changed.

# Phase 4.13e Follow-up Detail TV Correction Confirmation

- Movie detail and Episode detail TV Episode correction now use the same two-step interaction: choose a TMDB Series or Season target first, then click the explicit confirm button to apply.
- Series-row selection keeps the entered Season/Episode numbers; Season-row selection fills the Season number but still lets the user edit Season/Episode before confirming.
- Candidate clicks no longer write database changes directly.
- This does not change Movie correction apply semantics, single-source correction safety rules, no-source Movie semantics, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, or push.

# Phase 4.13e Follow-up Detail Correction AI Timeout / No-result Handling

- Detail-page AI correction requests now use a 90-second timeout for Movie, TV Episode, and TV Season correction prompts.
- Batch AI correction now uses a 75-second timeout.
- Scan-stage AI concurrency was checked and left unchanged: TV uncertain-range AI runs up to 3 concurrent batch requests; full-range TV AI remains disabled by default and is a single request when enabled.
- Movie detail and Episode detail AI correction no longer use local fallback TMDB searches when AI fails or returns no usable search title. Users are prompted to enter the search term manually.
- This does not change Movie correction apply semantics, batch AI rules, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, or push.

# Phase 4.13e Fix Batch AI Movie Confidence Scoring

- Batch AI Movie target confidence now treats missing AI year or missing TMDB year as neutral `yearScore=0.70` instead of the previous stronger penalty.
- The main Movie auto-apply threshold remains `confidence >= 0.80`.
- Movie target resolution now rejects strong year conflicts (`AI year` and `TMDB year` both present with a gap greater than one year) with `movie-year-conflict`.
- A conservative unique strong match can pass below the composite threshold only when top title score is at least `0.86`, the title-score margin to the next candidate is at least `0.12`, and there is no strong year conflict.
- Movie target diagnostics now log title score, year score, confidence, top1/top2 title scores, title-score margin, `uniqueStrongMatch`, and resolution / skipped reason.
- This does not change TV target logic, detail-page correction, prompts, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, or push.

# AI adaptive concurrency for batch AI and scan movie tags

- Added a shared adaptive AI batch executor for high-volume AI request paths.
- Initial concurrency is 5. Retryable AI request failures downgrade concurrency 5 -> 3 -> 1, and a success streak upgrades it back 1 -> 3 -> 5.
- Retryable failures include request timeout, HTTP 429, 502, 503, 504, and transient network I/O failures. HTTP `Retry-After` is honored with a capped delay.
- Batch AI correction now uses adaptive request concurrency for the AI target-classification request only. Local TMDB resolution and database apply still use existing safety gates, and apply remains serialized to avoid duplicate writes.
- Scan-stage Movie recognition still does not add a new LLM call. Only the existing background Movie AI tagging queue now uses the same adaptive executor.
- TV scan uncertain-range and full-range AI concurrency are unchanged.
- New sanitized logs include `ai-adaptive-concurrency-started`, `ai-adaptive-concurrency-changed`, `ai-request-retry-scheduled`, `ai-request-retry-exhausted`, `batch-ai-concurrency-summary`, and `scan-movie-ai-concurrency-summary`.
- This does not change AI prompts, Movie / TV safety gates, recommendation inputs, Watch Insights, schema, migrations, database update, commit, or push.

# AI settings routing and adaptive threshold follow-up

- Latest batch AI logs show the adaptive executor was active with initial concurrency 5. The checked run completed 8 AI request units successfully with no retry, no downgrade, and final concurrency 5.
- Adaptive AI concurrency now upgrades after 3 consecutive successful requests instead of 8.
- Settings now exposes separate model and timeout fields for detail correction, batch AI correction, scan TV uncertain range, scan TV full range, scan Movie tagging, AI recommendation, and Watch Profile.
- The expanded routing is stored in the existing AI model setting payload for compatibility. Existing plain model-name settings are still accepted and expanded into the current defaults.
- Runtime routing still keeps detail and batch correction on Pro by default, scan and recommendation on Flash by default, and Watch Profile on Pro with high thinking by default unless the user changes the per-purpose settings.
- This does not change Movie / TV safety gates, AI prompts, recommendation inputs, Watch Insights semantics, schema, migrations, database update, commit, or push.

# Phase 4.13e Follow-up Special Content AI Target Handling

- Detail Movie correction, detail TV correction, and batch AI correction prompts now treat SP / OVA / OAD / special / theatrical wording as a decision point rather than an automatic skip.
- Batch AI is asked to return Movie, TV Episode, or TV Season when a special-looking item can be safely represented as one of those supported targets, and to skip only when it cannot be represented safely or lacks required fields.
- Target-kind constraints remain unchanged: Movie detail Movie correction cannot switch to TV, TV detail correction cannot switch to Movie, and batch Season units still cannot return Movie or single Episode targets.
- This is prompt-only. No TMDB / OMDb resolver, local confidence threshold, correction apply rule, scan safety gate, schema, migration, database update, commit, or push was changed.

# TMDB / OMDb External API Adaptive Throttling

- TMDB HTTP requests now use a shared bottom-layer adaptive throttle in `TmdbService.SendGetAsync`.
- TMDB maximum concurrency is now 8, with adaptive levels 8 -> 4 -> 2 -> 1. A retryable failure downgrades one level, then an 8-request stable observation window upgrades one level.
- TMDB requests are globally rate limited to 12 requests per second at the same bottom-layer send path.
- OMDb HTTP requests now use the same adaptive throttle in `OmdbService.SendGetAsync`, with levels 2 -> 1 -> 2 and a conservative 2 requests per second limiter.
- Retryable external API failures include timeout / transient network errors and HTTP 429, 502, 503, and 504. HTTP 400, 401, 403, 404, empty search results, missing metadata, and local safety-gate rejects are not treated as throttle failures.
- Retry uses finite attempts with exponential backoff, jitter, and `Retry-After` support. One logical request can only downgrade one level, so repeated retries for the same request do not immediately collapse concurrency to the minimum.
- Business logic, TMDB / OMDb parsing, Movie / TV identification gates, AI prompts, schema, migrations, database update, commit, and push were not changed.

# Phase 4.13e Follow-up Movie Confidence Origin-name Relaxation

- Batch AI Movie target resolution no longer rejects a single exact-year TMDB Movie result solely because the AI returned title and the TMDB original title have low string similarity.
- The main Movie auto-apply threshold remains `confidence >= 0.80`, and the existing unique strong-title pass remains unchanged.
- A conservative single-result pass was added: when TMDB returns exactly one Movie result, AI year and TMDB release year are both present and equal, and there is no strong year conflict, the Movie target can apply even if the title-confidence score is below the composite threshold.
- Strong year conflicts still skip with `movie-year-conflict`.
- Diagnostics now include `singleExactYearResult` and resolution `movie-single-exact-year-result-applied` when this pass is used.
- This does not change TV target logic, detail-page correction, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, or push.
