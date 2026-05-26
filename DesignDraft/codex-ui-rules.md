# codex-ui-rules.md

# XFVerse Codex UI Rebuild Rules

本文件用于约束 Codex 在 XFVerse UI 重构中的执行方式。

---

## 1. Non-Negotiable Rules

Codex 必须遵守：

- 不修改后端业务逻辑
- 不擅自删除功能
- 不擅自改变无边记页面布局
- 不擅自改变滚动区域
- 不擅自改变组件顺序
- 不重新设计页面结构
- 不用自己的审美替换既定设计方向
- 不在页面中硬编码颜色
- 不混用多套图标风格
- 每次修改后必须保证项目可编译

只允许：

- 修正明显不合理的间距
- 修正明显不合理的对齐
- 优化实现方式
- 抽取公共组件
- 统一样式资源
- 按 DESIGN.md 改善视觉一致性

---

## 2. Project Constraints

项目技术栈：

- WPF + .NET
- WPF 内嵌 mpv 播放器
- 后端功能已完成
- 旧 UI 很简陋
- 新 UI 需要新建一套 Views / Controls，逐步替换旧 UI
- UI 必须支持浅色 / 深色双主题
- 基础 UI 库使用 WPF UI

---

## 3. Source of Truth

Codex 实现 UI 时，按以下优先级理解需求：

1. 当前任务 Prompt
2. `UI-REBUILD-README.md`
3. 对应页面 md
4. `DESIGN.md`
5. `resources-note.md`
6. `codex-ui-rules.md`
7. 当前项目真实代码结构
8. 截图

说明：

- 截图只作为布局辅助参考，不作为颜色、样式和业务规则来源。
- 如果截图和 md 冲突，以 md 为准。
- 如果 md 和现有业务逻辑冲突，不要直接改业务逻辑，先记录冲突并提出最小处理建议。
---

## 4. WPF UI Rules

- 使用 WPF UI 作为基础 UI 库
- 基础按钮、输入框、下拉框、菜单、弹窗、导航、滚动条优先使用 WPF UI 默认样式
- 不从零重写基础控件样式
- WPF UI 默认图标优先
- 不足部分再考虑 Fluent / IconPack
- 不混用风格差异明显的图标库

---

## 5. ResourceDictionary Rules

所有视觉资源必须统一管理。

应建立或使用类似结构：

- Colors.Light.xaml
- Colors.Dark.xaml
- Typography.xaml
- Buttons.xaml
- Inputs.xaml
- Cards.xaml
- Tags.xaml
- Lists.xaml
- Dialogs.xaml
- Player.xaml
- WindowChrome.xaml
- PlayerOverlays.xaml

要求：

- 页面内不要硬编码颜色
- 页面内不要重复写大段样式
- 业务状态颜色必须复用统一资源
- 深色 / 浅色主题必须通过资源切换实现
- 组件样式应尽量复用 StaticResource / DynamicResource
- 主窗口与播放器窗口的自定义标题栏和窗口按钮必须由公共资源定义
- 播放器及在线字幕搜索的 Menu / Popup / Popover / Dialog 使用固定深色资源变体

Phase 7 前置要求：

- 正式页面重构前先完成全局 token / 控件基线
- token 至少覆盖颜色、字体、圆角、间距、按钮等级和危险按钮
- 公共控件至少覆盖卡片、导航项、Popup、Dialog、EmptyState、Loading、Error、Disabled、ConfigMissing
- 公共控件还应覆盖自定义窗口标题栏、播放器深色菜单 / Popover 及状态浮层
- 数据展示控件还应覆盖统计卡、图表、日历热力图，以及 Empty / Loading / Error / DataInsufficient 状态
- 页面实现不得绕过公共基线自行定义重复样式
- Phase 6 仅更新设计文档，不修改资源字典

---

## 6. Componentization Rules

新 UI 应尽量组件化。

优先抽取：

- 影片卡片
- 播放源卡片
- 标签控件
- 状态标签
- 评分卡
- 设置项控件
- EmptyState
- 识别修正结果卡片
- 播放器控制栏
- 播放器音量 / 亮度浮层
- 自定义窗口标题栏与窗口按钮
- 详情页统一返回图标按钮
- 播放器播放源 / 字幕 / 音轨菜单
- 在线字幕搜索深色弹窗
- 在线字幕删除绑定轻量确认 Popover
- 删除本地扫描路径轻量确认 Popover
- 设置页敏感字段显示 / 隐藏输入控件
- 软件缓存卡与缓存禁用原因提示
- 扫描进度与脱敏历史记录卡
- 观影洞察图表组件
- 观影统计卡与日历热力图组件
- 用户画像卡片组件

页面应该负责组合组件，不应把所有 UI 写在一个巨大 XAML 文件里。

---

## 7. Page-Spec Rules

每个页面重构前必须参考对应 page-spec.md。

page-spec.md 应说明：

- 页面用途
- 布局结构
- 滚动区域
- 组件拆分
- 数据字段
- 按钮与交互
- 状态处理
- 主题注意事项
- 验收标准

Codex 不应在缺少 page-spec 的情况下直接重构复杂页面。

---

## 8. Business Logic Rules

Codex 不得破坏现有业务逻辑。

要求：

- 保留现有 ViewModel / Service / Command
- 优先复用已有绑定
- 不擅自改数据库结构
- 不擅自改 WebDAV 逻辑
- 不擅自改 mpv 播放逻辑
- 不擅自改 AI 推荐逻辑
- 不擅自改扫描识别逻辑

如果发现旧 UI 绑定不适合新 UI：

- 先说明问题
- 给出最小改动方案
- 不直接大改后端

---

### 长字段与路径展示规则

- 文件路径可以作为 UI 信息展示。
- 完整 WebDAV URL 不得在界面中裸露；只显示安全路径摘要或来源文案。
- 文件名、路径、release 名称等在无横向滚动区域中超长时必须省略，鼠标悬停显示允许展示的完整内容。
- 该规则适用于播放器菜单、在线字幕搜索结果、设置、扫描日志及其它包含长来源字段的组件。

### 敏感字段与设置操作规则

- password、token、API key、access token 等敏感字段默认隐藏。
- 敏感输入框末尾提供眼睛图标按钮；点击显示内容，再次点击隐藏内容。
- 状态反馈、错误文本、Tooltip、日志和设计示例不得回显凭据。
- 设置页保留页面内主题切换入口，并允许纵向滚动。
- 配置保存与连接测试是独立操作；保存不得自动触发测试连接。

### 扫描与缓存管理边界

- 扫描页分别呈现 WebDAV 与本地目录来源，并共享进度和历史日志区域。
- 扫描最终设计不包含暂停按钮；识别计数仅在任务能稳定产出结果时于完成摘要展示。
- 扫描日志不得显示完整 WebDAV URL、账号或凭据。
- 删除本地扫描路径使用按钮旁轻量 Popover，说明可能影响软件记录可见性但不删除真实本地文件。
- 软件缓存管理只包含海报、metadata / 其它和在线字幕缓存，不恢复视频缓存管理入口。
- 在线字幕缓存只清理孤立缓存；绑定引用无法确认时必须禁用清理。
- 缓存清理不删除真实媒体文件、WebDAV 远端文件或用户配置。

### 播放器链路边界

- 播放器与在线字幕搜索弹窗固定深色，不随普通页面主题切换。
- 字幕操作只属于播放器链路；详情页不新增字幕入口。
- 在线字幕配置归设置页，物理缓存清理归软件缓存管理。
- 删除在线字幕绑定使用轻量 Popover，只解除绑定，不删除缓存文件。

### 历史、收藏与洞察边界

- 观影历史沿用旧设计卡片视觉，但必须覆盖 Movie / Episode、观看进度、播放源不可用和对应详情导航能力。
- 收藏夹展示粒度为 Movie 与 Season，喜爱 / 想看 Tab 显示数量，并保留页内取消操作。
- 库外或无播放源收藏项仍允许取消收藏；不得将临时 disabled 状态写成最终业务规则。
- 观影洞察统计卡为已看、喜爱、想看、不想看四项；统计支持全部 / 本月，趋势统一写为相比上个月。
- 首页片库预览使用全部统计，趋势仍为相比上个月。
- 日历热度颜色、统计卡、图表和数据状态必须资源化；图表需统一 Empty、Loading、Error、DataInsufficient 状态。
- Watch Insights、Watch Profile、fingerprint、persona 与 AI 推荐画像只使用 Movie；TV、Episode、Season、Series 不进入电影洞察或画像。
- Movie-only 边界不在最终 UI 中新增排除提示，仅作为设计与实现约束。

### 详情页返回导航规则

- `MovieDetailPage`、`SeriesOverviewPage`、`TvSeasonDetailPage`、`EpisodeDetailPage` 使用同一详情返回图标按钮组件。
- 按钮固定在详情内容 Hero 左上角，不与壳层侧边栏展开 / 收起控件争夺位置。
- 统一覆盖图标、尺寸、hover、pressed、focus、disabled 和 Tooltip；默认 Tooltip 为 `返回上一页`。
- 返回优先恢复真实来源上下文，包括媒体库、影片发现、AI 推荐、首页、观影历史或收藏夹的可保存状态。
- 来源不可恢复时：Episode 返回 Season，Season 返回 Series，Movie / Series 返回媒体库或已知的影片发现来源；来源不可知时使用媒体库。
- 未识别、metadata-only、无播放源和 grouped placeholder 详情不能省略返回按钮。
- 若当前导航服务尚不支持来源状态或返回栈，应在 Phase 7 将其作为导航能力差距处理，不得在 Phase 6 修改实现。

---

## 9. Build and Verification

每个阶段完成后必须验证：

- 项目可以编译
- 页面可以打开
- 关键绑定没有丢失
- 按钮命令仍然存在
- 深色 / 浅色主题可用
- 主要滚动区域可用
- 没有明显布局溢出

如果无法运行完整应用，至少执行：

- dotnet build

并汇报结果。

---

## 10. What Codex Must Report

每次完成一个 UI 重构任务后，Codex 需要输出：

- 修改了哪些文件
- 新增了哪些文件
- 完成了哪些目标
- 哪些功能未做
- 是否修改了后端逻辑
- build / 验证结果
- 已知问题
- 是否建议进入下一步

输出格式需要清晰，方便人工验收。

---

## 11. Do Not Do

Codex 不要做：

- 不要擅自重排页面
- 不要删掉用户设计好的功能
- 不要把控件做成图片
- 不要用 AI 生成图替代真实 UI 控件
- 不要把图表做成静态图片
- 不要把按钮、输入框、卡片做成 PNG
- 不要把所有内容塞进单个页面 XAML
- 不要混用多个 UI 风格
- 不要为了好看牺牲可用性
- 不要绕过 MVVM 直接写大量 code-behind 逻辑

---

## 12. UI Rebuild Strategy

重构策略：

- 新建一套 Views / Controls
- Phase 7 第一批先建立全局 token / 控件基线、主题资源、主窗口及播放器自定义窗口基线和公共组件
- 再按页面逐步替换旧 UI
- 每个页面完成后单独验收
- 最后做全局统一和细节修复

推荐施工顺序由后续 UI 重构计划决定。
