# Phase 7.7 观影历史 / 收藏夹 / 观影洞察执行计划

Last updated: 2026-06-07

本文件是 Phase 7.7 的唯一详细执行计划。后续进入 7.7 任一小阶段前，必须先阅读本文件，再按对应小阶段的必读清单读取文档、代码和草图。除必读项外，其它资料按实现风险和代码证据按需读取。

## 阶段状态

- 状态：7.7a 已完成；7.7b 尚未开始。
- 当前范围：观影历史、收藏夹、观影洞察，以及三页需要复用的局部 UI 规则和组件。
- 主要视觉标准：
  - `DesignDraft/screenshots/观影历史/*`
  - `DesignDraft/screenshots/收藏夹/*`
  - `DesignDraft/screenshots/观影洞察/用户画像/*`
  - `DesignDraft/screenshots/观影洞察/观影统计/*`
- 主要规格来源：
  - `DesignDraft/page-spec/watch-history-page.md`
  - `DesignDraft/page-spec/favorites-page.md`
  - `DesignDraft/page-spec/watch-insights-page.md`
- 当前用户补充：
  - 7.7 继续重点对齐草图，草图是本阶段 UI 标准。
  - `收藏夹` 与 `观影洞察` 的 Tab 必须统一复制 `影片发现` 页面的 Tab，包括位置、横线、选中下划线、左对齐方式和手工按钮条结构。
  - 如果草图与安全 / 业务语义冲突，安全和业务语义优先，并在阶段输出中记录冲突和取舍。
  - 必要的数据模型变化允许进入 7.7，但必须记录原因、影响面、migration 状态和最小替代方案；默认仍不执行 database update。

## 计划阶段运行审计

本计划阶段已经执行过临时构建并运行软件查看当前页面。临时输出和截图不作为长期文档资产入库。

审计结论：

- `观影历史` 当前功能可用，能显示 Movie / Episode 历史并已有目标日期跳转入口，但视觉仍是行式列表和旧页内大卡片，不符合草图的日期分组 + 海报卡片网格密度。内容区可滚动，但未统一到 7.7 现代滚动细则。
- `收藏夹` 当前能合并 Movie 收藏和 Season 收藏，也已有取消动作，但视觉仍是旧 TabControl / 单模板卡片，不符合草图的顶层 Tab 位置、海报网格、评分徽章和 Movie / Season 差异化字段。
- `观影洞察` 当前功能完整度高，画像和统计均有真实 ViewModel / Service 支撑，但视觉层级、状态文案、图表和卡片密度还未对齐草图。当前页面内存在较多页面局部样式和硬编码热力色，后续需要收口到资源化规则。
- `观影洞察` 草图顶部统计卡显示 `未看`，但现有 Phase 7 / Watch Insights 规则要求最终总览只显示四类状态：`已看`、`喜爱`、`想看`、`不想看`。本处按业务文档和 Movie-only 统计口径优先，执行时必须记录为草图冲突取舍。

## 总目标

Phase 7.7 负责把观影历史、收藏夹和观影洞察收口到 Phase 7 最终页面级视觉标准：

- 观影历史显示 Movie / Episode 观看记录，按日期分组，支持日期筛选和从观影洞察日历跳转定位。
- 收藏夹显示 Movie / Season 的 `喜爱` 与 `想看` 集合，支持取消当前集合状态，不新增搜索、排序、批量或删除真实文件能力。
- 观影洞察保持 Movie-only 统计 / 画像 / 推荐边界，完成画像分析和观影统计的最终视觉、状态和图表收口。
- 收藏夹和观影洞察的 Tab 统一复制影片发现页面 Tab：Shell 标题下方、内容区上方、左对齐横线、固定宽手工按钮、选中项强调色文字和同色下划线；不得使用原生 `TabItem` 可见 Header 或页内圆角 Tab 按钮组。
- 所有可滚动区域统一走现代化滚动设计；不可滚动区域不得擅自加滚动。
- 有长度上限或可能溢出的字段统一按规则省略；只有实际省略时才允许悬停显示完整允许内容。未省略的完整文本不启用悬停显示。
- UI 对齐优先复用现有 ViewModel、Service、Command 和 ResourceDictionary，不为了视觉重排重写 Core 业务逻辑。

## 全阶段固定规则

### 必读基线

每个 7.7 小阶段都必须先读：

- `AGENTS.md`
- `docs/ui-redesign/PHASE_7_7_HISTORY_FAVORITES_INSIGHTS_PLAN.md`
- `DesignDraft/PHASE_7_PLAN.md`
- `DesignDraft/PHASE_7_STAGE_LOG.md`
- `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- `DesignDraft/UI-REBUILD-README.md`
- `DesignDraft/PHASE_6_COVERAGE_MATRIX.md`
- `DesignDraft/DESIGN.md`
- `DesignDraft/resources-note.md`
- `DesignDraft/codex-ui-rules.md`
- `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md` 中关于影片发现 Tab 位置、手工按钮条、滚动和菜单的已确认规则。

### 影片发现 Tab 复制规则

适用页面：

- `FavoritesPage`
- `WatchInsightsPage`

不适用页面：

- `WatchHistoryPage` 当前没有 Tab。不得为了统一而新增无业务意义的 Tab。

必须复制的结构：

- 页面根结构优先为 `Grid > TabControl`，不在 Tab 外再包一层可见整页大卡片。
- Shell 已提供页面标题时，页面内容区不得重复放同名大标题。
- Tab 区位于 Shell 标题下方、当前 Tab 内容区上方。
- Tab 区左边缘与页面内容列对齐。
- Tab 区有贯穿内容列的底部分隔横线。
- 可见 Tab 头使用 `TabControl` 模板内的手工按钮条，原生 `TabPanel` 仅保留为 0 高度隐藏 ItemsHost。
- 可见 Tab 按钮默认沿用影片发现的 104px 坐标系；文字和选中下划线在同一坐标系内居中。
- 选中态只切换强调色文字和同色下划线，不改变线宽、不改变按钮测量宽度。
- Tab 文案按页面规格：收藏夹为 `喜爱` / `想看`；观影洞察为 `画像分析` / `观影统计`。
- Tab 状态以 ViewModel 的单一 `SelectedIndex` 或等价枚举为状态源，手工按钮只负责选择，不额外保存跨重启偏好。

不得复制的影片发现特殊行为：

- 不复制 `榜单` Tab 的多级 ContextMenu、hover 延时和榜单筛选语义。
- 不复制 `AI推荐` Tab 的偏好弹窗遮罩逻辑，除非观影洞察某个弹窗明确需要并单独记录。
- 不复制影片发现搜索 / 榜单业务状态。

### 可复用组件和基础设施

优先复用或扩展：

- 视觉资源：`PageCardStyle`、`CompactCardStyle`、`GlassPageCardStyle`、`GlassCompactCardStyle`、`GlassPrimaryButtonStyle`、`GlassSecondaryButtonStyle`、`GlassSmallButtonStyle`、`SmallIconButtonStyle`、`StatusBadgeStyle`、`LongFieldTextBlockStyle`、`EmptyStateContainerStyle`、`LoadingSpinnerTemplate`。
- Tab 基线：影片发现页的 `DiscoveryManualTabButtonStyle` / `DiscoveryTabControlStyle` 结构和 `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md` 中的 Tab 对齐规则。7.7 可抽取等价共享样式，也可在目标页复制同构模板；无论采用哪种方式，必须验证位置和对齐一致。
- 7.7a 已建立共享 Tab 样式基线：`src/MediaLibrary.App/Resources/Styles/PageTabs.xaml`，包含 `PageTopManualTabButtonStyle`、`PageTopManualLastTabButtonStyle`、`PageTopTabItemStyle`、`PageTopTabDividerStyle`、`PageTopHiddenHeaderTabControlStyle`。
- 辅助行为：`TrimmedTextToolTipBehavior`、`ScrollBarAutoRevealBehavior`、`TextScrollOverflowCueBehavior`、`PosterCacheImageBehavior`。
- 图像资源：`poster-placeholder.png`，以及 Watch Insights 已存在的人格海报资源 `Assets/WatchPersonas/**`。
- 服务和 ViewModel：
  - `IWatchHistoryService`、`WatchHistoryViewModel`
  - `IUserCollectionService`、`ITvSeasonCollectionService`、`FavoritesViewModel`
  - `IWatchStatisticsService`、`IWatchProfileService`、`WatchInsightsViewModel`
  - `INavigationStateService`、`IDataRefreshService`

如需新增组件，优先小而局部：

- `HistoryMediaCard`：Movie / Episode 历史卡片外壳。
- `FavoriteMediaCard`：Movie / Season 收藏卡片外壳。
- `InsightSectionCard`：观影洞察模块外壳。
- `InsightMetricCard`：统计总览和小指标卡。
- `InsightModuleState`：模块级 loading / empty / error / data insufficient / config missing 状态。
- `CalendarHeatmap`：观影日历展示控件或模板。
- `InsightChartSurface`：偏好图谱、节奏和口味组合图的统一边界外壳。

新增组件不应强行抽成全局资源。只有两个以上 7.7 页面真实复用，或与影片发现 Tab 统一强相关时，才考虑放入共享资源。

### 可参考页面 / 组件

- `MovieDiscoveryPage.xaml`：顶层 Tab 位置、手工按钮条、内容区起点、现代滚动、结果区密度。
- `LibraryPage.xaml`：海报卡片密度、评分徽章、标签截断、列表滚动。
- `MovieDetailPage.xaml` / `TvSeasonDetailPage.xaml` / `EpisodeDetailPage.xaml`：海报、状态徽章、详情入口、Movie / Season / Episode 字段差异化。
- `SettingsPage.xaml`：7.6 已对齐影片发现 Tab 的页面级 Tab 放置方式。
- 7.6 新增控件：`SensitiveSettingInput`、`ApiConfigCard`、`SettingFieldRow`、`CacheCategoryCard`、`ScanPathCard`、`ScanLogCard`，用于理解卡片、字段行、真实截断 Tooltip 和现代滚动接入方式。

### 滚动区域矩阵

必须可滚动：

- 观影历史：日期分组和历史卡片所在内容区整体纵向滚动。
- 收藏夹：当前 Tab 的卡片列表内容区纵向滚动。
- 观影洞察：当前 Tab 内容区纵向滚动。画像分析和观影统计各自只有一个主滚动面。
- 观影洞察图表内部只有在内容不可避免超出且规格明确时才允许局部滚动；默认应响应式换行、缩放或省略，不启用横向页面滚动。

不得擅自滚动：

- Shell 侧边栏、标题栏、页面标题和副标题。
- 收藏夹与观影洞察的顶层 Tab 区。
- 观影历史的日期筛选栏。
- 单个历史卡、收藏卡、统计卡、日历单元格、排行项、标签 chip。
- 空状态、加载状态、错误状态容器。

现代化滚动要求：

- `ScrollViewer` 使用 `VerticalScrollBarVisibility="Auto"`，默认禁用横向滚动。
- 能接入 `ScrollBarAutoRevealBehavior` 的滚动区域必须接入自动显隐。
- 列表或卡片集合不得再使用旧的常驻重滚动条。
- 鼠标滚轮在输入框、ComboBox 或图表区域上方时，不应无故阻断所在内容区滚动。

### 字段长度和悬停规则

悬停显示完整内容只适用于已经发生视觉省略且允许展示的字段。未省略的普通文本不设置 Tooltip。

| 页面 | 字段 | UI 要求 |
| --- | --- | --- |
| 观影历史 | Movie / Episode 标题 | 最多 2 行，省略后才允许 Tooltip |
| 观影历史 | 季集号、年份、观看时间、进度 | 固定短字段，不启用 Tooltip |
| 观影历史 | 文件名 / 播放源摘要 | 单行省略；本地文件名可在实际省略时 Tooltip；完整 WebDAV URL 不裸露 |
| 观影历史 | 日期分组标题 | 不省略，不启用 Tooltip |
| 收藏夹 | Movie / Season 标题 | 最多 2 行，省略后才允许 Tooltip |
| 收藏夹 | Season 辅助信息 | 单行省略，省略后才允许 Tooltip |
| 收藏夹 | 类型 / 标签 chip | 数量受控；显示不下时减少数量或省略，不给每个 chip 固定 Tooltip |
| 收藏夹 | 来源摘要 | 单行省略；不展示完整 WebDAV URL、账号或敏感路径 |
| 观影洞察 | 画像总结 / DNA / 人格文案 | 优先换行或模块内限定高度；若省略，必须提供明确展开设计或真实截断 Tooltip，不能固定 Tooltip 全量 AI 文本 |
| 观影洞察 | 标签排行、图谱节点、组合名 | 单行省略，省略后才允许 Tooltip |
| 观影洞察 | 状态 / 警告文案 | 可换行；不得泄露 API key、token、完整路径、完整 URL 或 AI 原始响应 |

### 业务和数据模型规则

- 默认不 commit、不 push、不执行 database update。
- 本阶段允许必要的数据模型变化，但执行小阶段必须先说明原因、影响面、migration 名称、回滚风险和最小替代方案。
- 不新增 migration，除非当前小阶段确认确实需要 schema 变化并在阶段输出中记录；即使新增 migration，也默认不执行 database update。
- 观影历史展示 Episode 记录不代表 Episode 进入 Watch Insights 统计或画像。
- 收藏夹展示 Season 记录不代表 Season 进入 Watch Insights 统计或画像。
- Watch Insights / Watch Profile / recommendation profile 继续保持 Movie-only，TV / Episode / Season / Series 不进入统计、画像、人格、DNA、象限、watch-vs-like 或推荐画像输入。
- 不删除本地文件，不删除 WebDAV 文件。
- 取消喜爱 / 想看只取消软件集合状态，不删除 metadata 和媒体源。
- 如果某个收藏项因当前服务缺口无法取消，不能假装可取消；必须记录为 Blocker 或 Deferred，并给出最小业务补丁方案。

## 小阶段划分

### 7.7a - 审计与共享 UI 基线

目标：

- 建立 7.7 的实现入口、运行基线、复用组件清单、Tab 复制规则、滚动矩阵和长字段矩阵。
- 只补齐必要的共享 UI 小组件或样式基线，不重排完整页面。
- 确认是否需要把影片发现 Tab 结构抽成共享资源，或在收藏夹 / 观影洞察内复制同构模板。

执行时必读：

- 全阶段固定必读基线。
- `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md`
- `src/MediaLibrary.App/Views/Pages/MovieDiscoveryPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/MovieDiscoveryViewModel.cs`
- `src/MediaLibrary.App/Resources/Styles/*.xaml`
- `src/MediaLibrary.App/Helpers/TrimmedTextToolTipBehavior.cs`
- `src/MediaLibrary.App/Helpers/ScrollBarAutoRevealBehavior.cs`
- `src/MediaLibrary.App/Helpers/TextScrollOverflowCueBehavior.cs`

可复用组件：

- 影片发现 Tab 手工按钮条结构。
- 现有全局卡片、按钮、状态、滚动和 Tooltip 资源。
- `PosterCacheImageBehavior` 和 poster placeholder。

可参考页面 / 组件：

- `MovieDiscoveryPage.xaml` 的根 `Grid > TabControl` 放置方式。
- `SettingsPage.xaml` 已对齐影片发现 Tab 的页面级 Tab 放置方式。
- `LibraryPage.xaml` 的海报卡片密度和滚动。

已有 UI 规则：

- `codex-ui-rules.md` 的长字段、滚动、敏感信息和 Movie-only 边界。
- 7.4 文档中的 Tab 对齐根因修复规则。
- 7.6 文档中的滚动和真实截断 Tooltip 规则。

阶段输出要求：

- 完成了什么：列出运行审计、复用清单、共享样式 / 控件、未抽取共享资源的原因。
- 验收标准：Tab 复制规则明确；滚动和截断矩阵明确；新增组件 build 通过；未改业务页面布局。
- 不属于该阶段：不重建历史页、收藏夹或观影洞察完整 UI；不改统计口径；不改收藏服务；不加数据库字段。
- 是否修改业务逻辑：默认无；若新增纯 App 层 UI service，说明不改变 Core 业务语义。
- 是否修改非本阶段处理的页面：如抽取共享 Tab 样式影响影片发现或设置页，必须列出受影响页面、改动内容和回归结果；否则写“无”。

7.7a 执行结果（2026-06-08）：

- 完成了什么：
  - 复核 7.7 计划、Phase 7 规则、影片发现 Tab 根因修复记录、当前资源字典、`MovieDiscoveryPage` 和 `SettingsPage` 的手工 Tab 实现。
  - 确认完整抽取通用 `TabControl` 模板会把可见按钮文案和选择命令硬耦合到具体页面 ViewModel，不适合在 7.7a 直接替换已验收页面。
  - 新增 `src/MediaLibrary.App/Resources/Styles/PageTabs.xaml`，沉淀后续 7.7 页面可复用的顶层手工 Tab 按钮、末项按钮、隐藏 Header `TabControl`、隐藏 Header `TabItem` fallback 和分隔横线样式。
  - 在 `App.xaml` 合并 `PageTabs.xaml`，仅提供新资源 key，不改现有页面引用。
  - 保留影片发现和设置页当前本地 Tab 模板，避免 7.7a 影响已验收页面；后续收藏夹和观影洞察接入时优先使用 `PageTabs.xaml` 的共享样式。
- 验收标准：
  - `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77a\"` 通过，0 warning / 0 error。
  - `PageTabs.xaml` 不硬编码页面业务文案、不绑定页面专属命令、不引入数据库字段。
  - 新资源当前未被任何页面引用，因此不会改变影片发现、设置页、历史页、收藏夹或观影洞察的运行时布局。
  - Tab 复制规则、滚动矩阵和截断矩阵仍以本文件全阶段固定规则为准。
- 不属于该阶段：
  - 未重建 `WatchHistoryPage.xaml`、`FavoritesPage.xaml` 或 `WatchInsightsPage.xaml`。
  - 未把影片发现 / 设置页切换到新共享样式。
  - 未新增历史卡、收藏卡、洞察卡、日历或图表控件。
  - 未修改统计口径、收藏服务、导航状态服务、Core 服务、数据库 schema、migration 或 database update。
- 是否修改业务逻辑：无。
- 是否修改非本阶段处理的页面：
  - `App.xaml` 只新增资源字典合并项，现有页面没有引用新样式，运行时视觉和业务行为不变。
  - 无其它非本阶段页面变化。

### 7.7b - 观影历史视觉与日期定位

目标：

- 对齐观影历史草图：Shell 标题 `观影历史`，副标题 `记录你的惬意时刻`，日期筛选栏，日期分组，Movie / Episode 海报卡片网格。
- 保留现有 `全部 / 今天 / 本周 / 本月 / 指定日期 / 刷新` 语义。
- 支持从观影洞察日历跳转到指定日期、滚动定位日期组并短暂高亮。
- 接入现代滚动和长字段真实截断 Tooltip。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/watch-history-page.md`
- `DesignDraft/screenshots/观影历史/*`
- `src/MediaLibrary.App/Views/Pages/WatchHistoryPage.xaml`
- `src/MediaLibrary.App/Views/Pages/WatchHistoryPage.xaml.cs`
- `src/MediaLibrary.App/ViewModels/Pages/WatchHistoryViewModel.cs`
- `src/MediaLibrary.Core/Models/ReadModels/WatchHistoryListItem.cs`
- `src/MediaLibrary.Core/Services/Interfaces/IWatchHistoryService.cs`
- `src/MediaLibrary.Core/Services/Implementations/WatchHistoryService.cs`
- `src/MediaLibrary.App/Services/Interfaces/INavigationStateService.cs`
- `src/MediaLibrary.App/Services/Implementations/NavigationStateService.cs`

可复用组件：

- `HistoryMediaCard` 如 7.7a 已建立。
- poster placeholder、`PosterCacheImageBehavior`、`TrimmedTextToolTipBehavior`、`ScrollBarAutoRevealBehavior`。
- 详情页已有 Movie / Season / Episode 导航命令和状态徽章样式。

可参考页面 / 组件：

- 媒体库和影片发现海报卡片网格密度。
- Episode 详情页中的 `SxxExx`、剧集标题和无图占位处理。
- 影片发现结果区的现代滚动和状态区域。

已有 UI 规则：

- 观影历史只有历史内容区滚动，标题、筛选栏、侧边栏不滚动。
- 不新增搜索、排序、删除历史、批量选择、继续播放按钮。
- 删除记录语义不属于本阶段，不能通过 UI 文案暗示可删真实文件。

阶段输出要求：

- 完成了什么：列出历史页布局、卡片、日期筛选、目标日期定位、高亮、空 / loading / error 状态和滚动改动。
- 验收标准：Movie 和 Episode 均可显示；点击 Movie 进 Movie 详情，点击 Episode 进 Season / Episode 相关详情；日期筛选有效；从日历跳转能定位；内容区现代滚动；无横向滚动；长标题只有实际省略才 Tooltip。
- 不属于该阶段：不改播放器写历史链路；不删除历史；不新增搜索 / 排序 / 批量；不让 TV Episode 进入 Watch Insights。
- 是否修改业务逻辑：预期无；如为日期定位修补 App 层导航状态，需记录为 UI 导航行为变化，不改变 Core 历史写入。
- 是否修改非本阶段处理的页面：仅允许观影洞察日历点击入口与导航状态联动；若触及其它页面，必须列出页面和原因。

### 7.7c - 收藏夹视觉 / Movie-Season 卡片 / 取消动作状态

目标：

- 对齐收藏夹草图：Shell 标题 `收藏夹`，副标题 `收藏你的独家喜好`，顶层 Tab `喜爱` / `想看` 按影片发现位置和样式放置，Movie / Season 海报卡片网格展示。
- 默认进入 `喜爱` Tab，不做跨重启 Tab 记忆。
- 显示 Movie / Season 差异化字段、评分徽章、来源 / 状态摘要和取消当前集合状态动作。
- 保留取消喜爱 / 想看动作的 loading、disabled、failed 状态。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/favorites-page.md`
- `DesignDraft/screenshots/收藏夹/*`
- `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md` 的 Tab 规则。
- `src/MediaLibrary.App/Views/Pages/FavoritesPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/FavoritesViewModel.cs`
- `src/MediaLibrary.Core/Models/ReadModels/CollectionMovieItem.cs`
- `src/MediaLibrary.Core/Services/Interfaces/IUserCollectionService.cs`
- `src/MediaLibrary.Core/Services/Interfaces/ITvSeasonCollectionService.cs`
- `src/MediaLibrary.Core/Services/Interfaces/IMovieManagementService.cs`

可复用组件：

- `FavoriteMediaCard` 如 7.7a 已建立。
- 影片发现 / 媒体库海报卡、评分徽章、chip、poster placeholder。
- 影片发现 Tab 手工按钮条结构。
- `TrimmedTextToolTipBehavior`、`ScrollBarAutoRevealBehavior`。

可参考页面 / 组件：

- 影片发现搜索卡：左上类型标签、右上状态 / 动作、评分徽章。
- 媒体库海报卡：密度、标签截断、来源摘要。
- Season 详情页：Season 标题和季辅助信息。

已有 UI 规则：

- 收藏夹只有当前 Tab 的卡片列表滚动，Tab 和标题不滚动。
- 不新增搜索、筛选、排序、批量、删除记录、扫描入口。
- 取消集合状态不删除媒体文件、不删除 WebDAV 文件、不删除 metadata。
- Season 收藏可展示，但不进入 Watch Insights。

阶段输出要求：

- 完成了什么：列出收藏夹 Tab、Movie / Season 卡片、评分、状态、取消动作、空 / loading / error 状态和滚动改动。
- 验收标准：`喜爱` / `想看` Tab 位置与影片发现一致；Movie 和 Season 都能显示；取消当前状态可用且有处理中 / 失败反馈；禁用状态必须有真实原因；长字段只在实际省略时 Tooltip；无横向滚动。
- 不属于该阶段：不新增搜索 / 排序 / 批量；不改 Watch Insights 统计；不改媒体库移出 / 删除记录语义；不删除真实文件。
- 是否修改业务逻辑：预期只复用现有集合服务。若发现库外喜爱项因缺少 `MovieId` 不能取消，先审计服务和数据模型；可做最小业务补丁，但必须记录字段 / migration / 服务影响或明确 Deferred。
- 是否修改非本阶段处理的页面：默认无；如修改共享集合服务影响详情页、媒体库或影片发现，必须列出受影响页面和回归结果。

### 7.7d - 观影洞察外壳 / Tab / 状态基线

目标：

- 对齐观影洞察草图的页面外壳：Shell 标题 `观影洞察`，副标题 `让你更懂你`，顶层 Tab `画像分析` / `观影统计` 按影片发现位置和样式放置。
- 移除旧的页内圆角 Tab 按钮组和重复页面标题。
- 建立观影洞察模块级状态基线：loading、empty、error、data insufficient、config missing、generation failed、cached fallback。
- 只处理外壳和共享状态，不重建所有画像和统计模块。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/watch-insights-page.md`
- `DesignDraft/screenshots/观影洞察/用户画像/*`
- `DesignDraft/screenshots/观影洞察/观影统计/*`
- `docs/watch-insights/WATCH_INSIGHTS_PLAN.md`
- `docs/watch-insights/WATCH_INSIGHTS_KNOWN_ISSUES.md`
- `docs/watch-insights/WATCH_INSIGHTS_STAGE_LOG.md`
- `src/MediaLibrary.App/Views/Pages/WatchInsightsPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/WatchInsightsViewModel.cs`
- `src/MediaLibrary.Core/Models/ReadModels/WatchStatisticsSnapshot.cs`
- `src/MediaLibrary.Core/Models/ReadModels/WatchProfileSnapshot.cs`

可复用组件：

- 影片发现 Tab 手工按钮条结构。
- `InsightSectionCard`、`InsightMetricCard`、`InsightModuleState` 如 7.7a 已建立。
- 现有 status / warning / empty / spinner 资源。
- `ScrollBarAutoRevealBehavior`、`TrimmedTextToolTipBehavior`。

可参考页面 / 组件：

- 影片发现顶层 Tab。
- 设置页 API Tab 的现代滚动和模块状态反馈。
- Watch Insights 当前 ViewModel 的状态投影，不新增重复状态源。

已有 UI 规则：

- 观影洞察默认打开 `画像分析`。
- 只有当前 Tab 内容区滚动，标题和 Tab 不滚动。
- 统计刷新与画像刷新保持独立。
- TV 不进入 Movie Watch Insights，不在 UI 上额外提示 “TV 被排除”。

阶段输出要求：

- 完成了什么：列出外壳、Tab、共享模块状态、滚动面、状态文案脱敏和未重建模块。
- 验收标准：Tab 位置与影片发现一致；默认画像 Tab；Tab 切换不重置主题 / 导航；画像和统计各自只有一个主滚动面；状态文案中文可读且不泄露敏感信息；build 通过。
- 不属于该阶段：不改 AI prompt；不改统计口径；不重建图表；不引入 TV Insights；不改推荐。
- 是否修改业务逻辑：预期无；如只增加 ViewModel UI 投影，记录为 UI 投影变化。
- 是否修改非本阶段处理的页面：默认无；如抽取共享 Tab 样式影响影片发现或收藏夹，必须列出并回归。

### 7.7e - 观影洞察画像分析 Tab 视觉收口

目标：

- 对齐画像分析草图：`观影口味总结`、`观影 DNA`、`你的观影人格`、`口味象限`、`看得多 vs 真喜欢`。
- 使用现有画像服务、画像缓存、人格式样和 WatchPersona 资源，不改变 AI 生成语义。
- 覆盖模块级 loading、empty、data insufficient、config missing、generation failed、cached fallback。
- 对 AI 文案做可读布局和安全截断，不展示原始 prompt、原始响应或敏感设置。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/watch-insights-page.md` 中画像分析相关章节。
- `DesignDraft/screenshots/观影洞察/用户画像/*`
- `docs/watch-insights/WATCH_INSIGHTS_PLAN.md`
- `docs/watch-insights/WATCH_INSIGHTS_KNOWN_ISSUES.md`
- `src/MediaLibrary.App/Views/Pages/WatchInsightsPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/WatchInsightsViewModel.cs`
- `src/MediaLibrary.Core/Models/ReadModels/WatchProfileSnapshot.cs`
- `src/MediaLibrary.Core/Services/Interfaces/IWatchProfileService.cs`
- `src/MediaLibrary.Core/Services/Implementations/WatchProfileService.cs`
- `src/MediaLibrary.App/Assets/WatchPersonas/**`

可复用组件：

- `InsightSectionCard`、`InsightModuleState`、`InsightChartSurface`。
- WatchPersona 海报和头像 / 边框资源。
- 全局 empty / warning / loading / error 资源。

可参考页面 / 组件：

- 当前 Watch Insights 画像绑定字段和 persona 映射。
- 详情页的人物 / 标签 / 信息卡层级。
- 草图中的口味总结 + DNA 网格 + 人格卡区域组合。

已有 UI 规则：

- 画像默认 Movie-only。
- AI 画像数据不足阈值、缓存、刷新节流和失败回退由现有服务决定，UI 不重写。
- 画像文案可换行或真实截断，但不得固定 Tooltip 全量显示。
- 旧画像缓存可能含旧人格类型，UI 应可显示并提示刷新，不自动改写缓存。

阶段输出要求：

- 完成了什么：列出画像总结、DNA、人格、象限、watch-vs-like、状态和长文案处理改动。
- 验收标准：有画像数据时五个模块都可读；数据不足 / 未配置 / 生成失败 / 旧缓存 fallback 均清晰；人格资源渲染正常；AI 长文案不撑破布局；无横向滚动；不泄露敏感字段。
- 不属于该阶段：不改 AI prompt、模型选择、画像输入聚合、缓存 fingerprint、推荐画像读取或 TV 纳入范围。
- 是否修改业务逻辑：预期无；如增加 UI 专用 projection，不改变服务输出和缓存结构。
- 是否修改非本阶段处理的页面：默认无；如修改推荐画像相关共享模型，必须记录 Recommendation / Discovery AI Tab 回归结果。

### 7.7f - 观影统计上半区：范围 / 总览 / 日历

目标：

- 对齐观影统计顶部草图：范围切换、洞察总览、观影日历、月度概览指标。
- 总览状态卡坚持四类：`已看`、`喜爱`、`想看`、`不想看`。不恢复草图中的 `未看` 卡。
- 月 / 全部范围、刷新统计、日历月份切换和日历日期点击跳转观影历史必须可用。
- 日历热力色和卡片状态资源化，不再在页面内硬编码颜色。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/watch-insights-page.md` 中观影统计总览、日历和跳转章节。
- `DesignDraft/screenshots/观影洞察/观影统计/01-*`
- `DesignDraft/page-spec/watch-history-page.md` 的日历跳转落点规则。
- `src/MediaLibrary.App/Views/Pages/WatchInsightsPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/WatchInsightsViewModel.cs`
- `src/MediaLibrary.Core/Models/ReadModels/WatchStatisticsSnapshot.cs`
- `src/MediaLibrary.Core/Services/Interfaces/IWatchStatisticsService.cs`
- `src/MediaLibrary.Core/Services/Implementations/WatchStatisticsService.cs`
- `src/MediaLibrary.App/Services/Interfaces/INavigationStateService.cs`
- `src/MediaLibrary.App/ViewModels/Pages/WatchHistoryViewModel.cs`

可复用组件：

- `InsightMetricCard`、`CalendarHeatmap`、`InsightModuleState`。
- 观影历史目标日期定位能力。
- 影片发现 / 设置页现代滚动和状态卡资源。

可参考页面 / 组件：

- 当前 Watch Insights 日历和范围切换逻辑。
- 观影历史日期筛选和目标日期高亮。
- 媒体库 / 影片发现中紧凑状态卡和按钮组。

已有 UI 规则：

- 统计刷新不能触发画像刷新或 AI 调用。
- Watch Insights 统计保持 Movie-only。
- 日历日期点击跳转观影历史，应用指定日期筛选并定位，不在统计页弹出伪结果。
- 草图 `未看` 卡与当前产品规则冲突，本阶段不实现。

阶段输出要求：

- 完成了什么：列出范围切换、四张总览卡、日历、月度指标、刷新状态、日期跳转和热力色资源化改动。
- 验收标准：本月 / 全部切换有效；刷新统计只刷新统计；四张状态卡无 `未看`；日历月份切换有效；点击有记录日期进入观影历史并定位；无记录日期显示合理空 / 反馈；状态和警告不泄露敏感信息。
- 不属于该阶段：不改统计服务口径；不改自动已看算法；不改画像；不改推荐；不纳入 TV / Episode / Season。
- 是否修改业务逻辑：预期无；如修补日历跳转到历史页的 App 层导航行为，记录为 UI 导航变化。
- 是否修改非本阶段处理的页面：允许联动 `WatchHistoryPage` 的指定日期定位；其它页面默认无。

### 7.7g - 观影统计下半区：偏好图谱 / 排行 / 节奏 / 口味组合地图

目标：

- 对齐观影统计中下部草图：`偏好图谱`、`标签排行月榜`、`观影节奏`、`口味组合地图`。
- 图表和排行均使用真实 `WatchStatisticsSnapshot` 数据，不伪造标签、计数或趋势。
- 图表视觉资源化，避免页面局部硬编码一组颜色。
- 保持一个主滚动面，图表在可用宽度内响应式排布，不添加页面横向滚动。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/watch-insights-page.md` 中偏好图谱、排行、节奏和口味组合章节。
- `DesignDraft/screenshots/观影洞察/观影统计/02-*`
- `DesignDraft/screenshots/观影洞察/观影统计/03-*`
- `src/MediaLibrary.App/Views/Pages/WatchInsightsPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/WatchInsightsViewModel.cs`
- `src/MediaLibrary.Core/Models/ReadModels/WatchStatisticsSnapshot.cs`
- `src/MediaLibrary.Core/Services/Implementations/WatchStatisticsService.cs`

可复用组件：

- `InsightChartSurface`、`InsightMetricCard`、`InsightModuleState`。
- 当前 Watch Insights 中已有的分布、排行、节奏和组合 projection。
- `TrimmedTextToolTipBehavior` 用于排行和节点标签真实截断。

可参考页面 / 组件：

- 草图中的左右分栏、Top 3 排行卡和底部组合地图。
- 当前 Watch Insights 的 Canvas / ItemsControl 图表实现，仅作为数据绑定参考，不直接接受旧视觉。
- 媒体库 / 影片发现 chip 和评分标签密度。

已有 UI 规则：

- 不为了图表“好看”硬编码电影名、标签名或样本数据。
- 数据不足时显示模块级空状态，不伪造圆点、柱状或排行。
- 标签过长省略，只有实际省略才 Tooltip。
- 图表不能遮挡文本，不能依赖横向滚动才能看到核心信息。

阶段输出要求：

- 完成了什么：列出偏好图谱、标签排行、观影节奏、口味组合、模块空状态、响应式布局和资源化改动。
- 验收标准：有数据时中下部模块均可读；无数据时模块级空状态明确；图表节点 / 标签不重叠；Top 3 和 Top 10 长字段省略规则正确；无横向滚动；Light / Dark 主题均可读。
- 不属于该阶段：不改统计计算、标签聚合、AI 分类、扫描识别或缓存 fingerprint。
- 是否修改业务逻辑：预期无；如新增 UI projection，只记录为展示层变化。
- 是否修改非本阶段处理的页面：默认无；如抽取图表资源影响其它页面，必须列出。

### 7.7h - 回归 / 文档 / 阶段收口

目标：

- 对 7.7 全部页面做 build、migration diff、运行软件人工检查和文档收口。
- 复核历史、收藏、观影洞察之间的跨页跳转、Movie-only 边界和滚动 / Tooltip 规则。
- 更新阶段日志和 Known Issues。

执行时必读：

- 全阶段固定必读基线。
- 本文件全部 7.7 小阶段执行结果。
- `DesignDraft/PHASE_7_STAGE_LOG.md`
- `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- `docs/watch-insights/WATCH_INSIGHTS_KNOWN_ISSUES.md`
- `docs/watch-insights/WATCH_INSIGHTS_STAGE_LOG.md`
- 7.7 改动过的所有 XAML、ViewModel、Service 和迁移文件。

可复用组件：

- 前序 7.7 已建立或复用的全部组件。
- 现有 build、git diff、migration diff 检查命令。

可参考页面 / 组件：

- 影片发现 Tab 作为最终 Tab 对齐参考。
- 媒体库 / 影片发现卡片密度作为最终卡片密度参考。
- Watch Insights 旧功能版作为业务功能完整性参考。

已有 UI 规则：

- 每次最终报告必须说明 migrations diff 是否为空。
- 文档不写入私有样本、完整本地路径、账号、token、password、API key 或完整 WebDAV URL。
- Known Issues 固定分类：Blocker / Deferred / Noise。

阶段输出要求：

- 完成了什么：列出 7.7 全阶段完成模块、运行检查页面、文档更新和回归结果。
- 验收标准：`dotnet build MediaLibrary.sln` 通过；migration diff 明确；三页运行可打开；收藏夹和观影洞察 Tab 与影片发现位置一致；历史页日期跳转可用；Watch Insights 仍 Movie-only；无未记录的业务逻辑变化。
- 不属于该阶段：不新增 7.8 全局 polish；不处理播放器、字幕、扫描、详情页、发现页阶段外视觉问题。
- 是否修改业务逻辑：汇总全部小阶段业务变化；没有则写“无”。
- 是否修改非本阶段处理的页面：汇总全部跨页影响和回归；没有则写“无”。

## 全阶段人工验收矩阵

| ID | 验收项 |
| --- | --- |
| 7.7-A01 | 观影历史页面标题和副标题由 Shell 承载，页面内容区不重复同名标题。 |
| 7.7-A02 | 观影历史按日期分组显示 Movie / Episode 历史，内容区使用现代纵向滚动，无横向滚动。 |
| 7.7-A03 | 观影历史日期筛选 `全部 / 今天 / 本周 / 本月 / 指定日期` 和刷新可用。 |
| 7.7-A04 | 从观影洞察日历点击日期后进入观影历史，应用指定日期筛选并定位 / 高亮目标日期组。 |
| 7.7-A05 | 收藏夹 `喜爱 / 想看` Tab 的位置、横线、下划线和左对齐方式与影片发现一致。 |
| 7.7-A06 | 收藏夹能显示 Movie 和 Season，卡片有评分 / 类型 / 来源或状态摘要，取消当前集合状态可用或给出真实禁用原因。 |
| 7.7-A07 | 收藏夹不出现搜索、排序、批量、删除真实文件或扫描入口。 |
| 7.7-A08 | 观影洞察 `画像分析 / 观影统计` Tab 的位置、横线、下划线和左对齐方式与影片发现一致。 |
| 7.7-A09 | 观影洞察默认打开 `画像分析`，Tab 切换不触发不相关刷新或 AI 调用。 |
| 7.7-A10 | 画像分析五个模块在有数据、数据不足、配置缺失、生成失败和旧缓存 fallback 下均有清晰状态。 |
| 7.7-A11 | 观影统计总览只显示 `已看 / 喜爱 / 想看 / 不想看` 四张状态卡，不显示 `未看` 卡。 |
| 7.7-A12 | 观影统计范围切换和刷新统计只影响统计，不触发画像刷新或推荐变化。 |
| 7.7-A13 | 观影统计日历、偏好图谱、排行、节奏和口味组合图使用真实数据；数据不足时显示模块级空状态。 |
| 7.7-A14 | 所有可滚动区域接入现代自动显隐滚动条，不可滚动区域不新增滚动。 |
| 7.7-A15 | 长标题、来源摘要、排行标签和图表节点只在实际省略时 Tooltip；未省略文本不启用 Tooltip。 |
| 7.7-A16 | 日志、状态、Tooltip 和文档不泄露完整本地路径、完整 WebDAV URL、账号、token、password、API key 或 AI 原始响应。 |
| 7.7-A17 | Watch Insights / Watch Profile / recommendation profile 仍保持 Movie-only，Episode / Season / Series 不进入统计、画像或推荐画像输入。 |
| 7.7-A18 | migration diff 被明确检查；如为空则记录为空，如不为空则列出原因、migration 名称和影响面。 |

## 初始 Known Issues

### Blocker

- 当前无确认 blocker。

### Deferred

- 收藏夹中库外喜爱项如果缺少可取消所需的稳定业务标识，需要在 7.7c 审计后决定最小业务补丁或保留 Deferred，不能在 UI 上伪装为可取消。
- 观影洞察图表可能需要局部自定义 WPF 绘制或模板化控件；不得用静态图片替代真实图表。
- 当前 Watch Insights 部分服务警告可能来自 Core 英文错误文本；7.7 只做 UI 可读投影和脱敏，不重写服务错误体系，除非阶段明确记录。
- TV Watch Insights、TV 画像、混合 Movie + TV 画像和 TV 推荐画像均不属于 7.7。
- 7.8 仍负责跨页面全局 Light / Dark、卡片、菜单、弹窗和最终视觉一致性审计。

### Noise

- 草图颜色不是最终实现色值，7.7 使用 ResourceDictionary 资源和主题可读性，不按截图逐色硬编码。
- 观影统计草图中的 `未看` 卡与当前产品规则冲突，7.7 不实现该卡。
- 计划阶段临时运行截图不作为入库文档资产。

## 建议执行顺序

1. 7.7a 审计与共享 UI 基线。
2. 7.7b 观影历史视觉与日期定位。
3. 7.7c 收藏夹视觉 / Movie-Season 卡片 / 取消动作状态。
4. 7.7d 观影洞察外壳 / Tab / 状态基线。
5. 7.7e 观影洞察画像分析 Tab 视觉收口。
6. 7.7f 观影统计上半区：范围 / 总览 / 日历。
7. 7.7g 观影统计下半区：偏好图谱 / 排行 / 节奏 / 口味组合地图。
8. 7.7h 回归 / 文档 / 阶段收口。

建议先完成历史和收藏，再进入观影洞察。观影洞察依赖历史日期跳转和统一 Tab 基线，且模块复杂度高，应按外壳、画像、统计上半区、统计下半区拆开验收。
