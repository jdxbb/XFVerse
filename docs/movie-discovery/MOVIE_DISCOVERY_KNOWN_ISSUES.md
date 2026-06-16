# 影片发现 Known Issues

## 2026-06-16 Ranking Medal Size And Number Alignment Polish

Blocker:

- None identified by static patch review.

Deferred:

- Verify the normal ranking medal is visually half the previous size and sits closer to the poster top-left corner.
- Verify the first-place hero medal is visually half the previous size while remaining proportionally larger than normal medals.
- Verify rank digits use the modern sans-serif appearance and no longer look like the old serif digits.
- Verify rank digits are centered on the medal face before the overall medal is positioned on the poster.
- Verify one-digit and multi-digit ranks remain readable on gold, silver, bronze, and default medal images.

Noise:

- The medal PNGs may include transparent padding; final perceived centering should be checked in the running WPF window.
- Existing unrelated workspace edits remain preserved.

## 2026-06-16 Ranking And AI Tag Overflow Parity

Blocker:

- None identified by solution build.

Deferred:

- Verify Movie and TV ranking hero rows keep the date visible before the poster-style tag groups.
- Verify Movie and TV normal ranking rows keep the date visible before the poster-style tag groups.
- Verify long type, emotion, and scene tags truncate with the same `..` overflow behavior used by media-library poster cards.
- Verify ranking tag separator color matches the Discovery poster-card separator in light and dark themes.
- Verify AI recommendation cards truncate type, emotion, and scene tag groups like media-library poster cards after recommendation metadata is hydrated.

Noise:

- This change is display-only for ranking and AI recommendation tags; ranking order and recommendation generation remain unchanged.
- Existing unrelated workspace edits remain preserved.

## 2026-06-16 Ranking Medal Badge Follow-up

Blocker:

- None identified by solution build.

Deferred:

- Verify gold, silver, bronze, and default medal assets load on Movie and TV ranking posters.
- Verify normal and hero ranking posters use consistent top-left medal placement relative to the poster.
- Verify the number is centered consistently on the medal face for one-digit and multi-digit rankings.
- Verify rank-specific number colors, translucent outline, shadow, and highlight remain readable over each medal asset.

Noise:

- Medal digits are rendered by `RankMedalBadge` at runtime rather than baked into the PNG assets.
- Existing unrelated workspace edits remain preserved.

## 2026-06-06 7.4e Discovery 回归收口

Blocker:

- None after build validation and migration diff verification.

Deferred:

- 仍需实际窗口人工确认 Discovery 搜索 / 榜单 / AI 推荐三个 Tab 的交互、卡片文案、偏好弹窗打开 / 编辑 / 清空 / 取消 / 确认流程和 Home 入口跳转手感。
- Exact media-library visual parity for Discovery search controls remains deferred per current stage boundary.

Noise:

- TV 搜索卡片想看季标签 `当前想看` 已由用户确认为后期语义更新，7.4e 按通过记录，不需要修改代码。
- 本轮为 7.4e 回归收口和文档更新，不改推荐算法、prompt、fingerprint、TV 推荐、扫描、播放器或数据库 schema。
- 7.4b follow-up 曾按授权新增 `AddTvSeriesRatingSources` migration；7.4e 本轮 `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。
- `dotnet build MediaLibrary.sln` 已通过，0 warnings / 0 errors。

## 2026-06-06 榜单 Tab 悬停下拉延时

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认鼠标快速划过榜单 Tab 时不会弹出下拉菜单。
- 仍需实际窗口人工确认停留约 420ms 后下拉菜单能正常出现。
- 仍需实际窗口人工确认点击榜单 Tab 的即时打开 / 关闭行为未受影响。

Noise:

- 本轮只调整榜单 Tab hover 触发时机，不改菜单项、榜单数据源、排序、评分计算、AI 推荐或数据库 schema。
- `dotnet build MediaLibrary.sln` 已通过，0 warnings / 0 errors。

## 2026-06-05 榜单首次加载兜底与双列行距 28px

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认首次进入榜单时加载转圈会立即出现。
- 仍需实际窗口人工确认首次进入榜单不会停在无数据静止状态，返回榜单时空闲无数据兜底能重新拉起加载。
- 仍需实际窗口人工确认 Movie / TV 普通双列行 28px 间距符合预期。

Noise:

- 本轮只调整榜单激活兜底、重置加载时序和普通双列行底部间距；不改变榜单数据源、排序、评分计算、AI 推荐或数据库 schema。
- `git diff --check` 仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` 已通过，0 warnings / 0 errors。

## 2026-06-05 榜单双列行间距微调

Blocker:

- None after temp-output build validation.

Deferred:

- 仍需实际窗口人工确认 Movie 双列普通行之间的上下间隔是否符合预期。
- 仍需实际窗口人工确认 TV 双列普通行之间的上下间隔是否符合预期。
- 仍需实际窗口人工确认行距增加后分页位置和滚动手感是否仍自然。

Noise:

- 本轮只把双列普通行 `Grid` 底部 margin 从 14px 改为 24px；不改第一名区域、左右列间距、海报尺寸或右侧信息模板。
- 标准 solution build 和临时输出目录 App 项目 build 均已通过。
- 本轮不改变榜单数据源、排序、评分计算、AI 推荐或数据库 schema。

## 2026-06-05 榜单简介上方留白微调

Blocker:

- None after temp-output build validation.

Deferred:

- 仍需实际窗口人工确认导演 / 演员行与简介之间是否多出一行符合预期。
- 仍需实际窗口人工确认第一名简介减少一行可视高度后，滚动和可读性是否自然。
- 仍需实际窗口人工确认普通双列项简介减少一行可视高度后，整体信息密度是否合适。

Noise:

- 标准 `dotnet build MediaLibrary.sln` 被正在运行的 `MediaLibrary.App (16200)` 锁定默认输出；临时输出目录的 App 项目 build 已通过。
- 本轮只改简介区域上边距，不改变简介文本内容、滚轮交接逻辑、海报、标题、日期、标签、导演 / 演员长度上限或榜单数据。
- 第一名按 20px 简介行高增加间距；普通项按 18px 简介行高增加间距。
- 本轮不改变榜单数据源、排序、评分计算、AI 推荐或数据库 schema。

## 2026-06-05 榜单滚动视口左侧安全区补偿

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认第一列左侧海报阴影是否已经完整显示，不再被榜单滚动视口裁剪。
- 仍需实际窗口人工确认 `ScrollViewer -84` / 内容 `+84` 的补偿没有造成海报、标题、简介、双列分隔线、分页或滚动条可见位移。
- 仍需实际窗口人工确认左侧阴影扩出内容卡边界后的视觉是否符合预期。

Noise:

- 本轮没有再调整海报模板尺寸、列宽、普通行 margin 或第一名 margin；只扩展榜单滚动视口左边界，并用内部根 Grid 等量补偿内容坐标。
- 84px 数值来自媒体库海报 shadow padding；它作用于榜单 Movie / TV 两个 ScrollViewer。
- 本轮只改榜单可视区域，不改变榜单数据源、排序、评分计算、AI 推荐或数据库 schema。

## 2026-06-05 榜单海报阴影安全留白对齐媒体库

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认榜单普通双列左侧第一列海报的左侧发光阴影是否完整显示。
- 仍需实际窗口人工确认第一名海报在 84px 安全区下是否没有被外层视口裁剪。
- 仍需实际窗口人工确认 84px 阴影安全区没有影响榜单海报本体、标题区、简介区或双列分隔线位置。

Noise:

- 媒体库普通海报使用 `348x438` / `Canvas.Left=-84` / `Canvas.Top=-84` / `Padding=84`；榜单普通海报本轮按该值对齐。
- 第一名海报尺寸为 `216x324`，因此 shadow host 使用 `384x492`，仍然是每边 84px 安全区。
- 本轮只扩大海报阴影位图安全区，不改变榜单数据源、排序、评分计算、AI 推荐或数据库 schema。

## 2026-06-05 榜单右上标签与海报阴影可视区修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认 TV 榜单普通项和第一名海报右上角 `当前想看` 标签已完全删除。
- 仍需实际窗口人工确认 Movie / TV 榜单海报左侧发光阴影是否完整显示，尤其是第一名和普通双列左侧第一列。
- 仍需实际窗口人工确认滚动条向右移动后，没有改变榜单内容、分页、第一名区域或双列区域的位置。

Noise:

- 本轮保留搜索页和非榜单列表里的 TV `当前想看` 展示，只删除榜单海报模板内的状态标签。
- 本轮通过关闭榜单卡片裁剪和恢复 shadow canvas 偏移扩大阴影可视区，不调整海报本体坐标、海报列宽或第一名左右 margin。
- 本轮只改榜单展示和滚动条视觉，不改变榜单数据源、排序、评分计算、AI 推荐或数据库 schema。

## 2026-06-05 榜单下拉阴影与第一名留白再修

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认榜单下拉主菜单和二级菜单外侧是否不再出现直角黑色阴影。
- 仍需实际窗口人工确认第一名左右 36px 留白是否让海报左边缘与简介右边缘到背景边框距离一致。
- 仍需实际窗口人工确认第一名右侧文字宽度缩短后，标题、原名和简介截断 / 滚动是否自然。
- 仍需实际窗口人工确认导演 210px / 105px 上限与演员剩余宽度是否达到 3:7 的主观效果。

Noise:

- 本轮移除的是影片发现页 Discovery 下拉菜单自身 `ShadowPopup`，不调整全局 ContextMenu 或其他页面菜单阴影。
- 第一名留白只作用于榜单顶部第一名区域；普通双列项没有跟随增加左右 margin。
- 本轮只改榜单展示和菜单视觉，不改变榜单数据源、排序、评分计算、AI 推荐或数据库 schema。

## 2026-06-05 榜单标题行与第一名留白修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认标题、竖线、原名是否在评分标签高度内垂直居中，且标题左端是否和下面三行左端一致。
- 仍需实际窗口人工确认评分标签在原名右侧的固定位置是否符合主观预期，长标题 / 长原名时是否截断自然。
- 仍需实际窗口人工确认竖线改为主文本色后是否足够“黑”，且没有被标题最大宽度再次推偏。
- 仍需实际窗口人工确认第一名区域左右 18px 留白是否让海报左边缘与简介右边缘到背景边框距离一致。
- 仍需实际窗口人工确认导演 3.5、演员 6.5 的长度上限在长文本时是否合适。

Noise:

- 本轮未新增 ViewModel 字段；标题 / 原名跟随位置通过 XAML Auto + MaxWidth + 剩余列实现。
- 第一名区域留白只作用于第一名 Movie / TV 顶部区域，不改变下方普通双列项的列宽。
- 本轮只改榜单展示模板，不改变榜单数据源、排序、评分计算、搜索、AI 推荐或数据库 schema。

## 2026-06-05 榜单第一名与普通项层级微调

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认第一名 216x324 海报相对普通 180x270 海报是否正好体现 20% 层级差。
- 仍需实际窗口人工确认普通双列项整体字号下调后，标题、原名、评分、简介和 meta 信息是否仍清晰可读。
- 仍需实际窗口人工确认标题 / 原名之间 2px 竖线是否真正居中于文本组，且两侧约两个空格的视觉间距是否合适。
- 仍需实际窗口人工确认导演最大宽度和演员紧随间距在长导演 / 长演员数据下是否符合预期。
- 仍需实际窗口人工确认 `+ 想看` 更窄、`取消想看` 仅增高后的按钮宽高是否稳定。

Noise:

- 第一名区域保留更大的标题 / 评分层级；本轮普通双列项字号缩小不套用到第一名标题层级。
- 榜单 TV 右上角 `当前想看` 标签未改为按钮，本轮只调整 Movie 的 `+ 想看` / `取消想看` 榜单按钮样式。
- 本轮只改榜单展示模板和样式，不改变榜单数据源、排序、评分计算、搜索、AI 推荐或数据库 schema。

## 2026-06-05 榜单视觉二次微调

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认 180x270 榜单海报是否足够小，同时不影响排名标签和右上角状态标签可读性。
- 仍需实际窗口人工确认去掉外层大圆角玻璃背景后，双列内容在不同主题下是否仍然清晰。
- 仍需实际窗口人工确认原名与片名 / 剧名同字体后，第一行长标题截断和竖线分隔是否符合预期。
- 仍需实际窗口人工确认第二 / 第三行统一 13.5px `BrushForegroundSecondary` 后，日期、标签、导演、演员是否达到“大一点点、黑一点”的主观目标。

Noise:

- 海报本体仍保留圆角、阴影、占位模板和右上状态标签；本轮去掉的是海报 + 右侧信息区背后的外层大圆角玻璃背景。
- 标签分隔符颜色在榜单四行模板内同步为 meta 文本色；搜索海报、媒体库海报和列表模板未跟随改变。
- 本轮只改榜单视觉密度和静态文案恢复，不改变榜单数据源、排序、评分计算、AI 推荐或数据库 schema。

## 2026-06-05 榜单海报与四行信息布局反馈修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认 Movie / TV 榜单海报是否与详情页海报视觉一致，且右上角状态标签是否与影片搜索海报对应状态一致。
- 仍需实际窗口人工确认第一名评分标签、普通榜单评分标签、标题 / 原名基线、竖线居中和四行间距是否符合主观预期。
- 仍需实际窗口人工确认简介内部滚动与外层榜单滚动交接是否与影片识别修正弹窗“修正为电影”简介一致。

Noise:

- 榜单海报左上角排名标签本轮按反馈保留，不参与“完全复制详情页海报”的对齐判断。
- 第三行导演 / 演员只有溢出截断时才显示 tooltip；完整显示的文本不额外启用悬停机制。
- 本轮只改榜单展示模板和简介滚轮处理，不改变榜单数据源、排序、评分计算、搜索、AI 推荐或数据库 schema。

## 2026-06-05 榜单二次测试反馈修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认榜单加载动画、分页底部位置、Tab 菜单展开时机和二级菜单文字 / 箭头间距是否符合主观预期。
- 榜单分页已移动到各自滚动内容末尾；仍需实际窗口确认内容不足 / 内容超过一屏时是否与影片搜索列表分页一致。

Noise:

- 空关键词切换 `搜索方式` 只更新按钮文案、placeholder 和空态提示；不会重新请求 Movie 或 TV 搜索结果。
- 从非榜单 Tab 点击 `榜单` 会先切换页面，再通过同一个榜单 Tab 打开下拉菜单；非榜单页仅悬停不打开。
- TV 空关键词 discover 依赖 TV 自己的 `SelectedTvSortOption`；排序菜单必须按当前媒介切换选项和写入目标。

## 2026-06-05 搜索海报标题日期对齐根因修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认 Movie / TV 搜索海报标题、日期和标签左边缘是否与左上角类型标签外边框对齐。
- 仍需实际窗口人工确认评分 chip 右边缘是否与右上角 `+ 想看` / `取消想看` / `当前想看` 标签外边框对齐。

Noise:

- 搜索海报不能继续照抄媒体库底部 `StackPanel`；搜索卡片多了右侧评分 chip，本轮改为整卡三列 `Grid` 来固定左右边缘基准。
- 本轮只改 Movie / TV 搜索海报卡片布局，不改变搜索结果列表、媒体库海报、TMDB 请求或评分计算口径。

## 2026-06-05 榜单与空关键词 Discover 测试反馈修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认榜单 Tab 悬停 / 点击下拉菜单在不同缩放和侧栏状态下的位置、二级 / 三级菜单展开方向是否与媒体库标签菜单主观一致。
- TMDB discover 只能直接表达部分筛选条件；`其它`、多年代组合、入库状态、播放源、观看状态和收藏状态继续在本地结果池中过滤。

Noise:

- 空关键词搜索仅在排序不是 `相关度` 时调用 TMDB `/discover/movie` 或 `/discover/tv`；切回 `相关度` 会清空 discover 结果并回到关键词提示。
- Movie `片名` discover 排序使用 TMDB `original_title`，TV `剧名` discover 排序使用 TMDB `name`；显示层仍沿用现有本地化标题投影。
- 本轮移除榜单顶部筛选卡片，榜单类型选择入口迁移到顶层 `榜单` Tab 下拉菜单。

## 2026-06-05 搜索卡片与列表二次测试反馈修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认海报卡片标题 / 日期 / 标签左边距与左上角类型标签左边框的主观对齐效果。
- 仍需实际窗口人工确认 20px 高度的想看按钮、当前想看标签和评分 chip 在不同字体渲染环境下是否与类型标签等高。
- 仍需实际窗口人工确认搜索列表补入导演 / 演员后，在侧栏展开 / 收起状态下标题截断、导演演员截断和标签行间距是否与媒体库一致。

Noise:

- 搜索结果 Movie 标签展示限定为 TMDB 类型标签；本地 AI 类型、情绪和场景标签仍可在媒体库 / 详情 / AI 推荐相关入口使用，但不参与影片搜索卡片和列表展示。
- Movie 媒体库列表评分改为 TMDB/IMDb 加权展示后，单源评分仍显示单源值，双源评分才显示 `TMDB/IMDb` 加权值。
- TV 搜索结果仍不请求 OMDb / IMDb 评分；无评分同样以 `--` 占位。

## 2026-06-05 工具栏、结果区域与想看摘要测试反馈修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认影片搜索第一行左侧搜索组和右侧四按钮组在侧栏展开 / 收起状态下的主观间距。
- 仍需实际窗口人工确认 Discovery 海报结果使用 194x288 槽位和安全留白后，左右边距、行尾余量和阴影裁切是否与媒体库视觉一致。
- 仍需实际窗口人工确认媒体库与影片搜索布局切换组件的小圆角选中态是否符合预期。

Noise:

- Movie 取消想看后的摘要刷新以状态解析器返回结果为准；解析器仍返回主动入库状态时不会因为取消想看而强制出库。
- 本轮只调整 Discovery 和媒体库的局部 XAML / ViewModel 状态投影，不改数据库、TMDB、推荐、扫描或播放语义。

## 2026-06-05 搜索结果卡片测试反馈修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认搜索海报结果与媒体库海报卡片在阴影、裁剪、渐变、字号和标签间距上的主观一致性。
- 仍需实际窗口人工确认搜索列表结果与媒体库列表在行高、评分块、标题截断和右侧标签区上的主观一致性。

Noise:

- TV 搜索结果未新增 series 级 `+ 想看` / `取消想看` 操作；右上角和列表右侧仍只展示已有“存在想看季”的 `当前想看` 标签。
- 本轮按视觉反馈移除了搜索结果卡片 / 列表上的直接加入媒体库按钮；未改变详情页或服务层入库语义。

## 2026-06-05 搜索工具栏三次测试反馈修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认第一行输入框缩短为 544 / 440 后，顺序图标 / 排序 / 影视 / 搜索方式 / 清除筛选在展开和收起侧栏状态下的主观间距是否满意。
- 仍需实际窗口人工确认第二行从 `类型` 到 `播放源` 的按钮文本截断是否符合预期。

Noise:

- `搜索方式` 仅移动到第一行复用原有菜单和绑定；本轮不改变按片名 / 人物搜索语义，也不改变 Movie / TV 的筛选结果。

## 2026-06-05 搜索工具栏二次测试反馈修复

Blocker:

- None after build validation.

Deferred:

- 仍需实际窗口人工确认第一行加入 `影视` 后，排序 / 影视 / 清除筛选在展开和收起侧栏状态下的主观间距是否满意。
- 仍需实际窗口人工确认第二行从 `搜索方式` 到 `播放源` 的按钮文本截断是否符合预期。

Noise:

- 第二行不再包含 `影视`，但 Movie / TV 仍分别渲染一套筛选网格，以保留已有 Movie / TV 专属类型菜单和筛选绑定。

## 2026-06-05 搜索工具栏测试反馈修复

Blocker:

- None after build validation.

Deferred:

- 仍需用户在实际软件窗口中确认第二行 10 个筛选按钮在不同窗口宽度下的主观间距和文本截断是否满意。
- 榜单分页按钮仍沿用 7.4c 工具栏按钮样式；本轮只按反馈修改影片搜索结果区域上一页 / 下一页。

Noise:

- `入库状态` 使用可见入库状态过滤；`播放源` 继续使用是否有可用播放源过滤。隐藏项会按未入库处理，但不会删除 metadata、播放源或用户状态。
- 搜索摘要统计基于当前过滤后的结果池，不代表 TMDB 全量库的最终总数。

## 2026-06-05 Tab 对齐回归

Blocker:

- None after build validation.

Deferred:

- 仍需用户在实际软件窗口中人工确认顶层 Tab 位置是否符合主观视觉预期；自动验证已覆盖固定宽度和像素中心，但不替代最终肉眼验收。

Noise:

- 可见 Tab 头现在是 `MovieDiscoveryPage` 本地手工按钮条，原 `TabItem` header 仅作为隐藏 ItemsHost 容器参与内容选择；这是为规避 WPF `TabPanel` / header 裁剪与测量偏差，暂不抽为全局 Tab 组件。

## Phase 7.4c 榜单视觉一致性

Blocker:

- None after build validation.

Deferred:

- AI 推荐 Tab 和推荐偏好弹窗仍需按 7.4d 做完整视觉收口。
- 搜索 / 榜单滚动 offset 目前按媒介和布局保存，不按关键词、筛选组合或榜单类型保存；新搜索、筛选重建、翻页、榜单重载会清零 offset，避免旧内容位置污染新内容。

Noise:

- 榜单头部、重点卡、普通行、分页和榜单专用样式仍是 `MovieDiscoveryPage` 本地资源，未抽为全局组件，避免影响媒体库、Home 或 Recommendation 页面。
- `SelectRankingMediaTypeCommand` 只是榜单头部按钮菜单的 UI 绑定补齐，最终仍写入既有 `SelectedRankingMediaType`。
- TV 榜单加入媒体库 / 恢复按钮复用已有 TV metadata hydration 和 Season collection 服务；不创建播放源，也不改变 TV 的 AI / Watch Insights 排除边界。

## Phase 7.4b 媒体库一致性修复

Blocker:

- None after build validation.

Deferred:

- 榜单 Tab 仍需按 7.4c 做完整视觉一致性收口；本轮只补入榜单内容区滚动位置恢复。
- AI 推荐 Tab 和推荐偏好弹窗仍需按 7.4d 做完整视觉收口。
- 搜索 / 榜单滚动 offset 目前按媒介和布局保存，不按关键词、筛选组合或榜单类型保存；新搜索、筛选重建、翻页、榜单重载会清零 offset，避免旧内容位置污染新内容。

Noise:

- Discovery 的媒体库式菜单、Tab、工具栏网格、结果卡片首行和滚动条自动显隐样式目前仍是 `MovieDiscoveryPage` 本地资源，未抽为全局组件，避免影响媒体库以外页面。
- TV 观看状态是按 Series 下 Episode 聚合的 Discovery 展示投影；它不创建 `WatchHistory`、不创建播放源，也不改变 TV 的 AI / Watch Insights 排除边界。

## Phase 4.13b-fix No-source Movie Semantics

- External no-source Movie candidates reuse the previous not-in-library detail add-to-library write semantics; media-library projection now shows visible no-source external rows, and no-source Movie detail hides correction because there is no source to correct.

Blocker:

- None after build validation.

Deferred:

- No-source TV / Season / Episode semantics are not broadly redesigned in this Movie-focused fix.
- Batch correction, manual grouping, grouped Season correction, and historical wrong-binding cleanup remain later Phase 4.13 work.

Noise:

- Recognized Movies with local metadata but zero active sources are now visible as `暂无播放源` unless explicitly hidden.
- External TMDB candidates also open the unified no-source Movie detail state. `Not in library` is only a label / action state, not a separate details page.
- Failed Movie placeholders and orphan carriers remain unidentified items and are intentionally not treated as no-source recognized Movies.

## Phase 4.12h Movie Boundary

Blocker:

- None for Movie Discovery from the Episode detail closeout.

Deferred:

- Cross-type correction, Movie / TV candidate correction, batch correction, manual grouping, and unidentified carrier correction remain outside Movie Discovery.

Noise:

- Movie detail now uses `从当前电影拆分` for the old reset-to-unidentified source action. The logic still detaches the source from the current Movie into unidentified carrying and does not delete real files.
- Failed Movie placeholders and orphan unknown carriers remain unidentified detail carriers; their split action stays disabled.
- Phase 4.12 does not change Movie Discovery ranking, search, recommendation inputs, Watch Insights, visibility semantics, schema, or migrations.

## Phase 4.10 TV Notes

- TV search / ranking not-in-library Series clicks now write metadata-only TV rows and enter `SeriesOverviewPage`.
- Metadata-only TV rows are not playback sources; they should remain not-in-library in discovery status until active Episode `MediaFile` rows exist.
- TV discovery still does not create `MediaFile`, fake `RemoteUri`, `AiRecommendationItem`, or Watch Insights input.
- Cleanup for metadata-only TV rows remains deferred.
- `ExternalTvSeriesDetailPage` is no longer planned for Phase 4.

## Phase 4.10.1 TV Library Notes

- Discovery browsing may create metadata-only TV rows, but rows with no active Episode source and no Season state are intentionally hidden from the default media-library list.
- Source-backed Series expand to all known Seasons in batch mode, including source-less Seasons and Season 0.
- Metadata-only Season watched / unwatched batch operations update TV Episode state only and do not create `WatchHistory`, `MediaFile`, or fake sources.
- Phase 4.10.1 originally skipped source-less TV Seasons and not-in-library Movies; Phase 4.10.4 supersedes this with `LibraryVisibilityState.Hidden`, while batch delete record remains the software-record removal path.
- Automatic cleanup and stale-refresh strategy for metadata-only TV rows remains deferred.

## Phase 4.10.3 Visibility Schema Notes

- `LibraryVisibilityState` exists after Phase 4.10.3 but Discovery does not use it yet.
- Source-less TV rows opened from Discovery remain metadata-only unless explicit add-to-library actions write `Visible`.
- Discovery add-to-library-specific labels are available after Phase 4.10.5, while fuller added / hidden status design can still be refined later.

## Phase 4.10.4 Visibility Semantics Notes

- Source-less media-library remove now writes `Hidden` and preserves state / metadata.
- Media-library source-state filters use active source presence instead of old in-library wording.
- Discovery metadata browsing still does not write `Visible`; only explicit add-to-library actions do.
- Phase 4.10.4d changes source-status labels to `有播放源` / `无播放源`; explicit add / restore actions are available after Phase 4.10.5.
- Pure visibility-only Movie rows should stay out of movie AI/profile/statistics/recommendation fingerprints and fallback external candidates.
- Phase 4.10.4f changes remove-from-library to hide-only semantics; it no longer disables active playback sources.
- Old `MediaFile.IsDeleted` rows created by earlier remove-from-library behavior are not restored automatically because they cannot be safely distinguished from missing files, removed paths, or delete-record cleanup; rescan recovery is a Phase 4.13 validation item.

## Phase 4.10.5 Add-to-Library Notes

- Add-to-library writes `LibraryVisibilityState.Visible` only.
- Restore-to-library is superseded by Phase 4.10.5b and now writes `Auto` when active source or real current state exists, falling back to `Visible` only for source-less no-state rows.
- Movie add-to-library does not set want-to-watch, favorite, not-interested, watched, or `MediaFile`.
- TV Series add-to-library writes `Visible` for all known Seasons, including Season 0 / Specials, after metadata is available.
- Add-to-library does not restore old `IsDeleted` source rows.
- TV Discovery remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.
- Phase 4.10.6 handles TV Discovery navigation / hydration contention with summary-first navigation and TV pagination guards.
- Phase 4.11 default scan AI is limited to sanitized TV range hints and does not affect Discovery AI recommendation semantics.
- Phase 4.11 protects strong TV ranges from silent Movie fallback; mixed-folder corrections are deferred to Phase 4.12.
- Phase 4.11b changes default scan AI to directory-range output only. It no longer asks for `episodeFiles`, but large scan trees may still need budget-driven summary batching if the short production timeout is exceeded.
- Phase 4.11c optimizes scan AI summaries with direct-video samples and short evidence fields. Discovery behavior is unchanged, and batch is still deferred unless real scans require it.
- Phase 4.11d tightens TV scan generalization: strong TV context now requires multiple evidence signals, weak TV risk blocks silent Movie fallback only when appropriate, and TMDB candidate conflicts should downgrade to placeholder / review instead of chasing bound counts.
- Phase 4.11e-prep disables default production full AI range calls and only emits log-only `aiCandidateRanges`; Discovery behavior remains unchanged.
- Phase 4.11e-prep-2 keeps scan candidate range fixes out of Discovery surfaces: Movie low-information query blocking and TV-risk fallback protection only affect scan identification plumbing.
- Numeric-only / low-information Movie scan queries now become placeholders instead of automatic TMDB binds; users can still correct them through later manual / AI-assisted correction flows.
- Phase 4.11e-prep-3 broadens generic Movie release/audio/source cleanup and emits final scan candidate summaries, but it still does not enable AI-on-uncertain or change Discovery surfaces.
- Phase 4.11e-prep-4 blocks Movie `NeedsReview` scan candidates from automatic binding and keeps them as placeholders / future AI-candidate diagnostics.
- Phase 4.11e-prep-4 also downgrades TV localized-title version conflicts and still does not enable AI-on-uncertain or change Discovery surfaces.
- Phase 4.11e enables scan AI-on-uncertain only for final sanitized candidate ranges. It is still scan plumbing and does not change Discovery ranking cards, Movie AI recommendation inputs, Watch Insights, or TV exclusion from AI surfaces.
- Phase 4.11e-fix-1 changes scan AI hint application to `inputRangeId`-first mapping. It should reduce dropped AI hints caused by sanitized directory text mismatch, but Discovery surfaces remain unchanged.
- Phase 4.11e-fix-2 binds scan AI candidate ranges to runtime `MediaFileIds`, so mapped AI hints no longer depend primarily on sanitized path parent/child matching to recover files. Discovery surfaces remain unchanged.
- Phase 4.11f uses AI refined TV title hints for local scan TMDB lookup. It remains scan plumbing only and does not change Discovery cards, Movie AI recommendation inputs, Watch Insights, or TV exclusion from AI surfaces.
- Phase 4.11f-fix-2 prefers original-language AI title hints for scan refined TV lookup, but English / localized fallback titles can still produce wrong TMDB top1 matches. These remain Phase 4.12 active correction / manual review work.
- Phase 4.11f-perf-1 adds same-run TMDB search caching and limits post-AI TV retry scope. It does not change Discovery behavior or scan matching quality; remaining wrong top1 matches still require Phase 4.12 active correction / manual review.
- Phase 4.11f-fix-3 adds only a narrow AI refined Series-year gate. Wrong top1 matches without a clear `seriesYearHint` conflict still remain Phase 4.12 active correction / manual review work.
- Phase 4.11f-fix-3 groups consecutive numbered Movie placeholders as TV-like ranges for later correction. Phase 4.11f-fix-4 / fix-5 surface those ranges through `Other`, but they remain unresolved data.
- Phase 4.11f-fix-5 moves ordinary unrecognized Movie placeholders into `Other` and stores grouped TV-like placeholders as unidentified `TvSeason` / `TvEpisode` rows. They can play and use Season detail, but remain unresolved and unbound to TMDB.
- Follow-up fixes ensure those failed unidentified Seasons are included in `Other` during normal media-library mode. Bracketed episode-number segments are grouped only under the existing conservative same-parent / strict-contiguous / minimum-three-file rules.
- Active correction, manual regrouping, full Episode detail management, and multi-source episode handling remain deferred.
- Anime specials, folk season splits, course / extras folders, and multi-source Episode cases are intentionally not solved in default scan.

## Phase 4.11f-fix-6 Scan Known Issues

Blocker:

- None.

Deferred:

- `01: title` / leading-number-colon-title, movie collections, courses, theatrical collections, anime specials, SP/OAD/OVA mapping, and multi-episode file splitting remain outside default scan.
- Title+number sequence candidates are TV-like uncertain inputs, not successful bindings. Active correction or manual grouping still owns ambiguous results.

Noise:

- Movie placeholder grouping now accepts numeric quality tails and mixed episode patterns, but only for already failed Movie placeholders in one direct parent folder with strict contiguous numbering.
- Bare four-digit numbers are intentionally not treated as episodes unless an explicit episode marker exists.
- Phase 4.11f-fix-7 ignored-file extension summaries are diagnostic evidence only; they do not imply that skipped extensions should immediately enter the video whitelist.

## Phase 4.11f-fix-8 Scan Known Issues

Blocker:

- None.

Deferred:

- `.sup` is accepted as a subtitle candidate, but playback / renderer support still needs validation.
- `.rmvb` is accepted by scan discovery; playback quality depends on the player stack and codec support.
- Orphan `Other` rows remain unresolved until active correction, manual regrouping, or later metadata binding.
- `01: title`, movie collections, courses, theatrical collections, anime specials, SP/OAD/OVA mapping, and multi-episode splitting remain outside default scan.

Noise:

- Orphan and placeholder grouping remains strict and may leave scatter items when numbering has gaps, crosses directories, or includes excluded extras / trailer tokens.

## Phase 4.10.6 TV Discovery Notes

- TV search / ranking Series clicks only require Series + Season summary metadata before navigation.
- Full Episode metadata is completed later by Series overview background hydration or by Season detail on-demand hydration.
- TV pagination and repeated TV card clicks are guarded while a TV Series navigation request is active.
- Discovery still does not create playback sources or TV AI / Watch Insights input.

## Phase 4.11f-fix-1 Scan AI Refined Title Risk

- AI refined title lookup now accepts TMDB TV top1 directly for uncertain scan ranges when `refinedSeriesTitle` is non-empty and TMDB returns a result.
- This intentionally removes the previous original/year/version refined safety gate for this path; wrong top1 matches should be handled by Phase 4.12 active correction / manual confirmation.
- Movie Discovery, Movie recommendation AI, Watch Insights, and media-library visibility are unchanged.

## 搜索

- 人物中文搜索依赖 TMDB alias / 本地化数据，不保证稳定；无结果时仍建议尝试英文名或原名。
- 影片搜索仍优先使用 TMDB `search/movie` 保留相关度召回，不切换为全文 `discover/movie`。
- 类型、年代、语言、片名排序和本地观看状态筛选是在扩展后的搜索结果池上处理，不是 TMDB 全库级精确筛选。
- 本地状态筛选无法传给 TMDB，只能在已合并状态的 TMDB 结果池内过滤。
- 片名排序是当前结果池内的本地排序，不代表 TMDB 全量搜索结果的全局片名排序。
- 地区筛选准确性受 TMDB 字段限制：
  - `search/movie` 的 `region` 更接近发行地区，不等同出品地区。
  - 本地优先使用 `origin_country`。
  - 当 `origin_country` 缺失时，使用 `original_language` 弱映射，可能把同语言不同地区影片纳入结果。
  - 中国大陆 / 香港 / 台湾在缺少 `origin_country` 时都会使用中文语言兜底，因此无法精确区分三地。
- OMDb 请求失败会降级为 TMDB 分或暂无评分，不阻塞搜索结果展示。
- “切布局”仍是占位按钮，只提示后续接入。

## 榜单

- 榜单最多展示前 200 名。
- 榜单严格保持 TMDB 返回顺序；如果 TMDB 当前榜单第一名变化，UI 会随 API 返回变化，不硬编码标题。
- 热门榜和高分榜当前人工核查结果均为 `奇幻变身大冒险` / `Swapped`，TMDB ID `1007757`，但该结果随 TMDB API 变化可能失效。
- 榜单加载态和空态已收敛为单一主内容状态覆盖层；如果后续新增状态文案，应继续避免在同一区域放置多个居中文案层。
- 情绪标签仅已入库影片或已完成未入库详情 AI 标签后可显示；榜单卡片本身不请求 AI 标签。
- OMDb 请求失败会降级为 TMDB 分或暂无评分，不阻塞榜单展示。
- 发现页海报已接入现有海报缓存行为；后续新增远程图片仍应继续使用同一行为。
- Phase 4.8 TV 榜单使用 TMDB Series 级结果；不提供 Season 榜单。
- Phase 4.8 TV 搜索 / 榜单不请求 OMDb / IMDb 评分，只显示 TMDB Series 评分或暂无评分。
- Phase 4.8 Bugfix 已补齐 TV 基础筛选，但筛选仍是当前结果池内的客户端筛选，不代表 TMDB 全量 TV 库级精确筛选。
- TV 搜索 / 榜单总季数通过 TMDB Series detail 按需加载；加载失败时显示未知，不在发现页补全 Series / Season metadata。
- 未入库 TV Series 外部详情仍是后置项，当前仅显示提示并保留卡片简介。
- TV 修正入口、跨类型修正 UI、无播放源 Season 展示和 TV 功能缺口大审查仍在后续阶段处理。

## 未入库详情

- 未入库详情会在进入详情页时自动请求 AI 标签，不在搜索或榜单卡片上提前请求。
- 自动请求期间缺失标签字段显示“AI 正在分析影片”，避免生成中误显示为“未提供”。
- 详情页使用内存级 TMDB ID 标签缓存避免同一运行会话内重复请求；该缓存不落库，重启应用后不会保留。
- AI 标签请求失败只更新状态提示，不阻塞详情页基础信息展示。

## 边界

- AI 推荐 Tab 未在本轮修改。
- 推荐算法、画像 AI、媒体库、观影洞察、首页、设置页和播放器未在本轮修改。
- 未新增 DB 字段，未新增 migration。
## Phase 4.11f-fix-9 Notes

- Short bare-number folders that look like movie collections and have no TV evidence are intentionally kept out of unidentified TV season grouping.
- Long-running unknown numeric ranges may still stay split when gaps are too large, duplicate episode numbers exist, or folder evidence is too ambiguous.
- TV.Parse warnings are no longer scan errors; real scan errors should still come from enumeration, save, access, or provider failures.
- Existing `._*` MediaFile rows from older scans may remain until covered by missing-file cleanup or manual delete-record cleanup. New scans ignore macOS AppleDouble files before Movie identification.

## Phase 4.11f-fix-10 Notes

- TV part hints such as `Pt.2` / `Part.2` are preserved for correction, but they are not Movie collection evidence and do not change Movie discovery behavior.
- Automatic part offset mapping remains deferred; unresolved part ranges should stay pending correction rather than becoming wrong Movie or TV bindings.

## Phase 4.11f-fix-11 Notes

- Safe part offset is TV-only and requires same-Series / same-Season sibling evidence before any episode remapping.
- Movie discovery does not use part hints as Movie title evidence, collection evidence, ranking input, recommendation input, or Watch Insights input.
- Part files without enough sibling evidence remain unresolved / pending correction in `Other`.

## Phase 4.11f-fix-11-hotfix Notes

- Automatic part offset apply is disabled until the TV-side evidence model is redesigned.
- Structural part-only queries are rejected before TMDB search and cannot become Movie or TV matches.
- Part hints remain correction context only; Movie Discovery, Movie recommendation AI, Watch Insights, and delete / visibility semantics are unchanged.

## Phase 4.11f-fix-13 Notes

- AI-on-uncertain batching is TV scan plumbing. Partial batch failure can leave TV ranges unresolved, but it should not change Movie discovery or Movie recommendation behavior.
- Provider rate limits or a single oversized batch can still leave some ranges unresolved; retry-time sub-batching is deferred.

## Phase 4.11f-fix-14 Notes

- Safe TV part offset can reduce unresolved later-part episodes only when previous same-Series / same-Season episode evidence is available.
- If part evidence is unsafe, the range remains in TV / Other correction surfaces; Movie discovery should not reinterpret it as a Movie title or collection.
- Movie Discovery thresholds, recommendation AI inputs, Watch Insights, and delete / visibility semantics remain unchanged.

## Phase 4.11f-fix-14-hotfix Notes

- AI refined title lookup for unsupported TV part candidates is TV-side confirmation only.
- Structural part-only strings remain rejected and should not be treated as Movie titles, Movie collection hints, recommendation input, or Watch Insights input.
- Later-part files without safe offset evidence remain unresolved / pending correction in TV / Other surfaces.

## Phase 4.11g Notes

Blocker:

- None for Movie Discovery from the TV scan closeout.

Deferred:

- Cross-type active correction, manual regrouping, Episode detail, multi-source management, TV recommendation inputs, TV Watch Insights, and anime-special mapping remain outside Movie Discovery.
- Large mixed folders can still create many unresolved `Other` rows; UI ergonomics and performance tuning remain later work.
- `.sup` is accepted as a subtitle candidate, but playback / renderer behavior still needs validation.

Noise:

- Unrecognized Movie placeholders being visible in `Other` is expected and does not mean Movie Discovery ranking/search changed.
- TV scan logs may mention Movie fallback blocking or placeholder grouping; those are scan diagnostics and are not Movie Discovery recommendation inputs.

## Phase 7.4a / 7.4b Notes

Blocker:

- None after the Discovery search toolbar / layout implementation, 7.4b visual alignment, follow-up tab/filter polish and build validation.

Deferred:

- Exact media-library visual parity for non-search Discovery surfaces remains deferred per user instruction.
- AI recommendation tab and preference dialog polish remain 7.4d.
- Discovery poster-layout pager placement still needs actual window validation against the list-layout pager in short and multi-row search results.

Noise:

- TV person search is a Discovery search feature only. It uses TMDB `person/{id}/tv_credits` and does not add TV recommendation, Watch Insights, profile/persona inputs or fingerprint behavior.
- Discovery search layout memory is App-layer JSON preference state in `discovery-preferences.json`; it is not a database schema change.
- Discovery selected-tab memory is runtime-only ViewModel state; it is not written to the database, navigation state or the Discovery preference file.
- Genre / region / language / decade / collection-status multi-select filters operate on the loaded / expanded result pool and still do not imply TMDB full-catalog exact filtering.
- Discovery and Media Library collection-status `其他` means items without favorite / want-to-watch / not-interested state; it is a client-side filter projection, not a new persisted state.
- Discovery search result pager and poster shadow safe space were aligned closer to Media Library, but still need visual inspection in collapsed and expanded sidebar states.
- Discovery search poster results use the same `ListBox + VirtualizingWrapPanel` scroll contract as Media Library; avoid returning to `ScrollViewer + ItemsControl + VirtualizingWrapPanel`, which caused poster scrolling to stop.
- Discovery poster-layout pager is a bottom overlay driven by the internal poster `ListBox` scroll state; it should show when no poster scrolling is needed or when the poster list reaches the bottom.
- Media Library toolbar sort alignment is calculated from the current sidebar state: collapsed aligns to watched status, expanded aligns to collection status.
- In Discovery search results, TV `想看` is a display tag for existing want-to-watch seasons only. Absence of the tag means no known want-to-watch season state; it is not a missing `+ 想看` action.
## Phase 7.4b Follow-up Round 8 Notes

Blocker:

- None after build validation.

Deferred:

- Actual WPF window validation is still needed to confirm visual alignment of title/date/tag left edge and rating badge right edge with long text and high/empty ratings.

Noise:

- Discovery search poster cards intentionally diverge from Media Library's bottom `Margin="10"` because Discovery adds a right-side rating badge that must align to the top-right chip border.

## Phase 7.4b Follow-up Round 7 Notes

Blocker:

- None after build validation.

Deferred:

- Actual WPF window validation is still needed to confirm Discovery search poster title/date/type line visually matches Media Library with the rating badge present.

Noise:

- Media Library uses `Margin="10"` for the bottom title/date/type text group while the top media-type chip uses `Margin="8"`. Discovery now follows that Media Library baseline.

## Phase 7.4b Follow-up Round 6 Notes

Blocker:

- None after build validation.

Deferred:

- Actual WPF window validation is still needed to confirm the perceived poster edge alignment matches Media Library in both Movie and TV search results.

Noise:

- This pass only changes Discovery search poster XAML margins. It does not change ranking, filters, rating persistence or TMDB request behavior.

## Phase 7.4b Follow-up Round 5 Notes

Blocker:

- None after build validation.

Deferred:

- Search card poster/list visual alignment still needs actual WPF window validation, especially title/date left edge, rating badge size and TV current-want vertical centering.
- Existing databases need the generated `AddTvSeriesRatingSources` migration applied before TV series rating persistence can work at runtime.

Noise:

- Movie Discovery can now refresh existing local movie and TV series ratings from real-time search/ranking enrichment; this intentionally changes rating data, but not media files, playback sources, visibility, watch state or recommendation inputs.
- TV series rating persistence is series-level only. Season and episode ratings still use the existing on-demand detail query behavior.

## 2026-06-14 AI Recommendation Failure Status

Blocker:

- AI recommendation generation cannot complete while the configured provider rejects requests because of billing/quota, authentication, permission, model, or endpoint state; the UI now identifies these categories explicitly.

Deferred:

- Manually verify recommendation-page and Home status text for HTTP 402, 401/403, 429, timeout, and 5xx responses, including tooltip visibility for long messages.

Noise:

- A failed refresh can continue displaying the previous recommendation batch by design; the status text now makes that fallback explicit instead of implying success.

## 2026-06-14 Cached Shadow Theme Parity

Blocker:

- None confirmed by build after moving Movie Discovery ranking badge shadows to shared cached-shadow theme tokens.

Deferred:

- Manually compare ranking badge shadow visibility in light and dark themes.

Noise:

- The change is visual-only and does not affect discovery search, ranking, or rating data.

## 2026-06-14 Ranking Shadow And Bottom Corner Follow-up

Blocker:

- None confirmed by build after disabling shadows on transparent ranking wrappers and fixing shared bottom-corner initialization.

Deferred:

- Manually verify Movie Discovery ranking info areas no longer show full-card shadows, score badges are less diffuse, and search/ranking non-empty pages initialize with the expected bottom-corner state.

Noise:

- The fix is visual/interaction-state only and does not affect discovery data, paging, ranking, search, or rating persistence.

## 2026-06-14 Ranking Shadow Tightening And Empty-State Corner Fix

Blocker:

- None confirmed by temp-output build after tightening cached-shadow blur radii and restoring empty-state bottom-corner behavior.

Deferred:

- Manually verify empty/non-scrollable search and ranking surfaces are in the bottom state, true bottom does not oscillate between rounded and flat, and score-badge shadows are sufficiently concentrated.

Noise:

- Normal output build was blocked by a running app process; temp-output build passed.

## 2026-06-14 Search Poster And Ranking Badge Shadow Edge Fix

Blocker:

- None confirmed by temp-output build.

Deferred:

- Manually verify that movie/TV search poster glows are not clipped at the top, left edge, or right edge.
- Manually verify that ranking score-badge glow is not clipped on the right side in both themes.

Noise:

- The fix only changes cached-shadow visual parameters and edge-safe layout space; discovery data, ranking, search requests, paging, and rating persistence are unchanged.

## 2026-06-14 Phase 7 Viewport Compensation Restoration

Blocker:

- None confirmed by temp-output build.

Deferred:

- Manually confirm Movie/TV search poster columns and card coordinates remain unchanged after the shadow-safe viewport expansion.
- Manually confirm the first row, left edge, and right edge poster glow are visible.
- Manually confirm ranking score-badge glow is visible on the right without shifting the badge or text area.

Noise:

- The earlier direct panel-width correction is superseded; the current implementation follows the Phase 7 viewport expansion plus equal compensation model.

## 2026-06-14 Search Scroll Boundary And Ranking Top Glow Follow-up

Blocker:

- None confirmed by temp-output build.

Deferred:

- Manually confirm Movie/TV search poster bodies remain below the result-content boundary throughout vertical scrolling.
- Manually confirm the real top gutter exposes the first search row and ranking hero poster glow without changing poster columns.

Noise:

- Negative top viewport expansion was removed. Horizontal paint-room compensation remains because it does not alter the vertical scroll boundary.

## 2026-06-14 Search First-row Spacing Restoration

Blocker:

- None confirmed by temp-output build.

Deferred:

- Manually confirm Movie/TV search first-row spacing matches the original compact layout.
- Manually confirm scrolling content remains inside the result viewport and horizontal edge glows remain visible.

Noise:

- Search top padding is back to 10px; the ranking hero top gutter remains a separate correction.

## 2026-06-14 Discovery Scrollbar Alignment Follow-up

Blocker:

- None confirmed by temp-output build.

Deferred:

- Manually verify Movie/TV poster-search scrollbars sit inside the result-content boundary.
- Manually verify ranking scrollbars use the smaller left correction and the first hero row has an 18px top gutter.

Noise:

- Scrollbar transforms are render-only and do not participate in poster column calculation. The first build attempt was temporary-volume noise; the retry passed after scoped cleanup.

## 2026-06-16 Detail Task Recent-sort Touch Follow-up

Blocker:

- None confirmed by build.

Deferred:

- Manually verify Movie Detail lazy probe and AI tag generation both move the touched movie forward immediately after returning to Media Library under recent-update descending sort.
- Manually verify recognized no-source movie rows also move forward when their stored movie metadata or tags update.

Noise:

- External no-source items only move forward when they already have a collection row to touch; pure transient search/recommendation detail views remain outside the media-library sort list.
