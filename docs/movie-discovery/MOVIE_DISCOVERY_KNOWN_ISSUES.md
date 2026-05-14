# 影片发现 Known Issues

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
- Batch remove skips source-less TV Seasons and not-in-library Movies; batch delete record is the software-record removal path.
- Automatic cleanup and stale-refresh strategy for metadata-only TV rows remains deferred.

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
