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
