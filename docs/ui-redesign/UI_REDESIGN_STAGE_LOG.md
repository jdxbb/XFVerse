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
