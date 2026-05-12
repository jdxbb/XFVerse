# 影片发现阶段日志

## FD-2.1 影片搜索基础版

本阶段接续半成品实现，未从头重写。

完成内容：

- 在影片搜索 Tab 接入顶部搜索栏、搜索类型、静态筛选、结果网格、空状态、加载状态和加载更多按钮。
- 补齐 TMDB discovery search read model。
- 扩展 TMDB 服务：
  - `SearchDiscoveryMoviesAsync`
  - `SearchPeopleAsync`
  - `GetPersonMovieCreditsAsync`
  - `SearchDiscoveryMoviesByPersonAsync`
- 新增 TMDB 19 类 genre mapper。
- 新增发现页卡片 ViewModel。
- 新增发现页状态合并 resolver。
- 新增发现结果到 `AiRecommendationItem` 的适配器。
- 新增发现页评分 presenter，实现 TMDB + OMDb IMDb 票数加权。
- 搜索结果按 TMDB ID 合并已入库、想看、已看、喜爱、不想看状态。
- `+ 想看` 复用 `UserCollectionService`，change source 使用 `Discovery`。
- 未入库详情复用现有 `RequestExternalMovieDetail`。
- 榜单 Tab 保持占位。
- AI 推荐 Tab 未改动，继续绑定 `AiRecommendationViewModel`。

构建验证：

- `dotnet build MediaLibrary.sln`
- 0 warning
- 0 error

数据库：

- 未新增 DB 字段。
- 未新增 migration。

## FD-2.2 榜单基础版

完成内容：

- 将榜单 Tab 从占位替换为 TMDB 榜单基础版。
- 榜单类型支持：
  - 热门榜
  - 高分榜
  - 趋势榜
- 扩展 TMDB 服务：
  - `GetPopularMoviesAsync`
  - `GetTopRatedMoviesAsync`
  - `GetTrendingMoviesAsync`
- 热门榜时间文案固定为“当前热门”，不可展开。
- 高分榜时间文案固定为“历史高分”，不可展开。
- 趋势榜时间支持“今日趋势 / 本周趋势”。
- 首次进入榜单 Tab 自动加载第一页。
- 滚动接近底部加载下一页。
- 榜单最多展示前 200 名。
- 第一名使用独立大卡。
- 第 2 名开始使用双列行模型展示，双列之间有竖线。
- 榜单项显示排名、海报、`+ 想看`、加权评分、电影名｜原名、年份、类型标签、情绪标签和简介。
- `+ 想看`、状态合并、未入库详情、评分加权复用 FD-2.1 公共层。
- 影片搜索 Tab 未改业务逻辑。
- AI 推荐 Tab 未改动。

构建验证：

- `dotnet build MediaLibrary.sln`
- 0 warning
- 0 error

数据库：

- 未新增 DB 字段。
- 未新增 migration。
