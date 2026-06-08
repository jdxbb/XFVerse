# Phase 7.6 设置 / 扫描 / 缓存执行计划

Last updated: 2026-06-07

本文件是 Phase 7.6 的唯一详细执行计划。后续进入 7.6 任一小阶段前，必须先阅读本文件，再按对应小阶段的必读清单读取文档、代码和草图。除必读项外，其它资料按实现风险和代码证据按需读取。

## 阶段状态

- 状态：7.6a、7.6b、7.6c、7.6d、7.6e 已完成；7.6f 尚未开始。
- 2026-06-07 API 配置交互 follow-up：用户明确覆盖 7.6c 早期限制，允许新增大模型测试入口，并要求 API 配置保存后自动测试、测试按钮不自动保存。
- 当前范围：设置页、扫描任务页、软件缓存管理区，以及两页需要复用的设置 / 扫描 / 缓存局部组件。
- 主要视觉标准：`DesignDraft/screenshots/账号/设置/01-设置-通用-全屏.png`、`DesignDraft/screenshots/账号/设置/02-设置-API配置-全屏.png`、`DesignDraft/screenshots/账号/扫描任务/01-扫描任务-全屏.png`。
- 主要规格来源：`DesignDraft/page-spec/settings-page.md`、`DesignDraft/page-spec/scan-task-page.md`、`DesignDraft/page-spec/cache-management-page.md`。
- 当前任务明确要求：7.6 重点对齐草图；草图是本阶段 UI 标准。若草图与安全 / 业务语义冲突，安全和业务语义优先，并在阶段输出中记录冲突和取舍。

## 总目标

Phase 7.6 负责把设置、扫描任务和缓存管理收口到 Phase 7 视觉与交互标准：

- 设置页保留 `通用` / `API 配置` 两个 Tab；Tab 位置与按钮式模板完全参考影片发现页，`通用` Tab 不做页面级纵向滚动，`API 配置` Tab 允许纵向滚动并使用现代化滚动条。
- `API 配置` 中 OpenSubtitles 配置按 TMDB / OMDb 配置卡结构同构实现：字段区、配置状态、保存按钮、测试按钮、独立状态反馈。
- 扫描任务页清晰拆分 WebDAV 与本地目录两个来源，本地配置与草图中的扫描配置结构同构。
- WebDAV / 本地扫描分别启动，共享进度区和历史日志区。
- 软件缓存管理只呈现海报、metadata / 其它、在线字幕三类缓存，不恢复视频缓存管理入口。
- 所有可滚动区域统一走现代化滚动设计；不可滚动区域不得擅自加滚动。
- 有长度上限或可能溢出的字段统一按规则省略；只有实际省略时才允许悬停显示完整允许内容。
- 必要的后端逻辑和数据模型变化允许进入 7.6，但必须先在对应小阶段说明原因、影响面、migration 状态和最小方案。

## 当前审计结论

- `SettingsViewModel` 已有 TMDB、OMDb、OpenSubtitles、AI、主题和三类软件缓存相关命令，可优先复用。
- `SettingsPage.xaml` 已有 `通用` / `API 配置` Tab 和 OpenSubtitles 配置卡，但敏感字段显示 / 隐藏、同构卡片结构、状态徽章、字段长度和长字段 Tooltip 仍需按 7.6 统一。
- `ScanTasksViewModel` 已有 WebDAV 与本地目录扫描路径、独立本地扫描启动、取消扫描、统一扫描日志和进度命令，可优先复用。
- `ScanTasksPage.xaml` 已有 WebDAV / 本地目录 / 进度 / 记录结构，但本地路径选择器、WebDAV 路径选择器、删除本地路径轻量 Popover、长字段 Tooltip 和日志脱敏展示仍需收口。
- `IWebDavService.ListDirectoryAsync` 和 `RemoteEntry` 已能支撑 WebDAV 目录浏览；如果协议或服务只提供路径，应在 App 层用路径还原层级，做简单路径选择器。
- 当前仓库未发现本地文件夹 picker 服务；7.6 可新增 App 层选择服务或窗口代码，不应把该能力放进 Core 业务模型。
- `ApplicationSetting` 当前只有主题和 API 配置；7.6b 后续返工已把关闭窗口行为、点击播放后是否自动全屏改为 App 层本地偏好文件保存，不新增 Core 模型字段、migration 或 database update。
- 7.6b 当前真实可用的通用行为包括主题模式、关闭窗口行为（退出软件 / 缩小到托盘）、点击播放后是否自动全屏和启动时自动扫描 WebDAV；这些通用行为均不新增 Core 模型字段、migration 或 database update。
- `SettingsService.DeleteLocalScanPathAsync` 当前会将该本地路径下未删除的 `MediaFile` 标记为 `IsDeleted=true` 后删除扫描路径配置。7.6e 必须明确确认该行为是否符合“移除路径配置不删除真实文件、不清用户状态和 metadata”的产品语义；如修改，必须作为业务逻辑变更记录。

## 全阶段固定规则

### 必读基线

每个 7.6 小阶段都必须先读：

- `AGENTS.md`
- `docs/ui-redesign/PHASE_7_6_SETTINGS_SCAN_CACHE_PLAN.md`
- `DesignDraft/PHASE_7_PLAN.md`
- `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- `DesignDraft/PHASE_7_STAGE_LOG.md`
- `DesignDraft/UI-REBUILD-README.md`
- `DesignDraft/DESIGN.md`
- `DesignDraft/resources-note.md`
- `DesignDraft/codex-ui-rules.md`

### 可复用组件和基础设施

优先复用或扩展：

- 视觉资源：`PageCardStyle`、`CompactCardStyle`、`GlassPageCardStyle`、`GlassCompactCardStyle`、`GlassPrimaryButtonStyle`、`GlassSecondaryButtonStyle`、`ModernTabControlStyle`、`ModernTabItemStyle`、`FormTextBoxStyle`、`FormComboBoxStyle`、`FormCheckBoxStyle`。
- 辅助行为：`TrimmedTextToolTipBehavior`、`ScrollBarAutoRevealBehavior`、`TextScrollOverflowCueBehavior`、`PasswordBoxBindingHelper`。
- 服务：`ISettingsService`、`IWebDavService`、`IOpenSubtitlesClientService`、`ISoftwareCacheManagementService`、`IConfirmationDialogService`、`IDataRefreshService`。
- ViewModel：`SettingsViewModel`、`ScanTasksViewModel`。
- 确认体系：`ConfirmationDialogService` / `ConfirmationDialogWindow`；删除本地扫描路径使用按钮旁轻量 Popover，不默认使用居中大弹窗。

如需新增组件，优先小而通用：

- `SensitiveSettingInput`：敏感字段输入，默认隐藏，眼睛图标切换显示。
- `ApiConfigCard`：标题、配置状态、字段区、保存 / 测试、状态反馈。
- `SettingFieldRow`：标签、控件、说明 / 状态。
- `CacheCategoryCard`：缓存占用、可清理状态、禁用原因、清理按钮。
- `ScanPathRow`：路径显示、启用 / 递归状态、编辑 / 启停 / 移除操作。
- `ScanLogCard`：来源、状态、时间、统计、原因摘要，统一脱敏。
- `PathPickerPanel`：本地目录或 WebDAV 目录选择器的共享外壳。
- `LocalScanPathRemovePopover`：删除本地扫描路径轻量确认。

7.6a 已建立：

- `SensitiveSettingInput`
- `ApiConfigCard`
- `SettingFieldRow`
- `CacheCategoryCard`
- `ScanPathCard`

7.6a 暂不建立：

- `ScanLogCard`：需要 7.6e 先确认扫描日志字段、识别计数和脱敏摘要。
- `PathPickerPanel`：需要 7.6d 先确认本地目录 picker、WebDAV 目录浏览和路径层级还原后端入口。
- `LocalScanPathRemovePopover`：需要 7.6e 与删除本地扫描路径业务语义一起收口。

7.6e 已建立：

- `ScanLogCard`
- `LocalScanPathRemovePopover`

### 滚动区域矩阵

必须可滚动：

- 设置页 API 配置 Tab：卡片随 Tab 内容区纵向滚动，使用现代化滚动条，不给卡片内部加横向滚动。
- 扫描任务页 WebDAV 路径列表：列表较长时区域内滚动。
- 扫描任务页本地目录列表：列表较长时区域内滚动。
- 扫描任务页历史日志列表：右侧日志区域内滚动。
- WebDAV 路径选择器目录列表：目录较多时内部滚动。
- 本地路径选择器辅助列表如存在：内容较多时内部滚动。

不得擅自滚动：

- 设置页 `通用` Tab 不做页面级纵向滚动。
- 设置页标题和 Tab 本身不单独做横向滚动。
- 扫描任务页整体不做页面级纵向滚动；扫描配置、扫描进度和扫描记录按草图固定在一屏结构内，只有路径列表和日志列表等内部区域可滚动。
- 单个 API 卡、缓存卡、路径行、日志卡默认不独立滚动；文本超长按省略 / 换行规则处理。
- 扫描进度区不因统计项过多产生内部滚动；统计项不足以稳定产出时隐藏或明确禁用，不用占位伪装。
- 删除本地扫描路径 Popover 不作为可滚动详情容器。

现代化滚动要求：

- `ScrollViewer` 使用 `VerticalScrollBarVisibility="Auto"`，默认禁用横向滚动。
- 能接入 `ScrollBarAutoRevealBehavior` 的滚动区域应接入自动显隐。
- 纯文本长说明若采用内部滚动，才接入 `TextScrollOverflowCueBehavior`；表格、列表和卡片集合不套半行截断提示。

### 字段长度和悬停规则

输入字段必须按当前 EF /服务边界设置合理 `MaxLength` 或保存前校验：

| 字段 | 当前上限来源 | UI 要求 |
| --- | --- | --- |
| WebDAV 连接名 | `SourceConnection.Name` 120 | 输入限制 120；显示名溢出时省略 |
| WebDAV BaseUrl | `SourceConnection.BaseUrl` 500 | 输入限制 500；配置表单可显示，日志 / Tooltip 不裸露完整 URL |
| WebDAV 用户名 | `SourceConnection.Username` 200 | 输入限制 200；日志显示安全摘要，不显示完整账号 |
| WebDAV 密码 | `SourceConnection.PasswordEncrypted` 1000 | 默认隐藏；不在状态、日志、Tooltip 回显 |
| 扫描路径 Path | `ScanPath.Path` 1000 | 输入限制 1000；路径行单行省略，只有实际省略且允许展示时 Tooltip |
| 扫描路径显示名 | `ScanPath.DisplayName` 200 | 输入限制 200；路径行标题溢出时省略 |
| TMDB Read Access Token | `ApplicationSetting.TmdbReadAccessToken` 2048 | 敏感字段，默认隐藏，眼睛按钮 |
| TMDB API Key | `ApplicationSetting.TmdbApiKey` 256 | 敏感字段，默认隐藏，眼睛按钮 |
| OMDb API Key | `ApplicationSetting.OmdbApiKey` 256 | 敏感字段，默认隐藏，眼睛按钮 |
| OpenSubtitles Endpoint | `ApplicationSetting.OpenSubtitlesEndpoint` 512 | 普通输入；状态文案不输出密钥 |
| OpenSubtitles API Key | `ApplicationSetting.OpenSubtitlesApiKey` 512 | 敏感字段，默认隐藏，眼睛按钮 |
| OpenSubtitles Username | `ApplicationSetting.OpenSubtitlesUsername` 256 | 普通输入；状态 / 日志不泄露账号细节 |
| OpenSubtitles Password | `ApplicationSetting.OpenSubtitlesPasswordEncrypted` 2048 | 敏感字段，默认隐藏，眼睛按钮 |
| OpenSubtitles Language | `ApplicationSetting.OpenSubtitlesDefaultLanguageCode` 32 | 下拉选择，避免自由输入 |
| AI Base URL | `ApplicationSetting.AiBaseUrl` 512 | 普通输入 |
| AI API Key | `ApplicationSetting.AiApiKey` 2048 | 敏感字段，默认隐藏，眼睛按钮 |
| AI 模型 / 路由字段 | `ApplicationSetting.AiModel` 128 存储边界 | 输入需限制或保存前校验，不新增未存在业务用途 |
| 扫描错误消息 | `ScanTaskLog.ErrorMessage` 4000 | 日志卡可换行或摘要；不得泄露凭据 |
| 扫描原因摘要 | `ScanTaskLog.ReasonSummaryJson` 12000 | 默认显示摘要，不做完整 JSON 裸露 |

悬停显示完整内容只适用于：

- 已经发生视觉省略的允许展示字段，如本地路径、文件名、路径别名、扫描目标别名。
- 不适用于完整 WebDAV URL、账号、password、token、API key、access token、完整远端敏感路径和未被省略的普通文本。
- `TrimmedTextToolTipBehavior` 必须作为首选实现；不要给所有 TextBlock 固定设置 Tooltip。

### 业务和数据模型规则

- 默认不 commit、不 push、不执行 database update。
- 允许必要的数据模型变化，但必须在对应小阶段先说明原因、影响面、migration 名称、回滚风险和最小替代方案。
- 不新增 migration，除非当前小阶段明确需要且用户已允许；执行后必须记录 migration diff。
- 扫描识别逻辑、AI 识别规则、TMDB / OMDb / OpenSubtitles API 调用语义不得因 UI 对齐被重写。
- 删除本地扫描路径、缓存清理、取消扫描都不得删除真实本地文件或 WebDAV 文件。
- 不恢复视频缓存管理入口。

## 小阶段划分

### 7.6a - 审计与共享组件基线

目标：

- 建立 7.6 的实现入口、组件复用清单和字段 / 滚动 / 脱敏矩阵。
- 抽取或补齐设置 / 扫描可共用的 UI 小组件，但不重排完整页面。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/settings-page.md`
- `DesignDraft/page-spec/scan-task-page.md`
- `DesignDraft/page-spec/cache-management-page.md`
- `DesignDraft/page-spec/global-dialogs.md`
- `src/MediaLibrary.App/Resources/Styles/*.xaml`
- `src/MediaLibrary.App/Helpers/TrimmedTextToolTipBehavior.cs`
- `src/MediaLibrary.App/Helpers/ScrollBarAutoRevealBehavior.cs`
- `src/MediaLibrary.App/ViewModels/Pages/SettingsViewModel.cs`
- `src/MediaLibrary.App/ViewModels/Pages/ScanTasksViewModel.cs`

可复用组件：

- 已有 ResourceDictionary 中的卡片、按钮、输入、Tab、Tooltip、滚动条资源。
- `TrimmedTextToolTipBehavior`、`ScrollBarAutoRevealBehavior`、`PasswordBoxBindingHelper`。
- `ConfirmationDialogService` 作为后续确认体系基础。

可参考页面 / 组件：

- 现有 `SettingsPage.xaml` 的通用 / API Tab 分区。
- 现有 `ScanTasksPage.xaml` 的 70% / 30% 扫描配置和记录结构。
- 详情页和影片发现页已接入的现代化滚动 / Tooltip 用法。

已有 UI 规则：

- `codex-ui-rules.md` 中的长字段、敏感字段、扫描与缓存管理边界。
- `DESIGN.md` 中的设置页、扫描页、滚动和液态玻璃资源规则。

阶段输出要求：

- 完成了什么：列出新增或复用的共享组件、资源、行为和不抽取的原因。
- 验收标准：组件能在设置页和扫描页复用；不硬编码颜色；不产生横向滚动；build 通过。
- 不属于该阶段：不改完整设置页布局、不改扫描启动流程、不改缓存清理业务、不加数据模型字段。
- 业务逻辑变化：默认无；若新增 App 层纯 UI service，说明不改变 Core 业务语义。
- 非本阶段页面变化：如改动全局资源导致其它页面视觉变化，必须列出受影响页面和回归结果；否则写“无”。

7.6a 执行结果（2026-06-06）：

- 完成了什么：
  - 新增 `src/MediaLibrary.App/Controls/SensitiveSettingInput.xaml`：敏感字段输入控件，默认使用 `PasswordBox` 隐藏，眼睛按钮切换明文显示，复用 `PasswordBoxBindingHelper` 和全局输入 / 图标按钮样式。
  - 新增 `src/MediaLibrary.App/Controls/SettingFieldRow.xaml`：字段标签、控件内容和说明文字的通用行，标签按 `TrimmedTextToolTipBehavior` 处理真实省略 Tooltip。
  - 新增 `src/MediaLibrary.App/Controls/ApiConfigCard.xaml`：API 配置卡外壳，包含标题、说明、配置状态、字段区、动作区和反馈文案；供 TMDB / OMDb / OpenSubtitles / AI 配置同构复用。
  - 新增 `src/MediaLibrary.App/Controls/CacheCategoryCard.xaml`：缓存类别卡外壳，覆盖缓存说明、占用、详情、操作和状态文案；供海报、metadata / 其它、在线字幕三类缓存复用。
  - 新增 `src/MediaLibrary.App/Controls/ScanPathCard.xaml`：WebDAV / 本地扫描路径行外壳，标题和路径按真实截断 Tooltip 规则处理，动作区由页面传入。
  - 复用现有 `CompactCardStyle`、`FormTextBoxStyle`、`FormPasswordBoxStyle`、`SmallIconButtonStyle`、`StatusBadgeStyle`、`LongFieldTextBlockStyle`、`TrimmedTextToolTipBehavior`、`PasswordBoxBindingHelper`。
- 验收标准：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。
  - 新增控件只引用全局 ResourceDictionary，不硬编码业务颜色。
  - 新增控件默认不启用横向滚动；长标题 / 路径字段使用省略和真实截断 Tooltip。
  - 敏感输入控件显示 / 隐藏只影响 UI 呈现，不改变保存语义。
- 不属于该阶段：
  - 未重排 `SettingsPage.xaml` 或 `ScanTasksPage.xaml`。
  - 未接入本地路径选择器、WebDAV 路径选择器、删除本地扫描路径 Popover。
  - 未修改扫描启动、缓存清理、API 保存 / 测试或通用设置业务逻辑。
  - 未新增数据模型字段、migration 或 database update。
- 业务逻辑变化：无。
- 非本阶段页面变化：无；本阶段只新增未接入页面的共享 UI 控件。

### 7.6b - 设置页通用 Tab 与缓存管理

目标：

- 对齐 `01-设置-通用-全屏.png`：标题、Tab、缓存设置、行为设置、关于区域。
- 软件缓存管理对齐 `cache-management-page.md`，只显示海报、metadata / 其它、在线字幕三类。
- 审计并实现或降级通用设置：关闭行为、播放后自动全屏、主题模式、自动扫描 WebDAV。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/settings-page.md`
- `DesignDraft/page-spec/cache-management-page.md`
- `DesignDraft/page-spec/global-dialogs.md`
- `DesignDraft/screenshots/账号/设置/01-设置-通用-全屏.png`
- `src/MediaLibrary.App/Views/Pages/SettingsPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/SettingsViewModel.cs`
- `src/MediaLibrary.App/Services/Interfaces/IThemeService.cs`
- `src/MediaLibrary.App/Services/Interfaces/ISoftwareCacheManagementService.cs`
- `src/MediaLibrary.Core/Models/Entities/ApplicationSetting.cs`
- `src/MediaLibrary.Core/Models/Settings/ApplicationSettingModel.cs`
- `src/MediaLibrary.Core/Services/Interfaces/ISettingsService.cs`
- `src/MediaLibrary.Core/Services/Implementations/SettingsService.cs`

可复用组件：

- `CacheCategoryCard`、`SettingFieldRow`、`SensitiveSettingInput` 如 7.6a 已建立。
- `ISoftwareCacheManagementService` 的 overview / clear / save limit 能力。
- `IConfirmationDialogService` 的危险确认。
- `IThemeService` 的主题模式列表和保存能力。

可参考页面 / 组件：

- 现有设置页缓存区。
- 全局确认弹窗中的缓存清理危险变体。
- 影片发现 / 媒体库中已使用的 Tab、按钮、状态文案密度。

已有 UI 规则：

- 设置页 `通用` Tab 不做页面级纵向滚动；`API 配置` Tab 内容区允许纵向滚动并使用现代化滚动条。
- 缓存清理不删除真实本地媒体、WebDAV 文件、用户配置或绑定关系。
- 在线字幕缓存只清孤立缓存；绑定引用不可确认时禁用清理。
- 未实现的行为设置不得伪装为可用功能。

阶段输出要求：

- 完成了什么：逐项记录通用 Tab、缓存卡、行为设置和关于区域改动；记录哪些通用设置是真实可用、哪些保持禁用或 Deferred。
- 验收标准：通用 Tab 不出现页面级纵向滚动；三类缓存卡状态完整；视频缓存入口不存在；缓存确认文案明确不删除媒体源；主题保存可用；未实现行为项不触发假命令。
- 不属于该阶段：不改 API 配置卡细节、不改扫描任务页、不改播放器、不改在线字幕搜索。
- 业务逻辑变化：如新增关闭行为、自动全屏、自动扫描 WebDAV 的持久化或启动逻辑，必须列出新增字段、默认值、migration、触发点和回归结果；否则写“无业务逻辑变化”。
- 非本阶段页面变化：如主题或关闭行为影响主窗口 / 播放器，必须列出页面和验证；否则写“无”。

7.6b 执行结果（2026-06-06）：

- 完成了什么：
  - 重排 `SettingsPage.xaml` 的 `通用` Tab：保留页面标题、Tab、软件缓存、行为设置和关于区域；后续返工已移除页内右上角主题图标入口，并取消 `通用` Tab 的页面级滚动。
  - 软件缓存区复用 `CacheCategoryCard`，只呈现海报缓存、metadata / 其它缓存、在线字幕缓存三类；不恢复视频缓存管理入口。
  - 海报缓存复用 `SettingFieldRow` 展示容量上限，输入限制 `MaxLength=8`，保存上限与清理操作分离；清理按钮按真实 `IsPosterCacheClearAvailable` 禁用状态绑定。
  - metadata / 其它缓存和在线字幕缓存清理按钮按 `IsOtherCacheClearAvailable`、`IsSubtitleCacheClearAvailable` 绑定禁用状态，清理按钮使用危险操作样式。
  - 行为设置区显示关闭窗口、播放时全屏、外观主题、自动扫描 WebDAV。主题是真实可保存项；其它三项按现有行为只读 / disabled 展示，不触发假命令。
  - 关于区域改为紧凑信息行，保留版本和本地桌面影音库说明，不再显示“后续阶段完善”类占位文案。
  - `SettingsViewModel` 已补齐主题图标切换、固定行为展示属性、海报缓存清理 CanExecute、软件缓存 overview 状态和关于文案。
  - `IThemeService` 增加 `ThemeChanged` 事件，`ThemeService` 在主题资源替换后广播，`MainWindowViewModel` 订阅该事件以同步标题栏主题图标。
- 验收标准：
  - `dotnet build MediaLibrary.sln` 必须通过。
  - `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 必须为空。
  - 通用 Tab 不出现页面级纵向滚动，页面标题和 Tab 不单独横向滚动。
  - 缓存管理只显示海报、metadata / 其它、在线字幕三类，且清理确认文案继续明确不删除真实媒体源、WebDAV 文件、用户配置或绑定中的字幕缓存。
  - 主题保存和主窗口标题栏主题图标同步生效；设置页内部额外右上角主题按钮不显示；未实现的关闭行为、播放时全屏、自动扫描 WebDAV 不提供可点击保存入口。
  - 长说明文本换行显示；有限制输入字段设置长度上限；本阶段没有给敏感值或未省略普通文本新增固定 Tooltip。
- 不属于该阶段：
  - 不改 API 配置 Tab 的 TMDB / OMDb / OpenSubtitles / AI 卡结构；7.6c 处理。
  - 不改扫描任务页、路径选择器、扫描运行区、扫描日志或删除本地扫描路径 Popover；7.6d-7.6e 处理。
  - 不改播放器窗口全屏行为，不新增关闭该行为的配置。
  - 不新增自动扫描 WebDAV 启动调度。
  - 不新增数据模型字段、migration 或 database update。
- 通用设置审计结论：
  - 真实可用：主题模式，复用 `IThemeService.ApplyAndSaveAsync` 和 `ApplicationSetting.ThemeMode`。
  - 真实可用：海报缓存容量上限、海报缓存清理、metadata / 其它缓存清理、在线字幕孤立缓存清理，复用 `ISoftwareCacheManagementService`。
  - Disabled / Deferred：关闭窗口行为。当前主窗口关闭按钮直接结束应用，没有托盘驻留设置字段或托盘生命周期后端。
  - Disabled / Deferred：播放时自动全屏。当前播放器窗口打开即进入全屏，但没有可保存开关；7.6b 不改变播放器行为。
  - Disabled / Deferred：自动扫描 WebDAV。当前无启动时自动扫描配置字段或调度入口；扫描任务页仍手动启动。
- 业务逻辑变化：
  - 无 Core 业务语义、扫描、缓存清理、播放器全屏、关闭行为或自动扫描逻辑变化。
  - App 层 UI 同步变化：主题服务新增 `ThemeChanged` 事件，设置页保存 / 切换主题后主窗口主题图标同步刷新；不改变持久化字段或业务默认值。
- 非本阶段页面变化：
  - `MainWindowViewModel` 仅订阅主题变化以刷新标题栏主题图标和 Tooltip。
  - 未修改播放器页、扫描任务页、媒体库、发现、详情页或在线字幕搜索页。
- 验证结果：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。
  - `git diff --check` 通过，仅有 LF / CRLF 换行提示。
  - `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 返回空。

7.6b 返工补丁（2026-06-07，通用设置卡片结构对齐）：

- 完成了什么：
  - 将 `SettingsPage.xaml` 的 `通用` Tab 卡片层级进一步贴近 `01-设置-通用-全屏.png`：外层保留 `缓存设置`、`行为设置` 两张大区块，区块内部统一为一个浅底连续行列表。
  - 缓存区不再呈现多个独立缓存小卡，改为海报缓存大小、其它缓存大小、海报缓存上限、在线字幕缓存四行连续结构；右侧操作按钮宽度对齐。
  - 行为区移除界面上的阶段说明文案，改为关闭行为、播放后全屏、主题模式、自动扫描四行连续结构；关于 XFVerse 作为行为区下方同构浅底信息行，而不是独立营销卡。
  - 行标题和主要数值接入 `TrimmedTextToolTipBehavior` 的真实省略 Tooltip 规则；长说明继续换行，不给未省略说明文本固定 Tooltip。
- 验收标准：
  - `dotnet build MediaLibrary.sln` 通过。
  - `通用` Tab 仍只显示海报、metadata / 其它、在线字幕三类真实缓存能力，不恢复视频缓存入口。
  - 两张外层区块和内部连续行列表与草图层级一致；关于区域位于行为设置区内。
  - 缓存、主题和关于按钮仍绑定原有命令；当时未实现的关闭行为、播放后全屏、自动扫描 WebDAV 仍只读 / disabled。该状态已被后续行为接入补丁覆盖。
- 不属于该补丁：
  - 不重做 `API 配置` Tab。
  - 不新增缓存目录打开入口，不恢复视频缓存大小、视频缓存上限或视频缓存目录字段。
  - 不实现关闭到托盘、播放器全屏开关或自动扫描 WebDAV 后端逻辑。
  - 不修改扫描任务页、播放器、媒体库、发现、详情页或在线字幕搜索窗口。
- 业务逻辑变化：无。该补丁只调整 `SettingsPage.xaml` 的通用 Tab 呈现结构和局部样式。
- 非本阶段页面变化：无。
- 验证结果：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。

7.6b 返工补丁（2026-06-07，通用设置草图结构二次收口）：

- 完成了什么：
  - 移除 `SettingsPage.xaml` 设置页最外层可见整页卡片外壳，避免 `通用` / `API 配置` 卡片再被包进一张大卡里；设置内容直接位于主窗口内容区。
  - 隐藏设置页内部重复标题和说明，保留主窗口标题与页内 Tab，减少草图中不存在的重复顶部层级；后续 Tab 收口补丁已移除额外页内右上角主题按钮。
  - 压缩 `通用` Tab 的两张区块卡片内边距、连续行列表内边距和卡片间距，使缓存设置、行为设置和关于行在首屏关系上更接近草图。
  - 行为设置说明列改为单行省略，并继续使用 `TrimmedTextToolTipBehavior`，只有真实省略时才允许悬停显示完整允许内容。
  - `API 配置` Tab 同步受益于外层整页卡移除：TMDB / OMDb / OpenSubtitles / AI 服务卡直接位于页面内容区，仍保持 7.6c 的同构卡结构。
- 验收标准：
  - `dotnet build MediaLibrary.sln` 通过。
  - `通用` Tab 呈现为两张主区块卡，每张卡内部是一块连续浅底行列表；关于 XFVerse 仍在行为设置列表内。
  - `API 配置` Tab 不出现整页卡包服务卡的卡片套卡片结构，单个 API 卡仍不产生内部横向滚动。
  - 当时未实现的关闭行为、播放后全屏、自动扫描 WebDAV 仍只读 / disabled；视频缓存入口仍不恢复。该状态已被后续行为接入补丁覆盖。
- 不属于该补丁：
  - 不新增缓存目录入口，不新增关闭到托盘 / 播放全屏 / 自动扫描后端。
  - 不改 API 保存 / 测试业务语义，不新增 AI 测试命令。
  - 不修改扫描任务页业务逻辑、播放器、媒体库、发现、详情页或在线字幕搜索窗口。
- 业务逻辑变化：无。该补丁只调整 `SettingsPage.xaml` 的可见卡片层级、通用页局部样式和设置页顶部呈现。
- 非本阶段页面变化：无。设置页内 `API 配置` Tab 的外层卡片层级随同一页面外壳调整变化；其它页面未改。
- 验证结果：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。
  - 运行截图对比了 `通用` Tab 和 `API 配置` Tab，确认通用页两张主卡和 API 服务卡不再处于整页卡套卡结构。

7.6b 返工补丁（2026-06-07，设置页 Tab 按影片发现页收口）：

- 完成了什么：
  - `SettingsPage.xaml` 根布局改为与影片发现页一致的 `Grid` 直接承载 `TabControl`，移除原先占位的顶部行。
  - 设置页 Tab 可见头部改为影片发现页同构的手写按钮模板，原生 `TabPanel` 隐藏；标签仅替换为 `通用` / `API 配置`。
  - 移除设置页页面内部右上角新增的主题切换按钮；主窗口标题栏主题按钮和通用设置卡内主题操作不属于本次移除对象。
  - `通用` Tab 不再套页面级 `ScrollViewer`；`API 配置` Tab 保留纵向滚动，并接入 7.6 现代化滚动条样式。
- 验收标准：
  - 设置页 Tab 顶部位置、基线、按钮宽度、高度和下划线行为与影片发现页模板一致。
  - `通用` Tab 页面内部不出现右上角主题按钮，不出现页面级纵向滚动条。
  - `API 配置` Tab 内容溢出时显示现代化纵向滚动条，且不出现横向滚动。
  - `dotnet build MediaLibrary.sln` 通过。
- 不属于该补丁：
  - 不移除主窗口标题栏主题按钮。
  - 不移除通用设置卡内既有主题保存 / 即时切换操作。
  - 不改变缓存清理、API 保存 / 测试、主题持久化或扫描任务业务语义。
- 业务逻辑变化：无设置业务变化；仅新增设置页 Tab 选择索引和命令用于匹配影片发现页的按钮式 Tab 模板。
- 非本阶段页面变化：无。影片发现页仅作为模板参考，未修改。
- 验证结果：
  - 首次 build 因旧 `MediaLibrary.App` 调试进程锁定 exe 失败；关闭该旧进程后重新执行 `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。

7.6b 返工补丁（2026-06-07，通用设置细节对齐）：

- 完成了什么：
  - `SettingsPage.xaml` 的 `通用` Tab 去掉 `缓存设置` 标题右侧状态文本和刷新按钮；软件缓存状态仍在页面激活时自动加载。
  - `缓存设置`、`行为设置` 标题左对齐到第一列文字左侧，并在标题和行列表之间增加更长、更粗、更深的分隔线。
  - 去掉两张设置卡内部行列表外的浅底圆角矩形；行分隔线保留为更短、更轻的内部线。
  - 右侧缓存操作按钮取消 154px 固定宽度，改为按文字长度自适应。
  - `海报缓存上限` 删除副标题 `单位 MB`，将 `MB` 放入输入框末尾；输入框高度、可输入区域、字体和圆角按草图收紧。
  - `关于 XFVerse` 从 `行为设置` 卡片移出，作为通用页底部独立行展示，并接入与主窗口一致的软件 icon。
  - 设置卡、行列表和通用页内容显式使用普通箭头，避免继承 Tab 的点击手势。
- 验收标准：
  - `缓存设置` 标题右侧无状态文本和刷新按钮。
  - `海报缓存上限` 输入框末尾显示 `MB`，输入框更矮、圆角更小，文字垂直可读。
  - 两张设置卡内部无圆角浅底容器，标题分隔线比内部行线更长、更粗、更深。
  - `关于 XFVerse` 独立位于页面底部靠上位置，并显示软件 icon。
  - `dotnet build MediaLibrary.sln` 通过。
- 不属于该补丁：
  - 不改变缓存加载、清理、主题保存、关闭行为、播放全屏或自动扫描业务语义。
  - 不改 API 配置 Tab、扫描任务页、播放器、媒体库、发现页或详情页。
- 业务逻辑变化：无。软件缓存仍沿用页面激活时自动加载逻辑。
- 非本阶段页面变化：无。
- 验证结果：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。

7.6b 返工补丁（2026-06-07，通用设置行为开关接入）：

- 完成了什么：
  - `行为设置` 的右侧控件统一改为分段切换：关闭窗口为“退出软件 / 缩小到托盘”，点击播放后自动全屏为“关闭 / 开启”，主题模式为“跟随系统 / 浅色 / 深色”；自动扫描 WebDAV 在当时保持禁用双段展示，后续补丁已接入真实配置。
  - 关闭窗口行为按用户澄清接入真实“退出软件 / 缩小到托盘”语义；缩小到托盘时关闭主窗口会隐藏到系统托盘，托盘菜单可打开主窗口或退出。
  - 点击播放后是否自动全屏接入真实播放器打开偏好；关闭后播放器以普通窗口打开，开启后沿用打开即全屏。
  - 新增 App 层 `app-behavior-preferences.json` 本地偏好文件保存关闭窗口行为和播放全屏偏好；不新增 Core 数据模型字段、不新增 migration。
  - 主题模式改为三段即时切换，支持跟随系统；设置页内主题保存按钮和主题切换按钮均不再显示。
  - `缓存设置`、`行为设置` 第二列显式左对齐，并增加标题行高度、内容行高度和两张卡片上下间隔。
- 验收标准：
  - `行为设置` 不再出现禁用按钮、复选框、主题保存按钮或页面内主题切换按钮。
  - 关闭窗口行为切换后能保存为退出软件或缩小到托盘，不再表达为“是否弹窗”。
  - 点击播放后自动全屏切换后能影响后续播放器打开方式。
  - 主题三段选中态能保存 `System` / `Light` / `Dark`，选择 `System` 时不被资源解析结果覆盖为 Light / Dark。
  - `dotnet build MediaLibrary.sln` 通过。
- 不属于该补丁：
  - 不实现自动扫描 WebDAV 启动调度。
  - 不改变缓存加载、清理、API 保存 / 测试、扫描任务、识别规则或在线字幕搜索逻辑。
  - 不新增数据库字段、migration 或 database update。
- 业务逻辑变化：
  - App 层新增本地偏好服务 `IAppBehaviorPreferencesService` / `AppBehaviorPreferencesService`，保存关闭窗口行为和播放器打开全屏偏好。
  - `MainWindow` 关闭流程读取该偏好：`exit` 正常退出，`tray` 隐藏到托盘；读取失败时回退正常退出。
  - `PlayerWindowService` 打开播放器前读取该偏好，并通过 `PlayerWindow.StartFullscreenOnOpen` 控制是否初始全屏。
  - `ThemeService` 支持 `System` 主题模式并保存原始选择值。
- 非本阶段页面变化：
  - `MainWindow`：关闭按钮行为受通用设置影响，托盘菜单新增打开 / 退出能力。
  - `PlayerWindow`：初始打开是否全屏受通用设置影响；播放控制、字幕、音轨和关闭确认流程不变。
- 验证结果：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。

7.6b 返工补丁（2026-06-07，通用设置列对齐与自动扫描接入）：

- 完成了什么：
  - 缓存设置和行为设置行改为固定第一列 + 弹性第二列，确保两张卡片的第二列文字左侧起点一致。
  - `自动扫描 WebDAV` 合并为同一行标签，并从禁用展示改为真实可点击分段开关。
  - `AutoScanWebDavOnStartup` 写入 App 层 `app-behavior-preferences.json`，默认关闭。
  - 新增 App 层 `IStartupWebDavScanService` / `StartupWebDavScanService`，App 启动后读取偏好；开启时后台调用现有 `IMediaScanService.RunScanAsync` 并发出 `NotifyScanChanged`。
  - 缓存设置、行为设置标题行高度略减小，标题本身向下微调。
  - 关于行下移并改为整行按钮；去掉中间说明文案，只保留软件 icon、关于标题、版本和右侧箭头。
- 验收标准：
  - 缓存设置、行为设置的第二列起点一致。
  - 自动扫描 WebDAV 标签为单行，开关可保存开启 / 关闭状态。
  - 开启自动扫描 WebDAV 后，下次 App 启动会后台触发现有 WebDAV 扫描服务。
  - 关于行整体可点击，行内不显示中间的 XFVerse 说明文案。
  - `dotnet build MediaLibrary.sln` 通过。
- 不属于该补丁：
  - 不新增扫描暂停、进度弹窗或新的扫描识别规则。
  - 不改变扫描任务页手动扫描流程、API 配置、缓存清理、播放器字幕或媒体库业务语义。
  - 不新增 Core 数据模型字段、migration 或 database update。
- 业务逻辑变化：
  - App 层启动逻辑新增后台自动 WebDAV 扫描触发点；仅在本地偏好开启时运行，复用现有 `IMediaScanService.RunScanAsync`。
  - App 行为偏好新增 `AutoScanWebDavOnStartup` 字段；旧偏好文件缺失该字段时默认关闭。
- 非本阶段页面变化：
  - `App.xaml.cs` 启动流程会在主窗口创建后排队执行自动扫描检查。
  - 扫描任务页未修改，但自动扫描完成后会通过 `IDataRefreshService.NotifyScanChanged` 通知刷新。
- 验证结果：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。

7.6b 返工补丁（2026-06-07，通用设置列距与高度细调）：

- 完成了什么：
  - `SettingsPage.xaml` 的通用设置行第一列从较窄固定宽度调整到更宽固定宽度，使缓存设置和行为设置第二列整体大幅右移。
  - 关于行在 `通用` Tab 的固定无滚动布局内继续下移，保持整行可点击、软件 icon、关于标题、版本和右侧箭头结构。
  - 缓存设置、行为设置标题行高度约缩减三分之一，并同步压缩标题下方分隔线偏移。
  - 二次细调中将 `行为设置` 卡片继续下移，并修正关于行定位方式：原 `*` 弹性空白行 + 最后一行 `Auto` 会把关于行钉在可用区域底部，继续加上边距会被弹性行抵消；现在改为底部锚定后的显式视觉位移。仍保持通用页无页面级滚动。
  - 修复设置页在本轮细调中暴露出的可见中文文案乱码；该修复只恢复 UI 文案，不改变绑定字段、命令或保存逻辑。
- 验收标准：
  - 缓存设置和行为设置第二列相对上一版明显右移。
  - 关于行明显下移，仍独立于行为设置卡片且整行可点击。
  - 缓存设置、行为设置标题高度更低，标题和分隔线不挤压内容行。
  - `通用` Tab 不新增页面级滚动；`API 配置` Tab 仍使用现代化滚动。
- 不属于该补丁：
  - 不做人工 UI 运行测试。
  - 不继续调整缓存清理、主题、关闭窗口、播放全屏或自动扫描 WebDAV 行为。
  - 不修改扫描任务、播放器、媒体库、观影发现、详情页或在线字幕页面行为。
  - 不新增 Core schema、migration、database update、commit 或 push。
- 业务逻辑变化：
  - 无。本补丁只是设置页布局和可见文案修复。
- 非本阶段页面变化：
  - 无。
- 验证结果：
  - `dotnet build MediaLibrary.sln -p:OutDir="%TEMP%\\XFVerseCodexBuild\\"` 通过，0 warning / 0 error。
  - 标准 `dotnet build MediaLibrary.sln` 因当前正在运行的 `MediaLibrary.App` 锁定 Debug 输出文件而无法完成覆盖复制；未结束该进程。

### 7.6c - 设置页 API 配置 Tab

目标：

- 对齐 `02-设置-API配置-全屏.png`：TMDB、OMDb、OpenSubtitles、AI 配置卡统一结构。
- OpenSubtitles 配置与 TMDB / OMDb 配置同构：配置状态、字段、保存、测试、测试结果 / 状态反馈。
- 所有 password、token、API key、access token 默认隐藏，使用眼睛图标显示 / 隐藏。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/settings-page.md`
- `DesignDraft/screenshots/账号/设置/02-设置-API配置-全屏.png`
- `docs/online-subtitles/ONLINE_SUBTITLES_PLAN.md`
- `docs/online-subtitles/ONLINE_SUBTITLES_STAGE_LOG.md`
- `src/MediaLibrary.App/Views/Pages/SettingsPage.xaml`
- `src/MediaLibrary.App/Views/Pages/SettingsPage.xaml.cs`
- `src/MediaLibrary.App/ViewModels/Pages/SettingsViewModel.cs`
- `src/MediaLibrary.Core/Services/Interfaces/IOpenSubtitlesClientService.cs`
- `src/MediaLibrary.Core/Models/Settings/ApplicationSettingModel.cs`
- `src/MediaLibrary.Core/Data/Configurations/ApplicationSettingConfiguration.cs`

可复用组件：

- `ApiConfigCard`
- `SensitiveSettingInput`
- `SettingFieldRow`
- `OpenSubtitlesLanguages` 静态语言列表和 probe 结果映射。
- 现有 `SaveTmdbSettingsCommand`、`TestTmdbConnectionCommand`、`SaveOmdbSettingsCommand`、`TestOmdbConnectionCommand`、`SaveOpenSubtitlesSettingsCommand`、`TestOpenSubtitlesConnectionCommand`、`SaveAiSettingsCommand`。

可参考页面 / 组件：

- 现有 TMDB / OMDb 卡片作为 OpenSubtitles 卡片结构参考。
- 在线字幕搜索窗口只作为状态 / 错误语义参考，不复用其深色窗口布局。
- 影片发现偏好弹窗中的表单密度可作为表单间距参考。

已有 UI 规则：

- 保存和测试连接必须是独立操作；保存不得自动触发测试。
- API key / token / password 不在状态、Tooltip、日志和文档样例中回显。
- 在线字幕配置只属于设置页；在线字幕搜索仍属于播放器链路。
- 不把尚未存在的 AI 业务用途作为新配置用途添加。

阶段输出要求：

- 完成了什么：逐卡记录字段、状态、保存 / 测试、敏感字段、长度限制和同构结构改动。
- 验收标准：TMDB / OMDb / OpenSubtitles 各自有独立保存、测试和状态；OpenSubtitles 支持 API-key-only 与可选账号密码；敏感字段默认隐藏且可切换；状态消息脱敏；AI 路由字段不溢出。
- 不属于该阶段：不改播放器字幕菜单、不改在线字幕搜索 / 下载流程、不改 OpenSubtitles API 合约、不新增字幕入口到详情页。
- 业务逻辑变化：默认不改；若修复 token 清理、保存或 probe 边界，必须说明是 bugfix 还是新逻辑，并验证不泄露凭据。
- 非本阶段页面变化：播放器、详情页、在线字幕搜索窗口默认不得修改；如因共享组件影响必须记录。

7.6c 执行结果（2026-06-06）：

- 完成了什么：
  - 将 `SettingsPage.xaml` 的 `API 配置` Tab 改为四张同构 `ApiConfigCard`：TMDB、OMDb、在线字幕、AI。
  - TMDB 卡复用 `SettingFieldRow` 和 `SensitiveSettingInput`，包含 Read Access Token 与 API Key，分别限制为 2048 / 256。
  - OMDb 卡复用 `SettingFieldRow` 和 `SensitiveSettingInput`，API Key 限制为 256。
  - OpenSubtitles 卡与 TMDB / OMDb 同构，包含启用开关、Endpoint、API Key、Username、Password、默认语言、保存、测试和独立状态反馈。
  - OpenSubtitles Endpoint / API Key / Username / Password / 默认语言分别按 512 / 512 / 256 / 2048 / 下拉选择处理；API key-only 模式在说明文案中明确，账号密码仍为可选。
  - AI 卡改为基础连接配置 + 高级路由设置结构，包含 Base URL、API Key、默认模型和当前真实业务用途的模型 / timeout 路由；模型字段限制为 128，timeout 输入限制为 5。
  - 所有 password、token、API key、access token 类字段默认隐藏，统一使用 `SensitiveSettingInput` 的眼睛按钮显示 / 隐藏。
  - `SettingsViewModel` 新增四张 API 卡的脱敏配置状态文本：`TmdbConfigStatusText`、`OmdbConfigStatusText`、`OpenSubtitlesConfigStatusText`、`AiConfigStatusText`。
  - 删除 `SettingsPage.xaml.cs` 中旧的 OpenSubtitles `PasswordBox` 手写同步逻辑，改由 `SensitiveSettingInput` 和 `PasswordBoxBindingHelper` 统一绑定。
  - AI 保存命令收窄为只保存 AI 配置字段和 AI 路由字段，避免点击 AI 保存误保存其它 API 卡未确认的输入。
- 验收标准：
  - `dotnet build MediaLibrary.sln` 通过。
  - `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 返回空。
  - API Tab 卡片随页面整体纵向滚动，不给单个 API 卡新增内部横向滚动。
  - TMDB / OMDb / OpenSubtitles 均有独立保存、测试和反馈文本；保存不会自动触发测试。
  - OpenSubtitles 支持 API-key-only 与可选账号密码的设计表达；测试逻辑继续复用现有 probe 命令。
  - 敏感字段默认隐藏且可切换；状态文本、卡片徽标和说明文案不回显凭据。
  - AI 只展示当前真实业务用途的路由字段，不新增尚未存在的 AI 用途，也不伪造测试命令。
- 不属于该阶段：
  - 不改播放器字幕菜单、在线字幕搜索窗口、下载、绑定或删除绑定流程。
  - 不改 OpenSubtitles 客户端 API 合约、probe/search/download 逻辑。
  - 不新增字幕入口到详情页。
  - 不改扫描任务页、路径选择器或缓存清理业务。
  - 不新增数据模型字段、migration 或 database update。
- 业务逻辑变化：
  - 无 Core 业务逻辑、API 合约、OpenSubtitles 客户端、播放器字幕链路、扫描或缓存清理变化。
  - App 层设置保存行为收口：AI 卡保存只写 AI 字段，不再通过旧私有方法一起保存 TMDB / OMDb 等其它 API 卡字段；这对齐 7.6c 的独立保存要求。
- 非本阶段页面变化：
  - 无。播放器、详情页、在线字幕搜索窗口、扫描任务页和缓存管理业务页面未修改。
- 验证结果：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。
  - `git diff --check` 通过，仅有 LF / CRLF 换行提示。
  - `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 返回空。

7.6c 返工补丁（2026-06-07，API 配置细节对齐）：

- 完成了什么：
  - API 配置 Tab 的 ScrollViewer 增加输入控件区域滚轮转发；鼠标位于 TextBox、PasswordBox 或未展开的 ComboBox 上时，滚轮仍滚动 API 配置页面。
  - `SensitiveSettingInput` 改为 28px 紧凑输入高度，眼睛按钮内置到输入框右侧，输入框宽度扩展到原眼睛按钮位置；输入内容字号和垂直居中规则保持输入控件一致。
  - TMDB 第二个字段标签由 `API Key` 改为 `API Key（可选）`。
  - `ApiConfigCard` 的右上角状态改为小圆角矩形 Badge，并增加呼吸灯；绿色表示测试通过，黄色表示未测试，红色表示测试失败 / 测试前缺少必要 Key。
  - 删除 API 配置字段行下方说明文字，删除默认搜索语言下拉说明，删除 AI 高级路由设置下方说明文字；卡片级说明保留。
  - 默认搜索语言下拉改用现代化 ComboBox 样式，左对齐输入列，显示中文语言名，并将简体中文、繁体中文、中英双语、粤语中文、英语、日语、韩语等常用语言排在前面。
  - `SettingsViewModel` 增加 TMDB / OMDb / OpenSubtitles 的测试状态种类；保存后回到未测试，测试成功置为通过，测试失败或缺少必要 Key 置为失败。AI 仍无独立测试命令，状态保持未测试。
- 验收标准：
  - API 配置 Tab 内容溢出时使用现代化纵向滚动条；鼠标停在输入框或未展开下拉框上滚轮仍可滚动页面。
  - 敏感字段眼睛按钮位于输入框内部，外侧不再额外占列；所有敏感字段默认隐藏并可切换。
  - 输入框高度为紧凑高度，不出现文本垂直裁切；字段不因眼睛按钮导致横向溢出。
  - TMDB 字段显示 `API Key（可选）`。
  - Badge 为小圆角矩形，文本左侧显示呼吸灯；测试通过 / 未测试 / 测试失败分别对应绿 / 黄 / 红。
  - API 字段行、默认语言下拉、AI 高级路由设置下方不再显示说明文字。
  - 默认搜索语言下拉显示中文选项，常用语言位于列表前部，仍保存 `OpenSubtitlesDefaultLanguageCode`。
  - build 通过，migration diff 为空。
- 不属于该阶段：
  - 不新增 AI 测试命令。
  - 不改 OpenSubtitles probe/search/download 合约和播放器字幕流程。
  - 不改通用设置、扫描任务页、缓存管理和媒体库业务语义。
  - 不新增数据模型字段、migration 或 database update。
- 业务逻辑变化：
  - 无 Core 业务逻辑变化；无数据模型变化。
  - App 层仅新增配置测试 UI 状态投影和 OpenSubtitles 语言中文显示 / 排序；保存的语言 code、API 保存、测试调用仍走原服务契约。
- 非本阶段页面变化：
  - 无。仅修改设置页 API 配置区域、API 卡片控件、敏感输入控件和设置页 ViewModel。
- 验证结果：
  - `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild\"` 通过，0 warning / 0 error。
  - `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 返回空。
  - 未执行人工 UI 点击测试；本次用户明确不要求由 Codex 进行 UI 测试。

### 7.6d - 扫描任务配置与路径选择器

目标：

- 对齐 `01-扫描任务-全屏.png` 的扫描配置主结构，同时按 `scan-task-page.md` 明确拆分 WebDAV 与本地目录两个来源。
- WebDAV 配置和本地目录配置保持同构：路径列表、添加 / 编辑、显示名、启用、递归、移除。
- 新增路径选择能力：本地选择按钮打开本地文件管理器 / 文件夹选择器；WebDAV 选择按钮打开基于 WebDAV 目录列表的轻量路径选择器。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/scan-task-page.md`
- `DesignDraft/page-spec/global-dialogs.md`
- `DesignDraft/screenshots/账号/扫描任务/01-扫描任务-全屏.png`
- `src/MediaLibrary.App/Views/Pages/ScanTasksPage.xaml`
- `src/MediaLibrary.App/Views/Pages/ScanTasksPage.xaml.cs`
- `src/MediaLibrary.App/ViewModels/Pages/ScanTasksViewModel.cs`
- `src/MediaLibrary.Core/Services/Interfaces/ISettingsService.cs`
- `src/MediaLibrary.Core/Services/Interfaces/IWebDavService.cs`
- `src/MediaLibrary.Core/Models/ReadModels/RemoteEntry.cs`
- `src/MediaLibrary.Core/Services/Implementations/SettingsService.cs`
- `src/MediaLibrary.Core/Helpers/WebDavPathHelper.cs`

可复用组件：

- `ScanPathCard`
- `SettingFieldRow`
- `SensitiveSettingInput` 用于 WebDAV 密码。
- `IScanPathPickerService` / `ScanPathPickerService`
- `IWebDavService.ListDirectoryAsync`
- `ISettingsService.SaveScanPathAsync` / `SaveLocalScanPathAsync` / toggle / delete。

可参考页面 / 组件：

- 草图中左侧 `已添加路径` 与右侧 `WebDAV 配置` 的并列结构。
- 设置页 API 卡的字段 / 保存 / 测试结构。
- 全局弹窗规格中的删除本地扫描路径轻量 Popover。

已有 UI 规则：

- WebDAV 与本地目录必须独立标题和独立操作。
- WebDAV URL 和凭据不进入历史记录、Tooltip 或错误摘要。
- 本地路径可展示；超长省略且仅在实际省略时 Tooltip。
- 删除本地扫描路径必须说明不删除真实本地文件。

路径选择器要求：

- 本地路径选择器属于 App 层 UI 能力，不写入 Core 业务模型。
- WebDAV 路径选择器优先通过当前连接调用 `IWebDavService.ListDirectoryAsync`。
- 如果 WebDAV 服务只提供路径而非完整目录树，使用返回的 virtual path 还原父子层级；不能把完整远端 URL 暴露到 UI。
- WebDAV 浏览失败时显示脱敏错误，不保存半选状态。
- 选择路径只填充编辑框，不自动保存；保存仍走现有保存命令和校验。

阶段输出要求：

- 完成了什么：记录 WebDAV / 本地配置结构、路径选择按钮、路径选择器、长字段处理和保存校验改动。
- 验收标准：本地选择按钮可选目录；WebDAV 选择器可从根或当前路径浏览目录；选择后只填入路径；保存 / 测试分离；路径列表内部可滚动；长路径不撑破布局；完整 WebDAV URL 不裸露。
- 不属于该阶段：不启动扫描、不改扫描识别算法、不做扫描进度和历史日志视觉收口、不新增暂停扫描。
- 业务逻辑变化：允许新增 App 层 picker service；Core 只在确有必要时新增只读目录浏览 read model，不改扫描写入语义。
- 非本阶段页面变化：默认无；如共享路径选择组件影响其它页面，必须记录。

7.6d 执行结果（2026-06-07）：

- 完成了什么：
  - 将 `ScanTasksPage` 的配置区按 WebDAV / 本地目录两块同构整理；两类路径列表复用 `ScanPathCard`，编辑表单复用 `SettingFieldRow`。
  - WebDAV 配置新增连接名称字段，连接名称、BaseUrl、用户名、密码、路径、显示名分别按 120 / 500 / 200 / 1000 / 1000 / 200 限制；密码改用 `SensitiveSettingInput` 默认隐藏。
  - 新增 App 层 `IScanPathPickerService` / `ScanPathPickerService`，本地目录选择使用系统目录选择器，不进入 Core。
  - 新增 `WebDavPathPickerWindow`，复用 `IWebDavService.ListDirectoryAsync` 浏览目录，只展示虚拟目录名和路径，不展示完整 BaseUrl、RemoteUri、账号或凭据。
  - WebDAV / 本地选择按钮只填充当前编辑框，不自动保存；显示名为空时按选中路径生成安全默认名。
  - 页面整体、WebDAV 列表、本地列表、右侧日志列表和 WebDAV 选择器目录列表接入 `ScrollBarAutoRevealBehavior`，默认禁用横向滚动。
  - 2026-06-08 follow-up：WebDAV 添加路径 / 账号配置卡片收紧底部空白，修正添加路径行文件夹图标，保存配置 / 测试连接按钮按文字自适应宽度；本地路径列表改为固定宽度 WrapPanel，避免少量路径时被拉高。
  - 2026-06-08 second follow-up：WebDAV 添加路径图标改为矢量文件夹；保存配置按钮只在连接字段相对已保存值发生变化时可用；本地多选目录 picker 在 `FolderNames` 为空但 `FolderName` 有值时仍返回当前选择，不检查目录内容。
  - 2026-06-08 second follow-up：扫描进度卡片恢复为标题下方先显示进度条、当前阶段、当前文件，再显示两行统计卡片和右侧操作按钮；统计卡片缩窄并加大列间距，进度条底轨改为灰色系。
- 验收标准：
  - 本地“选择目录”能打开系统目录选择器并回填编辑框。
  - WebDAV“选择路径”在填写 BaseUrl / 凭据后打开目录选择窗口，可从当前路径或根路径浏览。
  - WebDAV 选择器只返回虚拟路径，不保存半选状态；失败显示脱敏错误。
  - 保存后自动执行一次连接测试；测试按钮仍可手动重试；选择路径不会自动保存。
  - 长路径行使用 `ScanPathCard` 的真实截断 Tooltip 规则，完整 WebDAV URL、账号和凭据不进列表、日志或 Tooltip。
  - build 通过，migration diff 为空。
- 不属于该阶段：
  - 不改扫描启动、取消、进度统计、历史日志卡视觉收口。
  - 不新增暂停扫描。
  - 不改扫描识别算法、AI / TMDB / OMDb / OpenSubtitles 调用或 Core 写入语义。
  - 不实现删除本地扫描路径轻量 Popover；保留到 7.6e 与删除语义一起收口。
- 业务逻辑变化：
  - 无 Core 业务逻辑和数据模型变化；无 migration。
  - 新增 App 层 UI picker service 和 WebDAV 目录选择窗口；这是 UI 能力，不改变扫描保存或运行语义。
  - `ScanTasksViewModel` 只新增选择命令和安全默认显示名填充；保存仍走 `ISettingsService.SaveScanPathAsync` / `SaveLocalScanPathAsync`。
  - 2026-06-08 follow-up：`ScanTasksViewModel` 新增 WebDAV 连接测试状态投影，保存成功后自动调用现有 `IWebDavService.TestConnectionAsync`；本地目录选择器未收到显式初始目录时固定回到用户目录 / 文档目录，不沿用系统上次选择。
  - 2026-06-08 second follow-up：保存按钮禁用逻辑仅基于 App 层字段快照，不新增 Core schema；本地空目录是否含文件或视频不在配置阶段校验，扫描阶段按既有逻辑处理。
- 非本阶段页面变化：
  - 无。除 `AppServiceProvider` DI 注册外，未修改 Settings、Player、Library、Discovery、Detail、OnlineSubtitleSearch 等页面。
- 验证结果：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。
  - `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 返回空。

### 7.6e - 扫描运行、进度、历史日志与安全操作

目标：

- WebDAV 和本地扫描分别启动，共享进度区和历史日志区。
- 进度区只展示真实可用统计；识别计数仅在扫描服务稳定产出时显示。
- 历史日志保留旧设计紧凑任务卡结构，但按 7.6 统一现代化滚动和脱敏。
- 删除本地扫描路径使用按钮旁轻量确认 Popover，明确不删除真实本地文件。

执行时必读：

- 全阶段固定必读基线。
- `DesignDraft/page-spec/scan-task-page.md`
- `DesignDraft/page-spec/global-dialogs.md`
- `docs/local-folder/LOCAL_FOLDER_PLAN.md`
- `docs/local-folder/LOCAL_FOLDER_STAGE_LOG.md`
- `src/MediaLibrary.App/Views/Pages/ScanTasksPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/ScanTasksViewModel.cs`
- `src/MediaLibrary.Core/Models/ReadModels/ScanProgressUpdate.cs`
- `src/MediaLibrary.Core/Models/ReadModels/ScanExecutionResult.cs`
- `src/MediaLibrary.Core/Models/ReadModels/ScanTaskLogItem.cs`
- `src/MediaLibrary.Core/Services/Implementations/MediaScanService.cs`
- `src/MediaLibrary.Core/Services/Implementations/LocalMediaScanService.cs`
- `src/MediaLibrary.Core/Services/Implementations/SettingsService.cs`

可复用组件：

- `ScanLogCard`
- `LocalScanPathRemovePopover`
- `ScrollBarAutoRevealBehavior`
- `TrimmedTextToolTipBehavior`
- `CancelScanCommand`、`RunScanCommand`、`RunLocalScanCommand`、`RecentLogs` 合并逻辑。

可参考页面 / 组件：

- 草图右侧 `扫描记录` 卡片密度。
- 现有 `ScanTasksPage.xaml` 的日志卡数据绑定。
- 全局轻量确认 Popover 规则。

已有 UI 规则：

- 不保留 `暂停扫描` 按钮，不把未存在的暂停能力设计为最终 UI。
- 取消扫描只取消软件任务，不删除来源文件。
- 扫描日志不得显示完整 WebDAV URL、账号、密码、token 或 API key。
- 不支持的数据字段不得用固定占位符伪装为有效统计。

阶段输出要求：

- 完成了什么：记录进度区、按钮状态、日志卡、删除路径 Popover、脱敏与长字段改动。
- 验收标准：WebDAV / 本地扫描按钮分别可用且运行中禁用重复触发；取消按钮只在运行中可用；无暂停按钮；当前文件 / 路径单行省略；日志列表内部滚动；WebDAV 日志只显示安全目标摘要；识别计数未证实时隐藏或明确说明不可用。
- 不属于该阶段：不改识别准确率、不新增 AI 识别规则、不改 TMDB / OMDb 请求、不改播放器或详情页。
- 业务逻辑变化：必须明确处理 `DeleteLocalScanPathAsync` 当前标记 `MediaFile.IsDeleted=true` 的语义。如果保留，说明这是软件来源可见性处理且不删除文件；如果调整，说明新旧行为、数据影响和验证。
- 非本阶段页面变化：如本地路径移除导致媒体库可见性变化，必须记录影响和媒体库回归结果；不得顺手重构媒体库页面。

7.6e 执行结果（2026-06-07）：

- 完成了什么：
  - 新增 `ScanLogCard`，将右侧扫描记录从页面内联模板收敛为复用控件，保留来源、状态、开始 / 结束时间、扫描 / 新增 / 更新 / 忽略 / 错误 / 耗时和原因摘要。
  - 新增 `LocalScanPathRemovePopover`，本地目录路径行的“移除”改为按钮旁轻量确认；确认文案明确只移除配置并可能影响软件内记录可见性，不删除真实本地文件。
  - 扫描进度区保留 WebDAV / 本地两个启动按钮和取消按钮；运行中通过现有 `CanRunScan` / `CanRunLocalScan` / `CancelScanCommand` 禁用重复触发并只允许取消。
  - 移除进度区长期显示 `--` 的“已识别 / 未识别”占位指标；当前 `ScanProgressUpdate` / `ScanExecutionResult` / `ScanTaskLogItem` 未提供稳定识别计数字段，7.6e 不伪造该统计。
  - 当前文件行改为单行省略，并使用 `TrimmedTextToolTipBehavior` 仅在真实省略时显示允许内容；Core `ScanProgressReporter` 已将当前项压缩为文件名。
  - 历史日志不再显示 WebDAV 用户字段；日志错误摘要在 App 层对 WebDAV URL、凭据键值和本地路径做安全替换。
- 验收标准：
  - WebDAV / 本地扫描按钮仍分别绑定 `RunScanCommand` / `RunLocalScanCommand`，运行中不可重复触发。
  - 取消按钮仍绑定 `CancelScanCommand`，只在运行中可用；页面没有暂停扫描按钮。
  - 当前文件 / 路径长字段单行省略；日志列表仍在右侧区域内纵向滚动。
  - WebDAV 日志只显示安全目标摘要或显示名，不显示完整 URL、账号、密码、token 或 API key。
  - 本地扫描路径移除必须经过按钮旁 Popover 显式确认，取消、外部关闭或 Esc 不执行移除。
  - build 通过，migration diff 为空。
- 不属于该阶段：
  - 不改识别准确率、扫描匹配阈值、AI 识别规则、TMDB / OMDb 请求或 OpenSubtitles 流程。
  - 不新增暂停扫描、后台调度、文件系统监听或自动扫描。
  - 不改播放器、详情页、媒体库页面结构或缓存清理业务。
  - 不新增数据模型字段、migration 或 database update。
- 业务逻辑变化：
  - 无 Core 代码变化；无数据模型变化。
  - `DeleteLocalScanPathAsync` 保留既有语义：删除本地扫描路径配置，并将该路径关联的本地 `MediaFile` 软件记录标记为 `IsDeleted=true`，用于影响软件内来源可见性；该行为不删除真实本地文件，不删除 WebDAV 文件。
  - App 层新增日志脱敏投影和按钮旁确认控件；扫描运行、取消、保存和路径移除仍走现有服务命令。
- 非本阶段页面变化：
  - 无直接页面改动。媒体库可见性可能受到既有本地路径移除语义影响，但本次未修改媒体库页面或查询逻辑。
- 验证结果：
  - `dotnet build MediaLibrary.sln` 通过，0 warning / 0 error。
  - `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 返回空。

### 7.6f - 回归收口与文档同步

目标：

- 关闭 7.6 的静态 / build / migration / 手工验收矩阵。
- 更新 Phase 7 维护文档，不把实现细节写成长流水账。

执行时必读：

- 全阶段固定必读基线。
- 本文件的全部小阶段输出。
- `DesignDraft/PHASE_7_STAGE_LOG.md`
- `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- `docs/ui-redesign/UI_REDESIGN_STAGE_LOG.md`
- `docs/ui-redesign/UI_REDESIGN_KNOWN_ISSUES.md`

可复用组件：

- 无新增组件；只复核 7.6a-7.6e 的组件和页面结果。

可参考页面 / 组件：

- `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md` 的收口记录格式。
- `docs/player/PHASE_7_5_PLAYER_UI_PLAN.md` 如存在后续收口格式，可按当前仓库实际内容参考。

已有 UI 规则：

- 每个 Phase 7 子阶段必须 build。
- 每次最终报告必须说明 migration diff 是否为空。
- Known Issues 固定分类：Blocker / Deferred / Noise。

阶段输出要求：

- 完成了什么：汇总 7.6 全部小阶段完成项、业务逻辑变化、非本阶段页面变化和文档同步。
- 验收标准：`dotnet build MediaLibrary.sln` 通过；`git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 状态明确；设置页和扫描页人工验收矩阵通过或 Deferred；Known Issues 更新。
- 不属于该阶段：不新增功能、不重构非 7.6 页面、不夹带发布 / 安装策略。
- 业务逻辑变化：只汇总前面小阶段已发生的变化；不新增。
- 非本阶段页面变化：列出所有受共享资源、主题、路径移除语义影响的页面和验证结果。

## 7.6 人工验收矩阵

后续实现完成后，至少执行以下人工验收：

1. 设置页进入后默认 `通用` Tab，Tab 位置和按钮式模板对齐影片发现页；页面内部不显示额外右上角主题按钮，`通用` Tab 不出现页面级纵向滚动。
2. 通用 Tab 只显示海报、metadata / 其它、在线字幕三类缓存，不显示视频缓存管理入口。
3. 在线字幕缓存显示孤立 / 受保护 / 禁用原因，引用关系不明时清理按钮 disabled。
4. 缓存清理确认明确不删除真实本地媒体文件、WebDAV 文件或用户配置。
5. 行为设置中主题模式、关闭窗口行为、点击播放后自动全屏、启动时自动扫描 WebDAV 能保存并生效。
6. API 配置 Tab 中 TMDB / OMDb / OpenSubtitles 卡结构同构，保存和测试独立，Tab 内容区使用现代化纵向滚动条。
7. 所有 token、API key、password 默认隐藏，眼睛按钮切换正常，状态文案不泄露凭据。
8. OpenSubtitles API-key-only 模式可保存并测试；账号密码为空不会阻塞 API key-only。
9. 扫描任务页 WebDAV 与本地目录配置清晰分区，本地配置与 WebDAV 配置结构同构。
10. 本地路径选择按钮打开本地目录选择器；WebDAV 路径选择器只显示安全路径层级。
11. WebDAV / 本地扫描分别启动，共享进度和日志；运行中不可重复触发。
12. 页面不出现暂停扫描按钮，取消扫描只取消任务。
13. 当前扫描文件、路径、日志目标等长字段省略；只有实际省略时 Tooltip 显示允许内容。
14. 历史日志不显示完整 WebDAV URL、账号、密码、token 或 API key。
15. 删除本地扫描路径需要按钮旁轻量 Popover，并明确不删除真实本地文件。
16. 删除本地扫描路径后媒体库可见性 / 来源记录行为符合阶段记录的业务语义。
17. 浅色 / 深色主题下设置页、扫描页、Popover、Tooltip、滚动条均可读。
18. `dotnet build MediaLibrary.sln` 通过，migration diff 状态明确。

## 7.6 Known Issues 初始清单

### Blocker

- 无已确认 Blocker。

### Deferred

- 7.6b 已将关闭窗口行为、播放后自动全屏、启动时自动扫描 WebDAV 接入 App 层本地偏好文件；自动 WebDAV 扫描在 App 启动后后台触发现有 `IMediaScanService.RunScanAsync`，不新增扫描识别规则、Core schema 或 migration。
- WebDAV 路径选择器的目录浏览体验依赖不同 WebDAV 服务对 `PROPFIND Depth: 1` 的支持；异常和权限不足时只做脱敏错误提示。
- AI 配置卡已在 2026-06-07 follow-up 中新增独立测试命令；测试仅使用当前输入发起轻量连接探测，不自动保存输入或 token 变更。

### Noise

- 草图中的颜色仅用于理解区域关系和密度；实际实现继续使用项目 ResourceDictionary 和浅 / 深主题资源。
- 当前设置页 ViewModel 仍保留旧 WebDAV / 扫描路径命令，页面主入口已在扫描任务页；7.6 可清理绑定死角，但不要因此改变扫描业务语义。
- 当前扫描进度和日志模型没有稳定的已识别 / 未识别计数字段；7.6e 已移除 UI 占位，不再显示长期 `--` 指标。

## 建议阶段顺序

推荐按以下顺序执行：

1. `7.6a`：共享组件和规则基线。
2. `7.6b`：设置通用 Tab 和缓存管理。
3. `7.6c`：设置 API 配置 Tab。
4. `7.6d`：扫描配置和路径选择器。
5. `7.6e`：扫描运行、日志和安全操作。
6. `7.6f`：回归收口与文档同步。

不建议先做扫描运行区再做路径选择器；路径配置和脱敏规则是扫描日志、进度和删除确认的基础。
