# 影片发现实现计划

## 范围

影片发现页包含三个 Tab：影片搜索、榜单、AI 推荐。本阶段只维护影片搜索和榜单，AI 推荐 Tab 继续复用现有 `RecommendationsViewModel` 与推荐流程。

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
