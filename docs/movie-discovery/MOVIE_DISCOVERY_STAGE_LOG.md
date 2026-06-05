# 影片发现阶段日志

## 2026-06-06 - 7.4c follow-up / 榜单 Tab 悬停下拉延时

完成内容：
- 榜单 Tab 悬停打开下拉菜单从立即触发改为 420ms 延时触发，避免鼠标快速划过时弹出下拉菜单。
- 增加榜单 Tab `MouseLeave` 取消逻辑；鼠标在延时结束前离开按钮时，不再打开下拉菜单。
- 点击榜单 Tab 的行为保持即时：未在榜单页时仍切到榜单并打开菜单，已在榜单页时仍可点击开关菜单。

验证：
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改榜单下拉菜单内容、布局、筛选项、榜单请求、排序、评分计算、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工交互验收；仍需实际窗口确认 420ms 延时手感是否合适。

## 2026-06-05 - 7.4c follow-up / 榜单首次加载兜底与双列行距 28px

完成内容：
- 榜单激活入口增加空闲无数据兜底：已进入过榜单但当前 Movie / TV 榜单没有可见项且不在加载中时，再次激活榜单会重新拉起加载，避免首次进入未真正启动加载后被 `_hasActivatedRankings` 挡住。
- Movie 榜单重置加载时，在清空列表和刷新可见性前先设置 `IsRankingLoading=true`，确保首次进入就显示加载转圈，而不是静止空状态。
- TV 榜单重置加载时同步前置 `IsTvRankingLoading=true`，电视剧榜单首次加载也进入明确加载态。
- Movie / TV 榜单普通双列行底部间距从 24px 提高到 28px。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、评分计算、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认首次进入榜单转圈、自动加载和 28px 双列间距。

## 2026-06-05 - 7.4c follow-up / 榜单双列行间距微调

完成内容：
- 榜单 Movie 双列普通行之间的底部间距从 14px 提高到 24px，从双列海报第一行开始增加上下间隔。
- 榜单 TV 双列普通行之间的底部间距同步从 14px 提高到 24px。
- 未调整第一名区域、双列内左右间距、海报尺寸、海报列宽、标题 / 原名行、简介行距或简介滚轮处理。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `dotnet build src/MediaLibrary.App/MediaLibrary.App.csproj -o %TEMP%\xfverse-build-check` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、评分计算、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动新的完整桌面应用做人工截图验收；仍需实际窗口确认双列普通行间距是否合适。

## 2026-06-05 - 7.4c follow-up / 榜单简介上方留白微调

完成内容：
- 榜单第一名信息模板中，导演 / 演员行与简介之间的上边距从 12px 提高到 32px；第一名简介行高为 20px，因此简介可视区域减少约一行。
- 榜单普通双列项信息模板中，导演 / 演员行与简介之间的上边距从 12px 提高到 30px；普通项简介行高为 18px，因此简介可视区域减少约一行。
- 未调整榜单信息模板总高度、海报尺寸、海报列宽、标题 / 原名行、日期 / 标签行、导演 / 演员字段宽度或简介滚轮处理。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` 未完成：当前运行中的 `MediaLibrary.App (16200)` 锁定默认 `bin` 输出，复制 apphost / dll 失败。
- `dotnet build src/MediaLibrary.App/MediaLibrary.App.csproj -o %TEMP%\xfverse-build-check` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、评分计算、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认简介上方多一行留白和简介减少一行后的观感。

## 2026-06-05 - 7.4c follow-up / 榜单滚动视口左侧安全区补偿

完成内容：
- 根据截图复核，确认仅扩大海报模板内部 shadow host 不足以解决第一列左侧阴影裁剪；普通榜单行仍从 `ScrollViewer` 视口左边界开始，负向绘制会被视口裁掉。
- `RankingMovieScrollViewer` / `RankingTvScrollViewer` 增加 `Margin="-84,0,0,0"`，把榜单滚动视口向左扩展 84px，匹配媒体库海报阴影安全区。
- 两个榜单 ScrollViewer 内部根 `Grid` 增加 `Margin="84,0,0,0"`，用等量内容补偿抵消视口左扩，保持海报本体、右侧文字、分隔线、分页和滚动条视觉位置不随之移动。
- 保留上一轮媒体库同款 shadow host：普通海报 `348x438`、第一名海报 `384x492`，均使用 `Canvas.Left/Top=-84` 和 `Padding=84`。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、评分计算、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认第一列左侧阴影是否显示完整且内容坐标未发生可见位移。

## 2026-06-05 - 7.4c follow-up / 榜单海报阴影安全留白对齐媒体库

完成内容：
- 对照媒体库 `LibraryPage` 海报模板，确认媒体库普通海报发光阴影使用 `348x438` shadow host、`Canvas.Left=-84`、`Canvas.Top=-84`、`PosterCachedShadowBehavior.Padding=84`，海报本体仍为 `180x270`。
- 榜单 Movie / TV 普通海报同步媒体库阴影安全区：shadow host 从 `300x390` 改为 `348x438`，`Canvas.Left/Top` 从 `-60` 改为 `-84`，shadow padding 从 `60` 改为 `84`。
- 榜单 Movie / TV 第一名海报按同样 84px 安全区处理：shadow host 从 `336x444` 改为 `384x492`，`Canvas.Left/Top` 从 `-60` 改为 `-84`，shadow padding 从 `60` 改为 `84`。
- 保持榜单海报本体宽高、Grid 列宽、第一名左右 margin、普通双列 item margin 和右侧信息区位置不变。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、评分计算、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认左侧第一列海报阴影是否已完整显示。

## 2026-06-05 - 7.4c follow-up / 榜单右上标签与海报阴影可视区修复

完成内容：
- 榜单 Movie 普通项和第一名海报右上角想看按钮已保持移除状态；本轮继续删除 TV 普通项和第一名海报右上角 `当前想看` 标签，榜单所有海报右上角不再显示想看状态。
- 榜单外层滚动条继续只通过 `DiscoveryRankingOuterScrollBarStyle` 的 `RenderTransform X=4` 向右微移，作用域限定在 `RankingMovieScrollViewer` / `RankingTvScrollViewer` 的局部资源，不改变内容布局、列宽或组件坐标。
- 榜单第一名卡片和普通项卡片显式设置 `ClipToBounds=False`，避免继承玻璃卡片裁剪导致左侧发光阴影显示不完整。
- 榜单 Movie / TV 普通海报和第一名海报的 palette shadow 位图恢复 `Canvas.Left=-60`，匹配 60px shadow padding；海报本体宽高、列宽、Grid margin 和 ContentControl 位置未改。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、评分计算、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认 TV 右上标签完全消失、左侧发光阴影不再被裁剪、滚动条右移未带动其他组件位置。

## 2026-06-05 - 7.4c follow-up / 榜单下拉阴影与第一名留白再修

完成内容：
- 榜单下拉主菜单和二级菜单去掉 `ShadowPopup` 效果，避免圆角菜单外出现直角黑色阴影。
- 第一名 Movie / TV 顶部区域左右留白从 18px 加大到 36px，海报进一步右移，同时缩短右侧文字可显示宽度。
- 导演字段上限进一步收窄：第一名从 250px 降到 210px，普通项从 130px 降到 105px，演员继续使用剩余宽度，更接近 3:7。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认下拉阴影是否完全消失、第一名左右留白是否对齐、导演 / 演员 3:7 是否合适。

## 2026-06-05 - 7.4c follow-up / 榜单标题行与第一名留白修复

完成内容：
- 榜单第一行改为“标题 / 竖线 / 原名 + 右侧固定评分”结构，评分标签移到原名右侧固定位置，标题左端直接从信息区左侧开始，和日期、导演、简介三行左端对齐。
- 标题 / 原名不再使用 `1.1* / *` 预留剧名长度；标题列改为 Auto + MaxWidth，竖线跟随标题实际宽度，避免竖线被剧名预留列推偏。
- 标题 / 原名和评分标签统一放入同一高度行内垂直居中；竖线颜色改为主文本色、宽度保持 2px，两侧间距保持 8px。
- 第一名 Movie / TV 区域整体增加左右等距 18px margin，使海报左边缘到外层背景边框的距离与简介右边缘到背景边框距离一致，同时缩短右侧文字可显示宽度。
- 导演字段长度上限进一步收窄：第一名导演 MaxWidth 250，普通项导演 MaxWidth 130，演员继续紧随导演后以剩余宽度显示，接近 3.5:6.5 的可用宽度分配。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认评分固定位置、标题左对齐、竖线跟随标题和第一名左右留白。

## 2026-06-05 - 7.4c follow-up / 榜单第一名与普通项层级微调

完成内容：
- 榜单第一名新增专用 Movie / TV 海报模板，尺寸从普通项 180x270 放大到 216x324；普通项海报保持 180x270。
- 普通双列项标题、评分、简介和 meta 字号按反馈整体下调；日期、标签、导演、演员改为小于简介字号，简介改为更高对比的主文本色。
- 第一行标题 / 原名区域改为嵌套底部对齐的标题组，竖线加粗到 2px，并把竖线只在标题 / 原名文本组内垂直居中；标题、竖线、原名之间缩到约两个空格的视觉距离。
- 导演 / 演员行从固定双列改为导演 Auto + 最大宽度 + 演员紧随的流式结构，导演过长时先截断，演员与导演末尾保持固定间距。
- 榜单 Movie 右上角 `+ 想看` / `取消想看` 按钮改用榜单专用样式：整体高度从 20px 提到 22px，`+ 想看` 默认更窄，`取消想看` 只同步增高不额外收窄。
- 在第一名区域和下方普通双列区域之间新增横向分隔线，Movie / TV 分别按是否存在第一名控制显示。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认第一名 20% 放大、普通项字号、竖线垂直居中和导演 / 演员流式间距。

## 2026-06-05 - 7.4c follow-up / 榜单视觉二次微调

完成内容：
- 榜单 Movie / TV 海报从 240x360 缩小到 180x270，同步降低外层行高和海报列宽，减少榜单双列项占屏面积。
- 榜单第一名和普通项外层卡片背景改为透明，去掉海报与右侧信息区背后的大圆角玻璃矩形，只保留内容本身和双列中线。
- 榜单原名样式改为与片名 / 剧名同一字体、字号、字重和颜色。
- 日期、标签、导演、演员统一为同一 meta 文本样式，字号从 12.5 提到 13.5，颜色改为 `BrushForegroundSecondary`，并同步标签分隔符颜色。
- 恢复当前 XAML 中遗留的静态中文乱码点：多选筛选 `全部`、隐藏 TabItem `影片搜索` / `AI推荐`。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认海报缩小后的信息密度、右侧简介高度和无背景状态下的可读性。

## 2026-06-05 - 7.4c follow-up / 榜单海报与四行信息布局反馈修复

完成内容：
- 榜单 Movie / TV 顶部第一名和普通双列项统一改为左侧详情页式海报、右侧四行信息结构。
- 榜单海报复用详情页大海报尺寸、圆角、w780 解码、palette shadow 和大海报占位模板；左上角排名标签保留，右上角改为影片搜索海报同款 Movie 想看按钮 / TV 当前想看标签。
- 右侧第一行改为评分、标题、竖线、原名；评分标签复用首页 AI 推荐评分 chip 质感并放大，第一名使用更大尺寸。
- 第二行改为日期 + 媒体库海报标签行式类型标签；第三行改为导演 + 演员，长文本截断并提供 tooltip；第四行改为内部可滚动简介。
- 榜单简介滚轮处理复制影片识别修正弹窗“修正为电影”候选简介逻辑：简介可滚时内部消费滚轮，简介不可滚时交给外层榜单滚动。
- 修复本轮 XAML 写回暴露的影片发现页静态中文属性损坏点，恢复 Tab、榜单菜单、清除筛选、布局切换和分页 tooltip 文案。

验证：
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认榜单大海报密度、两列宽度、行距和简介滚轮手感。

## 2026-06-05 - 7.4c follow-up / 榜单二次测试反馈修复

完成内容：
- 榜单加载 / 切换加载层补齐与影片搜索一致的 34px 转圈加载动画，并将榜单内容卡片显式设为箭头光标，避免加载中出现可点击手势误导。
- `榜单` Tab 从非榜单 Tab 悬停时不打开菜单；点击时先切换到榜单页，再在榜单 Tab 下方打开多级菜单。
- 榜单分页按钮 / 页码移动到 Movie / TV 各自 `ScrollViewer` 内容末尾，按影片搜索列表分页结构使用 `Grid MinHeight=ScrollViewer.ActualHeight`，内容不足时靠底，内容超出时随滚动到末尾。
- 榜单二级菜单中 `趋势榜` 子菜单项文字居中到与 `热门榜` / `高分榜` 同一视觉列，右侧箭头间距由 8px 收紧到 4px。
- 空关键词切换 `搜索方式` 时不再触发 Movie / TV 结果重载；该保护走共同 `SelectedSearchType` setter，因此电视剧同样接入。
- 搜索工具栏排序菜单改为按当前 Movie / TV 媒介绑定 `ActiveSearchSortOptions`，TV 模式下写入 `SelectedTvSortOption`，修复空关键词按非相关度排序无法进入 TV discover 的问题。

验证：
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。

未做事项：
- 未修改 TMDB 搜索 / 榜单请求形态、榜单排序、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认榜单菜单、分页和加载层的主观位置。

## 2026-06-05 - 7.4b follow-up / 搜索海报标题日期对齐根因修复

完成内容：
- 重新核对 Movie 搜索内联海报模板和 TV 搜索 `DiscoveryTvSeriesCardViewModel` 资源模板，确认用户截图看到的是搜索海报卡片模板，不是媒体库模板或列表模板。
- 将 Movie / TV 搜索海报底部信息区从 `StackPanel + 内部评分 Grid` 改为整卡宽度的三列 `Grid`：左列标题 / 日期 / 标签，中间固定 8px 间距，右列评分 chip。
- 底部 `Grid` 显式 `HorizontalAlignment="Stretch"` 并使用 `Margin="8,0,8,10"`；标题和日期显式放在第 0 列并左对齐，评分 chip 继续在右列右对齐。
- 本轮不再照抄媒体库底部 `StackPanel`，因为搜索海报比媒体库多一个评分 chip；必须让左侧文字和右侧评分共享同一个整卡宽度坐标基准。

验证：
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。

未做事项：
- 未修改搜索服务、TMDB 请求、筛选语义、榜单、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收；仍需在实际窗口确认标题 / 日期左边框和评分右边框的视觉对齐。

## 2026-06-05 - 7.4b follow-up / 搜索卡片与列表二次测试反馈修复

完成内容：
- 影片搜索 Movie / TV 海报卡片底部标题、日期、标签左边距统一为 8px，对齐左上角 Movie / TV 类型标签左边框。
- 搜索海报评分标签改为与左上角类型标签一致的浅色 chip 质感，圆角降为 6px；无评分统一显示 `--`。
- Movie 海报右上角 `+ 想看` / `取消想看` 按钮和 TV 海报右上角 `当前想看` 标签固定为 20px 高度，与左上角类型标签顶部对齐。
- Movie 搜索结果不再从本地状态回填 AI 类型、情绪或场景标签；搜索卡片和列表只使用 TMDB 类型标签，入库影片也不混入本地 AI 标签。
- Movie / TV 搜索列表增加 14px 条目间距，并补齐媒体库式标题行右侧的导演 / 演员字段。
- Movie 搜索详情补全条件从仅缺 IMDb 扩展为缺 IMDb、缺导演演员或缺 TMDB 类型时拉取 TMDB 详情；TV 搜索继续复用 Series detail 水合导演 / 演员。
- 媒体库 Movie 列表评分由优先 OMDb 单源显示改为 TMDB/IMDb 加权评分；双源时来源显示为 `TMDB/IMDb`，与搜索页评分口径一致。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改 TMDB 搜索请求形态、筛选规则、榜单排序、AI 推荐、扫描识别、播放器、数据库 schema、migration 或 database update。
- 未新增 TV series 级 `+ 想看` / `取消想看` 操作；TV 仍只展示已有想看季状态。
- 未启动完整桌面应用做人工截图验收。

## 2026-06-05 - 7.4b follow-up / 工具栏、结果区域与想看摘要测试反馈修复

完成内容：
- 影片搜索第一行改为左侧输入框 / 搜索图标 / 顺序图标一组，右侧排序 / 影视 / 搜索方式 / 清除筛选四按钮一组；顺序图标移出右侧筛选组，去掉常驻边框和背景，仅沿用图标按钮 hover / pressed 效果。
- 影片搜索 `清除筛选` 增加当前激活 Movie / TV 模式下的默认状态判断，筛选全部为默认时禁用按钮；切换筛选、切换 Movie / TV 和重建结果时同步刷新 CanExecute。
- 影片搜索海报结果增加媒体库同参数的海报槽位和安全留白，使用 194x288 槽位与 `0,10,0,30` 内容边距，降低左右不齐和阴影裁切风险。
- 影片搜索和媒体库的海报 / 列表布局切换组件统一为本地 28px 小圆角模板，外层圆角降为 4px，选中按钮圆角降为 3px，并移除 Discovery 组件的固定最小宽度。
- Movie 搜索结果取消想看后重新解析本地状态；当状态解析器确认没有本地 / 用户状态时清空卡片的入库、可见、播放源和用户状态字段，摘要重新按实际状态统计。主动入库仍存在时不强制出库。
- 媒体库第一行同步改为左侧输入框 / 搜索图标 / 顺序图标一组，右侧排序 / 清除筛选两按钮一组；顺序图标去掉常驻边框和背景，并绑定按钮前景色。
- 媒体库海报和列表小字样式补充 `SemiBold` 字重，对齐影片搜索结果的小字粗体观感。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：
- 未修改搜索服务、TMDB 请求、榜单、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未新增 TV series 级 `+ 想看` / `取消想看` 操作；TV 仍只展示已有想看季状态。
- 未启动完整桌面应用做人工截图验收。

## 2026-06-05 - 7.4b follow-up / 搜索结果卡片测试反馈修复

完成内容：

- 影片搜索 Movie / TV 海报结果改为媒体库 180x270 海报卡片结构：裁剪、双层阴影、海报占位、上下渐变、左上类型标签和底部信息区对齐媒体库卡片。
- Movie 海报右上角从播放源标签改为 `+ 想看` / `取消想看` 按钮，并用不同底色区分状态；TV 海报右上角改为存在想看季时才显示 `当前想看` 标签。
- 海报卡片去掉媒体库进度条和进度标签；标题 / 日期列缩短，右侧加入首页 AI 推荐同款评分标签，评分标签相对标题和日期两行垂直居中。
- 影片搜索 Movie / TV 列表结果改为媒体库列表结构：深色圆角行、左侧评分块、中部标题和标签行、右侧标签区；去掉进度条和进度标签。
- Movie 列表右侧播放源标签改为 `+ 想看` / `取消想看` 按钮；TV 列表右侧保留 `当前想看` 标签语义，仅在存在想看季时显示。

验证：

- `git diff --check` passed。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。

未做事项：

- 未修改搜索服务、TMDB 请求、筛选语义、榜单、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未新增 TV series 级想看切换；TV 仍按已有“存在想看季”状态展示。
- 未启动完整桌面应用做人工截图验收。

## 2026-06-05 - 7.4b follow-up / 搜索工具栏三次测试反馈修复

完成内容：

- 影片搜索第一行搜索输入框在侧栏收起 / 展开状态下分别从 816 / 660 调整到 544 / 440，释放出的宽度由右侧按钮组的等权星号间隔承接，最右侧 `清除筛选` 仍锚定在工具栏右端。
- 影片搜索第一行右侧控件改为：顺序图标、排序、影视、搜索方式、清除筛选；`搜索方式` 从第二行移动到 `影视` 右侧。
- 影片搜索第二行 Movie / TV 筛选均从 `类型` 开始，顺序为：类型、地区、语言、年代、入库状态、观看状态、收藏状态、播放源；第一个按钮左端仍对齐搜索输入框左端。

验证：

- `git diff --check` passed。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。

未做事项：

- 未修改搜索服务、TMDB 请求、筛选语义、榜单、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收。

## 2026-06-05 - 7.4b follow-up / 搜索工具栏二次测试反馈修复

完成内容：

- 媒体库搜索输入框和结果摘要字体加粗；摘要常规行和批量状态行均显式使用半粗字重。
- 影片搜索第一行右侧控件改为：顺序图标、排序、影视、清除筛选；`影视` 从第二行移动到排序按钮右侧。
- 影片搜索第二行 Movie / TV 筛选均从 `搜索方式` 开始，顺序为：搜索方式、类型、地区、语言、年代、入库状态、观看状态、收藏状态、播放源。
- 第二行筛选按钮继续使用等宽星号间隔，左端对齐搜索输入框左端，播放源右端对齐清除筛选右端。

验证：

- `git diff --check` passed。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。

未做事项：

- 未修改搜索服务、TMDB 请求、筛选语义、榜单、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未启动完整桌面应用做人工截图验收。

## 2026-06-05 - 7.4b follow-up / 搜索工具栏测试反馈修复

完成内容：

- 媒体库搜索输入框显式使用与影片搜索输入框一致的基础字体族、12px 字号和普通字重。
- 影片搜索新增单选筛选 `入库状态：全部 / 已入库 / 未入库`；Movie / TV 各自保存筛选状态，默认均为 `全部`。
- 影片搜索第二行筛选改为 10 列等距网格，视觉顺序为：影视、搜索方式、类型、地区、语言、年代、入库状态、观看状态、收藏状态、播放源；左端对齐搜索输入框，右端对齐清除筛选按钮。
- 搜索结果加载时清空当前可见结果和摘要，保留加载覆盖层转圈动画与当前媒介 / 搜索方式提示文案。
- 搜索结果摘要改为单行媒体库式统计：`共找到 x 项媒体 · 已入库 x 项 · 未入库 x 项`，不再显示布局切换或缓存扫描提示。
- 搜索结果容器空白区域显式使用箭头光标，保留结果卡片和可点击控件的手形光标。
- 影片搜索 Movie / TV 的上一页 / 下一页按钮改为透明 chevron 图标按钮，仅 hover / pressed 时显示边框和选中态。

验证：

- `git diff --check` passed。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：

- 未修改 TMDB 请求、搜索排序、榜单算法、AI 推荐、扫描、播放器、数据库 schema、migration 或 database update。
- 未改榜单分页按钮；本轮反馈限定在影片搜索结果区域分页按钮。
- 未启动完整桌面应用做人工截图验收。

## 2026-06-05 - 7.4b follow-up / Tab 对齐根因修复

完成内容：

- 复查 Tab 偏移问题后确认：原 `TabItem` 模板内文字和下划线本身可居中，但可见效果仍受 WPF `TabPanel` / `TabItem` header 单元测量和潜在裁剪影响，可能表现为文字完整而下划线一侧被截断或视觉中心偏移。
- 顶层可见 Tab 头改为 `TabControl` 模板内的固定宽度手工按钮条，三个按钮都使用 104px 固定坐标系；文字 `TextBlock` 与选中下划线 `Border` 绑定同一宽度，选中状态只切换下划线刷色，不改变宽度或重新测量。
- 原 `TabPanel` 保留为 0 高度、不可点击、透明的 `ItemsHost`，仅用于维持 `TabControl.SelectedContent` 内容选择；可见 Tab 头不再依赖 `TabItem` header 渲染。
- `SelectedTabIndex` 增加三个选中状态投影和手工 Tab 选择命令，确保点击按钮、导航持久化和程序切 Tab 都共享同一个状态源。
- 顶层 Tab 区域和分隔横线整体继续上移 4px。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- WPF 离屏像素验证：三个可见 Tab 下划线均为 104px；`影片搜索` / `榜单` 文字中心与线中心差值为 0px，`AI推荐` 为 -0.5px。

未做事项：

- 未改搜索、榜单、AI 推荐、TMDB 请求、扫描、数据库 schema、migration 或 database update。
- 未启动整包应用做人工截图；本轮以代码结构、离屏像素验证和 build 作为自动验证。

## Phase 7.4c 榜单视觉一致性

完成内容：

- 榜单 Tab 头部改为媒体库式紧凑按钮 / ContextMenu：媒介、榜单类型和趋势时间不再使用旧 ComboBox / Menu 外观。
- 榜单头部保留 Discovery 专属状态文案和摘要，右侧补当前 Movie / TV 榜单页码徽标。
- 榜单内容区改为统一玻璃卡片容器，Movie / TV 独立滚动、加载、空状态和分页绑定保持不变。
- Movie 榜单 Top card 和普通双列行对齐 7.4b 搜索卡片语言：排名、类型、评分、播放源状态、观看状态、想看动作、加入媒体库 / 恢复动作。
- TV 榜单 Top card 和普通双列行对齐 Movie 榜单视觉：类型、评分、媒体库状态、季数、Season 状态摘要、想看季标签、加入媒体库 / 恢复动作。
- TV 榜单继续使用既有 metadata hydration / 剧详情入口，不进入 AI 推荐、Watch Insights、画像、观影人格或推荐 fingerprint。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

未做事项：

- AI 推荐 Tab 和推荐偏好弹窗仍按 7.4d 处理。
- 榜单样式仍是 `MovieDiscoveryPage` 本地资源，未抽取为全局组件。
- 未新增数据库字段、未新增 migration、未执行 database update。

## Phase 7.4b 媒体库一致性修复

完成内容：

- 移除影片发现页内重复标题和整页外层卡片背景，副标题改为草图文案 `遇见更多适合你的影片`。
- 顶层 Tab 改为标题栏下方横线上的左对齐轻量样式；当前 Tab 使用强调色文字和同色下划线，搜索 Tab 使用短文案 `影片` 避免重复 Shell 页面标题。
- 影片搜索工具栏补齐媒体库式紧凑输入框、输入清空、搜索图标、顺序图标、右侧三控件网格、菜单圆角壳、菜单居中打开和再次点击关闭逻辑。
- 搜索结果圆角区域第一行对齐媒体库：左侧为状态 / 摘要，右侧为海报 / 列表分段切换；实际结果或空状态从第二行开始。
- 搜索海报卡片密度收紧到媒体库式 180x270 基准，避免搜索结果列数明显少于媒体库。
- 搜索结果和榜单主滚动区接入隐藏式 6px 垂直滚动条自动显隐，横向滚动禁用。
- Movie / TV 的地区、语言改为多选筛选；Movie 和 TV 均拆分观看状态、播放源、收藏状态，收藏状态当前为单选筛选。
- 搜索方式和媒介文案改为 `搜索方式：电影 / 人物`、`搜索方式：电视剧 / 人物`、`影视：电影 / 电视剧`。
- `清除筛选` 只重置筛选，不清空搜索词和当前结果池。
- 搜索结果区和榜单内容区新增当前软件运行会话内滚动位置恢复；导航离开和详情返回保留位置，新搜索 / 翻页 / 榜单重载回到顶部，重启后自然清零。
- TV 搜索卡片新增 Series 观看状态投影，作为 TV 观看状态筛选依据；TV 仍不进入 AI 推荐、Watch Insights、画像、观影人格或推荐 fingerprint。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- 本轮按用户提供的媒体库 / 影片发现截图做静态对照；用户明确已截图后未再额外启动软件截图。

未做事项：

- 榜单 Tab 的完整视觉重做仍按 7.4c 处理。
- AI 推荐 Tab 和推荐偏好弹窗仍按 7.4d 处理。
- 未新增数据库字段、未新增 migration、未执行 database update。

## Phase 4.18 Phase 4 Discovery Regression Note

- Movie Discovery remains the default discovery surface for Movie search, person search, Movie rankings, Movie detail navigation, no-source Movie detail, and Movie-side collection actions.
- TV Discovery remains integrated into the existing discovery page through Movie / TV switches on search and ranking tabs; no independent TV Discovery page was added.
- TV search and rankings still navigate through TV metadata hydration into `SeriesOverviewPage`.
- metadata-only TV hydration is TV-only: it may create or update `TvSeries`, `TvSeason`, and `TvEpisode` summary/detail rows, but it must not create `Movie`, `MediaFile`, `UserMovieCollectionItem`, Movie `WatchHistory`, or playback-source rows.
- The AI recommendation tab remains Movie-only and is not used as a TV recommendation entry.
- Movie Discovery, Movie AI recommendation, Watch Insights, Watch Profile, and recommendation fingerprint boundaries were rechecked during Phase 4.18 with no required code fix.
- No DB field, migration, database update, Phase 5 subtitle feature, final UI redesign, commit, or push was performed in this closeout.

## Phase 4.16 TV Discovery 收口回归

- Phase 4.16a 审计结论已收口：当前 TV Discovery / TV 搜索 / TV 榜单基础能力已可用，没有必须新增功能的 Blocker。
- `MovieDiscoveryPage` 继续承载影片发现入口，不新增独立 TV Discovery 页。
- 影片搜索 Tab 和榜单 Tab 保留 Movie / TV 媒介切换；Movie 搜索、人物搜索、Movie 榜单和 Movie 无播放源详情语义不变。
- AI 推荐 Tab 仍只服务 Movie，不提供 TV 推荐入口。
- TV 搜索 / 榜单点击会先写入 metadata-only `TvSeries` / TMDB Season summary，然后进入 `SeriesOverviewPage`。
- metadata-only TV 不创建 `MediaFile`，不创建播放源，不进入 Movie detail。
- 无播放源 Episode 播放按钮保持禁用 / 不可用，Episode 详情和用户状态仍可使用。
- TV 仍不进入 Movie AI 推荐、Watch Insights、Watch Profile、persona input 或 recommendation fingerprint。
- 隐藏项独立徽标未在本阶段新增；现有恢复入口可用，独立徽标记录为 Deferred / UI polish。
- 修复了 AI pool / AI perf 临时诊断中的硬编码本机日志路径，改为解析到工作区或应用目录下的 `logs`。
- 未新增 DB 字段，未新增 migration，未执行 database update。

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

## Phase 4.12-post-fix Unknown TV-Like Other Projection

- Media-library normal mode now keeps recognized Movie, recognized TV, and unknown TV-like containers separated by item kind: all-failed no-TMDB TV-like containers are projected as `Other` Series items instead of loose unknown Season items.
- Batch mode still expands to Season / grouped item granularity so unresolved unknown ranges remain selectable for future correction.
- Failed Movie placeholders and orphan single files remain `Other` and are not converted into TV containers by display projection alone.
- TV remains excluded from Movie Discovery AI recommendation input, Watch Insights, Watch Profile, persona input, and recommendation fingerprints.
- No migration was added and database update was not executed.

## Phase 4.8 TV Discovery Extension

- 影片发现页仍保持三个顶层 Tab：影片搜索、榜单、AI 推荐。
- 影片搜索 Tab 增加 Movie / TV 二级切换；默认仍是 Movie。
- Movie 搜索和人物搜索路径保持原逻辑。
- TV 搜索调用 TMDB TV search，并显示独立 TV Series 卡片。
- 榜单 Tab 增加 Movie / TV 二级切换；默认仍是 Movie。
- Movie 榜单路径保持原逻辑。
- TV 榜单接入 TMDB TV popular、top-rated、trending day、trending week。
- TV 榜单显示 Series 级排名，不伪造 Season 榜单。
- TV Series 卡片海报使用现有海报缓存行为。
- 已入库 Series 跳转 `SeriesOverviewPage`；未入库 Series 仅显示 TV 外部详情后置提示。
- TV 不进入 AI 推荐、Watch Insights、画像、观影人格或推荐 fingerprint。
- 未新增 DB 字段，未新增 migration。

## Phase 4.8 Bugfix TV Discovery Parity

- TV 榜单 UI 对齐电影榜单：第一页第 1 名为一行大卡，第 2-21 名为两列；后续页每页 20 部两列展示。
- TV 榜单上一页 / 下一页在加载中禁用，失败后恢复可用并保留错误提示。
- TV 搜索增加基础筛选区：TV 类型、地区、入库 / Season 状态、顺序、排序、年代和语言。
- TV 类型筛选使用 `TmdbTvGenreMapper`，不复用电影类型表。
- TV 搜索 / 榜单卡片通过 TMDB Series detail 显示 `共 N 季`，加载中显示季数加载状态，失败显示季数未知。
- TV Discovery 页面文案已补充电视剧搜索语义，人物搜索仍限定在 Movie 模式。
- 未入库 TV 详情页、Series 全季 metadata 补全、无播放源 Season 展示、TV 修正入口和 TV 功能缺口大审查仅记录为后续事项。
- 未新增 DB 字段，未新增 migration。

## Phase 4.10 TV Discovery Hydration Update

- TV search / ranking Series clicks now call the TV metadata hydration path before opening `SeriesOverviewPage`.
- Not-in-library TV Series no longer use the deferred external-detail placeholder.
- Hydration creates or updates TV metadata only: `TvSeries`, all TMDB Seasons including Season 0, and all TMDB Episodes.
- Hydration does not create `MediaFile`, does not fabricate playback sources, and does not route TV into Movie detail.
- Metadata-only TV Series remain not-in-library from a playback-source perspective until active Episode sources exist.
- Movie search, Movie rankings, and AI recommendations remain separate from TV.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.1 Metadata-only TV Library Visibility Note

- Discovery-hydrated TV metadata remains allowed, but pure metadata-only Series with no active Episode source and no Season state no longer pollute the default media-library list.
- Source-backed Series still appear normally and expand into all known Seasons in batch mode.
- Metadata-only Seasons with explicit user state can be surfaced in library-related views and participate in watched / unwatched batch operations.
- Phase 4.10.1 originally skipped source-less TV Seasons and not-in-library Movies with a no-source message; Phase 4.10.4 supersedes this with `LibraryVisibilityState.Hidden`.
- Batch delete record remains software-record deletion only and does not delete local or WebDAV files.
- Movie search, Movie rankings, TV search, TV rankings, and AI recommendations remain separated by media type.
- Did not add a migration.
- Did not execute database update.

## Phase 4.10.3 Library Visibility Schema

- Added `LibraryVisibilityState` as schema-only groundwork for source-less media-library visibility.
- Added the field to Movie and TV Season user-state rows with `Auto = 0` default.
- Discovery behavior is unchanged: opening not-in-library TV hydrates metadata but does not mark rows `Visible`.
- Discovery labels and filters are unchanged in this phase.
- AI recommendations and recommendation fingerprints are unchanged.
- Database update was not executed.

## Phase 4.10.4 Media Library Visibility Semantics

- Connected `LibraryVisibilityState` to media-library query and remove behavior.
- Media-library filters now separate visible rows by active source state: all visible, with active source, without active source.
- Source-less Movie / TV Season remove writes `Hidden` instead of skipping.
- Source-backed remove still removed app source rows in Phase 4.10.4; Phase 4.10.4f supersedes this with hide-only behavior.
- Discovery remains metadata-only for not-in-library TV clicks and does not write `Visible`.
- AI recommendations, Watch Insights, and recommendation fingerprints are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.10.4d Visibility Tail Bugfix

- Discovery search / ranking filters now use source wording: `全部`, `有播放源`, and `无播放源`.
- Discovery Movie and TV cards now show `有播放源` / `无播放源` as source status.
- Add-to-library-specific Discovery wording remains deferred until Phase 4.10.5.
- Pure visibility-only Movie rows are filtered out of movie AI/profile/statistics/recommendation fingerprints and fallback external candidates.
- Real-state source-less Movie rows remain eligible movie preference inputs.
- TV remains excluded from AI recommendations, Watch Insights, profile/persona inputs, and recommendation fingerprints.

## Phase 4.10.4f Hide Library Items Without Removing Sources

- Movie and TV Season remove-from-library now writes `LibraryVisibilityState.Hidden` only.
- Active Movie and Episode `MediaFile` rows are not marked deleted by remove-from-library.
- Hidden source-backed rows are excluded from media-library `全部`, `有播放源`, and `无播放源`.
- Discovery remains source-oriented and can still show Hidden source-backed items as `有播放源`.
- Old `IsDeleted` source rows are not automatically restored; rescanning existing files remains the safe recovery path to verify later.

## Phase 4.10.5 Add-to-Library Actions

- Discovery Movie rows now expose add / restore actions when they are not media-library-visible.
- Discovery TV Series rows now expose add / restore actions and write `Visible` for all known Seasons after metadata is available.
- Add-to-library writes media-library visibility only; it does not set want-to-watch, favorite, not-interested, watched, or source rows.
- Hidden source-backed rows can be restored without changing active playback sources.
- TV Discovery remains excluded from `AiRecommendationItem`, Watch Insights, profile/persona inputs, and recommendation fingerprints.
- No new migration was added.
- Database update was not executed.

## Phase 4.10.5b Restore Strategy And Series Actions

- Discovery restore actions now use source / state-aware restore instead of blindly writing `Visible`.
- Hidden Movie / TV rows with active source or real current state restore to `Auto`; source-less no-state rows restore to `Visible`.
- Explicit add-to-library remains `Visible` and remains separate from preference state.
- SeriesOverview now keeps a series-level action area visible for one-season, partial, hidden, and all-visible series states.
- TV Discovery navigation and hydration loading contention is superseded by Phase 4.10.6.
- No new migration was added.
- Database update was not executed.

## Phase 4.10.6 TV Discovery Navigation And Hydration

- TV search / ranking Series clicks now use summary-first metadata hydration before opening `SeriesOverviewPage`.
- Summary-first hydration ensures `TvSeries` and TMDB Season summaries, including Season 0 / Specials, without blocking on every Season detail / Episode metadata request.
- The summary path verifies all TMDB Season summaries exist locally before skipping, so stale one-season local Series can still be completed before navigation.
- TV card repeat-click, TV search pagination, TV ranking pagination, and TV trend-time switching are disabled while a TV Series navigation request is active.
- `SeriesOverviewPage` keeps full background hydration for eventual Episode metadata completion.
- `TvSeasonDetailPage` can hydrate missing current-Season Episodes on demand and refresh the Episode list.
- The deleted-Season reopen case from TV search / ranking is handled by recreating Series / Season summary metadata.
- Discovery still does not write playback sources, preference state, TV AI rows, Watch Insights rows, or recommendation fingerprint input.
- No new migration was added.
- Database update was not executed.

## Phase 4.11 TV Scan Identification Note

- Default scan can use the configured AI service only for sanitized TV directory range hints.
- TV range hints are not Discovery results, do not create `AiRecommendationItem`, and do not change TV search / ranking cards.
- Strong TV ranges are protected from silent Movie fallback; files outside TV ranges continue to use the Movie identification path.
- Active cross-type correction UI is deferred to Phase 4.12.
- TV remains excluded from AI recommendations, Watch Insights, Watch Profile, persona inputs, and recommendation fingerprints.

## Phase 4.11b TV Scan AI Schema Boundary

- Default scan AI range schema is reduced from `episode-files-v1` to `directory-ranges-v1`.
- AI returns Series / Season directory ranges only and does not return per-file `episodeFiles`.
- Local TV parser continues to parse Episode numbers inside accepted TV ranges.
- Long-timeout probing against the same active-video set showed valid JSON with a much smaller assistant response, but the request can still exceed the production short timeout on large trees.
- Discovery surfaces remain unchanged; this is scan identification plumbing only.
- No new migration was added.
- Database update was not executed.

## Phase 4.11c TV Scan AI Summary Optimization

- Default scan AI directory summaries now use direct-video-only samples.
- Child folder names are emitted separately from sample video files.
- Sample count is 10% of direct videos, clamped to 1-5.
- AI output uses short evidence values instead of natural-language reasons.
- Long-timeout probing showed a smaller prompt and shorter duration than Phase 4.11b, while preserving valid JSON output.
- Discovery surfaces remain unchanged; this is scan identification plumbing only.
- No new migration was added.
- Database update was not executed.

## Phase 4.11d TV Scan Generalization And Match Validation

- TV scan strong context now requires multiple independent directory / episode / season evidence signals.
- Single title-number, bare numeric, or explicit episode-like evidence is treated as weak until directory context supports it.
- TV query generation records query source and rejects generic season / count / release-only queries before TMDB matching.
- TV candidate conflicts can downgrade automatic matches to placeholder / review instead of relying only on top title similarity.
- Movie fallback is blocked only when TV risk evidence is present; obvious Movie paths without TV risk continue through Movie identification.
- Discovery UI, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-prep AI-on-Uncertain Candidate Preparation

- Default production scan no longer calls the full AI directory range request.
- The scan-only AI directory range code remains available for diagnostic / optional experiment use.
- Local TV-risk analysis emits log-only `aiCandidateRanges` for future AI-on-uncertain processing.
- TV candidate conflict and placeholder paths emit candidate-range diagnostics without calling AI.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-prep-2 Scan Candidate Range Quality Fixes

- Movie scan identification now blocks numeric-only / low-information queries from automatic TMDB binding and records a Movie placeholder instead.
- TV-risk pre-analysis now catches total-count / season-range hints combined with multiple sequential numeric direct videos so those files do not silently fall into Movie fallback.
- `aiCandidateRanges` are refined for later AI-on-uncertain processing: successful strong TV ranges are not emitted just because they are strong, repeated ranges are merged by sanitized directory, and query diagnostics are split into usable / rejected / noisy buckets.
- Local directory hints no longer use AI-oriented names unless the hint came from an actual AI response.
- Full AI directory range analysis remains disabled by default.
- No AI call is made for uncertain ranges in this phase.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-prep-3 Scan Candidate Input Finalization

- Movie scan identification now applies broader generic release / audio / source cleanup before Movie TMDB search.
- Movie cleanup remains pattern-based and does not add specific movie title, release group, folder, or TMDB ID special cases.
- Movie diagnostics record raw title, cleaned title, removed noise category, query quality, and low-information blocking state.
- TV candidate query diagnostics keep usable, noisy, and rejected buckets separate for later AI-on-uncertain input.
- TV placeholder / conflict / unresolved ranges emitted during TV identification are merged into a final run-level `aiCandidateRanges` summary.
- The final summary is sanitized, de-duplicated by directory, and includes risk tags, query buckets, conflict counts, and fallback-block counts.
- Same-name / version conflicts remain downgrade-only and are not locally forced into a specific match.
- Full AI directory range analysis remains disabled by default, and this phase does not call AI for uncertain ranges.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-prep-4 Scan Auto-Bind Validation Gates

- Movie scan identification no longer applies `NeedsReview` candidates to Movie metadata. Only clear `Matched` results auto-bind during default scan.
- Movie low-confidence, dirty-query, low-information, and conflict-like outcomes remain placeholders / future AI-candidate diagnostics.
- Movie diagnostics record `movieResultStatus`, `movieAutoApply`, and `movieAutoApplyBlockedReason`.
- Movie title cleanup received a small generic release/source/audio/subtitle extension, including spaced source tokens and symbol-heavy trailing release tails.
- TV scan identification now applies the same auto-bind gate: `NeedsReview`-level candidates become placeholders / AI-candidate diagnostics instead of matched Seasons.
- TV localized-title exact-match candidates are downgraded when a generic version qualifier / original-title conflict is detected.
- Full AI directory range analysis remains disabled by default, and this phase does not call AI for uncertain ranges.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e AI-on-Uncertain Directory Assistance

- Scan AI-on-uncertain now uses only final sanitized `aiCandidateRanges`; full AI directory range analysis remains disabled.
- The AI prompt returns directory / title / season hints keyed by `inputRangeId`, not `episodeFiles` or per-file Season / Episode decisions.
- Low-confidence, `needsReview`, unknown-range, malformed, empty, timeout, or provider-error AI results preserve local placeholders / candidate ranges.
- Accepted AI hints are added as `ai-on-uncertain` scan hints and then validated through the existing local TV parser, TMDB query, candidate conflict downgrade, and auto-bind gates.
- AI does not write records directly and does not change Movie `NeedsReview` or TV conflict behavior.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-fix-1 Scan AI Hint Mapping

- AI-on-uncertain now applies accepted hints primarily by `inputRangeId`.
- AI-returned directory hints are auxiliary diagnostics and no longer need to exactly match sanitized local directory strings.
- Directory mismatch is logged without discarding the mapped range; fuzzy path matching remains only as a fallback for unknown IDs.
- AI hints still do not write records directly and still pass through local parser, TMDB validation, and safety gates.
- Media-library batch mode adds current-list select-all and clear-selection helpers for scan retesting.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11e-fix-2 Scan AI Candidate Range File Binding

- AI candidate ranges now carry runtime `MediaFileIds` so accepted AI-on-uncertain hints can recover their covered files without relying on sanitized path parent/child matching.
- Candidate range merge / dedupe now merges `MediaFileIds` with risk tags, queries, conflict reasons, and sample diagnostics.
- AI hint application resolves files by `inputRangeId -> MediaFileIds` first and uses sanitized path matching only as fallback.
- Diagnostics report `rangeMediaFileCount`, file resolution method, and aggregate counts for ranges with / without files and applied-by-media-file-ids.
- AI hints still do not write records directly and still pass through local parser, TMDB validation, and safety gates.
- Movie Discovery, TV Discovery, ranking cards, AI recommendation semantics, Watch Insights, and media-library visibility are unchanged.
- No new migration was added.
- Database update was not executed.

## Phase 4.11f Scan AI Refined Title Lookup

- AI-on-uncertain now requests refined TV lookup titles for uncertain scan ranges instead of generic directory title hints.
- AI still does not receive TMDB top-N candidates, select TMDB candidates, return episodeFiles, or write records.
- Local TV identification searches TMDB TV with the refined title and evaluates top1 with a lightweight safety gate before automatic binding.
- Safety-gate failures remain placeholder / NeedsReview / ai-candidate and do not affect Movie Discovery, AI recommendations, Watch Insights, or media-library visibility.
- No new migration was added.
- Database update was not executed.

## FD-2.1 / FD-2.2 Final Log Audit

收尾日志与运行侧检查：

- `dotnet test MediaLibrary.sln --no-build --verbosity normal` 执行成功，VSTest 目标 0 warning / 0 error；当前解决方案未发现独立测试项目输出。
- 日志落点检查：
  - 工作区 `logs\` 存在 `ai-perf-debug.log`、`ai-pool-debug.log`、`mpv-playback.log`、`video-cache-debug.log`、`watch-completion.log`。
  - `%LocalAppData%\MediaLibrary\logs` 不存在。
  - `src\MediaLibrary.App\bin\Debug\net8.0-windows\logs` 不存在。
  - `src\MediaLibrary.Core\bin\Debug\net8.0\logs` 不存在。
- 近窗口日志检查：
  - `2026-05-13 04:00:00` 后 `ai-pool-debug.log`、`mpv-playback.log`、`video-cache-debug.log`、`watch-completion.log` 未发现 error / exception / failed / fatal / 失败 / 错误 / 异常关键词。
  - `ai-perf-debug.log` 仅发现两条 AI 推荐 preview `TaskCanceledException` cancellation 记录，属于前台刷新/预览被取消的协调噪声，不属于影片发现搜索或榜单路径。
- Windows Application 事件日志检查：
  - 最近 6 小时未发现 `MediaLibrary`、`.NET Runtime` 或 `Application Error` 相关应用崩溃事件。
- XAML / ViewModel 绑定回查：
  - 未发现旧的 `LoadMoreSearch`、`LoadMoreRankings`、`IsRankingLoadingMore`、`ScrollChanged` 榜单滚动加载绑定残留。
  - 新的搜索 / 榜单状态覆盖层绑定均能在 `MovieDiscoveryViewModel` 找到对应属性。

数据库：

- 未新增 DB 字段。
- 未新增 migration。

## Phase 4.11f-fix-1 Scan AI Refined Title Update

- AI-on-uncertain prompt now asks for the best clean TV `refinedSeriesTitle` instead of using `needsReview` as an auto-apply judgement.
- Non-empty AI refined titles proceed to local TMDB TV lookup even when AI marks the range as low confidence or `needsReview=true`.
- The AI refined lookup path accepts TMDB top1 directly when a result exists and still keeps AI out of direct database writes.
- Original-title, localized-title, year, version, and top-candidate conflict checks are no longer hard blockers for this AI refined path.
- Empty refined title and TMDB no-result preserve placeholder / `ai-candidate`.

## Phase 4.11f-fix-2 Scan AI Original-Language Title Update

- AI-on-uncertain prompt now asks for original-language TV titles first, with English and localized titles as fallback aliases.
- Local scan refined lookup uses the AI title priority `originalLanguageTitle -> searchTitle -> englishTitleHint -> localizedTitleHint -> legacy refined title`.
- Diagnostics record the selected search title, title source, missing original-language title state, and English / localized fallback use.
- AI still does not receive TMDB top-N candidates, select TMDB candidates, return episodeFiles, or write records directly.
- This is scan plumbing only; Movie Discovery, AI recommendations, Watch Insights, and media-library visibility are unchanged.

## Phase 4.11f-perf-1 Scan Performance Update

- The post-AI TV retry now runs only for AI affected `MediaFileIds`, not the full scan set.
- A per-scan TMDB search cache deduplicates repeated TV and Movie search queries during scan identification.
- TV and Movie caches are separate, runtime-only, and not persisted.
- This is performance plumbing only. Movie Discovery UI, ranking cards, recommendation AI inputs, Watch Insights, and media-library visibility semantics are unchanged.

## Phase 4.11f-fix-3 Scan Closeout Guards

- AI refined TV top1 auto-apply now checks only AI `seriesYearHint` against TMDB Series first-air year and blocks only differences greater than two years.
- `seasonYearHint` is logged but does not block Series matching.
- Movie title parsing now normalizes HTML entities, trims dangling trailing punctuation, and removes only generic release/source/audio/subtitle noise such as `3D` quality tokens.
- Movie placeholder failures are analyzed after Movie identification. Files in the same direct parent folder with at least three strictly consecutive episode-like numbers are logged as TV-like placeholder ranges.
- The placeholder grouping is diagnostic / future-correction data only. It does not create Series, Season, Episode, or new schema.
- Movie Discovery UI, ranking cards, recommendation AI inputs, Watch Insights, and media-library visibility semantics are unchanged.
- No new migration was added and database update was not executed.

## Phase 4.11f-fix-4 Grouped Placeholder Projection

- Consecutive numbered Movie placeholder failures are now projected as media-library `Other` / TV-like placeholder ranges through runtime read models.
- The grouped range keeps `MediaFileIds`, file count, direct parent display, number span, samples, and reason tags for later correction or manual aggregation.
- Grouped Movie placeholder files are filtered out of the normal Movie scatter list; ungrouped Movie placeholders remain unchanged.
- The grouped range is unresolved data only. It does not create Series, Season, Episode, TMDB binding, or new schema.
- Movie placeholder grouping diagnostics now report query-time read-model persistence instead of `log-only`.
- AI refined year-gate logging is made consistent when the gate blocks auto-apply, and Movie cleaner receives only a tiny generic release-context cleanup.
- Movie Discovery rankings, recommendation AI inputs, Watch Insights, and media-library delete / visibility semantics are unchanged.

## Phase 4.11f-fix-5 Other Category Completion

- Media-library `Other` is now the display category for unrecognized / placeholder / NeedsReview / type-uncertain rows, not only grouped TV-like placeholders.
- Recognized Movie rows stay in Movie; ordinary failed Movie placeholders are projected into `Other`.
- Grouped TV-like placeholder ranges now persist through existing unidentified TV rows: a no-TMDB `TvSeries`, failed `TvSeason`, and failed `TvEpisode` rows are created without binding TMDB or marking recognition successful.
- The grouped files are moved from failed Movie placeholders to the unidentified Episodes, so playback, watched / unwatched marking, detail-page marking, hide / restore, and delete-record operations reuse the normal Season / Episode paths.
- Normal media-library mode now appends failed unidentified Seasons into `Other`; all-failed placeholder Series are not surfaced as recognized TV summaries.
- Movie placeholder grouping now recognizes conservative bracketed episode-number segments, still limited to the same direct parent folder, strict contiguous numbering, and at least three failed placeholders.
- Unidentified Episode titles use the original source file name, not the cleaned Movie query.
- The visible recognition-status filter is hidden from the main library UI; backend status data is retained for future correction workflows.
- Movie Discovery rankings, recommendation AI inputs, Watch Insights, and scan binding rules are unchanged.

## Phase 4.11f-fix-6 Scan Sequence Parsing Update

- TV scan preanalysis now admits repeated title+number folders into TV-like uncertain candidate ranges before Movie fallback, but still does not auto-bind a single title+number file.
- Multi-episode false positives are reduced by requiring plausible explicit ranges; years and quality numbers after a normal episode marker are not treated as second episodes.
- Unsupported TV parsing diagnostics now include sanitized sample names, match kind, detected range, and pattern where available.
- Explicit TV episode markers support four-digit episode numbers; bare four-digit filenames remain excluded from global TV parsing.
- Movie placeholder grouping now recognizes numeric filenames with quality/source/codec tails and can merge same-parent mixed episode patterns into one strict contiguous run.
- This is scan plumbing only. Movie Discovery rankings, recommendation AI inputs, Watch Insights, media-library delete / visibility semantics, and Movie AI classification behavior are unchanged.

## Phase 4.11f-fix-7 Scan Apply / Ignored Diagnostics Update

- Verified title+number TV-like sequences now apply during TV episode parsing after AI / TMDB validation, so those ranges no longer stop at candidate admission only.
- Generic TV parse failures no longer reuse the multi-episode unsupported reason unless an explicit multi-episode range is detected.
- Local and WebDAV scan discovery now logs ignored files by reason and extension with sanitized samples.
- Ignored extension diagnostics are evidence-only; this phase does not add formats to the video whitelist.
- Movie Discovery rankings, recommendation AI inputs, Watch Insights, media-library delete / visibility semantics, and Movie AI classification behavior are unchanged.

## Phase 4.11f-fix-8 Orphan / Other Category Update

- Orphan video `MediaFile` rows that have no Movie binding and no Episode binding now appear as `Other` unrecognized file items instead of disappearing from every media-library category.
- Unrecognized file items use their original safe file names as display titles; Movie cleaner output remains search-only and is not shown as the primary unresolved title.
- The scan closeout grouping pass now also considers historical orphan rows and newly unresolved single-source rows under the enabled scan paths. Strict same-parent contiguous episode-like runs can become unidentified TV Seasons / Episodes without TMDB binding.
- Bracketed episode segment sequences are promoted to TV-like uncertain candidates before Movie fallback, so they can be reviewed by AI-on-uncertain instead of only appearing after Movie placeholder grouping.
- `.rmvb` is accepted as a video extension and `.sup` as a subtitle extension. Playback / rendering support for those formats remains separate from scan admission.
- Movie Discovery rankings, recommendation AI inputs, Watch Insights, media-library delete / visibility semantics, Movie AI classification behavior, and TMDB thresholds are unchanged.

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
# Phase 4.11f-fix-9 Scan Closeout Notes

- Placeholder / orphan grouping now understands additional verified TV-like sequence shapes before they reach Movie fallback: fansub bracket numbers, bracket episode segments reused at apply time, and leading-number-title files with explicit season context.
- Long-running numeric unknown ranges can be grouped with a small gap ratio, but missing numbers are diagnostics only and do not fabricate Episode rows.
- A bare-number movie collection guard keeps short `1..5` collection-like folders without TV evidence out of unidentified TV season projection.
- Scan warnings from TV.Parse no longer inflate scan `ErrorCount`; final scan diagnostics log raw warning count, deduplicated warning count, and `warningsIncludedInErrorCount=false`.
- Scan discovery now ignores macOS AppleDouble `._*` resource fork files before media type detection, preventing `._*.mkv` from becoming Movie placeholders or AI candidates.
- Movie discovery, Movie AI recommendation inputs, Watch Insights, and normal Movie fallback thresholds are unchanged.

# Phase 4.11f-fix-10 TV Part Parsing Note

- TV verified title-number parsing now preserves dotted part markers such as `Pt.2` instead of truncating them as file extensions.
- Part hints are recorded for unresolved TV correction but are not converted into Movie fallback rules or Movie discovery behavior.
- Movie discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, delete-record semantics, and visibility semantics remain unchanged.

# Phase 4.11f-fix-11 Safe Part Offset Note

- TV part offset is now allowed only after the same TMDB Series and Season are established and a previous sibling part has already produced a safe contiguous episode range.
- The rule is generic and does not add Movie-side title, folder, or TMDB exceptions.
- Unsafe part ranges remain unresolved / pending correction instead of falling back into Movie matching.
- Movie discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, delete-record semantics, and visibility semantics remain unchanged.

# Phase 4.11f-fix-11-hotfix Part Query Boundary

- TV structural part-only queries are now rejected before TMDB search, including AI refined title sources.
- `Part`, `Pt`, `S Part`, `Part 2`, and season / part / number-only variants are not Movie or TV title evidence.
- The automatic sibling part offset apply path is disabled until a safer TV-side redesign is implemented.
- Dotted part parsing remains TV correction context only and does not change Movie fallback thresholds, Movie Discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, delete-record semantics, or visibility semantics.

# Phase 4.11f-fix-13 AI-on-uncertain Batching

- Scan AI-on-uncertain requests are split into at most 3 concurrent batches with one retry per failed batch.
- Successful TV AI batches still feed the existing TMDB validation path; failed batches keep their ranges unresolved.
- Partial AI batch failure is a scan warning instead of a scan error.
- Movie Discovery ranking/search pages, Movie recommendation AI inputs, Watch Insights, Movie fallback thresholds, delete-record semantics, and visibility semantics remain unchanged.

# Phase 4.11f-fix-14 Safe TV Part Offset Boundary

- Safe part sequence offset remains a TV identification concern and does not change Movie discovery or Movie fallback thresholds.
- Structural part-only strings are rejected before TV TMDB lookup and are not Movie title evidence.
- Later TV parts can bind only after TMDB Series / Season confirmation plus previous contiguous episode evidence; otherwise they stay unresolved / pending correction.
- No Movie ranking/search, Movie recommendation AI, Watch Insights, delete-record, or visibility semantics changed.

# Phase 4.11f-fix-14-hotfix Part Candidate Lookup Boundary

- Unsupported-only TV part candidates can now use applied AI refined / original-language titles for TMDB Series confirmation before safe offset evaluation.
- This remains TV-only plumbing. It does not change Movie discovery thresholds, Movie fallback behavior, Movie recommendation inputs, Watch Insights, delete-record semantics, or visibility semantics.
- Structural part-only strings remain rejected and are not Movie title evidence.

# Phase 4.11g TV Scan Closeout Boundary

- TV scan final acceptance completed as a scan and media-library closeout, not a Movie Discovery feature change.
- AI-on-uncertain batching, TV retry scoping, safe part offset, orphan `Other` projection, unidentified Seasons, warning/error semantics, `.rmvb` / `.sup` scan admission, and macOS `._*` ignores are accepted for the TV scan baseline.
- Movie Discovery search, ranking, Movie AI recommendation inputs, Watch Insights, Movie fallback thresholds, delete-record semantics, and visibility semantics are unchanged.
- Unrecognized Movie placeholders remain unresolved data under `Other`; recognized Movie rows remain the only rows in the Movie category.
- Phase 4.12 / 4.13 will own correction UI, Episode detail, multi-source handling, manual regrouping, and complex anime / special content workflows.

# Phase 4.12c-fix-4 Movie Detail Lazy Probe Boundary

- Movie detail now queues a background best-effort media probe check for the current Movie's active sources after the detail page loads.
- The lazy probe path is scoped to the current Movie detail source ids and capped at 10 candidates; it does not scan or enqueue the full library.
- Movie detail now refreshes automatically from probe status-change notifications when one of the currently displayed sources changes probe state.
- Movie source-row probe status copy now uses explicit stage language for waiting, running, completed, failed, unavailable, and skipped states.
- Movie detail source rows now include an `立即探测` action. It validates the source belongs to the current Movie and force-probes only that selected source.
- Movie detail disables the source-row `立即探测` action while the selected source is probing. Detail-page auto probe temporarily disables checked sources during candidate evaluation, then keeps only queued / pending sources disabled and restores skipped sources.
- Scan-time media-probe enqueue is disabled so large scan runs do not occupy the probe queue ahead of the current Movie detail page. Movie probing now enters through detail lazy probe or manual source-row probe.
- Failed Movie placeholders and orphan carriers reuse the same Movie detail behavior.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights, Movie fallback thresholds, delete-record semantics, visibility semantics, and correction flows are unchanged.

# Phase 4.12d Probe Diagnostics Boundary

- Movie detail source rows now distinguish successful probes with no readable technical fields from normal successful metadata updates.
- Probe lifecycle diagnostics now include graceful cancellation / abandoned-queue and worker exception records.
- Probe and ignored-file sample diagnostics now use extension plus stable hash fingerprints instead of raw sample file names.
- Movie detail lazy probe eligibility, manual probe actions, scan-time probe disablement, Movie Discovery ranking/search/recommendation inputs, Watch Insights, correction flows, visibility semantics, schema, and migrations are unchanged.

# Phase 4.12e Unidentified Movie Reset Guard

- Movie detail now disables `重置为未识别` when the loaded Movie is already a failed / unidentified placeholder.
- This covers Other / orphan single-source carriers that reuse Movie detail.
- `MovieManagementService.ResetMediaFileToUnidentifiedAsync` also rejects already-unidentified Movies, so non-UI calls cannot create another placeholder for the same already-unidentified source.

# Phase 4.12e-fix Rescan Reattach Boundary

- Rescan reattach now runs before placeholder / orphan grouping so reset-to-unidentified TV sources can safely return to an existing matched Episode when the same-source directory context is unambiguous.
- Movie reattach candidates are logged but not automatically attached in this fix; automatic Movie source reattach remains deferred because title / year matching without a prior-association tombstone can be ambiguous.
- Delete-record semantics are unchanged: deleting records clears XFVerse software history, and later scans treat the physical file as new or deleted-reappeared.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights, Movie fallback thresholds, visibility semantics, schema, and migrations are unchanged.

# Phase 4.12e UI Semantics Follow-up

- Movie detail now labels the source reset action as `从当前电影拆分`; the underlying operation still moves the selected source out of the current Movie into unidentified carrying without deleting real files.
- Failed / unidentified Movie placeholders, including orphan carriers shown through Movie detail, remain disabled for this action.
- Episode detail uses the parallel `从当前集拆分` wording. Failed / unidentified Episodes now also allow single-source detach so wrongly auto-grouped TV sources can return to Other; Movie failed placeholders and orphan carriers remain disabled for Movie-side split.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights, Movie fallback thresholds, visibility semantics, schema, and migrations are unchanged.

# Phase 4.12g Movie Detail Regression Boundary

- Movie detail source split semantics remain unchanged: `从当前电影拆分` moves the selected source into unidentified carrying without deleting real files.
- The diagnostic message for Movie source split now reports retained history / progress instead of the older resume-cleared wording.
- Movie detail playback, default source, source probe, watched state, Movie Discovery ranking / search / recommendation inputs, Watch Insights, visibility semantics, schema, and migrations are unchanged.

# Phase 4.12h Movie Detail Closeout Boundary

- Phase 4.12 closes with Movie detail source split wording set to `从当前电影拆分`.
- Failed Movie placeholders and orphan unknown carriers remain the Movie-side unidentified detail carrier. The split action stays disabled there because an orphan carrier has no multi-source current Movie boundary to split from.
- The underlying Movie split operation still detaches the selected source from the current Movie into unidentified carrying without deleting real files or changing Movie Discovery, Watch Insights, recommendation inputs, ranking, search, schema, or migration behavior.

# Phase 4.12-post Emergency Unknown TV Grouping Boundary

- TV-like failed Movie placeholder grouping now uses strict unknown TV Series / Season grouping keys before reusing existing no-TMDB unknown containers.
- Special / non-regular TV directories are skipped by automatic unknown append and grouped placeholder persistence instead of being absorbed into regular numbered unknown Seasons.
- Reused unknown TV containers preserve existing names; later placeholder ranges no longer overwrite the Series / Season display names.
- Movie Discovery ranking, Movie matching, Movie recommendations, Watch Insights inputs, Movie detail source split semantics, schema, and migrations are unchanged.
- Existing failed Movie placeholders may remain visible under Other when the conservative TV grouping safety gate skips them; Phase 4.13 correction workflows should handle manual grouping / correction.

# Phase 4.12-post Episode Split Boundary Follow-up

- TV Episode detail now allows `从当前集拆分` for single-source failed / unidentified Episodes, so an incorrectly auto-grouped TV source can be detached back to Other without deleting the real file.
- This is a TV Episode source-management boundary change only. Movie failed placeholders, orphan unknown carriers, Movie Discovery ranking / search, Movie recommendations, Watch Insights inputs, Movie matching, schema, and migrations are unchanged.

# Phase 4.13a Single Source Correction Boundary

- Movie detail now routes per-source `修正信息` through the unified single-source correction flow. Selecting a source switches to the correction tab, and clicking a Movie candidate applies the correction directly.
- The Movie target path still uses the existing Movie metadata binding logic; when the target Movie already has sources, the corrected source is appended as an additional playback source.
- The corrected source becomes the target Movie default source. If it was the old Movie default source, the old Movie recalculates default source with the local-first fallback strategy.
- Candidate-click correction now yields the UI thread, runs the apply path off the WPF dispatcher, and uses a 45-second timeout so slow TMDB calls return an error state instead of leaving the app apparently frozen.
- Movie single-source correction no longer waits for non-critical OMDb rating fetches inside the transactional apply path. TMDB metadata remains the source of the corrected Movie identity.
- Additional correction phase logs record Movie detail load, DB transaction start, DB apply, and commit without full paths or credentials.
- The correction panel now resets the target type before exposing the selected source, preventing first-entry UI flicker between TV and Movie target fields.
- Movie detail source correction resets to `修正为电影`, including failed Movie placeholders and orphan carriers. Episode detail owns the default `修正为电视剧集` behavior for unidentified Episodes.
- The TV Episode target path clears `MovieId`, binds `EpisodeId`, preserves the selected `MediaFile`, probe fields, and subtitle bindings, and does not migrate Movie collection states to TV.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights inputs, Movie fallback thresholds, delete-record semantics, schema, and migrations are unchanged.
- Phase 4.13a does not add batch AI correction, manual grouping, unknown Season target selection, ignore / blacklist, or historical data cleanup.

# Phase 4.13a-fix Target-kind Constrained AI Correction Boundary

- Movie detail single-source correction AI assist now reads the selected target kind before generating search terms.
- Movie target uses only the Movie search suggestion and Movie correction path; TV candidates are not shown or applied from that target.
- TV Episode target uses only the TV Series search suggestion and TV Episode correction path; Movie candidates are not shown or applied from that target.
- Episode detail now exposes the same target-kind constrained AI assist behavior as Movie detail.
- Detail page load returns to playback sources instead of retaining the correction tab.
- Same-detail refreshes from media probe completion preserve the user's current tab and selected correction source.
- The correction target selector ignores mouse-wheel changes while closed, avoiding accidental Movie / TV target switches during form scrolling.
- TV Episode AI assist can fill Season / Episode inputs when the AI response or local filename fallback provides a safe single-episode hint.
- Corrected-source default-source rules from Phase 4.13a are unchanged.
- Movie Discovery ranking / search / recommendation inputs, Watch Insights inputs, Movie fallback thresholds, scan identification rules, schema, migrations, and batch AI correction are unchanged.

# Phase 4.13b Join-existing Unknown Season Boundary

- Movie detail single-source correction now includes `加入已有未识别季` as an explicit target for the selected source.
- Choosing this target hides Movie and recognized-TV candidate search and shows a positive episode-number input plus a modal unknown Season picker.
- The picker groups target Seasons under expandable unknown Series rows and sorts Seasons by Season number.
- Applying it clears the source's Movie binding and binds the source to the selected unknown TV Episode.
- If the source was the old Movie default source, the old Movie recalculates default source with the local-first fallback.
- Failed Movie placeholders with no remaining source can be cleaned up by the existing safe orphan cleanup semantics; real files, probe fields, subtitle bindings, and Movie user states are not deleted or migrated.
- Movie Discovery ranking, search, recommendation inputs, Watch Insights inputs, Movie fallback thresholds, scan identification rules, schema, migrations, and batch AI correction are unchanged.

# Phase 4.13b-fix No-source Movie Detail Semantics

- Movie detail now distinguishes three states: external not-in-library candidate, in-library Movie with no active sources, and failed / unidentified placeholder.
- Follow-up semantic tightening: Movie detail no longer keeps a separate not-in-library details section. The page has only source-backed and no-source detail states; library membership is exposed through labels and available actions.
- External TMDB candidates now use the same detail tabs and no-source source-list empty state as local no-source Movies. They remain non-playable, and `Add to library` remains an action rather than a separate page mode.
- Search and ranking cards preserve local `MovieId` status separately from active source status. Clicking a local Movie with zero active sources now opens the local Movie detail instead of the external not-in-library detail.
- Search and ranking card clicks refresh the current local Movie status before routing, so cached TMDB cards do not keep opening the external not-in-library detail after a source correction changes local state.
- Discovery cards now label local Movies with zero active sources as `暂无播放源`; true external candidates remain `未加入媒体库`.
- Movie detail shows `已入库 / 暂无播放源`, disables playback, keeps the library tabs visible, and displays an empty source-list state instead of the external not-in-library explanation.
- Media-library movie queries keep recognized local Movies visible even when active source count is zero. Hidden rows still respect `LibraryVisibilityState.Hidden`.
- Single-source correction and manual correction cleanup now preserve recognized Movies after their last source moves away. The old safe cleanup remains limited to failed / unidentified placeholders with no remaining sources.
- Movie correction clears or recalculates the old Movie default source before the moved source becomes the target Movie default source, avoiding duplicate default-source ownership during cross-Movie correction.
- Movie single-source correction now materializes a newly created target Movie before moving watch-history / collection foreign keys to it, avoiding SQLite foreign-key failure when correcting into a Movie that did not already exist locally.
- External no-source Movie candidates keep the previous not-in-library detail `Add to library` write semantics: the action writes user collection visibility state, not a fake playback source. Movie detail carries that visible-in-library state so reopening shows the `no source / in library` label and hides the add button.
- Media-library projection includes visible external no-source Movie collection rows even without a local Movie record, including rows written by the previous not-in-library detail flow.
- No-source Movie detail hides the correction tab because there is no selected playback source to correct; source-backed failed placeholders still keep single-source correction.
- Recommendations and home / collection entry clicks now open local Movie detail whenever a local `MovieId` exists, even when the item has no active source.
- Failed Movie placeholders and orphan carriers keep the unidentified / 待修正 semantics and are not reclassified as no-source recognized Movies.
- Verification: `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors; migrations diff remained empty.

# Phase 4.13c Manual Unknown Season Aggregation Cross-impact

- Media-library batch mode can now move selected failed Movie placeholder sources into a newly created no-TMDB unknown TV Season.
- This path clears `MediaFile.MovieId`, binds the same `MediaFile` to an unknown Episode, preserves real local / WebDAV files, probe fields, subtitle bindings, and Movie user states, and recalculates old Movie default source with the existing local-first fallback when needed.
- Empty failed Movie placeholders continue to use the existing safe placeholder cleanup semantics. Recognized Movies and no-source Movies are not valid inputs for manual Season aggregation.
- Movie Discovery ranking / search, Watch Insights, recommendations, Movie metadata matching, schema, migrations, and batch AI correction are unchanged.

# Phase 4.13c-fix Manual Aggregation Duplicate Guard Cross-impact

- Manual aggregation now blocks creating a new unknown Series when the entered Series title normalized-equals an existing TV Series title.
- This keeps the Movie-side failed placeholder aggregation path from silently creating duplicate same-name TV containers. Users should use correction to join existing unknown / recognized Seasons.
- Movie Discovery ranking / search, Watch Insights, recommendations, Movie metadata matching, schema, migrations, and batch AI correction are unchanged.

# Phase 4.13d Unknown Season Correction Cross-impact

- Unknown TV Season detail can now move an entire failed / no-TMDB Season into a recognized TMDB Series / Season.
- This path only moves TV Episode sources already inside the unknown Season and does not change Movie discovery ranking, search, recommendations, Watch Insights, Movie metadata matching, Movie source correction, schema, or migrations.
- The operation preserves physical local / WebDAV files, probe fields, subtitle bindings, and Movie / TV user states; no Movie source is moved unless it was already part of the unknown TV Season.

# Phase 4.13e Batch AI Correction Cross-impact

- Media-library batch AI can now move selected Movie / failed Movie placeholder sources through the same single-source correction service used by Movie detail.
- AI can choose Movie or TV Episode for single-source units, but no-source Movies are skipped and no metadata-only correction is performed.
- Movie targets still use TMDB Movie search / apply; TV targets use TV Episode correction and remain outside Watch Insights / AI recommendation inputs.
- The batch path does not change Movie discovery ranking, search card status, recommendation inputs, Movie fallback thresholds, scan identification rules, schema, or migrations.
- SP / OVA / OAD / special / theatrical content is not hard-skipped by local token filters; AI can classify it as Movie or supported TV when confident, while unsupported special mapping remains a later manual special-mapping concern.

# Phase 4.13e Follow-up Source-path Weighted AI Context

- Movie detail and batch single-source AI correction now pass a sanitized playback source path hint together with the file name.
- AI prompts treat the current Movie / TV title and existing Season / Episode metadata as weak hints during correction, while prioritizing source path hints and file names.
- Season-level batch AI samples up to 18 source rows evenly across the Season, covering low, middle, and high Episode numbers when available.
- Batch AI correction now allows up to 3 concurrent AI unit requests; database apply remains serialized to avoid concurrent SQLite write conflicts.
- Batch AI no longer hard-skips locally based on special-content tokens in source file names or folders, avoiding false skips before AI can classify the item.
- Batch AI disables batch controls, exits selection mode as soon as the operation starts, and refreshes back to the normal media-library projection before AI requests run. This keeps the UI behavior aligned with clicking the batch "Done" action first, after selected rows are snapshotted.
- Movie and TV correction AI prompts now require TMDB `original_title` / `original_name` semantics: the official original title/name. This can still be English when the official original title/name is English; translated/localized/marketing aliases are not accepted as substitutes.
- Batch AI result logs now include sanitized returned Movie / TV title fields so title compliance with TMDB original-title/original-name semantics can be audited after a run.
- Batch AI now shows a preparation/progress message immediately after the action starts, before the first AI unit completes.
- Batch AI no longer asks for or trusts AI-returned TMDB ids. Movie and TV corrections now use AI-returned original-title/original-name style titles only, then resolve the final TMDB id through local TMDB search.
- Batch Movie title resolution now ranks TMDB movie candidates by local title/year confidence instead of applying the first result blindly.
- Batch TV title fallback initially required TMDB `OriginalName` equality, but a later follow-up relaxed this local filter to improve correction hit rate while keeping the prompt-level official-name instruction.
- Detail-page and batch TV prompts now explicitly reject English/international aliases for non-English-original TV series. Final-season wording can be used as a season-number clue only when the target TMDB season number is known confidently.
- Batch AI no longer local-skips solely because source context mentions SP / OVA / OAD / special / theatrical wording; AI may classify those as Movie or supported TV when confident, otherwise it returns Skip.
- Path hints are tail-only and sanitized; full local paths, full WebDAV URLs, query strings, and credentials are not sent to the prompt.
- Movie discovery ranking, search card status, recommendation inputs, Watch Insights, scan identification rules, schema, and migrations are unchanged.

# Phase 4.13e Follow-up Batch Delete / Removed Library Cleanup

- Batch delete-record now dispatches by persisted `MovieId` / `SeasonId` before display-state labels, so no-source local Movies and no-source Seasons with existing records can be deleted from batch mode.
- External no-source Movie rows without a local `MovieId` still use the existing collection-record delete path.
- The removed-library panel no longer renders Movie rows inside expandable groups. Only TV Series groups remain collapsible and keep group-level restore / delete actions.
- Latest batch AI log review remained read-only for prompt and recognition logic; no Movie discovery prompt, TMDB matching threshold, scan rule, schema, migration, database update, commit, or push was changed.

# Phase 4.13e Follow-up AI Correction Prompt Tightening

- Movie detail and batch single-source Movie correction prompts now emphasize source file / path evidence over current metadata, and prefer the specific work title from the source over collection, franchise, pack, or parent-folder names.
- Movie AI prompts now explicitly require TMDB `original_title` semantics and tell AI not to return TMDB ids; the app continues to resolve final ids through local TMDB search and validation.
- Romanized / transliterated Movie titles are treated as aliases unless they are the official TMDB original title.
- This follow-up does not change Movie discovery ranking, Movie search card status, recommendation inputs, Watch Insights, scan identification rules, TMDB matching thresholds, schema, migrations, database update, commit, or push.

# Phase 4.13e Follow-up Split No-source Shell Consistency

- Splitting a source from a recognized TMDB Movie now preserves the old Movie record when it becomes no-source, matching single-source correction semantics.
- Empty failed Movie placeholders can still be cleaned up when they have no sources, no history, and no retained user state.
- This keeps "split from current Movie" and "correction moved the last source away" consistent: recognized metadata and user state are retained; real local / WebDAV files are not deleted.
- TV Episode split already preserves Episode / Season metadata; this follow-up did not change TV cleanup or progress rules.

# Phase 4.13e Follow-up Movie Scan Prompt Alignment

- Movie scan / identification AI search prompt now uses the same TMDB `original_title` semantics as Movie detail and batch Movie correction prompts.
- The scan prompt now tells AI not to return TMDB ids, not to substitute translated / localized / marketing aliases, and not to use collection, franchise, pack, or parent-folder titles when a specific work title is present in the source.
- Movie scan AI input now sends the selected source file name plus a sanitized tail-only source path hint instead of a full local path / WebDAV URL.
- This follow-up does not change Movie discovery ranking, Movie search card status, recommendations, Watch Insights, TMDB matching thresholds, scan binding safety gates, schema, migrations, database update, commit, or push.

# Phase 4.13e Follow-up Source Language Prompt Tuning

- Movie scan, Movie detail correction, and batch Movie correction prompts now clarify that source file/path text can identify the work, but the language or script used by the file name must not be treated as proof of TMDB `original_title` language.
- English, localized, or romanized file names are explicitly allowed to point to non-English TMDB original titles; AI should return the actual TMDB `original_title` or skip when it cannot know it.
- This is prompt-only: no alias conversion, TMDB resolver fallback, ranking threshold, schema, migration, database update, commit, or push was changed.

# AI model routing cleanup

- Legacy DeepSeek model names `deepseek-chat` and `deepseek-reasoner` are no longer added as defaults or selectable options; if an old saved setting still uses either name, runtime routing resolves it to `deepseek-v4-flash`.
- Movie scan / tagging and AI recommendation requests explicitly stay on `deepseek-v4-flash` for DeepSeek endpoints with deep thinking disabled.
- Movie detail correction AI and batch AI correction now explicitly use `deepseek-v4-pro` for DeepSeek endpoints without enabling deep thinking / high thinking.
- Watch Profile keeps `deepseek-v4-pro` plus the existing high-thinking behavior.
- Central AI routing diagnostics now record sanitized requested / resolved model, provider kind, request purpose, thinking mode, and override reason. No endpoint credential, API key, local path, or WebDAV URL is logged.
- This routing cleanup does not change prompts, TMDB matching thresholds, scan binding safety gates, correction safety gates, schema, migration, database update, commit, or push.

# Phase 4.13e Follow-up Batch TV Search Hit-rate Relaxation

- Batch AI TV correction still asks AI to return TMDB `original_name` semantics, but no longer rejects a TMDB search result solely because the returned AI series title differs from the top result's `OriginalName`.
- When TMDB TV search returns results, batch AI now accepts the top search result and logs whether the AI series title matched `OriginalName`.
- Empty TMDB TV search results still skip the unit. Existing requirements for target kind, Season number, Episode number, no empty Episode creation, and correction safety gates remain unchanged.

# Phase 4.13e Follow-up Detail TV Correction Candidate Grouping

- Movie detail single-source correction to TV Episode now uses the same collapsible TMDB Series / Season candidate list as Episode detail.
- Applying at the Series row keeps the manually entered Season/Episode numbers; applying at a Season row overwrites only the Season number and keeps the manually entered Episode number.
- Detail-page TV AI assist now clears missing Season/Episode values and asks the user to enter them manually or select a Season, instead of silently reusing stale defaults.
- This is a UI/search refinement only. Movie correction apply semantics, no-source Movie semantics, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, and push were not changed.

# Phase 4.13e Follow-up Detail TV Correction Confirmation

- Movie detail and Episode detail TV Episode correction now use the same two-step interaction: choose a TMDB Series or Season target first, then click the explicit confirm button to apply.
- Series-row selection keeps the entered Season/Episode numbers; Season-row selection fills the Season number but still lets the user edit Season/Episode before confirming.
- Candidate clicks no longer write database changes directly.
- This does not change Movie correction apply semantics, single-source correction safety rules, no-source Movie semantics, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, or push.

# Phase 4.13e Follow-up Detail Correction AI Timeout / No-result Handling

- Detail-page AI correction requests now use a 90-second timeout for Movie, TV Episode, and TV Season correction prompts.
- Batch AI correction now uses a 75-second timeout.
- Scan-stage AI concurrency was checked and left unchanged: TV uncertain-range AI runs up to 3 concurrent batch requests; full-range TV AI remains disabled by default and is a single request when enabled.
- Movie detail and Episode detail AI correction no longer use local fallback TMDB searches when AI fails or returns no usable search title. Users are prompted to enter the search term manually.
- This does not change Movie correction apply semantics, batch AI rules, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, or push.

# Phase 4.13e Fix Batch AI Movie Confidence Scoring

- Batch AI Movie target confidence now treats missing AI year or missing TMDB year as neutral `yearScore=0.70` instead of the previous stronger penalty.
- The main Movie auto-apply threshold remains `confidence >= 0.80`.
- Movie target resolution now rejects strong year conflicts (`AI year` and `TMDB year` both present with a gap greater than one year) with `movie-year-conflict`.
- A conservative unique strong match can pass below the composite threshold only when top title score is at least `0.86`, the title-score margin to the next candidate is at least `0.12`, and there is no strong year conflict.
- Movie target diagnostics now log title score, year score, confidence, top1/top2 title scores, title-score margin, `uniqueStrongMatch`, and resolution / skipped reason.
- This does not change TV target logic, detail-page correction, prompts, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, or push.

# AI adaptive concurrency for batch AI and scan movie tags

- Added a shared adaptive AI batch executor for high-volume AI request paths.
- Initial concurrency is 5. Retryable AI request failures downgrade concurrency 5 -> 3 -> 1, and a success streak upgrades it back 1 -> 3 -> 5.
- Retryable failures include request timeout, HTTP 429, 502, 503, 504, and transient network I/O failures. HTTP `Retry-After` is honored with a capped delay.
- Batch AI correction now uses adaptive request concurrency for the AI target-classification request only. Local TMDB resolution and database apply still use existing safety gates, and apply remains serialized to avoid duplicate writes.
- Scan-stage Movie recognition still does not add a new LLM call. Only the existing background Movie AI tagging queue now uses the same adaptive executor.
- TV scan uncertain-range and full-range AI concurrency are unchanged.
- New sanitized logs include `ai-adaptive-concurrency-started`, `ai-adaptive-concurrency-changed`, `ai-request-retry-scheduled`, `ai-request-retry-exhausted`, `batch-ai-concurrency-summary`, and `scan-movie-ai-concurrency-summary`.
- This does not change AI prompts, Movie / TV safety gates, recommendation inputs, Watch Insights, schema, migrations, database update, commit, or push.

# AI settings routing and adaptive threshold follow-up

- Latest batch AI logs show the adaptive executor was active with initial concurrency 5. The checked run completed 8 AI request units successfully with no retry, no downgrade, and final concurrency 5.
- Adaptive AI concurrency now upgrades after 3 consecutive successful requests instead of 8.
- Settings now exposes separate model and timeout fields for detail correction, batch AI correction, scan TV uncertain range, scan TV full range, scan Movie tagging, AI recommendation, and Watch Profile.
- The expanded routing is stored in the existing AI model setting payload for compatibility. Existing plain model-name settings are still accepted and expanded into the current defaults.
- Runtime routing still keeps detail and batch correction on Pro by default, scan and recommendation on Flash by default, and Watch Profile on Pro with high thinking by default unless the user changes the per-purpose settings.
- This does not change Movie / TV safety gates, AI prompts, recommendation inputs, Watch Insights semantics, schema, migrations, database update, commit, or push.

# Phase 4.13e Follow-up Special Content AI Target Handling

- Detail Movie correction, detail TV correction, and batch AI correction prompts now treat SP / OVA / OAD / special / theatrical wording as a decision point rather than an automatic skip.
- Batch AI is asked to return Movie, TV Episode, or TV Season when a special-looking item can be safely represented as one of those supported targets, and to skip only when it cannot be represented safely or lacks required fields.
- Target-kind constraints remain unchanged: Movie detail Movie correction cannot switch to TV, TV detail correction cannot switch to Movie, and batch Season units still cannot return Movie or single Episode targets.
- This is prompt-only. No TMDB / OMDb resolver, local confidence threshold, correction apply rule, scan safety gate, schema, migration, database update, commit, or push was changed.

# TMDB / OMDb External API Adaptive Throttling

- TMDB HTTP requests now use a shared bottom-layer adaptive throttle in `TmdbService.SendGetAsync`.
- TMDB maximum concurrency is now 8, with adaptive levels 8 -> 4 -> 2 -> 1. A retryable failure downgrades one level, then an 8-request stable observation window upgrades one level.
- TMDB requests are globally rate limited to 12 requests per second at the same bottom-layer send path.
- OMDb HTTP requests now use the same adaptive throttle in `OmdbService.SendGetAsync`, with levels 2 -> 1 -> 2 and a conservative 2 requests per second limiter.
- Retryable external API failures include timeout / transient network errors and HTTP 429, 502, 503, and 504. HTTP 400, 401, 403, 404, empty search results, missing metadata, and local safety-gate rejects are not treated as throttle failures.
- Retry uses finite attempts with exponential backoff, jitter, and `Retry-After` support. One logical request can only downgrade one level, so repeated retries for the same request do not immediately collapse concurrency to the minimum.
- Business logic, TMDB / OMDb parsing, Movie / TV identification gates, AI prompts, schema, migrations, database update, commit, and push were not changed.

# Phase 4.13e Follow-up Movie Confidence Origin-name Relaxation

- Batch AI Movie target resolution no longer rejects a single exact-year TMDB Movie result solely because the AI returned title and the TMDB original title have low string similarity.
- The main Movie auto-apply threshold remains `confidence >= 0.80`, and the existing unique strong-title pass remains unchanged.
- A conservative single-result pass was added: when TMDB returns exactly one Movie result, AI year and TMDB release year are both present and equal, and there is no strong year conflict, the Movie target can apply even if the title-confidence score is below the composite threshold.
- Strong year conflicts still skip with `movie-year-conflict`.
- Diagnostics now include `singleExactYearResult` and resolution `movie-single-exact-year-result-applied` when this pass is used.
- This does not change TV target logic, detail-page correction, recommendation boundaries, Watch Insights, scan rules, migrations, database update, commit, or push.

# AI Settings Watch Profile Compatibility Follow-up

- Removed the obsolete single global model-name input from the Settings API page; model and timeout editing now happens only through the per-purpose rows.
- The stored global/default model remains runtime-compatible for old settings payloads, but it is no longer user-facing in the Settings UI.
- Watch Profile high thinking is now enabled only when the resolved DeepSeek model supports it (`deepseek-v4-pro`). If the Watch Profile row is changed to `deepseek-v4-flash`, the request keeps using Flash and automatically sends thinking off.
- AI routing diagnostics now include `thinkingRequested`, `thinkingEnabled`, and `thinkingSkipReason` so Flash profile requests can be distinguished from Pro high-thinking profile requests.
- This does not change Movie / TV safety gates, prompts, recommendation inputs, Watch Insights profile semantics, schema, migrations, database update, commit, or push.

# Phase 4.14b Scan / Rescan Safety Cross-impact

- Other / orphan / unassociated grouped source "remove from library" is now represented by a failed Movie placeholder visibility carrier plus `UserMovieCollectionItem.LibraryVisibilityState=Hidden`, instead of marking the unbound `MediaFile` as deleted.
- The source row stays present and `MediaFile.IsDeleted` stays false for remove-from-library, so probe fields, subtitle bindings, physical local files, and WebDAV files are not cleared by the hide action.
- Hidden failed Movie placeholders are excluded from automatic scan retry, rescan reattach, unknown Season append, and orphan grouping candidates. They are shown only through the removed-from-library surface until the user restores them.
- Delete-record behavior remains separate from remove-from-library behavior and was not converted to hide-only.
- Movie Discovery, no-source Movie detail semantics, recommendation inputs, Watch Insights, AI recommendation, schema, migrations, database update, commit, and push were not changed.

# Phase 4.14c Scan Reason Summary Cross-impact

- Movie scan outcomes now contribute aggregate task-level reason counts, including Movie identified, Movie fallback preserved as placeholder / needs review, hidden failed placeholder skipped, and placeholder / orphan grouping counts.
- These counts are persisted only in `ScanTaskLogs.ReasonSummaryJson` for scan-record explanation. They are not per-file history, are not used as matching rules, and do not affect Movie Discovery ranking, search, no-source detail semantics, recommendation inputs, Watch Insights, or AI recommendations.
- Scan history cards no longer show full WebDAV target URLs or usernames; they use scan-path display names and reason totals instead.
- This phase adds migration `20260524213322_AddScanTaskReasonSummary` but does not execute database update, commit, or push.
- Follow-up: Movie success in the reason summary now counts only source rows that end bound to matched / manually confirmed Movie records. Failed Movie placeholders stay in the needs-review bucket and do not inflate Movie success counts.

# Phase 4.14d Watch History / Probe / Subtitle Cross-impact

- Watch Insights calendar navigation now applies a Watch History target date filter and highlight without changing Watch Insights statistic inputs. Movie Watch Insights, Watch Profile, AI recommendations, persona inputs, and recommendation fingerprints remain Movie-only.
- Movie history rows continue to open Movie detail, including no-source Movie detail when a Movie record exists but has no active playable source.
- Deleted / unavailable source rows in history are shown as unavailable instead of treated as active playback sources.
- Direct probe now skips deleted `MediaFile` rows; remove-from-library continues to preserve Movie source probe fields, while delete-record keeps its software-record deletion semantics.
- Scan-time subtitle binding rebuild preserves the existing preferred subtitle when the same subtitle file is still present after rebuild. Online subtitle search / download and playback-source-level subtitle binding remain out of scope.
- Movie Discovery ranking, search, no-source detail semantics, recommendation inputs, scan matching rules, schema, migrations, database update, commit, and push were not changed by this phase.

# Phase 4.14 Closure Regression Cross-impact

- Rechecked Movie scan / placeholder / Other cross-impact as part of the Phase 4.14 closure pass. Hidden failed Movie placeholders remain excluded from automatic retry, reattach, unknown append, and orphan grouping until restored.
- WebDAV scan failure messages persisted to scan logs now use generic operation text plus exception type instead of raw exception messages, reducing the chance that full URLs, remote paths, or credentials appear on scan history cards.
- Movie Discovery ranking, search, no-source Movie detail semantics, recommendation inputs, Watch Insights, AI recommendations, scan matching rules, schema, migrations, database update, commit, and push were not changed by this closure pass.

# Phase 4.15b Media-library Refresh Cross-impact

- Media-library Movie / no-source / placeholder rows now participate in the same refresh scheduler as TV and Other rows, but their projection and display semantics are unchanged.
- DataChanged bursts from Movie detail correction, no-source state changes, collection state changes, delete-record, remove-from-library, restore, and scan completion are coalesced before reloading the media library.
- New diagnostics are aggregate refresh timings and counts only. They do not log titles, local paths, WebDAV URLs, account names, tokens, passwords, or API keys.
- Movie Discovery ranking, search, no-source detail semantics, recommendation inputs, Watch Insights, AI recommendations, scan matching rules, schema, migrations, database update, commit, and push were not changed by this phase.

# Phase 4.15c Media-library Query Diagnostics Cross-impact

- Movie / no-source / placeholder media-library rows now have additional aggregate query diagnostics through `LibraryQueryService`, including Movie collection-state, Movie row, orphan / Other, external no-source collection, and projection timings.
- Operation-local media-library refreshes are routed through the refresh scheduler so Movie detail correction, no-source state changes, collection changes, delete-record, remove-from-library, restore, and scan completion notifications can merge with the operation refresh where they occur in the same wave.
- The diagnostics do not change Movie Discovery ranking, search, no-source detail semantics, recommendation inputs, Watch Insights, AI recommendations, scan matching rules, schema, migrations, database update, commit, or push.
- No Movie query rewrite, projection cache, index migration, UI virtualization, poster loading, or image decoding change was added by this phase.

# Phase 4.15d Media-library TV Projection Aggregation Cross-impact

- TV media-library Series / Season projection now uses Season-level aggregate rows instead of materializing every Episode and active source row for card-level counts.
- Movie / no-source / placeholder projection code paths were not rewritten. Movie Discovery ranking, search, no-source detail semantics, recommendation inputs, Watch Insights, AI recommendations, scan matching rules, schema, migrations, database update, commit, and push were not changed.
- The aggregate diagnostics remain sanitized and count / timing only; they do not log titles, local paths, WebDAV URLs, account names, tokens, passwords, or API keys.

# Phase 4.15d-fix TV Source Aggregate Query Cross-impact

- TV source aggregate timing logs showed the first DB-side aggregate query shape was slower than the old flat read path, so the TV source metric path now reads minimal flat source rows and groups them in memory.
- Movie / no-source / placeholder projection code paths were not changed by this fix.
- Movie Discovery ranking, search, no-source detail semantics, recommendation inputs, Watch Insights, AI recommendations, scan matching rules, schema, migrations, database update, commit, and push remain unchanged.

# Phase 4.15e Poster Decode Cross-impact

- Media-library poster cards now request thumbnail-sized cached poster decode and decode local cache files off the UI thread before assignment.
- Movie / no-source / placeholder projection, Movie Discovery ranking, search, recommendation inputs, Watch Insights, AI recommendations, scan matching rules, schema, migrations, database update, commit, and push remain unchanged.
- Movie detail and external-detail poster behavior keeps the existing full decode path unless a page opts into `DecodePixelWidth`.

# Phase 4.15d Frontend Poster Virtualization Cross-impact

- Media-library poster view now uses a virtualized recycling container and no longer realizes every filtered Movie / no-source / placeholder card at once.
- Movie / no-source / placeholder projection, Movie Discovery ranking, search, no-source detail semantics, recommendation inputs, Watch Insights, AI recommendations, scan matching rules, schema, migrations, database update, commit, and push remain unchanged.
- The new diagnostics are aggregate-only layout / refresh timings and do not log movie titles, full local paths, WebDAV URLs, account names, tokens, passwords, or API keys.

# Phase 4.15 Closure Regression Cross-impact

- Rechecked the Phase 4.15 media-library performance work from the active worktree and latest aggregate logs. Movie / no-source / placeholder cards still use the same projection and display semantics while benefiting from refresh coalescing, query diagnostics, poster decode tuning, collection range reset, and poster-view virtualization.
- Latest sampled media-library logs show aggregate refresh / render / virtualization evidence only, including `items=165` and `realized=8` on the poster first viewport. They do not log movie titles, full local paths, WebDAV URLs, account names, tokens, passwords, or API keys.
- Movie Discovery ranking, search, no-source detail semantics, recommendation inputs, Watch Insights, AI recommendations, scan matching rules, schema, migrations, database update, commit, and push remain unchanged by the Phase 4.15 closure pass.

# Phase 7.4a Discovery Search Toolbar / Layout

Completed:

- Discovery search now keeps the search-method selector visible for both Movie and TV.
- Search placeholder / tooltip text is dynamic:
  - Movie title search: `输入需要搜索的电影`
  - TV title search: `输入需要搜索的电视剧名`
  - Person search: `输入需要搜索的导演/演员`
- TV person search is now supported in Discovery search by resolving the first TMDB person result and reading `person/{id}/tv_credits`.
- TV person search results stay as `DiscoveryTvSeriesCardViewModel` rows and are not converted to Movie rows or AI recommendation candidates.
- Discovery search layout switching now toggles baseline poster/list result containers instead of showing a placeholder status. Full visual alignment for the search toolbar, filters, summary and result areas remains 7.4b.
- Discovery search layout memory is stored in `discovery-preferences.json`, separate from the media-library layout preference file.
- Search status copy now follows active media type and search method for loading, empty, loaded and clear-filter states.

Acceptance:

- Movie / TV switching remains available.
- Movie and TV title/person search methods are selectable.
- The three required placeholder strings are bound through the existing placeholder behavior.
- Search filters retain the existing Discovery Movie / TV field sets.
- Poster/list layout switch works for Movie and TV search results.
- TV person search does not feed TV data into Movie AI recommendations, Watch Insights, profile/persona inputs or fingerprints.
- No `已移出媒体库` search-card action was added.

Business logic changes:

- Scoped Discovery search change only: `ITmdbService.SearchTvSeriesByPersonAsync` and `GetPersonTvCreditsAsync` add TV person credits lookup.
- UI preference change only: Discovery search layout is persisted through an App-layer JSON preference file.
- Recommendation algorithms, prompts, candidate generation, scan logic, correction semantics, player behavior, schema and migrations are unchanged.

Non-stage page changes:

- None. No Home, Media Library, Recommendation, Detail, Player or Settings page was edited.
- Shared DI registration was updated for the new Discovery preference service.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Follow-up Round 8 Search Poster Rating Grid Alignment

Completed:

- Reworked Discovery search poster bottom layout around the card's own extra rating badge instead of copying the Media Library bottom margin.
- Movie and TV search poster bottom containers now use `Margin="8,0,8,10"` so title/date/tag left edges align with the top-left media chip border.
- The bottom rating Grid now stretches across the full available card width, so the rating badge right edge aligns with the top-right action/state chip border.

Not done:

- No search behavior, rating persistence, schema, migration, database update, commit or push was changed in this follow-up.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Follow-up Round 7 Search Poster Title Date Layout Parity

Completed:

- Compared Media Library poster layout against Discovery search poster layout.
- Media Library title/date/type line uses a bottom `StackPanel` with `Margin="10"`; Discovery search had drifted to a different left margin while retaining a rating-badge grid.
- Discovery Movie and TV search poster title/date/type line containers now use the same `Margin="10"` baseline as Media Library.
- Removed extra explicit title/date text alignment properties that Media Library does not use.

Not done:

- No search behavior, rating persistence, schema, migration, database update, commit or push was changed in this follow-up.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Follow-up Round 6 Search Poster Edge Alignment

Completed:

- Discovery Movie and TV search poster cards now use the same 8px top-left media-type chip margin as the Media Library poster card.
- Discovery Movie and TV search poster right-top action/state chips were checked and already use the same 8px edge margin as Media Library.
- Poster title, release date and tag/type line now use the same 8px left content edge as the top-left media-type chip.

Not done:

- No business logic, TMDB calls, rating refresh behavior, schema, migration, database update, commit or push was changed in this follow-up.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Follow-up Round 5 Search Card Rating Consistency

Completed:

- Movie search poster/list cards now keep the category chip, title/date text, rating badge and want action on a consistent 10px left/top visual grid.
- Search rating badges are larger, bold and fixed-height, matching the requested no-more-than-two-line footprint.
- Movie `+ want` / cancel-want actions and TV current-want labels now use the same 20px chip height as the media type chip.
- Movie search and ranking enrichment now allow existing local movies to refresh TMDB and OMDb/IMDb rating sources from real-time Discovery results.
- Movie rating refresh updates `RatingSources` and related movie collection rating snapshots without touching media files, playback sources or library visibility.
- TV search/ranking cards now support weighted TMDB + OMDb/IMDb rating presentation and rating sorting through `RatingValue`.
- Added `TvSeriesRatingSources` so existing local TV series can persist series-level TMDB and OMDb/IMDb ratings refreshed from Discovery.
- TV detail query and media-library TV projections now read persisted series rating sources first, then keep existing external lookup as fallback.

Not done:

- No database update was executed.
- No scan, AI recommendation, playback, subtitle, watch insight or recommendation algorithm behavior was changed.
- No season-level or episode-level rating persistence was introduced; the new table is series-level only.

Known Issues:

- Blocker: None after build validation.
- Deferred: Actual window validation is still needed for poster chip/title alignment, TV current-want vertical centering and rating badge visual size.
- Noise: TV series ratings are persisted only for existing local `TvSeries` rows; unadded external TV search results remain in-memory search results.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `dotnet ef migrations add AddTvSeriesRatingSources --project src/MediaLibrary.Core/MediaLibrary.Core.csproj --startup-project src/MediaLibrary.Core/MediaLibrary.Core.csproj` succeeded.
- Migration adds only `TvSeriesRatingSources`, its FK to `TvSeries`, and a unique `(TvSeriesId, SourceName)` index.

# Phase 7.4b Follow-up Poster Pager Placement

Completed:

- Discovery Movie / TV poster-layout pagers were moved out of the fixed extra Grid row and into a bottom overlay inside the poster `ListBox` host.
- The overlay pager now follows the list-layout visibility rule: it is shown when the poster list does not need vertical scrolling, or when the internal poster `ListBox` scroll viewer is at the bottom.
- Poster results keep the media-library `ListBox + VirtualizingWrapPanel` sizing, viewport padding and internal scroll contract; this avoids returning to the earlier `ScrollViewer + ItemsControl + VirtualizingWrapPanel` path that broke poster scrolling.

Not done:

- No TMDB request shape, ranking logic, recommendation logic, scan logic, database schema, migration, database update, commit or push was changed.

Known Issues:

- Blocker: None found in this follow-up.
- Deferred: Actual window validation is still needed to compare poster-layout pager placement against the list-layout pager in short and multi-row search results.
- Noise: `git diff --check` reports line-ending normalization warnings only.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Follow-up Poster Scroll and Library Sort Alignment Fixes

Completed:

- Discovery search poster results now use a media-library-like `ListBox` with its own internal `ScrollViewer` and the shared `VirtualizingWrapPanel`, preserving both vertical scrolling and media-library poster spacing.
- Discovery search poster item width, content width, item height and viewport padding follow the media-library collapsed / expanded sidebar pattern.
- Media Library toolbar sort option button is now positioned by runtime layout calculation: collapsed sidebar aligns it to the watched-status filter below; expanded sidebar aligns it to the collection-status filter below.
- The Media Library clear-filter button remains right-aligned while sort alignment is recalculated on load, sidebar changes, batch-mode layout changes and page size changes.

Not done:

- No search service, TMDB request shape, ranking logic, recommendation logic, scan logic, player behavior, database schema, migration, database update, commit or push was changed.
- No screenshot / manual desktop-window validation was performed in this pass.

Known Issues:

- Blocker: None found in this follow-up.
- Deferred: Actual window validation is still needed for poster shadow spacing and the two media-library sort alignment targets.
- Noise: The previous `ScrollViewer + ItemsControl + VirtualizingWrapPanel` search structure was removed because it broke scrolling; Discovery now follows the media-library `ListBox + VirtualizingWrapPanel` scroll contract.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Follow-up Search / Library Toolbar and Result Regression Fixes

Completed:

- Discovery search filter changes now refresh paging / clear-filter command state without raising the search-submit command state from result rebuilds.
- Discovery search collection status is a normal multi-select filter with `全部 / 喜爱 / 想看 / 不想看 / 其他`; default state is `其他 + 喜爱 + 想看`, displayed as `收藏状态：3项`.
- Media Library collection status uses the same four concrete status options and default state; selecting all four concrete options folds back to `全部`.
- Discovery search input tooltip is suppressed while the input text is empty.
- TV series navigation no longer contributes to the Discovery search-result status overlay, avoiding the brief search-area spinner before detail navigation.
- Discovery search result poster layout now uses the media-library `ListBox + VirtualizingWrapPanel` sizing / viewport padding pattern so poster spacing and vertical scrolling are both preserved.
- Discovery search result pager is placed in a bottom row under the result content so short result pages keep pagination near the result area bottom.
- Discovery and Media Library layout toggle hosts remove inner vertical padding so the selected segment fills the component height more closely.
- Media Library toolbar right group was adjusted so the sort button tracks the available right-side space instead of sticking to the group edge.

Not done:

- No search service, TMDB request shape, ranking logic, recommendation logic, scan logic, player behavior, database schema, migration, database update, commit or push was changed.
- No screenshot / manual desktop-window validation was performed in this pass.

Known Issues:

- Blocker: None found in this follow-up.
- Deferred: Pager bottom placement and poster shadow spacing still need actual window inspection in both sidebar states.
- Noise: Collection-status `其他` is a UI/client-side filter meaning no favorite / want-to-watch / not-interested state; it is not a new database state.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Follow-up Discovery Search Tab / Filter Polish

Completed:

- Discovery top-level tabs now use full labels `影片搜索` / `榜单` / `AI推荐`, a thicker separator, centered tab text and a longer selected underline.
- Discovery tab selection is retained in memory for the current app run; direct first activation still defaults to `影片搜索`, while the existing AI-entry request can still open `AI推荐`.
- Search method labels were changed to `按电影名` / `按电视剧名` and `按人物名`; the media-type selector remains `电影` / `电视剧`.
- Pressing Enter in the search input still triggers search and then clears TextBox focus.
- The search toolbar card explicitly uses an arrow cursor on empty space; button hit targets retain the hand cursor.
- The Discovery clear-filter button now uses the same compact width behavior as the media-library clear-filter button.
- Movie / TV genre and decade filters now use the same multi-select model as region / language / collection status.
- Genre, region, language, decade and collection status all reset to `全部` when every non-`全部` option is selected.
- Genre and region menus use a shorter scrollable popup with the page-local modern scrollbar and a smaller wheel step.

Not done:

- No search service, TMDB request shape, ranking logic, recommendation logic, scan logic, database schema, migration, database update, commit or push was changed.
- Watch status and playback source remain single-select as designed for this toolbar.
- AI recommendation tab visual polish remains a separate 7.4d item.

Known Issues:

- Blocker: None found in this follow-up.
- Deferred: Full AI recommendation tab and preference dialog polish remains 7.4d.
- Noise: Multi-select filters are still applied to the loaded / expanded result pool, not as TMDB full-catalog exact filters.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4c Follow-up 榜单与 Discover 搜索反馈修复

Completed:

- 榜单页顶部圆角筛选卡片已移除，榜单内容区直接作为页面主体显示。
- 顶层 `榜单` Tab 在已进入榜单页后支持悬停 / 点击打开多级菜单；从其它 Tab 点击 `榜单` 仍只切换到榜单页。
- 榜单下拉菜单复用媒体库标签菜单的子菜单视觉和向右展开行为；一级仅保留 `电影`、`电视剧`，二级为 `热门榜`、`高分榜`、`趋势榜`，趋势榜三级为 `今日趋势`、`本周趋势`。
- 榜单分页改为与影片搜索一致的上一页 / 下一页 chevron 图标按钮和页码文本组件。
- 榜单双列结果的中间分隔线改为 ItemsControl 背景中的单条连续竖线，不再由每一行各自绘制短线。
- 榜单结果卡片默认光标改回箭头；按钮仍保留自身交互光标。
- 空关键词搜索在排序不是 `相关度` 时改走 TMDB `/discover/movie` 或 `/discover/tv`，并向 TMDB 传递可表达的排序、类型、地区、语言和单年代日期范围；本地状态类筛选继续复用现有结果池过滤。

Not done:

- 未改变 AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未把 TMDB discover 无法表达的 `其它`、多年代组合、入库状态、播放源、观看状态和收藏状态强行转换为远端过滤；这些仍由本地过滤完成。

Known Issues:

- Blocker: None after build validation.
- Deferred: 仍需实际窗口人工确认榜单 Tab 多级菜单的位置、悬停体验和连续竖线视觉效果。
- Noise: Movie `片名` discover 排序使用 TMDB `original_title`，TV `剧名` discover 排序使用 TMDB `name`。

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

# Phase 7.4b Follow-up Round 1 Regression Fixes

Completed:

- Discovery Tab headers now use a fixed header width / height so each label is centered against its selected underline instead of being measured only by text content.
- Tab header text was moved higher, increasing the gap between the label and the separator / selected underline.
- Search loading overlay now shows the shared `LoadingSpinnerTemplate` while `IsActiveSearchLoading` is true; empty states still show text only.
- Multi-select filter menu options now force `IsSelected` binding refresh even when the source value remains `false`, so selecting the final non-`全部` option and auto-resetting to `全部` no longer leaves that clicked menu item visually checked.

Not done:

- No search service, TMDB request shape, ranking logic, recommendation logic, scan logic, database schema, migration, database update, commit or push was changed.

Known Issues:

- Blocker: None found in this regression pass.
- Deferred: AI recommendation tab and preference dialog polish remains 7.4d.
- Noise: `git diff --check` reports line-ending normalization warnings only.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Follow-up Round 2 Regression Fixes

Completed:

- Discovery Tab template now binds the label `TextBlock` and selected underline to the same fixed width, avoiding ContentPresenter measurement drift and keeping each label centered against its underline.
- Tab label vertical position was lowered from the previous round so it is no longer excessively high above the separator.
- Movie / TV search collection-status filters were reverted from multi-select checkboxes to normal single-select menu items.
- Collection-status selection still stores a single active value through the existing selected-status set, so existing filtering code remains scoped and unchanged.
- Movie OMDb enrichment no longer rebuilds the whole search result list for each card update; details and rating properties update per card, and the search list is rebuilt once at the end of the enrichment batch when needed.
- TV search display loading avoids one duplicate filtered-count calculation when correcting an out-of-range page.

Not done:

- No TMDB request shape, ranking logic, recommendation logic, scan logic, database schema, migration, database update, commit or push was changed.

Known Issues:

- Blocker: None found in this regression pass.
- Deferred: If search still feels slow after this pass, the next targeted audit should capture aggregate timings for TMDB fetch, status resolve, poster cache load and UI apply separately.
- Noise: `git diff --check` reports line-ending normalization warnings only.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Follow-up Round 3 Regression Fixes

Completed:

- Root cause for repeated Tab centering failures was isolated to using the `TabItem` header cell as the visual centering basis. The text and selected underline were both in the item template, but their perceived center still depended on the TabPanel / TabItem header cell width rather than the selected underline itself.
- The Tab template now puts the label and selected underline inside the same centered `StackPanel`; the underline has a fixed 104px width and the label is centered relative to that exact underline, making the underline the single visual centering basis.
- The whole Tab strip and divider were moved slightly upward.
- The Tab label was lowered from the previous pass so it is no longer too high above the underline.
- The search-result header status block now collapses whenever the center overlay state is visible, so the idle / empty prompt is shown only in the page center and not duplicated in the top-left summary area.
- Documentation was updated to reflect that collection status is now single-select while genre / region / language / decade remain multi-select.

Not done:

- No TMDB request shape, ranking logic, recommendation logic, scan logic, database schema, migration, database update, commit or push was changed.

Known Issues:

- Blocker: None found in this regression pass.
- Deferred: Further Tab visual tuning should be done by adjusting the single centered stack only, not by independently moving text and underline.
- Noise: `git diff --check` reports line-ending normalization warnings only.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

# Phase 7.4b Discovery Search UI Visual Alignment

Completed:

- Discovery search toolbar now uses a local media-library-like structure with compact buttons and ContextMenu filters for media type, search method, genre, region, watch/source status, sort direction, sort option, decade, language, clear filters and layout switching.
- Existing Discovery Movie / TV filter option sets are reused; menu selection writes only to the existing `Selected*` filter properties.
- Search result status and summary continue to use Discovery-specific active search messages rather than media-library summaries.
- Movie poster search cards now remove the media-library progress bar, show the `电影` type tag, keep the top-right want action, show a rating badge, and retain the conditional add-to-library action.
- TV poster search cards now remove the progress bar, show the `电视剧` type tag, show a `想看` tag only when at least one season is marked want-to-watch, and do not expose a TV `+ 想看` action.
- Movie list rows now use a rating block, central title/status/tag copy, a top-right want action, conditional add-to-library action and a bottom-right `电影` tag.
- TV list rows now use a rating block, central title/season/library/tag copy, a top-right `想看` tag only for series with want-to-watch seasons, conditional add-to-library action and a bottom-right `电视剧` tag.
- Discovery page-local XAML styles and a page-local left-click ContextMenu opener were added; Home, Recommendations and Media Library pages were not changed.

Acceptance:

- Search toolbar and filters follow the media-library compact button/menu structure while keeping Discovery-specific fields.
- Poster cards have no progress bar and no `已移出媒体库` action.
- Movie cards expose the want-to-watch action in the expected top-right location.
- TV cards do not expose a `+ 想看` action; the `想看` label appears only when `HasWantToWatchSeasonTag` is true.
- Rating labels are visible in poster and list layouts.
- List rows keep the Movie / TV type label at the right-bottom position and no longer use that right-side slot for source labels.
- Source and add-to-library state remain represented without creating media files, playback sources or visibility side effects.
- Exact media-library visual parity is not claimed for this pass; button size, layout-toggle placement, search input size, search icon treatment and dropdown menu styling remain Deferred per user instruction.

Business logic changes:

- No search-service, TMDB, recommendation, scan, schema, migration, database update or media-library visibility business logic was changed.
- UI binding/projection only: added search filter selection commands for the button menus, short rating badge projection and a TV want-season tag projection.

Non-stage page changes:

- None. No Home, Recommendation, Media Library, Detail, Player or Settings page was edited.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
