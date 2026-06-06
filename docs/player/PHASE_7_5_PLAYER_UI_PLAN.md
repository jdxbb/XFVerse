# Phase 7.5 播放器最终 UI 对齐计划

本文件是 Phase 7.5 的唯一详细执行计划。后续进入 7.5 任一小阶段前，必须先阅读本文件，再按对应小阶段要求读取文档、代码和草图；除此以外按需读取。

## 阶段状态

- 当前状态：7.5a 播放器外壳与底部控制栏已完成；7.5b 加载 / 提示 / 亮度 / 音量浮层已完成；7.5c 字幕悬浮菜单与在线字幕入口已完成；7.5d 播放源与音轨悬浮菜单已完成；7.5e 在线字幕搜索窗口视觉对齐已完成；7.5f 回归、文档和收口已完成。Phase 7.5 已结束。
- 阶段性质：播放器 UI 对齐阶段。
- 默认验证：实现阶段以 `dotnet build MediaLibrary.sln` 为主；只改 Markdown 的阶段 build 可选。
- migration 边界：默认不新增 migration，不执行 database update。
- commit / push：默认不 commit，不 push。
- 7.5a 验证：`dotnet build MediaLibrary.sln` 通过，0 warning / 0 error；migration diff 为空。
- 7.5b 验证：`dotnet build MediaLibrary.sln` 通过，0 warning / 0 error；migration diff 为空。
- 7.5c 验证：`dotnet build MediaLibrary.sln` 通过，0 warning / 0 error；migration diff 为空。
- 7.5d 验证：`dotnet build MediaLibrary.sln` 通过，0 warning / 0 error；migration diff 为空。
- 7.5e 验证：`dotnet build MediaLibrary.sln` 通过，0 warning / 0 error；migration diff 为空。
- 7.5f 验证：`dotnet build MediaLibrary.sln` 通过，0 warning / 0 error；migration diff 为空；阶段结束前仅执行了播放器标题与在线字幕弹窗标题的局部视觉位移，其它审计差异已记录为报告项。

## 总目标

Phase 7.5 负责把播放器窗口、播放器悬浮层和播放器链路内的在线字幕搜索 UI 对齐最终草图。

本阶段重点：

- 草图是播放器 UI 结构标准，最终实现应优先对齐草图布局和交互结构。
- 草图颜色只用于区分区域，不作为最终配色来源；最终配色继续使用播放器固定深色资源。
- 字幕菜单在草图的 `无 / 内嵌 / 外挂` 基础上，必须补齐 `在线下载字幕` / 在线搜索字幕入口。
- 播放源、字幕、音轨悬浮菜单大部分先复制媒体库菜单的视觉和交互，再替换为播放器字段。
- 不重写 mpv 播放核心，不改变字幕 / 音轨 / 播放源切换业务语义。

## 总边界

属于本阶段：

- 播放器顶部标题 / 返回 / 窗口控制区。
- 播放器底部控制栏。
- 播放器 loading / buffering / notice 状态浮层。
- 左侧亮度浮层、右侧音量浮层、音量 hover 悬浮窗。
- 播放源 / 字幕 / 音轨悬浮菜单。
- 播放器链路内在线字幕搜索窗口的轻量视觉对齐。
- 可见文案修复，仅限本阶段触达的播放器 UI。
- 本阶段文档、阶段日志和 Known Issues 更新。

不属于本阶段：

- mpv 播放核心、WebDAV 大文件策略、R2.1a resume 逻辑、R3 字幕 / 音轨发现与切换核心。
- 扫描识别、TMDB / OMDb / AI、推荐、观影洞察、媒体库删除 / 移出语义。
- 在线字幕后端能力、OpenSubtitles API 合同、下载 / 绑定 / 缓存数据语义。
- 设置页在线字幕配置新增字段。
- 软件缓存物理清理策略。
- 数据库 schema、migration、database update。
- 详情页新增字幕入口。
- 全库自动字幕下载、字幕翻译、字幕编辑器、字幕 OCR。
- 主应用其他页面最终 UI 收口。

## 全阶段必读

每个 7.5 小阶段都必须先读：

- `AGENTS.md`
- `docs/player/PHASE_7_5_PLAYER_UI_PLAN.md`
- `docs/player/PLAYER_REWRITE_PLAN.md`
- `docs/player/PLAYER_KNOWN_ISSUES.md`
- `DesignDraft/page-spec/player-page.md`
- `DesignDraft/DESIGN.md`
- `DesignDraft/codex-ui-rules.md`
- `DesignDraft/resources-note.md`
- `DesignDraft/screenshots/播放器/01-播放器-弹窗^%全屏.pn`
- `DesignDraft/screenshots/播放器/02-播放器-悬浮窗组件.png`

按小阶段再读对应代码和补充文档。

## 已确认可复用内容

之前文档写明可复用的组件 / 规则：

- `DesignDraft/page-spec/player-page.md` 已写明可拆分 / 可复用的播放器组件：`PlayerWindowChrome`、`PlayerTopBar`、`PlayerStateOverlay`、`PlayerBottomControlBar`、`BrightnessOverlay`、`VolumeOverlay`、`VolumeHoverPopup`、`SubtitlePopupMenu`、`AudioTrackPopupMenu`、`PlaySourcePopupMenu`、`OnlineSubtitleSearchWindow`。
- `DesignDraft/codex-ui-rules.md` 写明播放器和在线字幕搜索弹窗使用固定播放器深色 Menu / Popup / Popover / Dialog 资源变体。
- `DesignDraft/resources-note.md` 写明播放器背景由 WPF / XAML 生成，不准备播放器背景图片。
- `docs/online-subtitles/ONLINE_SUBTITLES_PLAN.md` 写明在线字幕入口属于播放器字幕菜单，下载字幕保存到受管理字幕缓存，绑定不伪装成 `MediaFile`。
- `docs/player/PLAYER_REWRITE_PLAN.md` 写明播放器主线已切到 mpv/libmpv，不回退 LibVLC，不恢复旧 proxy。

与之前页面类似、可参考的组件：

- 媒体库筛选菜单：`src/MediaLibrary.App/Views/Pages/LibraryPage.xaml`、`LibraryPage.xaml.cs`。
- 影片发现菜单复用方式：`src/MediaLibrary.App/Views/Pages/MovieDiscoveryPage.xaml`、`MovieDiscoveryPage.xaml.cs`，仅参考其“本地复制媒体库菜单结构、不抽全局资源”的做法。
- 在线字幕搜索窗口：`src/MediaLibrary.App/Views/Player/OnlineSubtitleSearchWindow.xaml`、`OnlineSubtitleSearchWindow.xaml.cs`。
- 播放器现有浮层：`PlayerWindow.xaml` 中的 `ControlBarPopup`、`BufferingOverlayPopup`、`OperationNoticePopup`、`InteractionFeedbackPopup`。

已有 UI 规则：

- 播放器固定深色，不随普通页面浅色主题变成浅色播放器。
- 控制栏和菜单使用半透明深灰 / 黑灰浮层，不使用草图橙色 / 绿色 / 蓝色作为最终颜色。
- 播放进度、mpv buffering 反馈、离线缓存概念不能混用。
- 音量范围为 `0% - 200%`，超过 100% 是增益段。
- 亮度范围为 `0% - 100%`。
- 点击播放源 / 字幕 / 音轨按钮打开菜单；悬停按钮本身不打开菜单。
- 字幕菜单二级分类为 `内嵌`、`外挂`、`在线下载字幕`，其中在线下载字幕包含搜索入口和已下载 / 已绑定字幕。
- 删除在线字幕绑定只解除绑定，不删除缓存文件；物理清理归软件缓存管理。
- 文件名、路径、release 名等长字段需要省略和 tooltip；完整 WebDAV URL、账号、token、API key 不得裸露。

## 固定阶段输出模板

每个小阶段完成后，阶段记录必须包含：

- 完成了什么。
- 验收标准是什么。
- 哪些问题不属于该阶段。
- 是否修改业务逻辑；如有，写清修改了什么、为什么必要、如何验证未越界。
- 是否修改非本阶段处理的页面；如有，写清页面 / 共享资源、修改内容和可见影响。
- 复用了哪些之前文档写明可复用的组件。
- 参考了哪些之前页面的类似组件。
- 遵守了哪些已写好的 UI 规则。
- build 结果。
- migration diff 状态。
- Known Issues：Blocker / Deferred / Noise。

## 小阶段划分

### 7.5a 播放器外壳与底部控制栏

目标：

- 对齐草图中的顶部片名 / 返回入口 / 自定义窗口控制区。
- 对齐底部悬浮控制栏结构：播放 / 暂停、上一集 / 下一集、当前时间、进度条、总时长、音量区、播放源、字幕、音轨、全屏。
- 本阶段只调整播放器 UI 布局和本阶段触达的可见文案，不改变现有命令和播放状态逻辑。
- `停止` 按钮不作为最终底部控制栏组件；如果现有 UI 中仍显示，应在本阶段移出最终控制栏表面或隐藏。

必读：

- 全阶段必读材料。
- `src/MediaLibrary.App/Views/Player/PlayerWindow.xaml`
- `src/MediaLibrary.App/Views/Player/PlayerWindow.xaml.cs`
- `src/MediaLibrary.App/ViewModels/Player/PlayerWindowViewModel.cs`
- `src/MediaLibrary.App/Resources/Styles/Player.Controls.xaml`
- `src/MediaLibrary.App/Resources/Styles/Player.Dark.xaml`

可复用组件 / UI 规则：

- 现有 `PlayerWindowCaptionStyle`、`PlayerWindowCaptionButtonStyle`、`PlayerWindowCaptionCloseButtonStyle`。
- 现有 `ControlBarPopup`、`TogglePlayPauseCommand`、上一集 / 下一集命令、全屏处理。
- 播放器固定深色窗口规则。
- 草图中的顶部片名和底部悬浮控制栏结构。

可参考页面 / 组件：

- 当前 `PlayerWindow` 已有控制栏布局。
- `DesignDraft/page-spec/player-page.md` 的 `PlayerBottomControlBar` 拆分建议。

阶段输出要求：

- 完成内容：列出顶部外壳、控制栏结构、按钮文案 / 图标和可见状态的调整。
- 验收标准：弹窗和全屏状态下顶部 / 底部控件可见；控制栏不遮挡过多视频内容；播放 / 暂停、上一集 / 下一集、进度、音量、播放源、字幕、音轨、全屏仍触发原命令；`停止` 不再作为最终控制栏组件。
- 不属于本阶段：菜单内容重构、音量 hover popup、亮度 / 音量侧浮层细节、在线字幕搜索窗口、播放核心。
- 是否修改业务逻辑：预期为 `无`。如因移除 `停止` 可见入口触达命令绑定，必须记录只是 UI 表面变化，`StopCommand` 本身不改。
- 是否修改非本阶段页面：预期为 `无`。如抽取共享窗口样式，必须列出受影响窗口并验证可见影响。

### 7.5b 加载 / 提示 / 亮度 / 音量浮层

目标：

- 对齐草图中的中心加载提示。
- 对齐左侧亮度竖向浮层和右侧音量竖向浮层。
- 补齐或调整音量 hover 悬浮窗：从底部音量区域 hover 触发，包含音量图标、竖向音量条、当前百分比。
- 保持现有键盘 / 鼠标滚轮调节逻辑，不重写输入业务。

必读：

- 7.5a 必读材料。
- `src/MediaLibrary.App/Views/Player/PlayerWindow.xaml`
- `src/MediaLibrary.App/Views/Player/PlayerWindow.xaml.cs`
- `src/MediaLibrary.App/ViewModels/Player/PlayerWindowViewModel.cs`
- `DesignDraft/page-spec/player-page.md` 第 7、11、12、13、14、15、22、23 节。

可复用组件 / UI 规则：

- `BufferingOverlayPopup`、`OperationNoticePopup`、`InteractionFeedbackPopup`。
- `Brightness`、`BrightnessText`、`Volume`、`VolumeText`、`VolumeFeedbackText`。
- `PlayerPopoverStyle`、`PlayerSliderStyle`。
- 亮度 0-100、音量 0-200、音量增益段单独表现的规则。
- 不伪造离线缓存进度。

可参考页面 / 组件：

- 当前播放器已有 `ShowBrightnessFeedback`、`ShowVolumeFeedback`、`UpdateInteractionFeedbackPopupPlacement`。
- 草图的左右侧竖条和底部音量 hover 竖条。

阶段输出要求：

- 完成内容：列出中心状态浮层、亮度浮层、音量侧浮层、音量 hover popup 的 UI 和交互调整。
- 验收标准：loading / buffering / notice 能区分；无可靠百分比时不显示假进度；亮度显示 0-100；音量显示 0-200，超过 100 时为增益；浮层不遮挡关键控制栏；菜单打开时不误触发浮层。
- 不属于本阶段：播放状态机、mpv buffering 数据来源、R2.1a / R3 播放核心、菜单字段重构。
- 是否修改业务逻辑：预期为 `无`。如增加 hover popup 显隐状态，仅作为 UI 状态记录，不改变音量存储或播放音量语义。
- 是否修改非本阶段页面：预期为 `无`。

### 7.5c 字幕悬浮菜单与在线字幕入口

目标：

- 字幕菜单按草图和 page-spec 改为四类：`无字幕`、`内嵌`、`外挂`、`在线下载字幕`。
- `内嵌`、`外挂`、`在线下载字幕` 使用右开二级菜单。
- `在线下载字幕` 二级菜单必须包含 `搜索在线字幕` 入口和已下载 / 已绑定在线字幕列表。
- 删除在线字幕绑定使用按钮旁轻量确认 Popover；文案必须说明只删除绑定，不删除字幕缓存文件。
- 悬浮菜单优先复制媒体库菜单结构和行为，再替换字段。

必读：

- 全阶段必读材料。
- `docs/online-subtitles/ONLINE_SUBTITLES_PLAN.md`
- `docs/online-subtitles/ONLINE_SUBTITLES_KNOWN_ISSUES.md`
- `DesignDraft/page-spec/online-subtitle-search-page.md`
- `src/MediaLibrary.App/Views/Player/PlayerWindow.xaml.cs`
- `src/MediaLibrary.App/ViewModels/Player/PlayerWindowViewModel.cs`
- `src/MediaLibrary.App/Views/Pages/LibraryPage.xaml`
- `src/MediaLibrary.App/Views/Pages/LibraryPage.xaml.cs`

可复用组件 / UI 规则：

- 媒体库 `LibraryFilterContextMenuStyle`、`LibraryFilterMenuItemStyle` 的密度和交互方式，播放器内应复制成播放器深色本地样式或播放器专用资源。
- 媒体库右开二级菜单、按钮居中下拉、再次点击关闭逻辑。
- 现有 `NotifySubtitleMenuOpened`、`OpenOnlineSubtitleSearch`、`OnlineSubtitleMenuItems`、`SelectOnlineSubtitleFromMenu`、`DeleteOnlineSubtitleFromMenu`。
- 在线字幕只属于播放器链路，不在详情页新增入口。
- 删除绑定不删除缓存文件。

可参考页面 / 组件：

- 媒体库筛选菜单。
- 影片发现榜单多级菜单，仅参考其右开菜单实现方式。
- 当前播放器 `BuildSubtitleMenu`。

阶段输出要求：

- 完成内容：列出字幕菜单分类、二级菜单、在线字幕搜索入口、绑定列表、删除确认的调整。
- 验收标准：点击字幕按钮打开菜单；悬停分类打开右侧二级菜单；当前字幕高亮；无字幕可选；内嵌加载中 / 为空状态可见；外挂为空状态可见；在线搜索入口可打开搜索窗口；已下载 / 已绑定字幕可选择；删除绑定需要轻量确认且不删除缓存文件；菜单内容多时内部滚动。
- 不属于本阶段：OpenSubtitles API、下载 / 保存 / 绑定后端逻辑、字幕缓存物理清理、设置页配置、详情页字幕入口、字幕记忆。
- 是否修改业务逻辑：预期为 `无`。如新增删除确认门禁，记录为 UI 确认层，不改变最终删除绑定语义。
- 是否修改非本阶段页面：预期为 `无`。不得改详情页新增字幕入口。

### 7.5d 播放源与音轨悬浮菜单

目标：

- 播放源菜单对齐草图表头式菜单：`播放源 / 分辨率 / 码率 / 大小`。
- 播放源行使用既有 `PlaybackSourceItem` 展示字段，不新增探测逻辑。
- 音轨菜单对齐草图竖向列表，当前音轨高亮，支持空 / 加载 / 切换失败状态提示。
- 悬浮菜单优先复制媒体库菜单结构和行为，再替换字段。

必读：

- 全阶段必读材料。
- `src/MediaLibrary.Core/Models/ReadModels/PlaybackSourceItem.cs`
- `src/MediaLibrary.Core/Models/ReadModels/MediaSourceDisplayText.cs`
- `src/MediaLibrary.App/Views/Player/PlayerWindow.xaml.cs`
- `src/MediaLibrary.App/ViewModels/Player/PlayerWindowViewModel.cs`
- `src/MediaLibrary.App/Views/Pages/LibraryPage.xaml`
- `src/MediaLibrary.App/Views/Pages/LibraryPage.xaml.cs`

可复用组件 / UI 规则：

- `PlaybackSourceItem.FileName`、`ResolutionShortText`、`BitrateText`、`FormattedFileSize`、`SourceTypeText`、`VideoCacheStatusText`。
- 现有 `BuildSourceMenu`、`BuildAudioTrackMenu`、`SelectedSource`、`SelectAudioTrackFromMenu`。
- 媒体库菜单的紧凑高度、圆角、hover / selected、右开 / 居中下拉和内部滚动规则。
- 长字段省略 + tooltip，完整 WebDAV URL 不裸露。

可参考页面 / 组件：

- 媒体库筛选菜单。
- 当前播放器播放源菜单。
- 草图中的播放源表头式结构和音轨列表结构。

阶段输出要求：

- 完成内容：列出播放源表头、播放源行字段、当前源高亮、音轨行、音轨高亮和滚动状态调整。
- 验收标准：播放源菜单显示播放源、分辨率、码率、大小；当前播放源高亮；点击播放源仍走现有切换逻辑；音轨菜单显示可用音轨并高亮当前音轨；无音轨 / 加载中 / 切换失败有提示；菜单内容多时内部滚动；不显示完整 WebDAV URL、账号或密钥。
- 不属于本阶段：播放源选择算法、默认源业务、MediaProbe / ffprobe 探测逻辑、播放核心、音轨记忆。
- 是否修改业务逻辑：预期为 `无`。如新增只读展示投影，写清使用既有 `PlaybackSourceItem` 字段。
- 是否修改非本阶段页面：预期为 `无`。

### 7.5e 在线字幕搜索窗口视觉对齐

目标：

- 在线字幕搜索窗口作为播放器链路弹窗，固定使用播放器深色视觉。
- 轻量对齐标题栏、搜索条件区、状态提示、结果列表、下载按钮和错误 / 额度提示。
- 修复本阶段触达的可见乱码文案。
- 不改变搜索、下载、绑定、自动切换、额度处理和 pause-on-open 业务行为。

必读：

- 全阶段必读材料。
- `DesignDraft/page-spec/online-subtitle-search-page.md`
- `docs/online-subtitles/ONLINE_SUBTITLES_PLAN.md`
- `docs/online-subtitles/ONLINE_SUBTITLES_STAGE_LOG.md`
- `docs/online-subtitles/ONLINE_SUBTITLES_KNOWN_ISSUES.md`
- `src/MediaLibrary.App/Views/Player/OnlineSubtitleSearchWindow.xaml`
- `src/MediaLibrary.App/Views/Player/OnlineSubtitleSearchWindow.xaml.cs`
- `src/MediaLibrary.App/ViewModels/Player/OnlineSubtitleSearchViewModel.cs`

可复用组件 / UI 规则：

- `PlayerWindowCaptionStyle`、`PlayerWindowCaptionCloseButtonStyle`、播放器深色 Brush 资源。
- 现有 `OnlineSubtitleSearchViewModel` 搜索 / 下载 / 额度 / 错误投影。
- 长字段省略与 tooltip；敏感字段不回显。
- 关闭搜索窗口后不自动恢复播放。

可参考页面 / 组件：

- 当前 `OnlineSubtitleSearchWindow`。
- 播放器窗口标题栏。
- 设置页只作为配置归属参考，不改设置页。

阶段输出要求：

- 完成内容：列出搜索窗口标题栏、搜索区、状态区、结果行和可见文案调整。
- 验收标准：只能从播放器字幕菜单进入；打开时按现有逻辑暂停播放；关闭后不自动恢复；搜索条件和结果可用；下载状态、失败原因和额度提示仍显示；下载成功后仍自动切换字幕；窗口固定深色；不显示 API key、token、完整 WebDAV URL。
- 不属于本阶段：OpenSubtitles 配置、新增 provider、下载后端、绑定迁移、字幕缓存清理、设置页。
- 是否修改业务逻辑：预期为 `无`。
- 是否修改非本阶段页面：预期为 `无`；如只复用共享深色资源，需说明无普通页面可见变化。

### 7.5f 回归、文档和收口

目标：

- 对 7.5 做定向回归、文档更新和跨页面影响清点。
- 确认播放器、在线字幕搜索、媒体库、详情页和设置页没有越界变化。

必读：

- 全阶段必读材料。
- 7.5a - 7.5e 的阶段记录。
- `docs/player/PLAYER_STAGE_LOG.md`
- `docs/player/PLAYER_KNOWN_ISSUES.md`
- `docs/online-subtitles/ONLINE_SUBTITLES_KNOWN_ISSUES.md`

可复用组件 / UI 规则：

- 本文件所有固定输出模板。
- AGENTS.md 最终报告格式。

可参考页面 / 组件：

- 媒体库仅作为菜单参考影响回归。
- 详情页仅确认没有新增字幕入口。
- 设置页仅确认在线字幕配置入口仍归设置页。

阶段输出要求：

- 完成内容：汇总 7.5 全阶段完成内容。
- 验收标准：执行 `dotnet build MediaLibrary.sln`；确认 `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空；人工验收矩阵覆盖播放器弹窗 / 全屏、控制栏、加载、亮度、音量、三个菜单、在线字幕搜索、隐私脱敏和跨页面边界。
- 不属于本阶段：列出仍属于后续阶段的播放器缓存增强、字幕 / 音轨记忆、全局 UI 7.8 收口等。
- 是否修改业务逻辑：集中汇总；预期为 `无`。
- 是否修改非本阶段页面：集中汇总；预期为 `无` 或仅共享资源无可见变化。
- Known Issues：按 Blocker / Deferred / Noise 更新。

## 全阶段人工验收矩阵

| 编号 | 验收点 |
|---|---|
| 7.5-A01 | 播放器能以弹窗打开，顶部标题 / 返回 / 窗口控制清晰可用。 |
| 7.5-A02 | 播放器能进入 / 退出全屏，全屏下顶部和底部浮层位置正常。 |
| 7.5-A03 | 底部控制栏显示播放 / 暂停、上一集 / 下一集、当前时间、进度、总时长、音量、播放源、字幕、音轨、全屏。 |
| 7.5-A04 | `停止` 不作为最终底部控制栏组件。 |
| 7.5-A05 | loading / buffering / notice 区分清楚，不伪造离线缓存进度。 |
| 7.5-A06 | 左侧亮度浮层显示 0-100，右侧音量浮层显示 0-200。 |
| 7.5-A07 | 音量 hover popup 从音量区触发，单击音量图标不被改成打开菜单。 |
| 7.5-A08 | 点击播放源 / 字幕 / 音轨按钮打开菜单，悬停按钮本身不打开菜单。 |
| 7.5-A09 | 字幕菜单包含无字幕、内嵌、外挂、在线下载字幕。 |
| 7.5-A10 | 在线下载字幕二级菜单包含搜索入口和已下载 / 已绑定字幕列表。 |
| 7.5-A11 | 删除在线字幕绑定有轻量确认，并明确不删除字幕缓存文件。 |
| 7.5-A12 | 播放源菜单显示播放源、分辨率、码率、大小，且不暴露完整 WebDAV URL。 |
| 7.5-A13 | 音轨菜单显示可用音轨，当前音轨高亮，内容多时内部滚动。 |
| 7.5-A14 | 在线字幕搜索窗口固定深色，搜索 / 下载 / 额度 / 错误状态仍可用。 |
| 7.5-A15 | 详情页不新增字幕入口，设置页仍负责 OpenSubtitles 配置。 |
| 7.5-A16 | `dotnet build MediaLibrary.sln` 通过。 |
| 7.5-A17 | migration diff 为空。 |

## 已知风险和默认处理

Blocker：

- 当前计划阶段无已确认 blocker。

Deferred：

- 字幕 / 音轨记忆仍是播放器后续体验阶段，不纳入 7.5。
- 大 WebDAV 70GB+ 长时播放 stutter、HEVC 4K hwdec 风险仍归播放器核心 Known Issues，不在 7.5 UI 阶段解决。
- 主应用最终跨页面一致性仍归后续 7.8 或对应 UI 收口阶段。
- 主动全文件缓存、离线缓存、partial range cache 和下载队列仍不属于 7.5。

Noise：

- 草图中的橙色、绿色、蓝色只用于区域说明，不是最终配色。
- 媒体库菜单是参考实现；7.5 优先本地复制播放器菜单所需样式 / 行为，不强制抽全局组件。
- 文档中历史编码损坏文案不代表产品最终文案；实现阶段只修本阶段触达的播放器可见文案。

## 实现顺序建议

1. 先做 7.5a，锁定播放器外壳和底部控制栏。
2. 再做 7.5b，锁定状态 / 亮度 / 音量浮层。
3. 再做 7.5c，处理字幕菜单和在线字幕入口。
4. 再做 7.5d，处理播放源和音轨菜单。
5. 再做 7.5e，轻量对齐在线字幕搜索窗口。
6. 最后做 7.5f，回归、文档和收口。

每个小阶段尽量小补丁收口，不要把播放器外壳、字幕菜单和在线字幕搜索窗口混在一个补丁里。
