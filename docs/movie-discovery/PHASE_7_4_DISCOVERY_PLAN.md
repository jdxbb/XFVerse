# Phase 7.4 影片发现执行计划

Last updated: 2026-06-04

本文件是 Phase 7.4 的唯一详细执行计划。后续进入 7.4 任一小阶段前，必须先阅读本文件，再按小阶段要求读取对应文档、代码和设计草图。

## 阶段状态

- 状态：7.4a 已完成；7.4b 待开始。
- 当前范围：`影片发现` 页面，以及其中嵌入的 `AI 推荐` Tab。
- 主要规格来源：`DesignDraft/page-spec/movie-discovery-page.md`。
- 业务边界：7.4 可以完善搜索方式、卡片状态投影和 UI 偏好，但不能修改推荐算法，也不能让 TV 进入 Movie AI 推荐、观影洞察、画像、人格或推荐 fingerprint 链路。

## 执行记录

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
  - 右上角变为 `+ 想看` / 当前想看状态动作；
  - 评分标签可优先复用首页 AI 推荐中的视觉方式。
- 筛选按钮 UI 和媒体库完全一致，只是字段不同。
- 搜索卡片不显示 `已移出媒体库` 按钮。
- 电视剧搜索要在现有基础上增加搜索方式选项。若包含人物搜索，需要补齐 TMDB TV/person credits 的 Discovery 搜索链路，并明确记录为 7.4 的受限业务变更。
- 搜索框 placeholder 根据当前搜索方式变化：
  - 人物：`输入需要搜索的导演/演员`
  - 电影：`输入需要搜索的电影`
  - 电视剧：`输入需要搜索的电视剧名`
- 搜索结果提示字段使用 Discovery 自己的文案，不直接照搬媒体库含义不同的摘要文案。
- 列表布局右下角 Movie / TV 标签不变；右上角原本本地 / 网盘 / 无播放源一类来源标签位置，改为 `+ 想看` / 当前想看状态动作。

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
- 验收标准：搜索工具栏和筛选控件视觉与媒体库一致但字段为 Discovery 专属；结果提示文案和布局不照搬媒体库错误语义；海报卡片没有进度条；左上角 Movie / TV 标签存在；右上角想看状态动作存在；评分标签可见且不遮挡；列表右下角 Movie / TV 标签保留；列表右上角来源标签位置改为想看动作；不出现 `已移出媒体库`；来源 / 加入媒体库语义仍正确。
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
| 7.4-A05 | 搜索筛选使用媒体库式 UI，但字段和文案是 Discovery 专属。 |
| 7.4-A06 | 海报搜索卡没有进度条，并显示左上角类型、右上角想看动作和评分标签。 |
| 7.4-A07 | 列表搜索行保留右下角类型标签，并用右上角想看动作替代来源标签位置。 |
| 7.4-A08 | 搜索卡片不出现 `已移出媒体库` 按钮。 |
| 7.4-A09 | Discovery 搜索布局切换可用；如增加持久化，只能走批准的 UI 偏好路径。 |
| 7.4-A10 | Movie / TV 榜单顺序遵守 TMDB 返回顺序，分页仍可用。 |
| 7.4-A11 | AI 推荐 Tab 保持嵌入 Discovery，且仍为 Movie-only。 |
| 7.4-A12 | 推荐偏好弹窗可打开、编辑、清空、取消和确认，不改变推荐算法语义。 |
| 7.4-A13 | 加载 / 空 / 错误状态遵守 `UI_LOADING_STATE_GUIDELINES.md`。 |
| 7.4-A14 | migration diff 为空，除非后续用户明确批准改变阶段边界。 |

## 执行注意

- 每个小阶段尽量小补丁收口，不要把 7.4a 搜索行为和 7.4d AI 弹窗混在一起。
- 触碰共享 ResourceDictionary 时，需要做受影响 lazy-loaded view 的 build 或页面加载验证。
- 如果实现 TV 人物搜索，必须记录为受限 Discovery 搜索行为变更；应使用 TV-capable TMDB credits，不能把 Movie `movie_credits` 结果复用为 TV 行。
- 不把私有样本名、本地完整路径、账号、token、完整远端 URL 写入文档或日志。
