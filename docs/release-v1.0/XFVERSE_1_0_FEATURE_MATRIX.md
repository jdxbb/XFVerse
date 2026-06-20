# XFVerse 1.0 功能证据矩阵

Last updated: 2026-06-20

## 1. 文档目的

本矩阵是 XFVerse 1.0 的功能事实基线，用于约束软件设计说明书、安装说明、软件使用说明书、帮助文档、README、正式打包和 RC 验收。

本矩阵只记录当前代码、项目文件和既有阶段文档能够证明的能力。`代码已审计` 仅表示已找到实现证据，不代表已经在正式 RC 环境完成人工验收。

## 2. 状态定义

| 状态 | 含义 |
| --- | --- |
| 代码已审计 | 已找到当前实现入口、ViewModel 或核心服务证据。 |
| RC 待验证 | 尚未在正式候选安装包和目标环境完成端到端验证。 |
| Deferred | 不进入 XFVerse 1.0 GA 承诺范围。 |
| Blocked | 存在发布阻断项，完成对应阶段前不得宣称可发布。 |

## 3. 产品边界

- XFVerse 1.0 是 Windows 桌面媒体库、播放器、扫描识别、影片发现、推荐和观影洞察应用。
- 1.0 GA 目标平台冻结为 Windows 10 / Windows 11 x64。
- 本地目录和 WebDAV 都是媒体来源；WebDAV 不是使用本地媒体功能的前置条件。
- TMDB、OMDb、OpenSubtitles 和 AI 服务是分功能可选依赖，未配置时不得导致基础媒体库和本地播放不可用。
- Watch Insights、AI 推荐画像和 fingerprint 的 1.0 范围保持 Movie-only；TV 不进入这些能力。
- “移出媒体库”只改变可见性，不清除 metadata 和用户状态。
- “删除记录”只删除软件记录，不删除本地文件或 WebDAV 文件。
- 缓存清理可以删除 XFVerse 自己创建的缓存文件，但不得删除原始媒体。

## 4. 功能矩阵

### 4.1 应用外壳、导航与首页

| ID | 功能与用户入口 | 当前实际能力 | 前置条件、错误与降级 | 数据影响与 Movie / TV 边界 | 代码证据 | 文档归属 | RC 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-001 | 主窗口侧栏 | 首页、媒体库、影片发现、观影历史、收藏夹、观影洞察六个主入口；支持侧栏展开/收起 | 页面加载失败应留在应用内并提供可诊断错误，不应退出应用 | 仅导航；Movie/TV 具体边界由目标页决定 | `MainWindowViewModel.cs`、`MainWindow.xaml` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-002 | 隐藏路由 | 电影详情、剧集总览、季详情、集详情、扫描任务、AI 推荐、设置通过页面动作或用户菜单进入 | 无有效实体时不得构造不存在的媒体记录 | 详情页按 Movie、Series、Season、Episode 分流 | `MainWindowViewModel.cs`、`App.xaml` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-003 | 顶部和侧栏通用操作 | 主题切换、返回、用户菜单、扫描任务、设置、用户资料入口 | 系统主题读取失败时应使用可用主题；退出登录当前未接入 | 主题与资料写入软件配置；不修改媒体 | `MainWindowViewModel.cs`、`UserProfileDialogWindow.*` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-004 | 首页统计卡片 | 显示媒体数量、观看统计、最近新增和最近播放等首页摘要 | 空库显示空状态；查询失败不得伪造统计 | 只读；首页可以汇总电影和已有媒体记录 | `HomeViewModel.cs`、`HomeDashboardQueryService.cs` | 使用说明书 | 代码已审计；RC 待验证 |
| F-005 | 首页继续观看/最近项目 | 可打开详情或继续播放已有来源 | 无可播放来源时应阻止播放并给出提示 | 继续播放会更新播放历史；Movie/Episode 依来源进入播放器 | `HomeViewModel.cs`、`PlaybackSourceService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-006 | 首页快捷入口与推荐预览 | 可跳转媒体库、收藏、扫描、历史、影片发现和推荐页面 | 推荐不可用时基础快捷入口仍可用 | 推荐范围按当前长期语义保持 Movie-only | `HomeViewModel.cs`、`RecommendationsViewModel.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |

### 4.2 媒体库

| ID | 功能与用户入口 | 当前实际能力 | 前置条件、错误与降级 | 数据影响与 Movie / TV 边界 | 代码证据 | 文档归属 | RC 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-010 | 媒体库列表 | 海报/列表布局；显示电影、电视剧和其他内容 | 空库显示引导；单项元数据缺失时使用占位信息 | 只读展示；分类含全部/电影/电视剧/其他 | `LibraryPage.xaml`、`LibraryViewModel.cs`、`LibraryQueryService.cs` | 使用说明书 | 代码已审计；RC 待验证 |
| F-011 | 搜索、排序与筛选 | 支持关键字、来源、内容类型、年代、观看状态、标签、合集和排序 | 组合筛选无结果时显示空状态，不改变数据 | Movie/TV 均可作为库内容；部分电影字段对 TV 不适用 | `LibraryViewModel.cs`、`LibraryQueryService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-012 | 批量选择 | 进入/退出批量模式、选择全部、取消选择 | 处理中支持取消；无选中项时不执行写操作 | 只改变 UI 选择状态 | `LibraryViewModel.cs`、`BulkObservableCollection.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-013 | 批量观看状态 | 批量标记已看/未看 | 失败项应可定位，不应隐式删除记录 | 修改软件观看状态；不得删除媒体文件 | `LibraryViewModel.cs`、`UserCollectionService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-014 | 批量自动识别 | 对待识别项执行识别，支持进度和取消 | AI/TMDB 不可用时保留 placeholder、NeedsReview 或候选，不得高置信错绑 | 可能更新 Movie/TV metadata 和绑定；不得创建不存在的 `MediaFile` | `LibraryViewModel.cs`、`BatchAiCorrectionService.cs`、`MovieIdentificationService.cs`、`TvSeasonIdentificationService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-015 | 手工聚合未识别季 | 将 TV-like 未识别来源按用户选择聚合为季 | 仅对符合条件的未识别来源开放；失败应保持原记录可恢复 | 更新软件记录的 TV 聚合关系，不移动或删除源文件 | `ManualUnknownSeasonAggregationSourceRowViewModel.cs`、`ManualUnknownSeasonAggregationService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-016 | 移出媒体库 | 单项或批量隐藏；在“已移出”面板恢复 | 移出后不应从磁盘或 WebDAV 删除文件 | 只改变可见性；保留 metadata、想看、喜爱、不想看、已看等状态 | `LibraryViewModel.cs`、`MovieManagementService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-017 | 删除记录 | 单项、批量或已移出分组中删除软件记录，需确认 | 必须清楚提示不可恢复的软件数据影响；不得声称会删除源文件 | 清除相关软件记录；不删除本地文件/WebDAV 文件 | `LibraryViewModel.cs`、`ConfirmationDialogWindow.*`、`MovieManagementService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |

### 4.3 影片发现、推荐与用户状态

| ID | 功能与用户入口 | 当前实际能力 | 前置条件、错误与降级 | 数据影响与 Movie / TV 边界 | 代码证据 | 文档归属 | RC 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-020 | 影片发现搜索 | 电影/电视剧搜索，支持标题、人物、筛选和分页 | 需要 TMDB 配置与网络；失败时显示可重试错误，不影响本地库 | 搜索结果不自动创建媒体文件；Movie/TV 分别进入对应详情 | `MovieDiscoveryViewModel.cs`、`TmdbService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-021 | 影片榜单 | 热门、高分、趋势等榜单，支持电影与电视剧展示 | 需要 TMDB；缓存或网络失败时允许空状态 | 浏览本身只读 | `MovieDiscoveryViewModel.cs`、`DiscoveryRankingRowViewModel.cs`、`DiscoveryTvRankingRowViewModel.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-022 | 发现页收藏和入库 | 电影可标记想看/已看并加入媒体库；电视剧可打开详情并加入库 | 外部条目无本地来源时只能建立 metadata/用户状态，不得伪造 `MediaFile` | Movie 与 TV 使用各自状态和实体边界 | `DiscoveryMovieStatusResolver.cs`、`DiscoveryTvSeriesStatusResolver.cs`、`MovieDiscoveryViewModel.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-023 | AI 推荐页 | 读取推荐偏好、缓存和推荐结果，提供反馈入口 | AI 未配置或失败时允许缓存/本地降级；不得阻断基础浏览 | 1.0 推荐范围保持 Movie-only；不把 TV 纳入推荐 | `RecommendationsViewModel.cs`、`RecommendationService.cs`、`RecommendationPreferenceService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-024 | 电影用户状态 | 想看、已看、喜爱、不想看及状态变更记录 | 冲突状态按当前服务规则处理；失败不应造成媒体丢失 | 只修改软件状态和历史，不修改源文件 | `UserCollectionService.cs`、`UserMovieStateChangeHistoryRecorder.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-025 | TV 用户状态 | Season 级喜爱、想看、不想看、已看；Episode 级已看 | metadata-only Episode 可能无播放源；无来源时只允许状态操作 | Series/Season/Episode 边界不得折算为 Movie 状态 | `TvSeasonCollectionService.cs`、`TvSeasonDetailViewModel.cs`、`EpisodeDetailViewModel.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |

### 4.4 扫描、来源与识别

| ID | 功能与用户入口 | 当前实际能力 | 前置条件、错误与降级 | 数据影响与 Movie / TV 边界 | 代码证据 | 文档归属 | RC 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-030 | WebDAV 连接 | 保存连接、测试连接、浏览远端路径、启用/停用/删除扫描路径 | 需要服务器地址和凭据；日志及文档不得输出完整 URL、账号或密码 | 保存软件配置；删除扫描路径不删除远端文件 | `ScanTasksViewModel.cs`、`WebDavPathPickerWindow.*`、`WebDavService.cs` | 安装说明、使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-031 | 本地目录 | 选择、启用/停用和删除本地扫描目录 | 目录不可访问时记录安全错误；不得提升权限或删除目录 | 保存扫描路径；删除配置不删除本地目录或文件 | `ScanTasksViewModel.cs`、`LocalMediaScanService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-032 | 扫描任务 | 分别启动 WebDAV/本地扫描，显示进度、近期日志和原因摘要，支持取消 | 网络中断、权限或解析失败应保留可重试状态 | 新增/更新/软删除软件记录；不删除真实媒体 | `ScanTasksViewModel.cs`、`MediaScanService.cs`、`LocalMediaScanService.cs`、`ScanProgressReporter.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-033 | 重扫与重新关联 | 已消失来源使用软件删除标记；重新出现时尝试重新关联 | 不以绑定数量作为扫描成功唯一标准 | 更新 `MediaFile` 可用性和关联；不创建不存在的实际来源 | `RescanReattachService.cs`、`ScanCandidateVisibilityGuard.cs` | 软件设计说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-034 | 电影/电视剧识别 | 本地规则、TMDB、AI hint 和人工修正共同完成识别 | AI 只能作为 hint；证据不足时进入 placeholder/NeedsReview/候选 | 更新 metadata 与实体绑定；禁止硬编码具体片名、文件名或 TMDB ID 特例 | `MovieIdentificationService.cs`、`TvSeasonIdentificationService.cs`、`AiClassificationService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-035 | 媒体探测 | 使用 ffprobe 获取媒体时长、编码、分辨率、帧率等信息 | ffprobe 缺失或探测失败时保留原记录并允许重试 | 只更新媒体技术 metadata | `MediaProbeService.cs`、项目资源复制配置 | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |

### 4.5 详情页与人工修正

| ID | 功能与用户入口 | 当前实际能力 | 前置条件、错误与降级 | 数据影响与 Movie / TV 边界 | 代码证据 | 文档归属 | RC 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-040 | 电影详情 | 显示剧情、演职员、类型、上映信息、技术信息和多来源评分 | TMDB/OMDb 未配置时显示已有本地 metadata，不应阻止播放 | Movie-only | `MovieDetailViewModel.cs`、`MovieDetailQueryService.cs`、`MovieMetadataRefreshService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-041 | 电影来源管理 | 播放默认/指定来源、设置默认来源、手工探测、拆分或重置来源 | 无可用来源时禁用播放；来源失效时可重扫或修正 | 只改变软件绑定和默认来源，不移动/删除真实文件 | `MovieDetailViewModel.cs`、`PlaybackSourceService.cs`、`SingleSourceCorrectionService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-042 | 电影识别修正 | 通过 TMDB 搜索或 AI 建议修正错误识别 | 用户确认前不得写入最终绑定；AI 结果仍需安全门 | 更新 Movie metadata/关联；不创建虚假来源 | `MovieDetailViewModel.cs`、`SingleSourceCorrectionService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-043 | 剧集总览与季详情 | Series 页面展示季；Season 页面展示集列表、状态和入库操作 | metadata-only 季/集可以展示但可能不能播放 | TV-only；保持 Series/Season/Episode 层级 | `SeriesOverviewViewModel.cs`、`TvSeasonDetailViewModel.cs`、`TvDetailQueryService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-044 | 集详情与 TV 修正 | 查看多个播放源、播放/默认来源/探测、已看状态；可修正为电影、电视剧或未知季 | 无来源时显示 metadata-only 状态；修正需用户确认 | 更新 Episode/Season/Series 或 Movie 关联；不删除源文件 | `EpisodeDetailViewModel.cs`、`UnknownSeasonCorrectionService.cs`、`UnknownTvSeasonAppendService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |

### 4.6 播放器与字幕

| ID | 功能与用户入口 | 当前实际能力 | 前置条件、错误与降级 | 数据影响与 Movie / TV 边界 | 代码证据 | 文档归属 | RC 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-050 | 本地/WebDAV 播放 | 使用 mpv 播放本地文件或 WebDAV 来源 | WebDAV 需要 Range 支持和稳定网络；来源不可用时显示错误 | 播放 Movie 或 Episode 来源；不修改源文件 | `PlayerWindowViewModel.cs`、`PlaybackHostView.cs`、`WebDavDownloadService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-051 | 基础播放控制 | 播放/暂停/停止、进度跳转、全屏/窗口、音量、视频亮度 | 音量允许高于 100，可能削波；亮度是视频画面参数，不是显示器亮度 | 更新播放器状态和观看进度 | `PlayerWindowViewModel.cs`、`PlayerWindow.xaml` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-052 | 续播与完成判断 | 保存进度、恢复播放、达到规则后自动记为已看 | 异常退出时以最后成功写入进度为准 | Movie/Episode 分别写入观看历史和状态 | `WatchHistoryService.cs`、`WatchCompletionEvaluator.cs`、`PlayerWindowViewModel.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-053 | 多播放源 | 在可用来源间切换并维护默认来源 | 来源失效时保留其他来源；切换失败不应丢失当前绑定 | Movie/Episode 均可有多来源 | `PlaybackSourceService.cs`、`EpisodeSourceSelectionHelper.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-054 | 音轨与内嵌字幕 | 枚举并切换音轨、内嵌字幕 | 轨道缺失时隐藏或禁用对应操作 | 只改变当前播放会话 | `PlayerWindowViewModel.cs`、`PlaybackHostView.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-055 | 外挂字幕 | 绑定、选择和使用本地外挂字幕 | 路径失效时显示错误；不得删除原始媒体 | 维护字幕绑定软件记录 | `SubtitleBindingService.cs`、`PlayerWindowViewModel.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-056 | 在线字幕 | 搜索、下载、缓存、绑定和清理在线字幕 | 需要 OpenSubtitles 配置和网络；失败不影响已有字幕或播放 | 删除在线字幕只清软件绑定和 XFVerse 缓存，不删除媒体 | `OnlineSubtitleSearchViewModel.cs`、`OpenSubtitlesClientService.cs`、`OnlineSubtitleCacheService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-057 | 连续剧播放导航 | Episode 播放时支持上一集/下一集 | 首集/末集或无来源时禁用相应按钮 | TV Episode-only | `PlayerWindowViewModel.cs`、`TvDetailQueryService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |

### 4.7 历史、收藏与观影洞察

| ID | 功能与用户入口 | 当前实际能力 | 前置条件、错误与降级 | 数据影响与 Movie / TV 边界 | 代码证据 | 文档归属 | RC 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-060 | 观影历史 | 按日期筛选、刷新、打开 Movie 或 Episode 详情 | 无历史时显示空状态 | 只读展示历史；播放会写入历史 | `WatchHistoryViewModel.cs`、`WatchHistoryService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-061 | 收藏夹 | 喜爱/想看标签页，展示 Movie 和 Season，支持移除当前状态 | 空状态不删除实体或媒体 | 修改对应 Movie/Season 用户状态 | `FavoritesViewModel.cs`、`UserCollectionService.cs`、`TvSeasonCollectionService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-062 | 观影统计 | 月度/全部范围、日历导航、按日期跳转历史、统计卡片 | 数据不足时显示空统计，不应生成虚构趋势 | 1.0 Watch Insights 仅统计 Movie | `WatchInsightsViewModel.cs`、`WatchStatisticsService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-063 | 观影画像 | 根据观看输入生成/缓存画像并刷新 | AI 不可用时显示明确降级；不得把失败描述为成功画像 | 1.0 Movie-only；TV 不进入画像输入 | `WatchProfileInputService.cs`、`WatchProfileService.cs`、`WatchInsightCacheService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |

### 4.8 设置、用户资料与数据

| ID | 功能与用户入口 | 当前实际能力 | 前置条件、错误与降级 | 数据影响与 Movie / TV 边界 | 代码证据 | 文档归属 | RC 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-070 | 常规设置 | 关闭行为、启动全屏、自动 WebDAV 扫描、System/Light/Dark 主题 | 无效配置应回退默认值 | 写入应用设置；与媒体类型无关 | `SettingsViewModel.cs`、`SettingsService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-071 | 外部服务设置 | TMDB、OMDb、OpenSubtitles、AI 配置、保存和连接测试 | 各服务独立可选；凭据不得写入日志或文档 | 写入本地配置；当前凭据保护存在 P8-B04 | `SettingsViewModel.cs`、`SettingsService.cs`、各外部服务实现 | 安装说明、使用说明书、帮助文档 | 代码已审计；RC 待验证；Blocked |
| F-072 | 缓存管理 | 查看和清理视频、海报、metadata、在线字幕缓存 | 清理失败应指出被占用项；不得扩大到原始媒体目录 | 仅删除 XFVerse 缓存和缓存记录 | `SettingsViewModel.cs`、`VideoCacheService.cs`、`ExternalMetadataCacheMaintenanceService.cs`、`OnlineSubtitleCacheService.cs` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-073 | 用户资料 | 本地保存昵称、简介和头像 | 头像不可读时使用默认头像；当前无账号同步 | 写入本地 JSON 和头像目录；不上传 | `UserProfileService`、`UserProfileDialogWindow.*` | 使用说明书、帮助文档 | 代码已审计；RC 待验证 |
| F-074 | 关于与版本 | 显示应用名称、版本和产品说明 | 当前未建立显式统一版本源，受 P8-B03 阻断 | 只读 | `SettingsViewModel.cs`、`MediaLibrary.App.csproj` | README、使用说明书 | 代码已审计；RC 待验证；Blocked |
| F-075 | 用户数据目录 | 数据库、缓存、字幕和资料默认位于当前用户 LocalAppData 下，可由 `XFVERSE_APPDATA_DIR` 覆盖 | 目录不可写时应用启动应失败并给出诊断；文档不公开用户具体路径 | 数据库存储所有软件记录；目录名为兼容历史保留 `MediaLibrary` | `AppPaths.cs`、`AppDbContext.cs` | 安装说明、帮助文档、软件设计说明书 | 代码已审计；RC 待验证 |
| F-076 | 数据库初始化 | 启动时初始化数据库并应用仓库内 EF Core migrations | Migration 失败不得继续到不可预测状态；正式升级需备份与回滚验证 | 可能迁移软件数据库，不修改真实媒体文件 | `App.xaml.cs`、`DatabaseInitializer.cs`、`Data/Migrations/*` | 安装说明、帮助文档、软件设计说明书 | 代码已审计；RC 待验证 |
| F-077 | 日志与诊断 | 应用和服务记录诊断日志；扫描任务另有数据库日志 | 必须脱敏；当前日志目录尚未完全统一到用户数据目录 | 写入软件日志，不应记录完整路径、URL 或凭据 | 日志初始化代码、`ScanTaskLogConfiguration.cs` | 帮助文档、软件设计说明书 | 代码已审计；RC 待验证 |

### 4.9 安装与发布

| ID | 功能与用户入口 | 当前实际能力 | 前置条件、错误与降级 | 数据影响与 Movie / TV 边界 | 代码证据 | 文档归属 | RC 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-080 | 应用运行时与原生资源 | WPF .NET 8 应用，发布时复制 mpv、ffmpeg/ffprobe 和 WatchPersona 资源 | 原生架构必须与发布架构一致；X86 不在 1.0 范围 | 与媒体类型无关 | `MediaLibrary.App.csproj`、`NativeRuntimeResolver.cs` | README、安装说明、软件设计说明书 | 代码已审计；RC 待验证 |
| F-081 | 现有测试安装器 | 可构建 x64/ARM64 自包含测试包和 Inno 安装器 | 会复制/覆盖用户数据，仅限内部测试，禁止作为正式包 | 可能破坏用户软件数据，受 P8-B01/P8-B02 阻断 | `scripts/packaging/Build-TestInstaller.ps1`、`XFVerse.TestInstaller*.iss` | 仅内部阶段文档 | Blocked；不得发布 |
| F-082 | 正式安装器 | 尚未实现；8.4 必须建立独立空 staging、x64、当前用户安装链路 | 不得复用测试 seed-data 或危险删除逻辑 | 升级/卸载默认保留用户数据 | `PHASE_8_4_PACKAGING_PLAN.md`、发布决策记录 | 安装说明、README | Blocked；Phase 8.4 实现 |
| F-083 | 自动更新 | 当前没有正式自动更新器 | 1.0 采用手动下载新版安装器升级 | 升级必须保留用户数据库和配置 | 发布决策记录 | README、安装说明、帮助文档 | Deferred |
| F-084 | 数字签名 | 当前没有代码签名配置 | 若无证书，Windows 可能显示未知发布者 | 不影响应用数据，但影响安装信任提示 | 发布决策记录 | 安装说明、发布说明 | Deferred |

## 5. 外部依赖与降级基线

| 依赖 | 用途 | 是否为应用启动必需 | 未配置或失败时的基线行为 |
| --- | --- | --- | --- |
| mpv | 本地/WebDAV 视频播放 | 播放功能必需；媒体库浏览非必需 | 播放失败并提供诊断，媒体库和设置仍应可用。 |
| ffprobe | 媒体技术信息探测 | 否 | 保留已有记录，技术字段为空或维持旧值，允许重试。 |
| WebDAV 服务 | 远端扫描和播放 | 否 | 本地目录扫描与本地播放可继续使用。 |
| TMDB | 识别、发现、metadata | 否 | 使用已有本地 metadata；发现和在线识别显示配置/网络错误。 |
| OMDb | 外部评分补充 | 否 | 不显示对应评分，不阻止详情和播放。 |
| OpenSubtitles | 在线字幕 | 否 | 内嵌字幕、已有外挂字幕和播放仍可用。 |
| AI 服务 | 分类 hint、修正建议、推荐和画像 | 否 | 安全降级为本地规则、缓存、候选或明确不可用状态。 |

## 6. 1.0 明确不承诺

- ARM64 正式支持。
- X86 支持。
- 自动更新。
- 云账号、登录、跨设备同步或真实“退出登录”。
- 删除本地媒体文件或 WebDAV 文件。
- TV 观影洞察、TV AI 推荐和 TV fingerprint。
- 无人工确认的高风险 AI 自动绑定。
- Portable 免安装版。

## 7. 后续维护规则

- Phase 8.2 使用本矩阵编写软件设计说明书，不得扩写无证据能力。
- Phase 8.3/8.4 修改代码或打包行为后，必须同步更新受影响行。
- Phase 8.5～8.7 根据“文档归属”拆分内容，不复制相同章节成为多个事实源。
- Phase 8.9 每一行的 RC 状态必须更新为通过、失败或明确 Deferred。
