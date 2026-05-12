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

数据库：

- 未新增 DB 字段。
- 未新增 migration。

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
