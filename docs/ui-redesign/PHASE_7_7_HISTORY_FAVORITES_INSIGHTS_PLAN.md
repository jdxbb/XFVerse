# Phase 7.7 观影历史 / 收藏夹 / 观影洞察执行计划

Last updated: 2026-06-11

## 7.7g Follow-up (2026-06-11, Scan Tasks shadow-safe layout correction)

- Scope remains Phase 7.7g. This is not Phase 7.8.
- Removed the vertical divider line between the two WebDAV configuration inner cards.
- Reworked the Scan Tasks shadow-safe layout so the visible left/right content inset is equal and smaller, while the actual drawing gutter remains large enough for large-card glow.
- Reduced the hard minimum width constraints for the left/right Scan Tasks columns so the scan-record card is less likely to be pushed into the right clipping edge.
- Strengthened the dark-theme large-card white glow again with a larger centered blur and higher opacity, replacing the previous hard dark shadow feel.
- No scan behavior, WebDAV/local path semantics, Settings/API behavior, database schema, migration, database update, commit, or push change.
- Validation: `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gScanSafeGlowFix\"` passed with 0 warnings and 0 errors.

## 7.7g Follow-up (2026-06-11, Scan Tasks inline panels and glow boost)

- Scope remains Phase 7.7g. This is not Phase 7.8.
- Updated Scan Tasks inner cards, metric cards, empty-state cards and scan-log cards to use the same inline glass panel family as Watch Insights inner panels.
- Increased the dark-theme large-card white glow by raising `ShadowLargeCard` blur/opacity and making it centered instead of offset. Compact cards, poster cards and poster palette shadows still use their existing resources.
- No scan behavior, WebDAV/local path semantics, Settings/API behavior, database schema, migration, database update, commit, or push change.
- Validation: `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gScanInlineGlowBoost\"` passed with 0 warnings and 0 errors.

## 7.7g Follow-up (2026-06-11, Settings API card polish and dark large-card glow)

- Scope remains Phase 7.7g. This is not Phase 7.8.
- Restored the General Settings behavior card content width to match the cache settings card after the shadow-safe outer expansion.
- Updated API configuration card inner panels to use the same inline glass panel family as Watch Insights profile panels.
- Added a theme-specific `ShadowLargeCard` resource: light theme keeps the existing soft dark page-card shadow, while dark theme uses a weaker white glow for large cards only. Compact cards, poster cards and poster palette shadows continue using their existing shadow resources.
- No Settings/API service behavior, cache behavior, scan behavior, recommendation, database schema, migration, database update, commit, or push change.
- Validation: `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gSettingsApiGlow\"` passed with 0 warnings and 0 errors.

## 7.7g Follow-up (2026-06-11, cross-page card shadow safe areas)

- Scope remains Phase 7.7g. This is not Phase 7.8.
- Audited the user-reported shadow clipping candidates. Home, Library, Movie Discovery search, Movie Discovery ranking, Watch History, Settings and Scan Tasks all had page-edge or scroll-viewport card placement that could clip top/right shadows.
- Applied the same symmetric shadow-safe layout model used for Watch Insights: expand the outer viewport/container left, top and right, then compensate the card body back to its original content-column position.
- Unified Settings general/API cards and Scan Tasks outer cards onto `GlassPageCardStyle` so they use the same glass surface and card shadow family as the accepted modern pages.
- No service semantics, Movie/TV boundary, scan behavior, recommendation, database schema, migration, database update, commit, or push change.
- Validation: `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gCrossPageShadowSafe\"` passed with 0 warnings and 0 errors.

## 7.7g Follow-up (2026-06-11, poster action tooltip and Watch Insights shadow-safe grid)

- Scope remains Phase 7.7g. This is not Phase 7.8.
- Removed hover text from poster-corner want/favorite actions in Movie Discovery and Favorites while preserving click commands and toast feedback.
- Reworked Watch Insights Profile Analysis and Watch Statistics scroll content from the previous negative-margin/padding compensation to an explicit shadow-safe grid. The root issue was that module cards still sat on the effective clipped content edge; the final structure gives shadows real layout gutter with symmetric left/right viewport expansion, so module bodies keep the original content-column alignment.
- No service semantics, Movie/TV boundary, scan, recommendation, database schema, migration, database update, commit, or push change.
- Validation: `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gInsightShadowSymmetric\"` passed with 0 warnings and 0 errors.

本文件是 Phase 7.7 的唯一详细执行计划。后续进入 7.7 任一小阶段前，必须先阅读本文件，再按对应小阶段的必读清单读取文档、代码和草图。除必读项外，其它资料按实现风险和代码证据按需读取。

## 阶段状态

- 状态：7.7a 已完成；7.7b 已完成；7.7c 已完成；7.7d 已完成；7.7e 已完成；7.7f 已完成；7.7g 已完成；下一步进入 7.7h。
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
  - `观影历史` 的海报卡片必须完全复制 `媒体库` 的海报卡片视觉方向。
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

- 未新增全局 `HistoryMediaCard`。本阶段优先在 `WatchHistoryPage.xaml` 内复用媒体库海报卡片视觉结构，避免为了单页抽象扩大影响面。
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

7.7b 执行结果（2026-06-08）：

- 完成了什么：
  - 重建 `WatchHistoryPage.xaml`：移除旧页内大卡片和行式历史列表，改为固定顶部日期筛选栏 + 内容区日期分组滚动布局。
  - 日期筛选从 `ComboBox` 改为 `全部 / 今天 / 本周 / 本月 / 指定日期` 手工按钮条，保留 `刷新`，指定日期才显示 `DatePicker`。
  - 历史内容区改为按日期分组的海报卡片网格，日期组标题、横线、摘要和目标日期高亮保留。
  - 历史海报卡片按用户要求复制媒体库海报卡片视觉：180x270 poster card、圆角裁切、poster placeholder、poster cache image、palette cached shadow、顶部 chips、底部渐变、标题/辅助信息/tag line/进度条结构。
  - `WatchHistoryViewModel` 新增日期按钮选择命令、筛选选中态属性、历史卡片展示 projection 和 Shell 副标题文案 `记录你的惬意时刻`。
  - 内容区接入 `ScrollBarAutoRevealBehavior` 和现代纵向滚动；筛选栏、Shell 标题、侧边栏不滚动。
  - 空状态标题统一为页面说明中的 `暂无观影历史`。
- 验收标准：
  - `dotnet build MediaLibrary.sln --no-restore` 通过，0 warnings / 0 errors。
  - 运行桌面程序并进入观影历史页，页面未发生最终运行时崩溃；UI 自动化树可读到 `观影历史`、`全部`、`今天`、`本周`、`本月`、`指定日期`、`刷新`。
  - 有历史数据时卡片模板可加载；修复了 WPF `Run.Text` 和 `ProgressBar.Value` 默认双向绑定只读属性导致的运行时异常。
  - 日期筛选语义、刷新命令、目标日期定位 / 高亮代码路径保留。
  - 页面内容区无横向滚动，滚动只发生在日期分组和卡片所在内容区。
- 不属于该阶段：
  - 不改播放器写入观影历史链路。
  - 不删除历史，不新增删除单条历史、批量选择、搜索、排序或继续播放按钮。
  - 不让 TV Episode / Season / Series 进入 Watch Insights 统计、画像或推荐输入。
  - 不处理收藏夹和观影洞察视觉，它们仍留给后续 7.7c 及之后阶段。
- 是否修改业务逻辑：无 Core / Service / 历史写入 / 统计口径变化；仅新增 App 层 UI 选择命令和展示 projection。
- 是否修改非本阶段处理的页面：无。未修改观影洞察日历入口、播放器、媒体库、影片发现、收藏夹或 Shell。

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

7.7c 执行结果（2026-06-08）：

- 完成了什么：
  - 重建 `FavoritesPage.xaml`：移除旧页内大卡片和原生可见 `TabItem` Header，改为页面根 `Grid > TabControl`，并在本地 `TabControl` 模板内放置 104px 手工 `喜爱 / 想看` 按钮条和横向分隔线。
  - 收藏夹默认进入 `喜爱` Tab，Tab 文案显示当前数量；Tab 状态由 `FavoritesViewModel.SelectedTabIndex` 单一来源控制，不做跨重启记忆。
  - 当前 Tab 内容区改为唯一纵向滚动面，使用 `ScrollBarAutoRevealBehavior` 和禁用横向滚动；Tab 区、Shell 标题和侧边栏不滚动。
  - 新收藏卡片使用 180x270 海报卡片网格，包含 poster placeholder、poster cache image、左上 Movie / Season 类型、右上评分徽章、当前集合状态、标题、日期 / 季辅助信息、来源 / 观看状态摘要。
  - `FavoritesViewModel` 补充 Movie / Season 卡片展示 projection，包括评分文本、Season `Sxx / 集数 / 年份` 辅助信息、来源摘要、长字段截断 Tooltip 和详情导航投影。
  - 取消动作保留现有服务路径：Movie 喜爱走 `IMovieManagementService.SetFavoriteAsync`，Movie 想看走 `IUserCollectionService.RemoveWantToWatchAsync`，Season 喜爱 / 想看走 `ITvSeasonCollectionService`。
  - 卡片级取消状态新增 `取消中...`、失败文案和真实禁用原因；禁用 Tooltip / 状态文本不展示完整本地路径、完整 URL 或密钥。
  - loading、error、喜爱空状态、想看空状态均放在当前 Tab 的收藏列表区域内；页面不新增搜索、筛选、排序、批量、删除记录或扫描入口。
- 验收标准：
  - `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\\xfverse-build-7-7c\\"` 通过，0 warnings / 0 errors。
  - 常规 `dotnet build MediaLibrary.sln` 和 `dotnet build MediaLibrary.sln -p:UseAppHost=false` 均被正在运行的桌面程序锁住默认 `bin` 输出文件；该阻塞是运行实例占用，不是代码 / XAML 编译错误。
  - 静态核查确认收藏夹不显示搜索、排序、筛选、批量、删除真实文件或扫描入口。
  - Movie / Season 均从现有 `CollectionMovieItem` 投影显示；Season 仅展示收藏，不进入 Watch Insights。
  - 长标题、日期 / 季辅助信息和来源摘要使用 `TrimmedTextToolTipBehavior`，只在实际省略时 Tooltip。
  - 内容区只启用纵向滚动，横向滚动禁用。
- 不属于该阶段：
  - 不改 Watch Insights 统计、画像、AI 推荐画像或 Movie-only 边界。
  - 不新增搜索、排序、筛选、批量选择、删除记录、扫描入口或继续播放入口。
  - 不删除本地文件，不删除 WebDAV 文件，不删除 metadata。
  - 不新增库外喜爱字段，不新增 database migration，不执行 database update。
- 是否修改业务逻辑：
  - 未修改 Core 服务或数据库 schema；只复用现有集合服务。
  - 审计结果：当前 Movie 喜爱来源仍是库内 `Movie.IsFavorite`，缺少 `MovieId` 的库外喜爱没有稳定取消标识；7.7c 不新增字段，若未来出现此类项，卡片显示真实禁用原因，后续可在单独阶段补模型。
- 是否修改非本阶段处理的页面：无。未修改媒体库、影片发现、观影历史、观影洞察、详情页、播放器、Shell 或集合服务实现。

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

7.7d 执行结果（2026-06-08）：

- 完成了什么：
  - 将 `WatchInsightsPage.xaml` 从旧页内圆角按钮组改为根 `Grid > TabControl`，原生 `TabItem` Header 仅作为隐藏 ItemsHost，顶部可见 Tab 使用 7.7a `PageTabs.xaml` 的手工按钮条视觉基线。
  - 顶层 Tab 固定为 `画像分析` / `观影统计`，位置、分隔横线、左对齐和选中下划线与收藏夹 / 影片发现方向一致；页面内容区不重复 `观影洞察` 大标题，Shell 继续承载标题和副标题。
  - 为画像和统计各自保留一个主 `ScrollViewer`，接入 `ScrollBarAutoRevealBehavior` 和页面本地现代滚动条样式；Tab 区不滚动，禁用横向页面滚动。
  - 在 `WatchInsightsViewModel` 增加 `SelectedTabIndex` / `SelectTabCommand` 作为 TabControl 可绑定的单一选择投影，继续保留现有 `IsProfileTabSelected` / `IsStatisticsTabSelected` 兼容属性。
  - 增加 `InsightModuleState` UI 投影，覆盖 loading、empty、error、data insufficient、config missing、generation failed、cached fallback 等模块状态，并在画像 / 统计 Tab 顶部展示模块级状态卡。
  - 观影洞察 UI 层错误状态新增脱敏清洗：不裸露完整 URL、完整本地路径、token、password、API key 或 secret；配置缺失显示中文可读提示。
  - 统计警告移动到 `观影统计` Tab 内容区内展示，避免统计状态在画像 Tab 顶部跨 Tab 露出。
- 验收标准：
  - `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77d\"` 通过，0 warning / 0 error。
  - 默认仍由 `ActivateAsync` 选中 `画像分析`；Tab 切换只改变 `SelectedTabIndex` / `SelectedTab`，不改导航、主题、统计范围或画像缓存偏好。
  - 统计刷新仍只调用 `IWatchStatisticsService`；画像刷新仍只调用 `IWatchProfileService`；二者保持独立。
  - 画像和统计各自只有一个主滚动面；无新增页面横向滚动。
- 不属于该阶段：
  - 未重建画像分析五个模块的最终视觉，留给 7.7e。
  - 未重建观影统计总览、日历和图表最终视觉，留给 7.7f / 7.7g。
  - 未修改 AI prompt、画像服务、统计服务、推荐、播放器、扫描、集合服务、Core 业务规则或 Watch Insights Movie-only 口径。
  - 未新增数据库字段、migration，未执行 database update。
- 是否修改业务逻辑：
  - 无 Core 业务逻辑变化；仅新增 App 层 UI 选择投影、模块状态投影和 UI 状态文案脱敏。
- 是否修改非本阶段处理的页面：
  - 无。未修改影片发现、收藏夹、观影历史、媒体库、设置、详情页、播放器或 Shell。

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

7.7e 执行结果（2026-06-08）：

- 完成了什么：
  - 将画像分析 Tab 重建为草图对应的五个模块：`观影口味总结`、`观影 DNA`、`你的观影人格`、`口味象限`、`看得多 vs 真喜欢`。
  - `观影口味总结` 改为摘要 + 状态芯片 + 刷新入口 + 核心关键词双栏布局；loading、empty、data insufficient、config missing、generation failed、cached fallback 继续由 7.7d 模块状态投影承接。
  - `观影 DNA` 改为 3 列卡片网格，使用现有画像基因、标签、分数和说明；新增 App 层 icon / subtitle / progress 展示投影，不改变画像服务输出。
  - `你的观影人格` 继续使用现有 `WatchPersona` 海报资源和 persona 映射，只调整卡片比例、边框和文案层级。
  - `口味象限` 改为解释面板 + 小型坐标面板，沿用现有 X/Y 分数并只调整 UI 坐标投影范围。
  - `看得多 vs 真喜欢` 改为三组 Top3 卡片和结论区，长 AI 文案可换行，不展示原始 prompt、原始响应或敏感设置。
  - 移除了画像 Tab 内旧的隐藏占位 / 备用视觉块，避免后续维护时误用旧视觉。
- 验收标准：
  - `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77e\"` 通过，0 warning / 0 error。
  - 画像刷新仍只调用现有 `IWatchProfileService`，不触发统计刷新、推荐生成或扫描链路。
  - Watch Insights / Watch Profile / recommendation profile 继续保持 Movie-only；Episode / Season / Series 不进入画像、DNA、象限或 watch-vs-like 输入。
  - 模块状态、警告、空状态、数据不足、配置缺失、生成失败和缓存回退均有页面内可读展示。
- 不属于该阶段：
  - 未修改 AI prompt、AI 模型选择、画像输入聚合、画像缓存 fingerprint、推荐画像、统计服务、播放器、扫描、集合服务或 Core 业务规则。
  - 未重建观影统计范围、总览、日历或图表，继续留给 7.7f / 7.7g。
  - 未新增数据库字段、migration，未执行 database update。
- 是否修改业务逻辑：
  - 无 Core 业务逻辑变化；仅新增 App 层画像展示投影和 XAML 视觉布局。
- 是否修改非本阶段处理的页面：
  - 无。未修改影片发现、收藏夹、观影历史、媒体库、设置、详情页、播放器或 Shell。

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

7.7f 执行结果（2026-06-08）：

- 完成了什么：
  - 将观影统计上半区重排为草图对应的 `洞察总览` + `观影日历` 两个主模块。
  - `洞察总览` 保持四张状态卡：`已看`、`喜爱`、`想看`、`不想看`，未恢复草图中的 `未看` 卡。
  - 将本月 / 全部范围切换收口为统计总览右侧的分段按钮，并继续绑定现有 `SelectMonthRangeCommand` / `SelectAllRangeCommand`。
  - 保留统计刷新入口和最后刷新时间；刷新仍只走统计服务，不触发画像生成或推荐变化。
  - 新增 `StatisticsOverviewCardTemplate`、范围按钮样式、统计指标面板样式和日历图例 swatch 样式，状态卡通过 App 层 `Kind` / `Subtitle` 展示投影控制视觉状态。
  - 将观影总时长、当前范围观影数和高频标签放入总览底部三块指标区；高频标签显示标签名和计数。
  - 将观影日历重排为左侧月份切换 / 热力图 / 星期标签 / 日期格，右侧三张月度指标卡：观影天数、最长连续、最活跃日期。
  - 日历热力色从页面内十六进制硬编码改为现有主题资源键；日期格保留点击跳转观影历史并传递目标日期。
- 验收标准：
  - `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77f\"` 通过，0 warning / 0 error。
  - 本月 / 全部切换仍使用现有命令；统计刷新仍只刷新统计。
  - 总览状态卡只有四类，不包含 `未看`。
  - 日历月份切换、回到当前月和日期点击仍绑定现有命令。
  - Watch Insights 统计继续保持 Movie-only；TV / Episode / Season / Series 不进入统计。
- 不属于该阶段：
  - 未重建偏好图谱、标签排行、观影节奏或口味组合地图，继续留给 7.7g。
  - 未修改统计服务口径、自动已看算法、画像、推荐、播放器、扫描、集合服务或 Core 业务规则。
  - 未新增数据库字段、migration，未执行 database update。
- 是否修改业务逻辑：
  - 无 Core 业务逻辑变化；仅新增 App 层统计展示投影和 XAML 视觉布局。
- 是否修改非本阶段处理的页面：
  - 无。观影历史目标日期定位沿用 7.7b 已有能力，未改 `WatchHistoryPage`。

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

7.7g 执行结果（2026-06-10）：

- 完成了什么：
  - 将观影统计下半区收口为草图对应的四个模块：`偏好图谱`、`标签排行月榜`、`观影节奏`、`口味组合地图`。
  - `偏好图谱` 改为可缩放固定画布气泡图，使用真实类型 / 情绪分布；气泡大小代表出现次数，图例使用现有主题资源。
  - `标签排行月榜` 改为三组 Top3 卡片，继续绑定真实类型、情绪和场景排行；每行保留排名、标签名、次数和相对强度条。
  - `观影节奏` 改为常看时间段柱状图 + 周内 / 周末差异 + 常看片长分布；常看时间和片长摘要来自现有统计分布的 App 层展示投影。
  - `口味组合地图` 改为可缩放关系图画布 + 高频组合 Top10；节点使用真实类型 / 情绪 / 场景组合节点或 Top10 fallback，连线粗细代表共现频率。
  - 为下半区新增本页图表模板和样式，全部引用现有 ResourceDictionary 资源键；未新增静态图片或页面局部十六进制颜色。
  - 长标签、组合标签和节点标签接入真实截断 Tooltip 行为；未省略时不显示 Tooltip。
- 验收标准：
  - `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77g\"` 通过，0 warning / 0 error。
  - 下半区模块继续使用真实 `WatchStatisticsSnapshot` 数据；数据不足时显示模块级空状态。
  - 统计刷新仍只调用 `IWatchStatisticsService`；画像、推荐、扫描和播放器链路未被触发。
  - Watch Insights 统计继续保持 Movie-only；TV / Episode / Season / Series 不进入统计。
  - 图表画布通过 `Viewbox` 在可用宽度内缩放，未新增页面横向滚动。
- 不属于该阶段：
  - 未修改统计服务口径、标签聚合、AI 分类、扫描识别、缓存 fingerprint、画像、推荐、播放器、集合服务或 Core 业务规则。
  - 未新增数据库字段、migration，未执行 database update。
  - 未做 7.7 全阶段回归、运行截图验收或 7.8 全局 Light / Dark polish，继续留给 7.7h / 7.8。
- 是否修改业务逻辑：
  - 无 Core 业务逻辑变化；仅新增 App 层统计展示投影和 XAML 视觉布局。
- 是否修改非本阶段处理的页面：
  - 无。未修改影片发现、收藏夹、观影历史、媒体库、设置、详情页、播放器或 Shell。

7.7g Follow-up（2026-06-10）：

- 完成了什么：
  - 按用户反馈将 `WatchInsightsPage` 顶层 Tab 坐标对齐到影片发现已验收模板：负上边距、内容顶部 padding、分隔横线、隐藏原生 header 和选中内容起点均按影片发现同构处理。
  - 将观影洞察主模块卡片统一切换到 `GlassPageCardStyle`，内层统计 / 画像 / 图表面板统一接入 `GlassInlinePanelStyle` 基线。
  - 将观影洞察本地滚动条从硬编码白色 10px thumb 改为与影片发现 / 设置一致的资源化 6px 现代滚动条。
- 验证结果：
  - `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuildInsightsPolish\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 未修改统计服务口径、画像生成、推荐、Core 业务规则、Movie-only 边界、数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-10，画像分析首卡布局）：

- 完成了什么：
  - 按用户反馈移除画像分析顶部 `使用缓存画像` 状态卡的首屏位置，页面最上方直接进入 `观影口味总结`。
  - 将 `刷新画像` 放入 `观影口味总结` 标题行右侧，和画像状态、上次生成时间形成同一组轻量操作区。
  - 去掉口味总结左下角状态 / 时间 / 样本三个标签，避免摘要卡底部继续堆叠旧式胶囊。
  - 固定口味总结首卡高度，固定左右内容面板高度并底部对齐。
  - 将右侧核心关键词改为两行三列 `UniformGrid`，每个关键词使用低圆角矩形标签。
- 验证结果：
  - 构建过程中曾遇到临时构建输出空间不足；清理 Codex 临时构建输出后，`dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildWatchInsightsProfileLayoutFinal\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 未修改画像生成服务、统计服务口径、推荐、Core 业务规则、Movie-only 边界、数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-10，画像分析刷新入口与总结排版）：

- 完成了什么：
  - 将 `刷新画像` 从 `观影口味总结` 卡片内移到顶层 Tab 横线右侧动作区，只在 `画像分析` Tab 选中时显示。
  - 将刷新入口改为无背景 MDL2 刷新图标按钮，hover Tooltip 显示 `刷新画像`，图标左侧显示上次生成时间。
  - 为画像和统计两个 Tab 的普通内容区显式恢复箭头光标，避免整页普通区域呈现点击手势。
  - 将观影洞察大模块卡片切换为本页局部卡片样式，移除直角玻璃投影并增加模块底部间隔。
  - 为画像和统计滚动内容增加顶部 / 底部安全留白，降低卡片边缘被视口裁切的概率。
  - 去掉口味总结卡片内的缓存 / 状态标签展示。
  - 压缩口味总结副标题到正文内容的空白，正文与核心关键词区域紧跟副标题下方。
  - 口味总结正文改为引用式排版：大号灰色首尾引号、无内容时在原区域居中显示空状态文本，不额外套边框或背景。
  - App 层只保留 AI 返回的自然段并给每段段首补两个全角空格；不在本地强行切分自然段。
  - 调整画像 summary prompt，要求后续 AI 口味总结直接返回两个自然段并用换行分隔。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildWatchInsightsRefreshTabLayout\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 未修改统计服务口径、推荐、Movie-only 边界、数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-10，全局配色初步统一）：

- 完成了什么：
  - 按 `DesignDraft/DESIGN.md` 的灰粉主色、浅粉强调、中性色优先和低饱和状态色方向，替换 Light / Dark 共享颜色字典中上一轮临时米橙 / 青绿色方案。
  - 同步调整共享玻璃渐变、选中态、焦点、hover / pressed、状态色、空状态、警告 / 成功 / 错误 / 信息色资源，保持现有资源键不变。
  - 将播放器固定暗色资源中的进度条、focus ring、菜单 / 弹窗边框和状态色从青绿色临时方案调整到灰粉 / 中性色方向。
  - 按用户反馈降低粉色占比：粉色保留给主操作、选中态和少量强调，不再铺到海报卡片顶部标签或评分背景。
  - 将首页、媒体库、影片发现、推荐、收藏夹和观影历史的海报卡片标签 / 评分背景恢复为旧浅灰蓝标签视觉，海报叠层标题和辅助文字恢复为旧白色 / 浅灰文字。
  - 将通用进度条和海报卡片内观看进度条的底色恢复为旧浅灰底色，保留进度填充的偏蓝信息色语义。
  - 继续保留共享 Light / Dark 字典的灰粉主色方向，但将导航栏、暗色卡片和暗色玻璃面从深灰粉改为更干净的中性炭灰 / 暖灰，降低压抑和脏感。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildGlobalPalette77\"` 通过，0 warning / 0 error。
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildPaletteLessPink77\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 这不是 7.8；未做 7.8 全量跨页面视觉回归、截图验收、菜单 / 弹窗 / 详情页逐项 polish 或性能审计。
  - 未修改统计服务口径、画像生成、推荐、播放器业务、扫描、集合服务、Movie-only 边界、数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-10，视觉细节与日期选择器反馈）：

- 完成了什么：
  - 修正 `SmartDatePicker` 开关行为：下拉已打开时再次点击日期选择器按钮会关闭下拉，不会因为 `Popup.StaysOpen=False` 的外部点击关闭流程再次打开。
  - 为收藏夹海报右上角的实心喜爱 / 想看图标增加极细白色描边效果，保持原图标和操作语义不变。
  - 为影片发现海报右上角的实心想看星标增加极细白色描边效果；未选中的空星仍保持原展示。
  - 为 Movie / Season / Episode 详情页评分卡片的五颗星级文本增加极细白色描边效果，空星也一并覆盖。
  - 深色主题下为侧边导航右边缘增加 1px 半透明白色竖线；Light 主题使用透明边线，避免额外视觉噪声。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77VisualFixes\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 未修改日期筛选业务规则、收藏 / 想看状态语义、评分计算、详情页数据、数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-10，搜索星标、评分星级与官网入口修正）：

- 完成了什么：
  - 继续修正 `SmartDatePicker`：补充开关按钮触发的关闭抑制逻辑，覆盖 `Popup.StaysOpen=False` 先关闭、ToggleButton 随后再次打开的点击顺序。
  - 影片发现搜索海报卡已看影片不再折叠右上角星标；已看时显示空心星，点击不改变想看状态，并通过页面短暂提示显示“该电影已经看过，无法标记想看。”。
  - 收藏夹和影片发现海报右上角实心喜爱 / 想看图标的白色描边略加粗，保持原有图标、文字颜色和状态语义。
  - 新增 `RatingStarsBar` 精确比例评分星控件，Movie / Series / Season / Episode 详情评分卡片均改用按评分比例裁切的五颗星；9.5/10 显示为 4.75 颗星，不再被半星四舍五入成满星。
  - 设置页“关于 XFVerse”行点击后使用系统默认浏览器打开 `https://xfverse.fun`。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gLatest\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 这仍是 7.7g 初步优化补丁，不是 7.8；未做全量截图回归、运行时多页面人工验收、数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-10，日期控件专项修正与提示样式语义化）：

- 完成了什么：
  - 对 `SmartDatePicker` 做第四次专项修正：`Popup` 改为 `StaysOpen=True`，`PART_Switch` 的 `IsChecked` 改为单向读取 `IsDropDownOpen`，开关点击由控件代码统一切换，避免 Popup 自动关闭与 ToggleButton 双向绑定互相抢状态。
  - 为 `SmartDatePicker` 增加同窗口外部点击关闭逻辑；点击控件自身和弹层内容不关闭，点击页面其他区域关闭。
  - 海报右上角实心喜爱 / 想看图标描边再次略加粗。
  - 评分星描边改为主题资源：浅色主题使用导航栏背景色描边，深色主题保持当前白色描边。
  - 影片发现短暂页面提示新增 `Info / Warning / Error / Success` 语义样式；翻页失败走 Error，已看不可标记想看走 Warning。
  - 短暂提示位置从页面约 30% 高度上移到约 20% 高度，背景改为半透明语义色并加轻量玻璃质感阴影。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gDatePickerToast\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 未新增全局 toast 服务；当前只优化影片发现已有短暂提示通道。
  - 未做数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-10，画像分析卡片阴影、刷新状态与总结排版）：

- 完成了什么：
  - 画像分析大卡片恢复与首页同源的 `GlassPageCardStyle` 柔和阴影，移除前一轮为避免直角阴影而误去掉的普通阴影，并增加画像滚动区安全留白和大卡片底部间隔。
  - 刷新画像按钮在 `IsLoadingProfile` 时显示旋转刷新图标；按钮左侧标签改为 `ProfileRefreshStatusText`，同时承载“正在生成新的画像...”、上次生成时间、缓存 / 失败 / 数据不足状态。
  - 去掉观影口味总结下方空状态卡内单独的状态标题，避免状态信息重复出现在首卡下方。
  - 口味总结文本继续使用首尾大号灰色引号；Core 层对 AI 返回的 summary 做本地兜底：优先保留 AI 自然段，若只有一整段则按句末标点在中点附近拆成两段，UI 层继续为每段段首补两个全角空格。
  - 核心关键词仍限制最多 6 个，不新增关键词；右侧区域改为带轻量六宫格槽位的 2x3 结构，提升视觉密度并减少大面积留白。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gWatchInsightsProfilePolish\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 未增加画像关键词数量，未改变画像输入数据范围、Movie-only 边界、推荐逻辑、数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-10，主题层级、统计顶部与局部深色适配）：

- 完成了什么：
  - 详情页评分星从通用 `BrushAccent` 拆到主题资源 `BrushRatingStarFill`：浅色主题使用更浅、更亮的填充色，深色主题保持现有强调色观感；描边主题资源不变。
  - 浅色主题 `BrushGlassBorder` 从近白色改为更明确的半透明灰蓝边框，改善首页片库预览、观影洞察多层卡片和后续统计卡片叠放时的层级识别。
  - 影片发现搜索结果布局切换容器和悬停态改用 `BrushGlassButton` / `BrushGlassBorder` / `BrushGlassButtonHover` 等主题资源，不再使用固定浅灰蓝临时色，补齐深色主题适配。
  - 画像分析和观影统计滚动区增加顶部与左右阴影安全区，并用内容负边距抵消位置变化，避免卡片阴影被裁切。
  - 去掉观影口味总结与观影 DNA 之间的画像空状态卡；画像状态继续由 Tab 右侧状态 / 生成时间标签和口味总结卡片内部空状态承载。
  - 口味总结自然段显示从双换行改为单换行，去除两个自然段之间的空行，同时保留每段开头两个全角空格。
  - 核心关键词六宫格槽位降低透明度、缩小间距和高度，减弱外框晃眼感。
  - 观影统计移除顶部模块状态卡和警示标签区域，进入统计 Tab 后首个可见大卡片直接是 `洞察总览`。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gThemeLayering\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 这仍属于 7.7g 初步优化，不是 7.8；未做全量截图回归、菜单 / 弹窗 / 所有页面终态视觉审计、数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-10，集高亮、媒体库切换与统计刷新位置修正）：

- 完成了什么：
  - 季详情页 Episode 列表横向扩展到卡片内边距之外，并用行内容 Padding 抵消文字位置变化，让从观影历史 / 首页继续观看定位到的目标集高亮覆盖整行，不再被列表内边界截断。
  - 媒体库海报 / 列表布局切换组件对齐影片搜索布局切换组件：外层改为玻璃段容器，按钮默认前景、hover 和选中态均使用同一组主题资源。
  - 修正上一轮画像 / 统计滚动区安全区方向：移除把内容推回裁剪区的负边距，让顶部和左右 padding 真正成为阴影与卡片边缘可显示区域。
  - 观影口味总结文本区改为带 Watch Insights 现代滚动条的滚动视口，长总结不再直接裁断；视口高度保留非整行显示效果，溢出时底部会露出半行作为可滚动提示。
  - 核心关键词槽位和标签改为固定 44px 高度并居中，缩小外框视觉占比，保持 2x3 均匀布局。
  - 观影统计刷新入口移到 Tab 横线右侧，与刷新画像保持同位置同布局；新增 `StatisticsRefreshStatusText` 将刷新状态和上次刷新时间合并显示，刷新中图标旋转。`洞察总览` 卡片内只保留范围切换按钮。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gEpisodeLibraryStats\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 未改变统计、画像、推荐、扫描、播放、收藏业务语义；未做数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-11，颜色细节、列表背景与画像状态清理）：

- 完成了什么：
  - 季详情页 Episode 列表继续保留整行高亮能力，但将顶部横线从扩展后的 `ListBox` 边框移到正常宽度覆盖线，避免横线穿出到页面背景。
  - 媒体库仅列表布局条目的背景改为导航栏背景色；不影响海报卡、页面大卡片或其他全局卡片颜色。
  - 海报卡右上角实心想看星星 / 收藏心形改用 `BrushRatingStarFill`，与浅色主题详情评分星填充色保持一致，并保留白色描边。
  - 浅色主题评分星描边从导航栏深色改为浅灰色，深色主题描边保持白色不变。
  - 新增固定亮蓝 `ColorWatchProgressFill` / `BrushWatchProgressFill`，应用到媒体库 / 历史媒体进度条和首页继续观看细进度条；进度条底色不变。
  - 画像分析移除 `ProfileWarnings` 标签区和核心关键词解释性状态文本，状态信息只保留在右上角状态 / 时间标签。
  - 画像 / 统计滚动区进一步增加顶部和左右安全留白，避免卡片边缘与阴影被裁切。
  - 口味总结滚动视口改为物理像素滚动，并将核心关键词槽位高度从 44 调回 48。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gColorStateCleanup\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 未改变统计、画像、推荐、扫描、播放、收藏业务语义；未做数据库 schema、migration、database update、commit 或 push。

7.7g Follow-up（2026-06-11，短提示可读性与洞察阴影视口修正）：

- 完成了什么：
  - 影片发现短暂悬浮提示的语义背景透明度从 `0.76` 调整为 `0.88`，减少过度透明导致的不可读问题；提示位置、时长和触发逻辑不变。
  - 重新定位观影洞察大卡片阴影裁切根因：只给内容加 padding 会缩小卡片宽度，却没有扩大 `ScrollViewer` 裁剪视口。
  - 画像分析和观影统计 `ScrollViewer` 改为向外扩展 32px 视口，并用内部 padding 抵消位置，让大卡片保持原内容宽度，同时给顶部和左右阴影留出可绘制区域。
  - 保持卡片内容 `StackPanel` 正常宽度，不再用负内容边距把卡片推回裁剪区。
- 验证结果：
  - `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gToastShadowViewport\"` 通过，0 warning / 0 error。
- 不属于该 Follow-up：
  - 未改变统计、画像、推荐、扫描、播放、收藏业务语义；未做数据库 schema、migration、database update、commit 或 push。

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
