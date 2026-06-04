# Phase 7.4 影片发现执行计划

Last updated: 2026-06-05

本文件是 Phase 7.4 的唯一详细执行计划。后续进入 7.4 任一小阶段前，必须先阅读本文件，再按小阶段要求读取对应文档、代码和设计草图。

## 阶段状态

- 状态：7.4a、7.4b、7.4c 已完成；7.4d 待开始。
- 当前范围：`影片发现` 页面，以及其中嵌入的 `AI 推荐` Tab。
- 主要规格来源：`DesignDraft/page-spec/movie-discovery-page.md`。
- 业务边界：7.4 可以完善搜索方式、卡片状态投影和 UI 偏好，但不能修改推荐算法，也不能让 TV 进入 Movie AI 推荐、观影洞察、画像、人格或推荐 fingerprint 链路。

## 执行记录

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
