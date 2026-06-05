# Phase 7.4 影片发现执行计划

Last updated: 2026-06-06

本文件是 Phase 7.4 的唯一详细执行计划。后续进入 7.4 任一小阶段前，必须先阅读本文件，再按小阶段要求读取对应文档、代码和设计草图。

## 阶段状态

- 状态：7.4a、7.4b、7.4c 已完成；7.4d-1 到 7.4d-15 已完成；7.4e 回归收口已完成。
- 当前范围：`影片发现` 页面，以及其中嵌入的 `AI 推荐` Tab。
- 主要规格来源：`DesignDraft/page-spec/movie-discovery-page.md`。
- 业务边界：7.4 可以完善搜索方式、卡片状态投影和 UI 偏好，但不能修改推荐算法，也不能让 TV 进入 Movie AI 推荐、观影洞察、画像、人格或推荐 fingerprint 链路。

## 执行记录

### 2026-06-06 - 7.4e / Discovery 回归收口

完成了什么：

- 按 7.4e 验收矩阵定向核对 Discovery 搜索方式、placeholder、Movie / TV 搜索卡片、筛选、海报 / 列表切换、Movie / TV 榜单、AI Tab、推荐偏好弹窗、Movie-only 推荐边界和 migration diff。
- 确认 Home `发现更多影片` 入口仍通过 `MovieDiscoveryViewModel.OpenAiRecommendationsOnNextActivation()` 进入 Discovery 的 AI 推荐 Tab，主导航仍不显示独立 AI 推荐入口。
- 确认 TV 人物搜索走 TMDB `person/{id}/tv_credits` 并投影为 `DiscoveryTvSeriesCardViewModel`；TV 搜索 / 榜单未转换为 `AiRecommendationItem`。
- 确认推荐服务种子、fingerprint 和候选生成仍读取 Movie / `UserMovieCollectionItems`，未纳入 TV Series / Season / Episode。
- 确认 Discovery 布局偏好仍为 App-layer `discovery-preferences.json`，本轮未新增数据库字段、migration 或 database update。
- 按用户确认，当前 TV 搜索卡片想看季标签实际文案 `当前想看` 属于后期语义更新，不需要修改代码，7.4e 按通过记录。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。
- `git diff --name-only` 在文档更新前为空；本轮只修改收口文档。

不属于本次：

- 未修改业务代码、XAML、推荐算法、推荐 prompt、推荐 fingerprint、TV 推荐、观影洞察、画像、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图 / 点击验收；UI 交互仍需人工窗口复核。

Known Issues：

- Blocker：无。
- Deferred：仍需实际窗口人工确认 Discovery 搜索 / 榜单 / AI 推荐三个 Tab 和偏好弹窗交互。
- Noise：TV 搜索卡片想看季标签 `当前想看` 已由用户确认为后期语义更新，7.4e 按通过记录；7.4b follow-up 曾按授权新增 `AddTvSeriesRatingSources` migration，7.4e 本轮 migration diff 为空，未新增 migration。

### 2026-06-06 - 7.4d-5 / AI 推荐海报信息区测试反馈修复

完成了什么：

- AI 推荐三海报从 240x360 放大到 312x468，保持 w780 海报请求、DecodePixelWidth=780 和高质量缩放；海报内部去掉标题、评分、日期、标签、渐变和状态按钮，只保留纯海报与占位图。
- 海报下方新增无背景、无边框的信息区：第一行 `电影名 | 原名`，第二行 `yyyy-MM-dd` 日期，第三行 `类型标签 | 情绪标签 | 场景标签`，第四行按榜单样式显示 `导演：xx` 与 `演员：xx`，第五行显示推荐理由并保留最大高度与滚动溢出提示。
- 评分徽章移到信息区右侧，跨第一、第二行垂直居中，并与信息区右边缘对齐。
- 自定义偏好双段开关移除选中态上的 hover 覆盖，选中颜色继续使用媒体库布局切换同款 `BrushAccent / BrushOnAccent` 语义。
- 推荐项 read model 增加标题原名行、三类标签行、导演/演员展示行；推荐服务投影库内 Movie 与外部 TMDB 候选的导演/演员，并在推荐缓存快照中保留完整上映日期、导演和演员。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。
- `git diff --check` 无 whitespace error，仅保留既有 LF/CRLF 提示。

不属于本次：

- 不改 AI 推荐生成算法、候选排序、推荐 prompt、推荐 fingerprint、TV 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认 312x468 海报密度、信息区五行截断、评分徽章对齐和开关选中态观感。

Known Issues：

- Blocker：无。
- Deferred：标记 `不想看` 后仍沿用既有行为移除当前卡片，不做即时补位。
- Noise：旧外部推荐缓存若缺少完整上映日期或演职员字段，需换一批或刷新推荐后由新快照完整带出；库内缓存会尽量从 Movie 元数据回填。

### 2026-06-06 - 7.4d-4 / AI 推荐测试反馈二次修复

完成了什么：

- AI 推荐区默认光标显式设回箭头，只在海报点击层、菜单项、操作按钮和工具按钮保留手型，避免推荐理由、空白区域和非点击容器显示点击手势。
- 顶部右侧工具条外部按钮间距继续加大，并缩小自定义偏好双段开关与编辑图标按钮之间的距离；按钮间距保持固定两档，展开导航栏为 26px，收起导航栏为 35px，不再用伸缩列平均分配额外空间。
- 状态提示文案按测试反馈统一调整：默认提示改为 `为你量身“定制”下一部影片`，推荐成功改为 `已为你推荐 N 部影片`，无候选补充失败提示改为 `本次没有补充到新的候选影片，请稍后重试`，缺少偏好种子提示改为 `先标记几部影片，让 AI 更懂你`。
- 状态提示统一去掉句尾句号，并在 `StatusMessage` 赋值入口兜底裁剪句尾句号或省略号，覆盖从推荐预览状态和旧缓存消息回填的文本。
- 删除 `已看影片不需要再加入想看`、`已取消想看`、`已加入想看`、`已标记为不想看`、`已取消不想看` 等操作成功提示；相关操作仍保留状态变更和数据刷新。
- 自定义偏好弹窗说明改为 `偏好设置仅影响 AI 推荐，不会覆盖你的其他过滤规则。保存后，下一次推荐将生效`。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。
- `git diff --check` 无 whitespace error，仅保留既有 LF/CRLF 提示。

不属于本次：

- 不改 AI 推荐生成算法、候选排序、推荐 prompt、推荐 fingerprint、TV 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认光标、顶部工具条间距和弹窗说明文案。

Known Issues：

- Blocker：无。
- Deferred：标记 `不想看` 后仍沿用既有行为移除当前卡片，不做即时补位。
- Noise：旧缓存中已持久化的外部错误消息可能仍带旧措辞，但页面状态栏会裁剪句尾句号。

### 2026-06-06 - 7.4d-3 / AI 推荐测试反馈修复

完成了什么：

- AI 推荐评分无数据占位符从 `-` 调整为 `--`，并继续复用同一个 `AiRecommendationItem.WeightedAverageRatingText` 绑定路径，覆盖影片发现 AI 推荐和首页 AI 推荐预览。
- 库内 AI 推荐项补齐 `RatingSources` 中的 TMDB 与 OMDb/IMDb 评分投影；本地 fallback、推荐缓存快照恢复和预览缓存读取也保留或回填评分字段，避免详情页有评分但推荐卡片显示占位符。
- 推荐海报容器不再整体设置手型光标，只保留实际按钮手型；点击海报区域进入详情的既有行为保留。
- 顶部右侧工具条按钮间距加大；自定义偏好组件拆成 `自定义偏好` 标签 + `开 / 关` 双段开关，编辑入口拆成独立纯图标按钮。
- 修正推荐缓存快照恢复中一处既有 `已看 / 未看` 乱码兜底文案。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 AI 推荐生成算法、候选排序、prompt、推荐 fingerprint、TV 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认顶部工具条间距、双段开关选中态、纯图标按钮悬停态和推荐海报点击手感。

Known Issues：

- Blocker：无。
- Deferred：标记 `不想看` 后仍沿用既有行为移除当前卡片，不做即时补位。
- Noise：直接消费旧候选池快照时，如快照本身缺失评分字段，仍可能需要新一轮推荐刷新或候选补充后完全带出补齐后的评分字段。

### 2026-06-06 - 7.4d-2 / AI 推荐三海报布局与卡片交互

完成了什么：

- AI 推荐结果区从普通 WrapPanel 卡片改为三列海报展示；三张推荐海报使用等星间距布局，中间海报保持水平居中，左右海报与页面左右边距保持对称。
- 推荐海报主体复用电影详情页 240x360 海报结构、20px 圆角、双层投影、w780 海报加载和大海报占位模板。
- 海报左上角显示电影名，右上角纵向放置 `+ 想看 / 取消想看` 与 `不想看 / 取消不想看` 两个搜索海报同款胶囊按钮，按钮右边缘对齐。
- 点击海报主体、标题和海报标签区域进入电影详情页；点击两个状态按钮只执行对应按钮命令。
- 海报底部左侧四行依次显示日期、AI 类型、情绪、场景标签；右侧评分徽章使用影片搜索海报同款评分样式，并与右上角按钮右边缘对齐。
- 海报下方新增略宽于海报的推荐理由区域，使用现代化自动显隐滚动条和溢出提示。
- `想看` 写入失败时恢复原 `不想看` 状态，保证两个状态按钮在异常路径下仍保持互斥语义。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改推荐算法、候选来源、候选排序、AI 标签生成、推荐 fingerprint、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未新增 TV AI 推荐，也未让 TV 进入 Movie AI 推荐、观影洞察、画像、人格或推荐 fingerprint。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认三海报在目标分辨率下的视觉密度、按钮悬停态和推荐理由滚动条手感。

Known Issues：

- Blocker：无。
- Deferred：标记 `不想看` 后仍沿用既有行为移除当前卡片，不做即时补位。
- Noise：未做完整窗口截图验收前，不同窗口宽度下星间距压缩效果仍需人工确认。

### 2026-06-06 - 7.4d-1 / AI 推荐顶部工具条与自定义偏好弹窗

完成了什么：

- AI 推荐页顶部去掉重复的 `AI 推荐 / 观影偏好` 标题，只保留左侧状态提示文本。
- 顶部工具条从左到右调整为：提示文本、`播放源：全部 / 有播放源 / 无播放源`、`观看状态：全部 / 已看 / 未看`、自定义偏好开关组件、`换一批`。
- 播放源筛选默认改为 `全部`；观看状态默认保持 `未看`。`有播放源` 仍映射到库内已有播放源，`无播放源` 仍映射到库外无播放源候选。
- 筛选按钮和菜单使用媒体库同密度的 28px 玻璃按钮 / 菜单样式。
- 自定义偏好组件改为左侧标签 + 开关 + 齿轮入口，开关负责启停，齿轮打开偏好弹窗。
- 自定义偏好弹窗遮罩改为透明遮罩；发现页 AI 推荐弹窗打开时 Tab 头不响应点击，离开发现页或切换导航页时会自动关闭弹窗。
- AI 推荐 prompt 中的筛选说明从 `入库范围` 调整为 `播放源筛选`，只改提示语义，不改变本地候选生成、去重、过滤和推荐安全门。
- AI 推荐页去掉整页大圆角外层包裹，推荐卡片和弹窗面板使用较小圆角。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改推荐算法、候选来源、候选排序、AI 标签规则、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未新增 TV AI 推荐，也未让 TV 进入 Movie AI 推荐、观影洞察、画像、人格或推荐 fingerprint。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认顶部工具条横向间距、弹窗遮罩命中区域和开关交互。

Known Issues：

- Blocker：无。
- Deferred：推荐卡片主体视觉仍沿用现有结构，后续如需彻底对齐影片发现卡片节奏可单独处理。
- Noise：未做完整窗口截图验收前，齿轮悬停态和不同窗口宽度下的工具条压缩效果仍需人工确认。

### 2026-06-06 - 7.4c follow-up / 榜单 Tab 悬停下拉延时

完成了什么：

- 榜单 Tab 的 hover 下拉从 `MouseEnter` 立即打开改为 `DispatcherTimer` 延时打开。
- 延时时长设置为 420ms；鼠标在延时结束前离开榜单 Tab 会取消本次打开。
- 点击榜单 Tab 的即时切换和菜单开关逻辑保持不变。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改榜单下拉菜单内容、筛选预设、榜单请求、排序、评分计算、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工交互验收；仍需实际窗口确认 420ms 延时是否合适。

### 2026-06-05 - 7.4c follow-up / 榜单首次加载兜底与双列行距 28px

完成了什么：

- 榜单激活入口不再只依赖 `_hasActivatedRankings` 单次标记；当已激活过但当前活动榜单空闲且没有可见项时，会重新触发加载。
- Movie 榜单重置流程在清空列表和刷新可见性前先设置 `IsRankingLoading=true`，首次进入时立即显示加载转圈。
- TV 榜单重置流程同步前置 `IsTvRankingLoading=true`。
- Movie / TV 榜单普通双列行的 `Grid Margin` 从 `0,0,0,24` 调整为 `0,0,0,28`。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、评分计算、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认首次进入榜单加载态和 28px 间距。

### 2026-06-05 - 7.4c follow-up / 榜单双列行间距微调

完成了什么：

- Movie 榜单普通双列行的 `Grid Margin` 从 `0,0,0,14` 调整为 `0,0,0,24`，增加双列海报行与下一行之间的上下间隔。
- TV 榜单普通双列行同步调整为 `0,0,0,24`。
- 只调整双列普通行之间的纵向间隔，不改变第一名、左右列间距、海报、右侧信息模板或分页控件。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `dotnet build src/MediaLibrary.App/MediaLibrary.App.csproj -o %TEMP%\xfverse-build-check` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、评分计算、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动新的完整桌面应用做人工截图验收；仍需实际窗口确认双列普通行间距。

### 2026-06-05 - 7.4c follow-up / 榜单简介上方留白微调

完成了什么：

- 第一名榜单信息模板中，简介区域上边距从 12px 增加到 32px，使导演 / 演员与简介之间多出约一行。
- 普通榜单信息模板中，简介区域上边距从 12px 增加到 30px，使导演 / 演员与简介之间多出约一行。
- 保持模板总高度不变，因此第一名和普通项简介可视高度各减少约一行。
- 不调整海报、标题行、日期 / 标签、导演 / 演员上限、简介滚轮处理或榜单数据。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` 未完成：当前运行中的 `MediaLibrary.App (16200)` 锁定默认 `bin` 输出，复制 apphost / dll 失败。
- `dotnet build src/MediaLibrary.App/MediaLibrary.App.csproj -o %TEMP%\xfverse-build-check` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、评分计算、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认行距和简介可视高度。

### 2026-06-05 - 7.4c follow-up / 榜单滚动视口左侧安全区补偿

完成了什么：

- 依据截图复核，判断第一列阴影仍被裁剪的根因在榜单 `ScrollViewer` 视口左边界，而不是海报模板内部 shadow host。
- Movie / TV 榜单 ScrollViewer 向左扩展 84px 视口：`Margin="-84,0,0,0"`。
- 对两个 ScrollViewer 的内部根 Grid 增加 `Margin="84,0,0,0"`，抵消视口左扩带来的内容位移，保持海报和文本视觉坐标不变。
- 保留媒体库同款海报阴影安全区：普通海报每边 84px，第一名海报每边 84px。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、评分计算、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认左侧阴影和内容坐标。

### 2026-06-05 - 7.4c follow-up / 榜单海报阴影安全留白对齐媒体库

完成了什么：

- 查看媒体库 `LibraryPage` 海报实现：普通海报本体 `180x270`，发光阴影 host 为 `348x438`，`Canvas.Left/Top=-84`，`PosterCachedShadowBehavior.Padding=84`。
- 榜单普通 Movie / TV 海报同步媒体库 84px 阴影安全区；只扩大 shadow host 和 padding，不移动海报本体。
- 榜单第一名 Movie / TV 海报也使用每边 84px 阴影安全区，shadow host 调整为 `384x492`。
- 保持榜单内容布局不变：海报尺寸、海报列宽、第一名 margin、普通项 margin、右侧信息区和滚动条位置均不跟随移动。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、评分计算、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认左侧阴影完整性。

### 2026-06-05 - 7.4c follow-up / 榜单右上标签与海报阴影可视区修复

完成了什么：

- 榜单 Movie / TV 普通项和第一名海报右上角统一不再显示想看状态；本轮补删 TV 榜单 `当前想看` 标签。
- 榜单外层滚动条用局部 ScrollBar style 做 `RenderTransform X=4`，只移动滚动条视觉位置，不改 ScrollViewer 内容布局。
- 榜单第一名和普通项卡片显式关闭裁剪，并恢复海报 palette shadow 的 `Canvas.Left=-60`，让 60px 左侧发光阴影有完整可绘制区域。
- 保持海报本体尺寸、海报列宽、第一名 Grid margin、普通双列 item margin 和右侧信息区位置不变。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、评分计算、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认 TV 标签删除、左侧发光阴影完整性和滚动条位置。

### 2026-06-05 - 7.4c follow-up / 榜单下拉阴影与第一名留白再修

完成了什么：

- 榜单下拉主菜单和二级菜单去掉 `ShadowPopup`，避免圆角菜单外侧出现直角黑色阴影。
- 第一名 Movie / TV 顶部区域左右留白从 18px 提高到 36px，海报进一步右移，右侧文字可显示宽度同步缩短。
- 导演字段上限继续收窄：第一名 210px、普通项 105px；演员使用剩余宽度，目标接近 3:7。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认下拉阴影、第一名留白和导演 / 演员比例。

### 2026-06-05 - 7.4c follow-up / 榜单标题行与第一名留白修复

完成了什么：

- 榜单第一行改为标题 / 竖线 / 原名在左、评分标签固定在右的结构；标题左端与日期、导演、简介三行左端对齐。
- 去掉标题 / 原名之间的比例列预留，改用标题 Auto + MaxWidth、竖线、原名剩余列，避免竖线被剧名预留宽度推偏。
- 标题 / 原名 / 竖线和评分标签在同一行高度内垂直居中；竖线使用 `BrushForegroundPrimary`，保持 2px 宽和 8px 两侧间距。
- 第一名 Movie / TV 顶部区域整体增加左右 18px margin，海报左边缘和简介右边缘到外层背景边框的留白对齐，并缩短右侧文字宽度。
- 导演长度上限进一步收窄：第一名 250px、普通项 130px，演员继续跟随导演后方并使用剩余宽度。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认标题行、评分固定位置、竖线位置和第一名留白。

### 2026-06-05 - 7.4c follow-up / 榜单第一名与普通项层级微调

完成了什么：

- 新增第一名专用 Movie / TV 榜单海报模板，尺寸为 216x324；普通双列项继续使用 180x270，实现第一名海报比普通海报大 20%。
- 普通双列项标题、原名、评分、简介等字号下调；日期、标签、导演、演员统一为比简介更小的 meta 字号，简介颜色改为 `BrushForegroundPrimary`。
- 标题 / 原名行改为评分标签 + 底部对齐标题组结构；中间竖线加粗到 2px，并在标题 / 原名文本组内垂直居中，两侧间距收紧到约两个空格。
- 导演 / 演员行改为导演字段 Auto + `MaxWidth` + 演员字段紧随的流式布局，导演过长时截断，演员不再固定到半宽列。
- Movie 榜单海报右上角想看按钮改为榜单专用样式：高度 22px，`+ 想看` 默认更窄，`取消想看` 仅同步增高。
- Movie / TV 第一名和下方普通双列区域之间新增横向分隔线。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认第一名层级、竖线位置、导演 / 演员流式间距和按钮宽高。

### 2026-06-05 - 7.4c follow-up / 榜单视觉二次微调

完成了什么：

- 将榜单 Movie / TV 海报从 240x360 调整为 180x270，并同步调整榜单顶部第一名、普通双列项的列宽和最小行高。
- 将榜单第一名和普通项外层玻璃卡片背景改为透明，去掉海报与右侧信息区背后的大圆角矩形背景。
- 将原名样式改为与片名 / 剧名完全一致，包括字体大小、字重和颜色。
- 将日期、标签、导演、演员统一为同一 meta 样式：13.5px、19px line height、`BrushForegroundSecondary`，并同步榜单标签分隔符颜色。
- 恢复当前 XAML 中遗留的静态中文乱码点：`全部`、隐藏 TabItem `影片搜索` / `AI推荐`。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认海报尺寸、无背景可读性和第二 / 第三行字色字号是否符合主观预期。

### 2026-06-05 - 7.4c follow-up / 榜单海报与四行信息布局反馈修复

完成了什么：

- 榜单 Movie / TV 顶部第一名和普通双列项统一改为“左侧详情页式海报 + 右侧四行信息”结构；海报复用详情页 240x360、20px 圆角、w780 解码、双层 palette shadow 和大海报占位模板。
- 榜单海报左上排名标签保留；右上角改为复制影片搜索海报右上角状态：Movie 使用 `+ 想看` / `取消想看` 搜索海报按钮样式，TV 使用 `当前想看` 搜索海报标签样式。
- 榜单右侧第一行改为评分、标题、竖线、原名；评分标签复制首页 AI 推荐评分 chip 质感并放大，第一名使用更大的评分标签。
- 第二行改为日期 + 媒体库海报标签行式类型标签；第三行改为导演 + 演员，使用截断 tooltip；第四行为内部可滚动简介。
- 榜单简介滚轮处理复制影片识别修正弹窗“修正为电影”候选简介逻辑：简介可滚时滚轮留在简介内，简介不可滚时交给外层榜单滚动。
- 修复本轮 XAML 写回暴露的影片发现页静态中文属性损坏点，恢复 Tab、榜单菜单、清除筛选、布局切换和分页 tooltip 文案。

验证：
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 未启动完整桌面应用做人工截图验收；仍需实际窗口确认榜单大海报密度、两列宽度和简介滚轮手感。

### 2026-06-05 - 7.4c follow-up / 榜单二次测试反馈修复

完成了什么：

- 榜单加载层补齐影片搜索同款 spinner，并将榜单内容卡片显式设为箭头光标。
- 榜单 Tab 加强菜单打开门禁：非榜单 Tab 悬停不打开菜单；点击时先切到榜单页，再打开下拉菜单。
- 榜单分页位置调整到 Movie / TV 各自滚动内容末尾，按影片搜索列表分页结构使用 `Grid MinHeight=ScrollViewer.ActualHeight`，组件继续沿用 chevron 图标按钮和页码文本。
- 榜单二级菜单 `趋势榜` 文字与 `热门榜` / `高分榜` 对齐，并收紧右侧箭头间距。
- 空关键词切换 `搜索方式` 不再触发 Movie / TV 结果重载；电视剧通过共同 setter 同步接入。
- 搜索工具栏排序菜单改为当前媒介感知：Movie 写入 `SelectedSortOption`，TV 写入 `SelectedTvSortOption`，修复 TV 空关键词非相关度排序无法直接 discover。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。

不属于本次：

- 不改 TMDB 搜索 / 榜单请求、榜单排序、AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。

### 2026-06-05 - 7.4c follow-up / 榜单与空关键词 Discover 测试反馈修复

完成了什么：

- 榜单页移除顶部圆角筛选卡片；榜单媒体类型、榜单类型和趋势时间统一迁移到顶层 `榜单` Tab 的多级下拉菜单。
- `榜单` Tab 下拉菜单按媒体库标签菜单方式向右展开，一级为电影 / 电视剧，二级为热门榜 / 高分榜 / 趋势榜，趋势榜三级为今日趋势 / 本周趋势。
- 从其它 Tab 点击 `榜单` 仅切换页面；已在榜单页时悬停或点击 `榜单` 才打开下拉菜单。
- 榜单分页复制影片搜索分页组件，改为 chevron 图标按钮和页码文本。
- 榜单双列结果分隔线改为连续单条竖线，榜单结果卡片默认光标改为箭头。
- 空关键词搜索在排序不是 `相关度` 时调用 TMDB `/discover/movie` 或 `/discover/tv`；排序、类型、地区、语言、单年代日期范围尽量下推到 TMDB discover，其它状态类筛选继续本地过滤。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 AI 推荐、扫描识别、播放器、数据库 schema、migration、database update、commit 或 push。
- 不把 TMDB discover 不支持的筛选条件伪造成远端过滤；仍保留本地过滤闭环。

### 2026-06-05 - 7.4b follow-up / 搜索卡片与列表二次测试反馈修复

完成了什么：

- Movie / TV 搜索海报卡片底部标题、日期和标签统一对齐左上角类型标签左边框。
- 搜索海报评分 chip 改为和类型标签一致的浅色背景、无边框质感，圆角降为 6px；Movie / TV 无评分统一显示 `--`。
- Movie 海报右上角想看按钮和 TV 海报右上角 `当前想看` 标签固定为 20px 高度并顶部对齐。
- Movie 搜索卡片和列表只展示 TMDB 类型标签；本地已入库影片不再把 AI 标签、情绪标签或场景标签回填到搜索结果。
- Movie / TV 搜索列表使用媒体库同款 14px 行间距，并在标题行补齐导演 / 演员字段。
- Movie 搜索通过 TMDB details 补齐导演 / 演员；TV 搜索通过 Series details 补齐导演 / 演员。
- 媒体库 Movie 列表评分改为 TMDB/IMDb 加权值，双源时来源显示 `TMDB/IMDb`，与搜索页加权评分口径一致。

验证：

- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 搜索请求形态、搜索筛选规则、榜单排序、AI 推荐、扫描识别、播放器、数据库 schema、migration 或 database update。
- 不新增 TV series 级想看切换；TV 仍只展示已有想看季状态。

### 2026-06-05 - 7.4b follow-up / 工具栏、结果区域与想看摘要测试反馈修复

完成了什么：

- 影片搜索工具栏第一行拆为两个视觉组：左侧输入框 / 搜索图标 / 顺序图标，右侧排序 / 影视 / 搜索方式 / 清除筛选；顺序图标改为透明图标按钮，和搜索图标一样只在 hover / pressed 时出现选中反馈。
- 影片搜索清除筛选按钮新增 `CanClearSearchFilters`，按当前 Movie / TV 激活模式判断是否处于默认筛选状态，默认状态下禁用。
- 影片搜索海报结果对齐媒体库海报槽位和安全留白，补齐 194x288 槽位和 `0,10,0,30` 内容边距，降低结果区左右边距和阴影安全区不一致的问题。
- 影片搜索和媒体库布局切换组件统一为 28px 高度、小圆角本地模板，外层圆角 4px，选中按钮圆角 3px；Discovery 组件不再使用额外固定最小宽度。
- Movie 搜索结果的想看取消路径增加无本地状态 fallback：状态解析器返回无状态时清空卡片入库 / 可见 / 播放源 / 用户状态字段，再重建搜索摘要；主动入库状态仍由解析器保留。
- 媒体库工具栏同步改为左侧输入框 / 搜索图标 / 顺序图标、右侧排序 / 清除筛选两组；媒体库海报和列表小字加粗到 `SemiBold`。

验证：
- `git diff --check` passed，仅有 LF 将被 Git 转 CRLF 的提示。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改搜索服务、TMDB 请求、筛选语义、榜单、AI 推荐、扫描识别、播放器、数据库 schema、migration 或 database update。
- 不新增 TV series 级想看切换；TV 搜索结果仍只展示已有想看季状态。

### 2026-06-05 - 7.4b follow-up / 搜索结果卡片测试反馈修复

完成了什么：

- 影片搜索 Movie / TV 海报结果改为媒体库式 180x270 海报卡片，复制媒体库卡片的裁剪、阴影、占位、渐变、左上类型标签和底部信息区。
- Movie 海报右上角改为 `+ 想看` / `取消想看` 按钮并用不同颜色区分；TV 海报右上角改为存在想看季时显示 `当前想看`。
- 海报卡片移除进度条和进度标签；标题 / 日期区域为右侧预留评分空间，评分标签复制首页 AI 推荐评分标签样式并与标题 / 日期两行垂直居中。
- 影片搜索 Movie / TV 列表改为媒体库式列表行，保留左侧评分块、中间标题 / 标签行、右侧标签区，移除进度条和进度标签。
- Movie 列表右侧播放源标签改为 `+ 想看` / `取消想看` 按钮；TV 列表右侧按已有想看季状态显示 `当前想看` 标签。

验证：

- `git diff --check` passed。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改搜索服务、TMDB 请求、筛选语义、榜单、AI 推荐、扫描识别、播放器、数据库 schema、migration 或 database update。
- 不新增 TV series 级想看切换；TV 只展示已有想看季状态。

### 2026-06-05 - 7.4b follow-up / 搜索工具栏三次测试反馈修复

完成了什么：

- 影片搜索第一行搜索输入框在侧栏收起 / 展开状态下分别调整为 544 / 440，释放空间进入右侧控件组的等权星号间隔；`清除筛选` 继续锚定工具栏右端。
- 影片搜索第一行右侧控件顺序改为 `顺序图标 / 排序 / 影视 / 搜索方式 / 清除筛选`，`搜索方式` 从第二行移动到 `影视` 右侧。
- 影片搜索第二行 Movie / TV 筛选从 `类型` 开始，保持第一个按钮左端对齐搜索输入框左边缘，最右侧 `播放源` 对齐工具栏右边缘。

验证：

- `git diff --check` passed。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改搜索服务、TMDB 请求、筛选语义、榜单、AI 推荐、扫描识别、播放器、数据库 schema、migration 或 database update。

### 2026-06-05 - 7.4b follow-up / 搜索工具栏二次测试反馈修复

完成了什么：

- 媒体库搜索输入框和结果摘要字体加粗。
- 影片搜索第一行右侧控件改为 `顺序图标 / 排序 / 影视 / 清除筛选`，`影视` 从第二行移动到排序右侧。
- 影片搜索第二行 Movie / TV 筛选都从 `搜索方式` 开始，顺序固定为：搜索方式、类型、地区、语言、年代、入库状态、观看状态、收藏状态、播放源。
- 第二行筛选网格保留等距星号间隔，搜索方式左端对齐输入框左端，播放源右端对齐清除筛选右端。

验证：

- `git diff --check` passed。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。

不属于本次：

- 不改搜索服务、TMDB 请求、筛选语义、榜单、AI 推荐、扫描识别、播放器、数据库 schema、migration 或 database update。

### 2026-06-05 - 7.4b follow-up / 搜索工具栏测试反馈修复

完成了什么：

- 媒体库搜索输入框显式对齐影片搜索输入框字体设置。
- 影片搜索新增 `入库状态：全部 / 已入库 / 未入库` 单选筛选，Movie / TV 各自维护筛选状态。
- 影片搜索第二行筛选改为等距 10 列，顺序固定为：影视、搜索方式、类型、地区、语言、年代、入库状态、观看状态、收藏状态、播放源。
- 第二行筛选左端对齐搜索输入框，右端对齐清除筛选按钮。
- 搜索加载时清空当前结果和摘要，并显示原有加载转圈与提示文案。
- 搜索摘要改为 `共找到 x 项媒体 · 已入库 x 项 · 未入库 x 项`，不再显示布局切换或缓存提示。
- 搜索结果空白区域光标恢复为箭头，结果卡片和按钮仍保留可点击光标。
- 影片搜索分页改为透明 chevron 图标按钮，hover / pressed 时才显示边框和选中态。

验证：

- `git diff --check` passed。
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

不属于本次：

- 不改 TMDB 请求、搜索排序、榜单算法、AI 推荐、扫描识别、播放器、数据库 schema、migration 或 database update。
- 不改榜单分页按钮。

### 2026-06-05 - 7.4b follow-up / Tab 对齐根因修复

完成了什么：

- 顶层可见 Tab 头从 `TabItem` header 模板改为 `TabControl` 模板内的固定宽度手工按钮条。
- 每个可见 Tab 按钮固定为 104px，文字和选中下划线在同一坐标系中居中；选中状态只切换下划线颜色，不改变线宽。
- 原 `TabPanel` 仅保留为 0 高度隐藏 ItemsHost，避免 WPF header 单元测量 / 裁剪影响可见对齐。
- 新增 `SelectDiscoveryTabCommand` 和三个 Tab 选中状态投影，继续复用 `SelectedTabIndex` 作为唯一状态源。
- Tab 区域和底部分隔横线整体上移。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- WPF 离屏像素验证显示三个下划线均为 104px；`影片搜索` / `榜单` 文字中心与线中心差值为 0px，`AI推荐` 为 -0.5px。

不属于本次：

- 不调整搜索、榜单、AI 推荐、推荐算法、TMDB 请求、扫描、数据库 schema、migration 或 database update。

### 2026-06-04 - 7.4a

完成了什么：

- 影片搜索的搜索方式控件改为 Movie / TV 都可见。
- 搜索方式首项改为中性文案 `按片名搜`，避免 TV 模式下继续显示 `按影片搜`。
- 搜索框接入已有 `TextBoxPlaceholderBehavior`，根据当前媒介和搜索方式显示：
  - `输入需要搜索的电影`
  - `输入需要搜索的电视剧名`
  - `输入需要搜索的导演/演员`
- TV 人物搜索已接入 TMDB `person/{id}/tv_credits`，只投影为 Discovery TV 搜索结果。
- `切布局` 占位命令替换为真实海报 / 列表布局切换。
- Discovery 搜索布局记忆写入独立 App-layer 文件 `discovery-preferences.json`，不复用媒体库偏好文件。
- Movie / TV 搜索结果都增加了基础列表容器和分页控件；影片搜索整体视觉对齐归 7.4b。
- 搜索加载、空状态、结果加载和清除筛选文案按当前媒介 / 搜索方式生成。

验收标准：

- Movie / TV 切换正常。
- Movie / TV 都能选择标题搜索和人物搜索。
- 三条 placeholder 文案完全匹配本文件要求。
- Movie / TV 搜索筛选字段保持原有 Discovery 字段集。
- 海报 / 列表布局切换可用，并通过独立 Discovery 偏好文件持久化。
- TV 人物搜索结果保持为 TV Series 发现行，不进入 Movie AI 推荐、观影洞察、画像、人格或推荐 fingerprint。
- 搜索卡片没有新增 `已移出媒体库` 按钮。

不属于本阶段：

- 7.4b 影片搜索整体 UI 视觉对齐，包括工具栏、筛选控件、结果提示、滚动 / 密度、海报 / 列表结果区域和最终卡片视觉。
- 7.4c 榜单视觉一致性。
- 7.4d AI 推荐 Tab 和偏好弹窗。
- 扫描、修正、播放器、推荐算法、设置页、详情页、媒体库页面。

是否修改业务逻辑：

- 有，范围仅限 Discovery 搜索：新增 `ITmdbService.SearchTvSeriesByPersonAsync` / `GetPersonTvCreditsAsync`，供 TV + 人物搜索使用。
- 有，范围仅限 UI 偏好：新增 Discovery 搜索布局偏好文件。
- 无推荐算法、prompt、候选生成、画像 / 人格、Watch Insights、scan、correction、player、schema、migration、database update 变更。

是否修改非本阶段页面：

- 无页面修改。
- 共享 DI 注册新增 `IDiscoveryPreferencesService`。
- Core TMDB service/interface 新增 TV person credits 方法，仅由 Discovery 搜索调用。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。

### 2026-06-04 - 7.4b

完成了什么：

- 影片搜索顶部改为 Discovery 本地的媒体库式工具栏结构：媒介、搜索方式、关键词输入、搜索按钮、筛选按钮和清除筛选使用紧凑按钮 / ContextMenu 形态；布局切换放在搜索结果区域第一行右侧。
- Movie / TV 筛选字段继续使用既有 Discovery 字段集；菜单选择只写入已有 `Selected*` 筛选属性。
- 搜索结果提示仍使用 Discovery 的 `ActiveSearchStatusMessage` / `ActiveSearchSummaryText`，未照搬媒体库摘要语义。
- 搜索结果区改为统一玻璃卡片容器，Movie / TV 海报和列表结果保持独立可见性、滚动和分页。
- Movie 海报卡：无进度条；左上角 `电影`；右上角 `+ 想看` / 当前想看状态动作；底部显示标题、年份、类型和评分标签；条件显示加入媒体库入口。
- TV 海报卡：无进度条；左上角 `电视剧`；右上角只在 `HasWantToWatchSeasonTag=True` 时显示 `想看` 标签；没有 TV `+ 想看` 操作按钮；底部显示标题、年份、类型和评分标签。
- Movie 列表行：左侧评分块；中部标题、年份、来源状态、观看状态、标签和简介；右上角为 Movie 想看动作；右下角保留 `电影` 标签；条件显示加入媒体库入口。
- TV 列表行：左侧评分块；中部标题、年份、季数、媒体库状态、标签和简介；右上角只在有想看季时显示 `想看` 标签，没有想看季则不显示；右下角保留 `电视剧` 标签；条件显示加入媒体库入口。

验收标准：

- 搜索工具栏和筛选控件采用媒体库式结构但字段为 Discovery 专属。
- 结果提示文案和布局不照搬媒体库错误语义。
- 海报卡片没有进度条。
- Movie / TV 海报卡左上角类型标签存在。
- Movie 海报和列表右上角为想看状态动作。
- TV 海报和列表不提供 `+ 想看` 操作；仅当存在想看季时显示 `想看` 标签，没有想看季则不显示。
- 评分标签可见且不遮挡标题、想看状态或加入媒体库入口。
- 列表右下角 Movie / TV 标签保留。
- 不出现 `已移出媒体库` 搜索卡片按钮。
- 来源 / 加入媒体库语义仍正确，不创建播放源、不修改媒体库可见性规则。

不属于本阶段：

- 搜索服务排序、TMDB 请求方式、AI 推荐、榜单视觉、详情页布局、媒体库卡片行为、播放器行为。
- TV 整剧级 `+ 想看` 操作；TV 仅显示已有季级想看状态标签。
- 榜单和 AI 推荐 Tab 的完整视觉重做仍按 7.4c / 7.4d 处理；本轮只补齐影片搜索页头、Tab、搜索工具栏、筛选和滚动一致性。

是否修改业务逻辑：

- 无 service、TMDB、推荐算法、数据库、migration 或媒体库可见性业务变更。
- 有 UI 绑定层投影：新增搜索筛选菜单选择命令写入既有 `Selected*` 属性；新增短评分标签和 TV `HasWantToWatchSeasonTag` 展示投影。

是否修改非本阶段页面：

- 无。
- 未抽取或修改 Home、Recommendation、Media Library 共享资源；7.4b 样式和菜单打开处理均限定在 `MovieDiscoveryPage`。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。

### 2026-06-04 - 7.4b follow-up / 媒体库一致性修复

完成了什么：

- 影片发现页移除了页内重复标题和外层大卡片背景；页面标题、副标题由 Shell 标题栏承载，副标题使用草图文案 `遇见更多适合你的影片`。
- 顶层 Tab 改为标题栏下方横线上的左对齐样式：无边框，当前 Tab 使用强调色文字和同色下划线，内容区从横线下方开始；后续回归修正将 Tab 完整文案定为 `影片搜索` / `榜单` / `AI推荐`。
- 影片搜索工具栏继续向媒体库对齐：28px 搜索框、输入清空按钮、搜索图标按钮、独立顺序图标按钮、紧凑筛选按钮、右侧三控件网格、媒体库式 ContextMenu 点击 / 再点关闭逻辑。
- 搜索结果圆角区域第一行承载结果摘要和海报 / 列表分段切换，右侧布局切换位置与媒体库结果区域一致；第二行才是空状态、海报列表或列表结果滚动内容。
- 影片搜索海报卡片密度收紧到媒体库式 180x270 基准，保留 Discovery 专属类型、想看、评分和加入媒体库语义。
- Discovery 本地复用了媒体库菜单模板、菜单居中打开逻辑、空白处清焦点和滚动条自动显隐样式；未抽成全局资源，避免影响其他页面。
- Movie / TV 的类型、地区、语言、年代改为媒体库式多选：`全部` 清空并关闭菜单，非全部项可多选且保持菜单打开，按钮文案最多展示两个选项，超过两个显示数量；收藏状态回归普通单选菜单。
- `清除筛选` 只重置筛选选项和分页，不清空搜索词，也不清空当前搜索结果池。
- Movie 搜索筛选拆分为：观看状态 `全部 / 已看 / 未看`、播放源 `全部 / 有播放源 / 无播放源`、收藏状态 `全部 / 喜爱 / 想看 / 不想看`。
- TV 搜索筛选拆分为：播放源、观看状态、收藏状态；TV 收藏状态为单选逻辑，TV 观看状态通过 Series 下 Episode 已看状态聚合投影。
- 搜索方式按钮文案改为 `搜索方式：按电影名 / 按人物名` 或 `搜索方式：按电视剧名 / 按人物名`；媒介按钮文案改为 `影视：电影 / 电视剧`。
- 搜索结果区和榜单内容区增加单次运行内滚动进度持久化：切换导航页或进入详情页再返回时恢复原 offset；新搜索、筛选重建、翻页或重载榜单会回到顶部；重启软件后不保留。

验收标准：

- 页内不再出现重复 `影片搜索` 标题，也不再有包住整个页面的大边框背景。
- Tab 首项选中下划线左边缘与标题栏标题左边缘保持同一内容列；Tab 文字居中于选中下划线；Tab 使用完整文案 `影片搜索` / `榜单` / `AI推荐`。
- Tab 只有横线、强调色文字和同色下划线，不使用边框卡片。
- 影片搜索工具栏按钮、输入框、清空图标、搜索图标、顺序图标、右侧排序 / 清除控件网格、菜单尺寸和滚动条应按媒体库紧凑规则实现。
- 搜索结果区域第一行应同时包含摘要和海报 / 列表切换；不要把布局切换放回搜索工具栏，也不要放成工具栏与结果区之间的独立行。
- 海报结果应采用媒体库式 180x270 密度，避免搜索结果一屏列数明显少于媒体库。
- Movie / TV 类型、地区、语言、年代支持多选并保持菜单打开；选择 `全部` 或选满所有非 `全部` 项时清空并关闭菜单。收藏状态为普通单选。
- `清除筛选` 不清搜索词、不清结果池。
- Movie / TV 的筛选按钮文案必须分别带 `影视：`、`搜索方式：`、`播放源：`、`观看状态：`、`收藏状态：` 前缀。
- 搜索结果和榜单滚动 offset 只在当前软件运行会话内持久化，不落库、不进偏好文件、不进导航状态服务。

可复用规则 / 组件：

- Shell 已提供页面标题时，页面内容区不得再次放同名大标题；副标题应由 PageViewModel 标题栏副标题承载。
- 顶层 Tab 需要靠近标题栏时，优先使用“横线 + 左对齐 Tab + 当前项强调色下划线”的轻量样式；Tab 内容区从横线下方开始。
- 页面主标题和 Tab 属于不同层级：Shell 标题保留完整页面名，Tab 使用短文案承载分区，避免在内容区重复出现同名标题。
- 媒体库式搜索工具栏的第一行结构为：固定宽搜索框、8px 间距、30px 搜索图标、40px 间距、右侧 `Auto/*/Auto/*/Auto` 三控件网格。
- 与媒体库对齐的筛选按钮应使用 28px 紧凑高度、居中下拉菜单、再次点击关闭、ContextMenu 圆角壳和 12pt 菜单项。
- 与媒体库对齐的结果区域应使用一个独立圆角卡片：第一行左侧放状态 / 摘要文案，右侧放海报 / 列表分段切换；第二行放实际滚动内容或空状态。
- 搜索海报卡片的尺寸 / 间距优先按媒体库密度收紧；卡片内部动作和状态可以保留 Discovery 专属语义。
- 内容滚动区域使用隐藏式 6px 垂直滚动条；滚动、拖动或 hover 时自动显隐，横向滚动条禁用。
- 多选筛选统一规则：`全部` 表示空集合；非全部项 `StaysOpenOnClick=True`；选满所有非 `全部` 项时自动回到空集合 / `全部`；按钮文案按选项顺序显示，0 项为 `全部`，1-2 项直接拼接，超过 2 项显示数量。
- 结果列表 / 榜单的滚动位置属于 ViewModel 会话状态；用直接 `ScrollToVerticalOffset` 恢复，不使用动画，不写入数据库、migration 或持久化偏好。
- 内容集合发生实质重建时清零对应 offset；导航离开、详情返回、Tab 切换或布局切换不应清零当前内容的 offset。

### 2026-06-04 - 7.4b follow-up-2 / 搜索页细节回归修复

完成了什么：

- Tab 分隔线和选中下划线加粗，Tab 文字居中于更长的选中下划线；首个 Tab 的选中线左端保持与页面内容列对齐。
- Tab 文案固定为 `影片搜索` / `榜单` / `AI推荐`。
- Discovery Tab 选择改为运行期内存状态：首次直接打开仍默认 `影片搜索`，切换导航离开再返回不会重置；该状态不落库、不写偏好文件、不跨软件重启。
- 搜索输入框回车搜索后清除输入框焦点。
- `清除筛选` 按钮宽度按媒体库清除按钮的紧凑行为处理；搜索工具栏空白区显式使用箭头光标。
- 搜索方式改为 `按电影名` / `按电视剧名` / `按人物名`。
- Movie / TV 类型、年代改为多选；收藏状态选满所有非 `全部` 项时也会回到 `全部`。
- 类型和地区菜单使用较短的滚动弹层、现代化细滚动条和较小的鼠标滚轮步长。

明确未做：

- 未修改搜索服务、TMDB 请求形态、榜单算法、推荐算法、扫描识别、数据库 schema、migration、database update、commit 或 push。
- 播放源、观看状态仍为单选。

### 2026-06-04 - 7.4c

完成了什么：

- 榜单 Tab 头部改为 Discovery 本地的媒体库式按钮 / ContextMenu 结构：`影视：电影 / 电视剧`、`榜单：热门榜 / 高分榜 / 趋势榜`、`时间：今日趋势 / 本周趋势`。
- 榜单状态文案和摘要延续 `ActiveRankingStatusMessage` / `ActiveRankingSummaryText`，右侧补当前页徽标 `ActiveRankingPageStatusText`。
- 榜单内容区改为统一玻璃卡片容器，Movie / TV 继续使用独立 ScrollViewer、加载可见性、空状态和分页栈。
- Movie Top card 改为重点卡视觉：海报、排名、`电影` 类型徽标、评分徽标、播放源状态、年份、观看状态、想看动作和加入媒体库 / 恢复动作。
- Movie 普通榜单行改为左右双列紧凑卡片；保留排名、海报、评分、年份、标题、标签、简介、想看动作和加入媒体库 / 恢复动作。
- TV Top card 改为与 Movie 一致的重点卡视觉；显示 `电视剧`、评分、媒体库状态、年份、季数、观看状态、Season 状态摘要；只在存在想看季时显示 `想看` 标签。
- TV 普通榜单行改为左右双列紧凑卡片；补齐想看季标签和加入媒体库 / 恢复按钮，继续复用已有 TV metadata hydration 和剧详情入口。
- 榜单分页按钮改为 Discovery 工具栏按钮样式，保留 Movie / TV 独立 CanGoPrevious / CanGoNext 绑定。

验收标准：

- Movie / TV 榜单切换仍通过 `SelectedRankingMediaType` 触发既有榜单重载。
- 热门榜 / 高分榜 / 趋势榜和今日 / 本周趋势仍调用原有 TMDB 榜单服务。
- 榜单显示顺序仍由 `_rankingMovies` / `_rankingTvSeries` 的追加顺序和 `Skip/Take` 页内切片决定，不新增 UI 排序。
- 加载时隐藏旧内容，空状态和错误状态仍使用 Active ranking status 文案。
- Movie / TV 榜单卡片语言和 7.4b 搜索卡片保持一致：类型徽标、评分徽标、Movie 想看动作、TV 想看季标签、加入媒体库 / 恢复动作。
- TV 榜单仍不进入 AI 推荐、Watch Insights、画像、观影人格或推荐 fingerprint。

不属于本阶段：

- 榜单算法、排序规则、TMDB 请求方式、搜索过滤、AI 推荐、TV AI 推荐、详情页行为、媒体库卡片行为、播放器行为。
- 榜单样式未抽取为全局资源；仍限定在 `MovieDiscoveryPage` 本地资源。
- AI 推荐 Tab 和推荐偏好弹窗仍按 7.4d 处理。

是否修改业务逻辑：

- 无 service、TMDB、推荐算法、数据库、migration 或媒体库可见性业务变更。
- 有 UI 命令绑定层补齐：新增 `SelectRankingMediaTypeCommand`，只用于榜单头部按钮菜单写入既有 `SelectedRankingMediaType`；后续重载仍由原属性 setter 执行。
- 有 UI 展示投影补齐：Movie / TV 榜单卡片显示已有评分、播放源 / 媒体库状态、Movie 想看、TV 想看季、加入媒体库 / 恢复按钮；未改变 TMDB 顺序或候选纳入范围。

是否修改非本阶段页面：

- 无。
- 未修改 Home、Recommendation、Media Library 共享资源；7.4c 样式、卡片和菜单改动均限定在 `MovieDiscoveryPage`。

验证：

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。

## 总范围

7.4 负责把影片发现做成完整可验收的 UI 阶段：

- `影片搜索`：Movie / TV 搜索、搜索方式、筛选、搜索结果提示文案、海报 / 列表布局、结果卡片。
- `榜单`：Movie / TV 榜单视觉一致性、头部重点卡、列表行和分页控件；保持 TMDB 返回顺序。
- `AI 推荐`：嵌入式推荐 Tab 和自定义推荐偏好弹窗的视觉对齐；继续复用现有推荐服务。
- Discovery 通用状态：加载、空状态、错误状态、布局切换、滚动、海报画质 / 缺失海报、非破坏性来源和用户状态文案。

## 绝对不做

- 不改 Movie AI 推荐算法、prompt、候选生成、硬过滤、画像 / 人格输入或 fingerprint。
- 不新增 TV AI 推荐、Movie + TV 混合推荐、TV 观影洞察、混合画像或 TV 推荐偏好语义。
- 不改扫描匹配规则、TMDB 置信门、修正写入语义、播放器行为、数据库 schema、migration 或 database update 流程。
- 不创建假的 `MediaFile`、假的播放源、假的远端 URL 或隐式媒体库可见性写入。
- 不在 Discovery 搜索卡片中重新引入 `已移出媒体库` 按钮或破坏性文案。
- 不把截图当颜色、token 或业务语义来源。截图只作为布局参考，样式和行为以 page-spec 与共享资源为准。

## 用户确认的搜索页要求

这些点是 7.4 的强制要求：

- 影片搜索整体可以基本照抄媒体库的搜索 / 筛选 / 布局结构。
- 海报布局和媒体库不同：
  - 没有进度条；
  - 左上角 Movie / TV 标签不变；
  - Movie 右上角变为 `+ 想看` / 当前想看状态动作；
  - TV 右上角不显示 `+ 想看` 操作，只在存在想看季时显示 `想看` 标签，没有想看季则不显示；
  - 评分标签可优先复用首页 AI 推荐中的视觉方式。
- 筛选按钮按媒体库式结构组织，只是字段不同；尺寸、图标、下拉菜单、滚动条和多选逻辑应优先对齐媒体库。
- 搜索卡片不显示 `已移出媒体库` 按钮。
- 电视剧搜索要在现有基础上增加搜索方式选项。若包含人物搜索，需要补齐 TMDB TV/person credits 的 Discovery 搜索链路，并明确记录为 7.4 的受限业务变更。
- 搜索框 placeholder 根据当前搜索方式变化：
  - 人物：`输入需要搜索的导演/演员`
  - 电影：`输入需要搜索的电影`
  - 电视剧：`输入需要搜索的电视剧名`
- 搜索结果提示字段使用 Discovery 自己的文案，不直接照搬媒体库含义不同的摘要文案。
- 列表布局右下角 Movie / TV 标签不变；Movie 列表右上角原本本地 / 网盘 / 无播放源一类来源标签位置，改为 `+ 想看` / 当前想看状态动作；TV 列表右上角只在存在想看季时显示 `想看` 标签，没有想看季则不显示。

## 可复用内容

实现前先找这些既有内容，不要先写页面私有变体：

- 媒体库搜索与筛选结构：搜索框、提交语义、筛选菜单密度、布局切换、列表 / 海报节奏、来源状态文案、滚动条、加载和空状态。
- 媒体库布局偏好模式：若 Discovery 需要记住海报 / 列表布局，可使用文件型 UI 偏好；不得加数据库字段或 migration。
- 首页 AI 推荐卡片：`+ 想看` 按钮、评分标签、海报 lift / 裁切、紧凑原因和标签表达。
- AI 推荐页：推荐卡片操作、自定义推荐偏好弹窗、刷新 / 换一批、加载 / 空 / 错误状态。
- 全局弹窗壳：用于确认类或偏好类 overlay；弹窗正文仍归页面自己。
- 共享资源：`Inputs.xaml`、`Cards.xaml`、`Badges.xaml`、`Status.xaml`、`Dialogs.xaml`、`Glass.xaml`、`Text.xaml`、`Navigation.xaml`、`Theme.xaml`。
- 已有 helper：优先用 `TextBoxPlaceholderBehavior` 处理 placeholder，不写临时 TextBox overlay。
- 已有 UI 规则：`DesignDraft/codex-ui-rules.md`、`DesignDraft/UI_LOADING_STATE_GUIDELINES.md`、`DesignDraft/resources-note.md`、`DesignDraft/DESIGN.md`。

## 小阶段划分

### 7.4a - 搜索工具栏、搜索方式、筛选和布局状态

目标：

- 先完成 Discovery 搜索的 Movie / TV 切换、搜索方式、动态 placeholder、筛选字段、结果提示文案和海报 / 列表布局切换状态。媒体库式工具栏 / 筛选视觉、滚动密度和卡片视觉进入 7.4b。

必读：

- `AGENTS.md`
- `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md`
- `DesignDraft/PHASE_7_PLAN.md`
- `DesignDraft/PHASE_7_STAGE_LOG.md`
- `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- `DesignDraft/page-spec/movie-discovery-page.md`
- `DesignDraft/page-spec/media-library-page.md`
- `DesignDraft/UI_LOADING_STATE_GUIDELINES.md`
- `DesignDraft/codex-ui-rules.md`
- `DesignDraft/screenshots/影片发现/01-影片发现-展开导航栏-影片搜索-海报布局.png`
- `DesignDraft/screenshots/影片发现/02-影片发现-展开导航栏-影片搜索-列表布局.png`
- `DesignDraft/screenshots/媒体库/04-媒体库-关闭导航栏-海报布局.png`
- `DesignDraft/screenshots/媒体库/05-媒体库-关闭导航栏-列表布局.png`
- `src/MediaLibrary.App/Views/Pages/MovieDiscoveryPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/MovieDiscoveryViewModel.cs`
- `src/MediaLibrary.App/Helpers/TextBoxPlaceholderBehavior.cs`
- `src/MediaLibrary.App/Views/Pages/LibraryPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/LibraryViewModel.cs`
- `src/MediaLibrary.App/Models/Library/LibraryPreferencesModel.cs`
- `src/MediaLibrary.App/Services/Implementations/LibraryPreferencesService.cs`

按需读取：

- 只有实现 TV 人物搜索时，才读取 TMDB service interface / implementation。
- 只有布局记忆或想看状态绑定需要调整时，才读取用户收藏 / 偏好服务。
- 需要核对旧 Discovery 服务语义时，读取 `docs/movie-discovery/MOVIE_DISCOVERY_PLAN.md`。

可复用组件 / UI 规则：

- 媒体库搜索框、筛选菜单和布局切换。
- 媒体库滚动条与紧凑筛选样式。
- `TextBoxPlaceholderBehavior`。
- 加载规则：加载中只显示明确 loading，不提前显示空状态；加载完成后再显示 empty / error 文案。

可参考页面：

- 媒体库：搜索、筛选、列表密度。
- 电影修正弹窗：仅在工具栏输入框尺寸不明确时参考紧凑输入控件。

阶段输出要求：

- 完成了什么：列出工具栏、筛选、搜索方式、placeholder、结果提示和布局状态的实际改动。
- 验收标准：必须包含三条 placeholder 文案、Movie / TV 切换、TV 搜索方式、筛选 UI 与媒体库一致性、布局切换、加载 / 空 / 错误状态。
- 不属于本阶段：榜单卡片、AI 推荐卡片、详情页、扫描逻辑、播放器逻辑、推荐算法。
- 是否修改业务逻辑：必须明确写 `无` 或列出具体变更。允许的业务变更仅限 TV 人物搜索、布局 UI 偏好、状态文案 / 摘要投影，并且必须说明只影响 Discovery 搜索。
- 是否修改非本阶段页面：列出被触达的非 Discovery 页面或共享资源；没有就写 `无`。

### 7.4b - 影片搜索整体 UI 视觉对齐

目标：

- 按用户确认要求完成影片搜索整体 UI 视觉对齐，包括媒体库式搜索工具栏、筛选控件、结果提示、滚动 / 密度、海报布局、列表布局和 Movie / TV 结果卡片，同时保持已有来源、加入媒体库和想看状态语义。

必读：

- 7.4a 所有定义搜索行为的必读材料。
- `DesignDraft/page-spec/home-page.md`
- `DesignDraft/page-spec/recommendation-page.md`
- `DesignDraft/screenshots/影片发现/01-影片发现-展开导航栏-影片搜索-海报布局.png`
- `DesignDraft/screenshots/影片发现/02-影片发现-展开导航栏-影片搜索-列表布局.png`
- `DesignDraft/screenshots/媒体库/04-媒体库-关闭导航栏-海报布局.png`
- `DesignDraft/screenshots/媒体库/05-媒体库-关闭导航栏-列表布局.png`
- `src/MediaLibrary.App/ViewModels/Pages/DiscoveryMovieCardViewModel.cs`
- `src/MediaLibrary.App/ViewModels/Pages/DiscoveryTvSeriesCardViewModel.cs`
- `src/MediaLibrary.App/ViewModels/Pages/DiscoveryRatingPresenter.cs`
- `src/MediaLibrary.App/Views/Pages/HomePage.xaml`
- `src/MediaLibrary.App/Views/Pages/RecommendationsPage.xaml`

按需读取：

- 可复用资源章节列出的共享 ResourceDictionary。
- 只有触碰海报 decode size、缓存或缺失海报时，才读取海报缓存 / 图片 helper。
- 只有卡片点击进入详情出现回归时，才读取详情导航代码。

可复用组件 / UI 规则：

- 媒体库搜索工具栏、筛选按钮 / 下拉、布局切换和结果区域密度。
- 首页 AI 推荐评分标签与 `+ 想看` 按钮。
- 媒体库海报 / 列表几何、卡片间距和滚动表现。
- 共享海报圆角、缺失海报、海报画质选择规则。
- 现有加载动画和无海报状态规则。

可参考页面：

- 媒体库海报 / 列表卡片。
- 首页 AI 推荐卡片。
- AI 推荐页卡片操作区。

阶段输出要求：

- 完成了什么：列出搜索工具栏、筛选控件、结果提示、滚动 / 密度、海报结果区、列表结果区、Movie 卡片和 TV 卡片的实际视觉和绑定改动。
- 验收标准：搜索工具栏和筛选控件采用媒体库式结构但字段为 Discovery 专属；结果提示文案和布局不照搬媒体库错误语义；布局切换位于搜索结果区域第一行右侧；海报卡片没有进度条；左上角 Movie / TV 标签存在；Movie 右上角想看状态动作存在；TV 右上角仅在存在想看季时显示 `想看` 标签；评分标签可见且不遮挡；列表右下角 Movie / TV 标签保留；Movie 列表右上角来源标签位置改为想看动作；TV 列表右上角没有想看季时为空；不出现 `已移出媒体库`；来源 / 加入媒体库语义仍正确；与媒体库完全一致的卡片细节和非搜索 Tab 视觉为 Deferred。
- 不属于本阶段：搜索服务排序、TMDB 请求方式、AI 推荐、详情页布局、媒体库卡片行为、播放器行为。
- 是否修改业务逻辑：预期为 `无`。如确实修改命令绑定或状态投影，必须写明具体 Discovery card property / command，并证明未改变媒体库可见性或推荐语义。
- 是否修改非本阶段页面：列出共享样式、Home 或 Recommendation 资源抽取；确认这些页面的可见表现是否变化。

### 7.4c - 榜单 Tab 视觉一致性

目标：

- 统一 Movie / TV 榜单视觉、状态文案、头部重点卡、列表行和分页控件，并保持 TMDB 榜单顺序。

必读：

- `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md`
- `DesignDraft/page-spec/movie-discovery-page.md`
- `DesignDraft/page-spec/media-library-page.md`
- `docs/movie-discovery/MOVIE_DISCOVERY_PLAN.md`
- `DesignDraft/screenshots/影片发现/05-影片发现-展开导航栏-榜单.png`
- `DesignDraft/screenshots/影片发现/06-影片发现-关闭导航栏-榜单.png`
- `src/MediaLibrary.App/Views/Pages/MovieDiscoveryPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/MovieDiscoveryViewModel.cs`
- `src/MediaLibrary.App/ViewModels/Pages/DiscoveryMovieCardViewModel.cs`
- `src/MediaLibrary.App/ViewModels/Pages/DiscoveryTvSeriesCardViewModel.cs`

按需读取：

- 仅为核对顺序保持、加载或错误行为时读取 TMDB 榜单服务代码。
- 只有复用评分或想看视觉时，读取 Home / Recommendation 卡片代码。

可复用组件 / UI 规则：

- 7.4b 搜索卡片组件。
- 媒体库列表密度和滚动条。
- 首页 AI 评分标签和想看状态按钮。
- 榜单刷新、分页和空状态遵守加载规则。

可参考页面：

- 7.4b 完成后的 Discovery 搜索结果。
- 媒体库列表行。
- 首页推荐预览。

阶段输出要求：

- 完成了什么：列出榜单 Tab 的视觉、文案、分页和卡片 / 列表改动。
- 验收标准：Movie / TV 切换正常；热门 / 高分 / 趋势今日 / 本周保持现有语义；顺序遵守 TMDB 返回顺序；分页可用；加载和错误状态不会把旧内容伪装成当前内容；Movie / TV 卡片语言和 7.4b 一致。
- 不属于本阶段：榜单算法、排序规则、搜索过滤、AI 推荐、TV AI 推荐、详情页行为。
- 是否修改业务逻辑：预期为 `无`；如调整榜单状态投影，必须解释为何是 UI-only，并确认没有改变 TMDB 顺序或候选纳入范围。
- 是否修改非本阶段页面：列出被触达共享样式，并确认搜索、AI、媒体库页面是否受到可见影响。

### 7.4d - AI 推荐 Tab 和推荐偏好弹窗

目标：

- 对齐影片发现内嵌 AI 推荐 Tab 和自定义推荐偏好弹窗的视觉，同时保持现有 Movie-only 推荐服务语义。

必读：

- `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md`
- `DesignDraft/page-spec/movie-discovery-page.md`
- `DesignDraft/page-spec/recommendation-page.md`
- `DesignDraft/page-spec/home-page.md`
- `DesignDraft/page-spec/global-dialogs.md`
- `DesignDraft/screenshots/影片发现/07-影片发现-展开导航栏-AI推荐.png`
- `DesignDraft/screenshots/影片发现/08-影片发现-展开导航栏-AI推荐-自定义推荐偏好弹窗.png`
- `src/MediaLibrary.App/Views/Pages/MovieDiscoveryPage.xaml`
- `src/MediaLibrary.App/Views/Pages/RecommendationsPage.xaml`
- `src/MediaLibrary.App/ViewModels/Pages/RecommendationsViewModel.cs`
- `src/MediaLibrary.App/Views/Pages/HomePage.xaml`
- `src/MediaLibrary.Core/Services/Implementations/RecommendationPreferenceService.cs`
- `src/MediaLibrary.Core/Services/Implementations/SettingsService.cs`

按需读取：

- 只有 UI 绑定需要核实时，读取推荐 service interface / model。
- 只有首页 `发现更多影片` 或隐藏推荐路由受影响时，读取主导航 ViewModel。

可复用组件 / UI 规则：

- 现有 RecommendationsPage ViewModel 和服务流程。
- 已验收的首页 AI 推荐预览视觉。
- 全局弹窗壳。
- AI 刷新、换一批、空状态和错误状态遵守加载规则。

可参考页面：

- 首页 AI 推荐预览。
- 全局弹窗。
- 媒体库 overlay 仅用于间距和滚动容器参考。

阶段输出要求：

- 完成了什么：列出 AI Tab、推荐卡片、工具栏、加载状态和偏好弹窗改动。
- 验收标准：AI Tab 仍在 Discovery 内；首页 `发现更多影片` 仍进入 Discovery 的 AI Tab；主导航不新增独立 AI 页面；推荐仍为 Movie-only；偏好弹窗可打开、编辑、清空、取消、确认；想看 / 不想看动作仍正确；加载 / 空 / 错误状态清晰。
- 不属于本阶段：推荐算法、prompt、TV 推荐、画像 / 人格 / fingerprint、设置存储 schema、搜索和榜单行为。
- 是否修改业务逻辑：预期为 `无`；如偏好 UI 绑定必须调整，写明具体复用的设置 / 服务调用，并确认存储语义未变。
- 是否修改非本阶段页面：列出 Home 或隐藏 Recommendation 路由变化；确认 Home 预览视觉是否变化，还是仅抽取共享资源。

### 7.4e - Discovery 回归收口

目标：

- 对 7.4 做定向回归、文档更新和跨页面影响清点，确认 Discovery、媒体库、首页、AI 推荐之间没有越界改动。

必读：

- `AGENTS.md`
- `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md`
- `DesignDraft/PHASE_7_PLAN.md`
- `DesignDraft/PHASE_7_STAGE_LOG.md`
- `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- `docs/movie-discovery/MOVIE_DISCOVERY_PLAN.md`
- `docs/movie-discovery/MOVIE_DISCOVERY_STAGE_LOG.md`
- `docs/movie-discovery/MOVIE_DISCOVERY_KNOWN_ISSUES.md`
- `DesignDraft/PHASE_6_COVERAGE_MATRIX.md`
- 7.4a 到 7.4d 修改过的所有文件。

按需读取：

- 只有共享资源影响其他页面时，读取对应 page-spec。
- 仅做 migration diff 验证时读取 migration 目录。

可复用组件 / UI 规则：

- Phase 7 最终报告要求。
- Known Issues 固定分类：Blocker / Deferred / Noise。
- Phase 6 覆盖矩阵中的 Discovery 与 Recommendation 边界。

可参考页面：

- 媒体库、首页、AI 推荐和详情页仅作为共享资源影响的回归参考。

阶段输出要求：

- 完成了什么：汇总 7.4a 到 7.4d 的所有完成项和清理项。
- 验收标准：至少覆盖搜索方式 / placeholder、Movie 搜索卡、TV 搜索卡、筛选、海报 / 列表切换、Movie / TV 榜单、AI Tab 入口、偏好弹窗、Movie-only 推荐边界、migration diff 为空、非破坏性来源语义。
- 不属于本阶段：列出仍属于 7.5+ 或 7.8 的遗留工作。
- 是否修改业务逻辑：集中汇总 7.4 全阶段业务逻辑变更；没有就写 `无`。
- 是否修改非本阶段页面：集中汇总所有非 Discovery 页面 / 共享资源改动，以及可见或不可见影响。
- 文档：如果发现 blocker、deferred 或语义变化，更新 `DesignDraft/PHASE_7_STAGE_LOG.md`、`DesignDraft/PHASE_7_KNOWN_ISSUES.md` 和相关 `docs/movie-discovery/*` 日志。
- 验证：除纯文档收口外，记录 `dotnet build MediaLibrary.sln`；无论是否 build，都记录 `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`。

## 验收矩阵种子

7.4e 至少从这些项开始，实际实现触及更多状态时必须扩展：

| ID | 检查项 |
|---|---|
| 7.4-A01 | Movie 标题搜索 placeholder 为 `输入需要搜索的电影`。 |
| 7.4-A02 | 人物搜索 placeholder 为 `输入需要搜索的导演/演员`。 |
| 7.4-A03 | TV 标题搜索 placeholder 为 `输入需要搜索的电视剧名`。 |
| 7.4-A04 | TV 搜索方式按文档工作，且不会把 TV 写入 AI 推荐或观影洞察链路。 |
| 7.4-A05 | 搜索筛选使用媒体库式结构，但字段和文案是 Discovery 专属；精确尺寸 / 图标 / 下拉样式差异为 Deferred。 |
| 7.4-A06 | 海报搜索卡没有进度条，并显示左上角类型和评分标签；Movie 右上角为想看动作，TV 右上角仅在有想看季时显示 `想看` 标签。 |
| 7.4-A07 | 列表搜索行保留右下角类型标签；Movie 右上角用想看动作替代来源标签位置，TV 右上角仅在有想看季时显示 `想看` 标签。 |
| 7.4-A08 | 搜索卡片不出现 `已移出媒体库` 按钮。 |
| 7.4-A09 | Discovery 搜索布局切换可用；如增加持久化，只能走批准的 UI 偏好路径。 |
| 7.4-A10 | Movie / TV 榜单顺序遵守 TMDB 返回顺序，分页仍可用。 |
| 7.4-A11 | AI 推荐 Tab 保持嵌入 Discovery，且仍为 Movie-only。 |
| 7.4-A12 | 推荐偏好弹窗可打开、编辑、清空、取消和确认，不改变推荐算法语义。 |
| 7.4-A13 | 加载 / 空 / 错误状态遵守 `UI_LOADING_STATE_GUIDELINES.md`。 |
| 7.4-A14 | migration diff 为空，除非后续用户明确批准改变阶段边界。 |

## 7.4b Follow-up Regression Acceptance Addendum

| ID | 检查项 |
|---|---|
| 7.4-B01 | Discovery 筛选状态切换不因结果重建额外触发搜索提交按钮状态刷新。 |
| 7.4-B02 | Discovery 收藏状态为多选：默认 `其他 + 喜爱 + 想看`，按钮显示 `收藏状态：3项`，四个具体项全选时折回 `全部`。 |
| 7.4-B03 | 媒体库收藏状态与 Discovery 使用同一默认和多选规则。 |
| 7.4-B04 | Discovery 搜索输入为空时 hover / click 不显示输入框 tooltip。 |
| 7.4-B05 | 点击 TV 搜索结果进入详情前，搜索结果区域不出现由导航状态触发的居中 loading overlay。 |
| 7.4-B06 | Discovery 海报结果区域使用媒体库同款 poster item 宽度、内容宽度和 viewport padding，给阴影留出安全空间。 |
| 7.4-B07 | Discovery 结果很少、不需要滚动时，上一页 / 下一页 / 页码组件贴近结果区域底部。 |
| 7.4-B08 | Discovery 和媒体库布局切换选中段高度贴近组件外框高度，圆角保持小幅度。 |
| 7.4-B09 | Discovery 海报结果区域在多行结果时可以正常滚动。 |
| 7.4-B10 | 媒体库收起导航栏时排序按钮水平居中对齐观看状态；展开导航栏时水平居中对齐收藏状态。 |
| 7.4-B11 | Discovery 海报布局分页遵守列表布局的底部显示规则：不需要滚动时显示在底部，需要滚动时滚到底部才显示，同时保留 `ListBox + VirtualizingWrapPanel` 海报布局。 |

## 执行注意

- 每个小阶段尽量小补丁收口，不要把 7.4a 搜索行为和 7.4d AI 弹窗混在一起。
- 触碰共享 ResourceDictionary 时，需要做受影响 lazy-loaded view 的 build 或页面加载验证。
- 如果实现 TV 人物搜索，必须记录为受限 Discovery 搜索行为变更；应使用 TV-capable TMDB credits，不能把 Movie `movie_credits` 结果复用为 TV 行。
- 不把私有样本名、本地完整路径、账号、token、完整远端 URL 写入文档或日志。
## Follow-up Round 7 Addendum - Search Poster Bottom Grid Alignment

Scope:

- Fix the repeated Discovery search poster title/date edge-alignment issue at the layout-container level.
- Use one full-width 8px-margin bottom grid for Movie and TV search posters instead of a bottom StackPanel with a nested rating grid.
- Keep title, date and tag text on the left edge column, keep the rating chip in the right auto column, and keep the top-left/top-right chip margins at 8px.

Validation:

- `dotnet build MediaLibrary.sln`.
- Manual WPF window check for Movie and TV search poster left and right edge alignment remains required.

## Follow-up Round 6 Addendum - Search Poster Edge Alignment

Scope:

- Align Discovery Movie/TV search poster media-type chip edge spacing to the Media Library poster card.
- Keep right-top Movie action and TV state chips on the same 8px edge margin as Media Library.
- Align poster title, release date and tag/type line to the media-type chip left border.

Validation:

- `dotnet build MediaLibrary.sln`.
- Manual WPF window check for Movie and TV search poster cards remains required.

## Follow-up Round 5 Addendum - Rating Consistency And Search Card Polish

Scope:

- Fix Discovery search poster/list card alignment and rating badge sizing reported after testing.
- Allow existing local movies to refresh persisted TMDB and OMDb/IMDb rating sources from real-time Discovery search/ranking enrichment.
- Add a series-level TV rating persistence table because the user explicitly allowed it if needed for local TV rating consistency.

Acceptance:

- Movie/TV poster card title and date align to the left edge of the media-type chip.
- Rating badges are larger, bold and fixed-height.
- Movie want/cancel-want and TV current-want chips share the media-type chip height and vertically center their text.
- A local movie found in Discovery can update `RatingSources` so media-library/detail ratings can match the refreshed search result.
- A local TV series found in Discovery can update `TvSeriesRatingSources`; media-library TV projections and TV detail rating reads use that persisted series-level data.
- No database update is executed by the agent; applying the generated migration remains an operator action.

Validation:

- `dotnet build MediaLibrary.sln`.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is expected to include `AddTvSeriesRatingSources` in this authorized follow-up.

## 2026-06-06 - 7.4d-6 / AI Recommendation Loading And Info Layout Follow-up

Scope:

- Show the shared loading spinner slightly above center when AI recommendations are loading and no recommendation item is currently displayed.
- Resize the recommendation poster from `312x468` to `280.8x421.2`; keep the remote poster decode at `w780 / DecodePixelWidth=780`.
- Resize the recommendation info area from `312` to `343.2` width and keep it centered relative to the poster.
- Align the custom-preference switch hover behavior with the media-library layout toggle: selected state uses `BrushAccent / BrushOnAccent`; hover only lowers opacity to `0.92`.
- Render title/original-title and recommendation tags as split inline runs so title separators keep spaces and tag separators can use the media-library poster divider color `#7DD3FC`.
- Increase recommendation-reason type from `12` to `12.5`, set line height to `18`, and keep a half-line gap from the director/actor row.

Explicitly Not Done:

- No recommendation algorithm, prompt, fingerprint, TV recommendation, scanner, player, schema, migration, database update, commit, or push change.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.
- Manual WPF check remains required for spinner position, switch hover color, 3-column spacing, and tag separator color.

Known Issues:

- Blocker: None.
- Deferred: Full window screenshot validation was not performed in this coding pass.
- Noise: Existing unrelated workspace edits remain unstaged and are preserved.

## 2026-06-06 - 7.4d-7 / AI Recommendation Tab Crash Fix

Scope:

- Fixed the AI recommendation tab crash reported after the 7.4d-6 layout change.
- Evidence from Windows Application log: WPF threw `XamlParseException` because `Run.Text` defaulted to a source-updating binding against the read-only `AiRecommendationItem.TitleOriginalSeparatorText` property.
- Set all recommendation title/tag `Run.Text` bindings to explicit `Mode=OneWay`.

Explicitly Not Done:

- No recommendation service, AI request, database, schema, migration, scanner, player, commit, or push change.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.
- `git diff --check` reported only existing LF/CRLF warnings.

Known Issues:

- Blocker: None known after build.
- Deferred: User-side click-through verification for opening the AI recommendation tab is still required.
- Noise: The historical Windows event log still contains the pre-fix crash entry; it is useful evidence but not a new post-fix failure.

## 2026-06-06 - 7.4d-8 / AI Recommendation Spacing And Switch Follow-up

Scope:

- Kept the recommendation poster at `280.8x421.2` after the user clarified it should not be reduced further.
- Matched the custom-preference switch hover behavior to the media-library layout segment buttons: transparent default button background, selected Accent state, and no hover visual trigger.
- Moved each recommendation poster/info block down slightly with a top margin on the item container.
- Moved the info area down further relative to the poster.
- Let the recommendation reason panel stretch to the full info-area width and increased reason text to `13` with line height `19`.

Explicitly Not Done:

- No recommendation algorithm, service, prompt, database, schema, migration, scanner, player, commit, or push change.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.
- `git diff --check` reported only existing LF/CRLF warnings.

Known Issues:

- Blocker: None.
- Deferred: Manual WPF check is still required for the final spacing and switch hover parity.
- Noise: Existing unrelated workspace edits remain unstaged and are preserved.

## 2026-06-06 - 7.4d-9 / AI Recommendation Hover, Separators And Metadata Refresh Follow-up

Scope:

- Matched the custom-preference switch hover behavior to the visible media-library segment hover pattern: unselected switch halves use glass hover background plus accent border, while the selected half keeps the Accent state.
- Moved the recommendation poster/info blocks down slightly and kept the current `280.8x421.2` poster size unchanged.
- Reduced the vertical gaps between date/tag and tag/director rows.
- Added vertical separators between the three recommendation items; separators sit in the center gutter so the distance to neighboring posters is balanced.
- Added recommendation metadata backfill for director, actors and other non-reason fields. The detail page now writes hydrated TMDB details back to the source recommendation item and notifies recommendation refresh.
- Added recommendation-service hydration before cached/pool/generated items are returned, using existing TMDB detail cache paths and avoiding recommendation reason overwrite.
- Extended library-matched recommendation backfill to cover title/original title, full date, poster, overview, tags, country, language, runtime, identifiers and ratings where missing.

Explicitly Not Done:

- No recommendation algorithm, prompt, fingerprint, TV recommendation, scanner, player, schema, migration, database update, commit, or push change.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.
- Manual WPF check remains required for switch hover parity, separator spacing, and external-detail return refresh.

Known Issues:

- Blocker: None.
- Deferred: Full WPF click-through and screenshot validation was not performed in this coding pass.
- Noise: Existing unrelated workspace edits remain unstaged and are preserved; LF/CRLF warnings still appear in git checks.

## 2026-06-06 - 7.4d-10 / AI Recommendation Switch Border And Separator Alignment Follow-up

Scope:

- Fixed the custom-preference switch hover border clipping by giving the switch host an inset content area and drawing the hover border inside that area.
- Realigned recommendation item separators by giving the outer layout the same fixed item column width as the recommendation info area, so separators sit at the true midpoint between neighboring posters and info areas.

Explicitly Not Done:

- No recommendation algorithm, prompt, database, schema, migration, scanner, player, commit, or push change.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Manual WPF check remains required for the exact hover border and separator position.

Known Issues:

- Blocker: None.
- Deferred: Full WPF screenshot validation was not performed in this coding pass.
- Noise: Existing unrelated workspace edits remain unstaged and are preserved; LF/CRLF warnings still appear in git checks.

## 2026-06-06 - 7.4d-15 / Custom Preference Dialog Size And Scroll Follow-up

Scope:

- Enlarged the custom preference dialog to a fixed `624 x 384` panel, preserving the previous approximate aspect ratio while preventing content from increasing dialog height.
- Reworked the dialog body from a vertical stack to fixed grid rows so the preference input owns the remaining space and scrolls instead of pushing the action buttons downward.
- Applied the AI recommendation modern scrollbar style to the multiline preference input.
- Aligned the close button vertically with the title row and increased it to a `38 x 38` icon button.

Explicitly Not Done:

- No recommendation algorithm, prompt, database, schema, migration, scanner, player, commit, or push change.

Validation:

- XML reader validation passed for `RecommendationsPage.xaml`.
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Manual WPF check remains required for exact dialog sizing and scrollbar hover/reveal feel.

Known Issues:

- Blocker: None.
- Deferred: Full WPF screenshot validation was not performed in this coding pass.
- Noise: Existing unrelated workspace edits remain unstaged and are preserved; LF/CRLF warnings still appear in git checks.

## 2026-06-06 - 7.4d-14 / Rating Highlight Display-Value Consistency Follow-up

Scope:

- Fixed AI recommendation rating highlight logic so the high-score color is based on the displayed one-decimal value rather than the raw weighted score.
- Checked other rating badge highlight paths for the same pattern.
- Updated Movie Discovery movie cards, Movie Discovery TV cards and Media Library movie cards so high-score color uses the displayed one-decimal value while preserving raw rating values for sorting.
- Centralized Movie Discovery rating display rounding in `DiscoveryRatingPresenter`.

Explicitly Not Done:

- No recommendation algorithm, prompt, database, schema, migration, scanner, player, commit, or push change.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Search confirmed no remaining direct `IsHigh... >= 8` / `IsHigh... is >= 8` raw-value checks in the inspected read-model/view-model rating badge paths.
- Manual WPF check remains required for AI recommendation, Discovery and Media Library rating badge colors.

Known Issues:

- Blocker: None.
- Deferred: Full WPF screenshot validation was not performed in this coding pass.
- Noise: Existing unrelated workspace edits remain unstaged and are preserved; LF/CRLF warnings still appear in git checks.

## 2026-06-06 - 7.4d-13 / AI Recommendation Info Font Size Follow-up

Scope:

- Unified AI recommendation info-row text size for date, recommendation tags, director and actors to `11`.
- Kept director/actor text at normal weight while sharing the same base meta size and line metrics as the date/tag rows.

Explicitly Not Done:

- No recommendation algorithm, prompt, database, schema, migration, scanner, player, commit, or push change.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Manual WPF check remains required for final typography balance.

Known Issues:

- Blocker: None.
- Deferred: Full WPF screenshot validation was not performed in this coding pass.
- Noise: Existing unrelated workspace edits remain unstaged and are preserved; LF/CRLF warnings still appear in git checks.

## 2026-06-06 - 7.4d-12 / AI Recommendation Expanded Poster Style Parity Follow-up

Scope:

- Removed the expanded-sidebar poster `RenderTransform` scaling path because it could alter clipping and shadow presentation.
- In expanded-sidebar mode, the recommendation poster now uses direct 0.95 proportional dimensions for the card, clip radius, palette shadow, fallback shadow, shadow padding, blur radius and shadow depth.
- Collapsed-sidebar poster values remain unchanged.

Explicitly Not Done:

- No recommendation algorithm, prompt, database, schema, migration, scanner, player, commit, or push change.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Manual WPF check remains required to confirm expanded and collapsed poster styling differ only by size.

Known Issues:

- Blocker: None.
- Deferred: Full WPF screenshot validation was not performed in this coding pass.
- Noise: Existing unrelated workspace edits remain unstaged and are preserved; LF/CRLF warnings still appear in git checks.

## 2026-06-06 - 7.4d-11 / AI Recommendation Expanded Sidebar Responsive Layout Follow-up

Scope:

- Kept the collapsed-sidebar AI recommendation layout unchanged.
- In expanded-sidebar mode, reduced the poster visual scale to `0.95` of the current size and narrowed the info area from `343.2` to `326.04`.
- Reworked the recommendation item row to use three equal-width cells, with separators at cell boundaries so the separator remains centered between neighboring cards after responsive width changes.
- Increased the recommendation reason viewport to four and a half lines (`MaxHeight=86` with line height `19`) so content longer than four lines scrolls while showing a half-line overflow cue.

Explicitly Not Done:

- No recommendation algorithm, prompt, database, schema, migration, scanner, player, commit, or push change.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Manual WPF check remains required for expanded/collapsed sidebar sizing and the four-and-a-half-line reason scroll cue.

Known Issues:

- Blocker: None.
- Deferred: Full WPF screenshot validation was not performed in this coding pass.
- Noise: Existing unrelated workspace edits remain unstaged and are preserved; LF/CRLF warnings still appear in git checks.
