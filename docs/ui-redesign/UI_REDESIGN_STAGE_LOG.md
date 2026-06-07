# UI 初级重构阶段日志

## UI-0

- 只读审计已完成。
- 确认当前导航、页面模板、DI 注册和重复资源页面引用。

## UI-1

- 删除重复资源页面。
- 主导航改为六个可见项。
- 资源库入口显示名改为媒体库。
- 新增影片发现 / 观影历史最小页面壳。
- 新增个人资料弹窗空壳。
- 扫描任务和设置从主导航移到用户菜单。
- AI 推荐暂时隐藏主导航，后续 UI-4 迁入影片发现。

## UI-2

- 首页已调整为 70% / 30% 两栏结构。
- 左侧为片库预览、继续观看、最近新增。
- 片库预览复用 `IWatchStatisticsService` 的 All 范围状态总览，展示已看 / 喜爱 / 想看 / 不想看。
- 继续观看复用现有最近观看数据源，查看更多跳转观影历史。
- 最近新增复用现有首页入库时间倒序数据源，首页显示数量上限调整为 8。
- 右侧 AI 推荐预览复用现有 `RecommendationsViewModel`，换一批和详情跳转走原推荐逻辑。
- 发现更多影片跳转影片发现页面。
- 未改推荐算法，未新增 DB / migration。

## UI-4

- 影片发现页 AI 推荐 Tab 已承接现有 AI 推荐功能。
- AI 推荐 Tab 复用现有 `RecommendationsViewModel`，通过现有 DataTemplate 渲染 `RecommendationsPage`。
- 影片搜索和榜单继续保持占位。
- `Recommendations` 隐藏路由继续保留。
- 未改推荐算法，未新增 DB / migration。
- 首页接入影片发现已在 UI-2 完成，直接定位 AI 推荐 Tab 留到后续增强。

## UI-3

- 媒体库顶部筛选项已重排为搜索、排序、顺序、范围、标签、年代、收藏状态、识别状态、观看状态和清除筛选。
- 范围筛选已整理为“影片范围 / 影片来源”二级菜单；影片范围接真实筛选，影片来源为占位。
- 标签筛选已整理为固定二维多选菜单；类型、情绪、场景使用固定词表，多个已选标签按全部满足过滤。
- 年代筛选已改为按十年一档生成选项。
- 识别状态文案已改为自动匹配、待人工确认、手动确认、识别失败、未识别。
- 状态栏显示“找到 X 部影片”，右侧提供布局切换占位按钮和批量选择入口。
- 批量已看 / 未看、AI 辅助识别、移出媒体库、删除影片记录和详情跳转保留原命令与业务语义。
- 未新增 DB / migration，未改推荐、播放器、扫描、设置和观影洞察逻辑。

## UI-5

- 观影历史页已从占位壳接入真实观看记录。
- 新增 `WatchHistory` 只读查询，按本地观看日期分组。
- 观影历史页按播放记录流水展示，不套用观影统计的 60 秒有效观看阈值。
- 同一电影同一天多条观看记录只保留最新一条，不同日期分别显示。
- 日期筛选支持全部、今天、本周、本月和指定日期。
- 观影洞察日历点击日期会跳转观影历史，并筛选到对应当天。
- 历史记录显示海报、标题、观看时间、观看时长、播放进度和详情入口。
- 未改播放器、自动已看算法、`WatchHistory` 写入逻辑，未新增 DB / migration。

## UI-6

- 收藏夹页面已改为喜爱 / 想看两个 Tab。
- 移除收藏夹主结构中的全部下拉筛选，不展示不想看 Tab。
- 喜爱 Tab 使用库内 `Movie.IsFavorite` 数据，想看 Tab 使用 `UserMovieCollectionItem.IsWantToWatch` 数据。
- 同一 TMDB ID 去重，库内影片信息优先。
- 取消喜爱和取消想看继续走现有 service，并保留状态历史写入路径。
- 未新增 DB / migration，未改推荐算法和详情页业务逻辑。

## UI-7

- 扫描任务页已重构为 70% / 30% 结构。
- 左侧上方为扫描配置：已添加路径和 WebDAV 配置并列展示。
- 左侧下方为扫描进度：开始扫描、取消扫描、indeterminate 进度条和扫描结果计数。
- 右侧为扫描记录，复用最近 `ScanTaskLogs`，按时间倒序展示。
- WebDAV 配置和扫描路径管理已接入扫描任务页，设置页旧入口已在 UI-8 移除。
- 本阶段只做 WebDAV，不新增本地文件夹扫描。
- 未改扫描识别核心逻辑，未新增 DB / migration。

## UI-8

- 设置页已整理为通用 / API 配置两个 Tab。
- 通用 Tab 包含缓存设置、行为设置和关于卡片。
- 缓存设置复用现有视频缓存设置、刷新占用和清空缓存逻辑。
- 行为设置复用现有主题切换逻辑。
- API 配置 Tab 包含 TMDB、OMDb 和大模型配置。
- WebDAV 配置和扫描路径管理从设置页移除，保留扫描任务页作为主入口。
- 未新增设置项，未改 API 调用逻辑，未新增 DB / migration。

## UI-8 API 配置 Bugfix

- 修复 UI-8 后 API 配置 Tab 中 TMDB / OMDb 保存设置和测试连接按钮被合并的问题。
- TMDB 配置卡片已恢复独立保存设置、测试连接按钮和状态提示。
- OMDb 配置卡片已恢复独立保存设置、测试连接按钮和状态提示。
- 本修复不新增 DB / migration，不改 TMDB / OMDb API 调用逻辑。

## UI-3 媒体库标签筛选菜单微调

### 阶段目标

- 调整媒体库标签筛选二级菜单的标签呈现方式，去掉胶囊视觉，保留现有多选筛选语义。

### 完成内容

- 标签二级菜单项改为纯文本式可点击项，选中态使用文字颜色和字重提示。
- 标签选项按 4 列分行渲染，每行列内文本垂直居中。
- 行分割线只保留在非最后一行，末行底部不再绘制横线。

### 明确未做事项

- 未调整标签词表、筛选条件、筛选语义或按钮文案。
- 未新增数据库字段、migration 或后台查询逻辑。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## Settings API Sensitive Input Icon Follow-up

### Stage Goal

- Correct the API settings sensitive-field reveal button icon semantics.

### Completed

- Replaced the incorrect revealed-state font glyph with explicit vector eye icons.
- Hidden content now shows an open eye icon, and revealed content now shows a closed eye icon.
- Kept the existing reveal/hide binding and tooltip behavior unchanged.

### Explicitly Not Done

- No API save, test, scan, cache, database, or migration behavior was changed.
- No database update, commit, or push was performed.

### Verification

- `dotnet build MediaLibrary.sln`: passed, 0 warnings / 0 errors.
- `git diff --check`: no whitespace errors; existing CRLF conversion warnings only.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`: no output.

### Known Issues

- Blocker: None.
- Deferred: Runtime UI confirmation is still needed to verify the closed-eye icon reads clearly at the final rendered size.
- Noise: Existing CRLF conversion warnings may still appear in git checks.

## 详情页与修正弹窗实测回归修复补充 4

- 季详情 Hero 区恢复原始外层几何，不再增加卡片外边框高度；通过对称减小上下内边距扩展可显示内容区域，并让左侧首行、简介底边和右侧单季信息卡外边框使用同一垂直基线。
- 修正弹窗首次打开时增加即时和延迟鼠标子树捕获，并在捕获成功后同步 WPF 鼠标状态；打开期间显式关闭宿主窗口标题栏命中并在关闭后恢复，清理打开前遗留的标题栏悬停视觉和手势。透明背景命中层继续使用中性箭头光标，不覆盖弹窗内输入框和下拉框自身光标；丢失捕获后的重捕获降到后台优先级，避免与下拉 `Popup` 抢占鼠标捕获。
- 电影详情已有未识别季选择器的旧版半透明遮罩同步改为透明命中层，修正弹窗链路不再绘制有色遮罩。
- 电影、单集和单季修正的 AI 辅助识别增加独立取消令牌；顶部关闭会先取消并脱离旧请求，再清理弹窗状态。AI 加载期间禁用下拉框、输入框和弹窗内取消按钮。
- 电视剧集候选和已有未识别季候选的日历、影片图标统一放入固定 `16px` 容器，并针对 Segoe MDL2 字形视觉基线偏低统一上移 `1px`；导演列继续扩宽，类型列整体右移。
- 单集修正弹窗复用电影修正弹窗现代化 UI，标题改为 `单集修正识别`，播放源下拉框保留单集业务绑定和切换状态。
- 新增单季修正弹窗现代化 UI，标题为 `单季修正识别`；移除播放源文件行，仅保留 `修正为季` 和 `加入到已有未识别季` 两种模式。
- 单季修正两种模式统一使用 `目标集号映射` 子弹窗。子弹窗使用透明阻断层并禁用外层表单键盘交互，保留子弹窗自身输入；列表使用现代化滚动条、对齐内容区左右边界的分隔线行、路径溢出省略与按需悬停提示、目标集号输入框，以及水平平均分布的确认 / 取消按钮。
- `目标集号映射` 子弹窗打开时保存映射快照：确认保留修改，取消和右上角关闭恢复打开前映射，避免未确认内容泄漏到季修正事务。
- 季修正每集只投影一个有效默认播放源；不提供逐集播放源选择。单季 AI 忙状态显式通知交互启用属性，确保加载期间实际禁用单季表单。
- AI 取消令牌继续传递到后续 TMDB 搜索和详情补全，并在候选集合写回前再次检查取消状态，防止旧请求回写已关闭或重新打开的弹窗。
- 未新增 DB / migration，未修改扫描识别阈值、媒体库语义或修正事务边界。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## Settings API Cursor Polish Follow-up

### Stage Goal

- Fix the API settings tab cursor state so the full panel no longer appears clickable.

### Completed

- Set the API settings content `ScrollViewer` cursor to `Arrow`, overriding the hidden tab item's inherited hand cursor.
- Kept button styles unchanged so real actions still use the hand cursor.

### Explicitly Not Done

- No API save, test, scan, cache, database, or migration behavior was changed.
- No database update, commit, or push was performed.

### Verification

- `dotnet build MediaLibrary.sln`: passed, 0 warnings / 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`: no output.

### Known Issues

- Blocker: None.
- Deferred: Runtime UI confirmation is still needed to verify text inputs keep their normal editing cursor and buttons keep the hand cursor.
- Noise: Existing CRLF conversion warnings may still appear in git checks.

## AI 推荐加载与媒体库播放源默认值 Follow-up

### 阶段目标

- 修正 AI 推荐加载动画中静态环与动态弧线尺寸不一致的问题。
- 将媒体库播放源状态默认值改为 `有播放源`，并同步清空筛选和默认状态判定。

### 完成内容

- 共享 `LoadingSpinnerTemplate` 的静态环固定到与动态弧线一致的 `34px` 坐标尺寸，避免 AI 推荐加载时静态圈视觉偏大。
- 媒体库播放源状态新增默认常量，初始进入和清空筛选后均回到 `有播放源`。
- `CanClearFilters` / 默认筛选判定同步改为以 `有播放源` 为默认值；实际查询仍沿用现有 `HasActiveSource` 过滤，不改变媒体库删除、移出或播放源语义。

### 明确未做事项

- 未修改推荐算法、AI 请求、候选生成、扫描识别、播放器播放源选择或媒体库记录语义。
- 未新增数据库字段、migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：未执行运行时人工 UI 验收；建议确认 AI 推荐空结果加载层的环线尺寸，以及媒体库首次进入 / 清空筛选后按钮显示 `播放源：有播放源`。
- Noise：共享 `LoadingSpinnerTemplate` 的尺寸修正会同步影响其他使用该模板的加载层。

## AI 推荐加载 Spinner 对齐 Follow-up

### 阶段目标

- 修正 AI 推荐页空结果加载时橙色动态弧线与灰色静态环中心不重合的问题。

### 完成内容

- `LoadingSpinnerTemplate` 改为外层跟随调用方尺寸、内层固定 `34px` 并居中。
- 灰色静态环和橙色动态弧线现在共用同一个 `34px` 内层坐标系，避免 AI 推荐页 `38px` 调用尺寸下橙色弧线偏向左上。

### 明确未做事项

- 未修改推荐算法、AI 请求、媒体库筛选语义、播放器或扫描识别逻辑。
- 未新增数据库字段、migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：仍建议运行时人工确认 AI 推荐加载层视觉完全重合。
- Noise：共享 Spinner 模板调整会同步影响其他使用该模板的加载控件，但坐标系保持一致。

Known Issues：

- Blocker：静态检查和构建检查未发现阻塞问题。
- Deferred：像素级对齐、弹窗首帧标题栏悬停阻断、AI 请求中关闭后立即重开仍需人工运行时验收。
- Noise：Windows 换行转换提示仍存在。

### Known Issues

#### Blocker

- 无。

#### Deferred

- 标签筛选仍沿用 UI-3 固定词表和全部满足过滤逻辑。

#### Noise

- 本次仅为媒体库标签筛选二级菜单视觉微调，不处理最终 UI 统一阶段中的整体菜单动效。

## UI-9

- 完成 UI-1 到 UI-8 初级 UI 重构静态回归审计。
- 确认主导航只保留首页、媒体库、影片发现、观影历史、收藏夹、观影洞察六个可见入口。
- 确认 MovieDetail、ScanTasks、Settings、Recommendations 作为隐藏路由保留并有 DataTemplate / DI 注册。
- 确认重复资源页面和 `Duplicates` 有效引用已清理。
- 确认首页、媒体库、影片发现、观影历史、收藏夹、观影洞察、扫描任务、设置页关键命令绑定未发现阻塞回归。
- 确认 UI-8 后 TMDB / OMDb 保存设置和测试连接为独立按钮，状态提示为独立字段。
- 整理 `UI_REDESIGN_PLAN.md`，标记 UI-0 到 UI-9 完成。
- 重写 `UI_REDESIGN_KNOWN_ISSUES.md`，按 Blocker、Deferred、Known Limitation、Noise 收口。
- 本阶段未新增功能，未新增 DB / migration，未改推荐算法、播放器、扫描识别核心或 API 调用逻辑。

## 详情页体验回归修复

- 统一输入框占位提示进入 `TextBox` 模板，与实际输入内容复用同一 `Padding`，并在 IME 首次组合输入时立即隐藏。
- 媒体库搜索和修正窗口输入框已接入统一占位提示；修正窗口空输入时恢复提示显示。
- 电影、剧、季、集简介以及修正候选简介的纯文本滚动区已接入半行截断提示；表格和列表滚动区不应用该规则。
- 剧详情 Season 列表、季详情 Episode 列表和集详情信息卡完成针对性间距与对齐调整。
- 未新增 DB / migration，未改媒体库语义或扫描识别规则。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 修正弹窗模态化与详情页闪退修复

- 修正弹窗遮罩改为独立 `Popup`，打开后覆盖整个宿主窗口并拦截下层交互；窗口移动、缩放和状态切换时同步刷新遮罩尺寸。
- 修正弹窗下拉框改为现代化圆角样式，候选列表补充分隔线、层级间距、文本对齐和滚动提示。
- 电影详情页进入即闪退的根因是 `CorrectionDialogComboBoxStyle` 在 `Controls.xaml` 中提前引用尚未合并的 `ComboBoxStyle`。该样式已移动到 `Inputs.xaml`，确保基础样式先加载再继承。
- 未新增 DB / migration，未修改识别、修正事务或媒体库语义。

验证：

- `dotnet build MediaLibrary.sln -p:UseAppHost=false`：通过，运行中的应用实例锁定旧 `.exe`，产生 1 条删除 apphost 警告。
- STA 运行时实例化：`MovieDetailPage`、`SeriesOverviewPage`、`TvSeasonDetailPage`、`EpisodeDetailPage` 均通过。
- STA 模态遮罩验证：`CorrectionDialogShell` 打开后覆盖宿主窗口，通过。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 详情页 UI 实测回归修复

- 撤回修正弹窗的独立 `Popup` 遮罩实现，恢复为详情页内稳定遮罩。修正窗口继续拦截当前详情页下层交互，不再创建容易错位的额外窗口层。
- 半行截断提示行为在布局更新后持续同步，避免剧、季详情简介首次显示时尚未拿到稳定可滚动高度。
- 单季信息、剧信息、单集信息和首页 AI 推荐简介补齐半行截断提示接入。
- 剧详情 Season 行海报使用圆角遮罩裁剪实际图片和占位图，左侧扩展阴影安全区但保持海报本体与 `Season` 标题左边缘对齐。
- 集详情播出日期行改为底边对齐布局；日历图标在与日期同高的容器中垂直居中，图标和日期颜色与剧名一致。
- 未新增 DB / migration，未改业务语义。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## Settings API Config Polish Follow-up

### 阶段目标

- 收口 API 配置页的输入密度、状态提示、保存/测试交互和大模型路由测试能力。

### 完成内容

- API 配置输入框字体缩小，`ApiConfigCard` 标题和右上状态呼吸灯放大，状态圆点颜色加深。
- 在线字幕配置移除启用开关 UI，设置模型与保存/读取路径固定视为启用；右上状态只展示配置/缺少 API Key/测试结果，不再展示启用或停用状态。
- 大模型高级路由移除原“模型 / 超时”表头行，每行改为输入框左侧内联显示“模型”和“超时（s）”，并将模型输入框缩短为固定宽度。
- 大模型配置新增测试按钮，会从默认模型和全部高级路由模型中提取去重模型，并逐个发送轻量连接探测；任一失败会同步右上状态为失败。
- API 配置页的 TMDB、OMDb、OpenSubtitles 和大模型保存按钮改为仅在输入相对已保存值发生变化时可用。
- API 配置保存成功后自动执行对应测试；单独点击测试不会保存设置或 token 变更。

### 明确未做事项

- 未修改 TMDB、OMDb、OpenSubtitles 或 AI 生产调用语义。
- 未新增数据库字段、migration、database update、commit 或 push。
- 未扩展到扫描任务页或非 API 配置表单的保存按钮。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：仍建议进行运行时人工 UI 验收，确认浅色/深色主题下状态灯、按钮禁用态和高级路由布局与草图密度一致。
- Noise：大模型测试会对每个去重模型发送一次轻量请求，实际可用性仍取决于用户配置的服务端模型权限和网络状态。

## Settings API And General Tab Polish Follow-up

### 阶段目标

- 微调 API 配置高级路由区视觉密度，并减少通用设置首次打开时字段占位值到真实值的闪烁感。

### 完成内容

- 高级路由设置标题字号调大、字重加重，并整体下移 2px。
- 高级路由模型输入列由半宽进一步缩短为约三分之一宽度。
- 通用设置 Tab 首次激活时在应用设置、行为偏好和缓存状态加载完成前保持内容 Hidden；加载完成或加载失败需要展示错误时再显示，避免默认字段先渲染后刷新。

### 明确未做事项

- 未修改 API 测试请求、保存语义、缓存清理语义、扫描逻辑或数据库结构。
- 未新增 migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。

### Known Issues

- Blocker：无。
- Deferred：仍需运行时人工确认首次打开设置页时通用 Tab 不再出现字段闪烁。
- Noise：通用 Tab 首次加载期间会短暂隐藏内容，等待缓存状态加载完成后一次性显示。
- 最终补丁未执行应用运行时 UI 验收；早期遮罩诊断入口已停止并清理。

## 详情页 UI 实测回归修复补充

- 半行截断提示改为按文本行高对齐到半行边界；剧、季详情简介在静止状态下也会保留明确的半行截断提示。
- 单季信息继续接入半行截断提示；该规则只应用于纯文本滚动区域，不扩展到表格或列表。
- 修正弹窗外层遮罩改为透明命中层：继续禁止弹窗外按钮交互，不再绘制有色背景。
- 输入框提示层与实际输入内容复用同一个内边距容器，提示文字字首与输入光标起点对齐；IME 组合输入开始时仍立即隐藏提示。
- 修正弹窗下拉框的点击层改为透明模板，避免悬停时出现系统默认蓝色矩形。
- 电影修正结果海报补充真实圆角裁剪；三类修正结果列表统一顶部、底部分隔线和右移滚动条布局。
- 修正弹窗切换播放源时只更新当前来源和预览上下文，不清空电影、电视剧集或已有未识别季结果，不切换当前修正模式。
- 未新增 DB / migration，未修改媒体库语义、扫描识别规则或修正事务。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 详情页与修正弹窗实测回归修复补充 3

- 季详情 Hero 区改为按完整信息行增量同步加高：右侧单季信息卡和左侧简介区同时增加 `20px`，整体向上移动 `10px`；左侧标题、评分与简介保持内部结构不变，简介继续保留半行滚动提示。
- 修正弹窗打开期间增加鼠标子树捕获，并继续在宿主窗口级拦截弹窗外鼠标移动、按下、抬起和触摸输入；标题栏按钮不再响应悬停、手势变化或点击，弹窗背景仍保持透明。
- 修正弹窗下拉框和选项补充手型光标；电影详情播放源下拉框使用显式模板显示当前播放源文本，不再回退显示对象类型名。
- 电影修正候选海报补充左侧阴影安全区：列表向左扩展并等量内缩海报内容，保持海报本体原位置不变。质感优化策略统一要求海报四周预留阴影或发光空间，扩展安全区时不得移动海报本体的既有对齐基线。
- 电视剧集修正候选的日历和影片图标统一进入固定 `16px` 容器，在保留文字底边对齐的前提下进行视觉居中；导演列和类型列扩宽，使国家/地区与语言继续右移。
- `修正到剧`、`修正到季`、`选择该季` 候选按钮统一为 `36px` 高度；展开季行右侧增加箭头槽位补偿，使 `修正到剧` 与 `修正到季` 的右边缘对齐。
- 未新增 DB / migration，未修改识别、修正事务或媒体库语义。

## 详情页与修正弹窗实测回归修复补充 2

- 季详情单季信息卡不再应用半行截断；上方 Hero 区增加一行高度并整体向上移动半个增量，左侧简介同步增高且继续保留半行截断提示。
- 修正弹窗打开期间在宿主窗口级拦截弹窗外鼠标和触摸输入；透明遮罩继续无色显示，同时阻断标题栏按钮和拖动区域交互。
- 输入框提示文字改为读取实际首个光标矩形后校准水平起点，不再依赖固定内边距推断。
- 修正弹窗下拉框点击后不再改变外框颜色；切换播放源继续保留搜索结果、当前修正模式和原状态文本。
- 修正弹窗搜索按钮明确覆盖继承的最小高度，固定为 `36px`；电影候选海报补充调色阴影、圆角遮罩和高光层。
- 修正结果列表改为逐行绘制上下分隔线；列表始终预留透明滚动条槽位，避免剧分组和未识别季分组展开前后箭头、按钮水平位移。
- 剧集修正与已有未识别季分组图标改为固定高度容器内垂直居中；分组按钮和季按钮复用同一条右侧基线。
- 未新增 DB / migration，未修改媒体库语义、扫描识别规则或修正事务。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。
