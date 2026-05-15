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
- Add-to-library-specific labels such as `已加入媒体库` remain Phase 4.10.5 because that action does not exist yet.
- Pure visibility-only Movie rows are excluded from movie AI/profile/statistics/recommendation fingerprints and fallback external candidates.
- Real-state source-less Movie rows still represent user preference and remain eligible for movie AI/profile/recommendation inputs.
- TV Discovery still does not create `AiRecommendationItem` and TV remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.

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
