# 影片发现实现计划

## FD-2.1 影片搜索基础版

本阶段只实现“影片发现 > 影片搜索”Tab。榜单 Tab 保持占位，AI 推荐 Tab 继续复用现有 AI 推荐页面和 ViewModel。

## 数据源

- 搜索源只使用 TMDB。
- 按影片搜调用 TMDB movie search，并支持分页。
- 按人物搜调用 TMDB person search，取最相关人物后读取 person movie credits，合并 cast / crew 电影并去重。
- OMDb 只用于按 IMDb ID 补充 IMDb 评分，不参与搜索召回。

## 类型体系

影片发现搜索筛选只展示 TMDB 官方电影类型 19 类：

- 动作、冒险、动画、喜剧、犯罪、纪录片、剧情、家庭、奇幻、历史、恐怖、音乐、悬疑、爱情、科幻、电视电影、惊悚、战争、西部。

本地扩展类型不进入影片发现搜索筛选：

- 传记、运动、歌舞、灾难、武侠、古装。

## 评分规则

评分展示使用 TMDB vote average 与 OMDb IMDb rating 做票数加权：

- `effectiveTmdbVotes = min(tmdbVotes, 100000)`
- `effectiveOmdbVotes = min(omdbVotes, 100000)`
- `weightedScore = (tmdbScore * effectiveTmdbVotes + omdbScore * effectiveOmdbVotes) / (effectiveTmdbVotes + effectiveOmdbVotes)`

Fallback：

- TMDB 和 OMDb 都有效时显示加权分。
- 只有 TMDB 有效时显示 TMDB 分。
- 只有 OMDb 有效时显示 OMDb IMDb 分。
- 都无效时显示“暂无评分”。

Rotten Tomatoes 和 Metacritic 第一版不参与加权。

## 状态合并

搜索结果按 TMDB ID 合并本地状态：

- 库内 `Movie` 优先。
- `UserMovieCollectionItem` 补充想看、已看、不想看等 collection 状态。
- 同一 TMDB ID 多播放源只视为一个已入库影片。
- 已入库影片优先展示本地类型标签和情绪标签。
- 未入库影片没有情绪标签时不显示，不伪造。

## 详情复用

- 已入库影片点击进入现有库内详情。
- 未入库影片转换为 `AiRecommendationItem`，复用现有未入库详情入口。
- 不新增未入库详情页，不修改 `MovieDetailViewModel`。

## FD-2.2

FD-2.2 实现“影片发现 > 榜单”Tab。影片搜索 Tab 和 AI 推荐 Tab 不纳入本阶段。

## 榜单 API 映射

- 热门榜：TMDB `movie/popular`，时间文案为“当前热门”，不提供日榜/周榜/月榜。
- 高分榜：TMDB `movie/top_rated`，时间文案为“历史高分”，不提供日榜/周榜/月榜。
- 趋势榜：TMDB `trending/movie/{time_window}`。
  - 今日趋势：`trending/movie/day`
  - 本周趋势：`trending/movie/week`

榜单只使用 TMDB；OMDb 仍只用于通过 IMDb ID 补充 IMDb 评分。

## 榜单加载

- 首次进入榜单 Tab 加载第一页。
- 滚动接近底部时加载下一页。
- 每次只请求下一页，不一次拉多页。
- 最多展示前 200 名，达到上限后停止请求。
- 请求失败时保留已加载结果并显示错误提示。

## 榜单布局

- 第一名使用独立大卡，海报比普通榜单项更大。
- 第 2 名开始按 `RowViewModel` 双列展示，每行左侧排名高于右侧。
- 双列之间保留竖线分割。
- 最后一行只有左项时右侧留空。
- 描述区包含加权评分、电影名｜原名、年份、类型标签、情绪标签和简介。
- 简介区域内部滚动，避免撑高整行。

## 榜单复用规则

- `+ 想看` 复用 `UserCollectionService`，`changeSource` 使用 `Discovery`。
- 状态合并复用 `DiscoveryMovieStatusResolver`。
- 未入库详情复用 `DiscoveryExternalMovieAdapter` 转换到 `AiRecommendationItem`。
- 评分展示复用 `DiscoveryRatingPresenter` 的 TMDB + OMDb IMDb 票数加权规则。
- 不新增 DB 字段，不新增 migration。
