# XFVerse UI 重构说明入口

## 1. 文档用途

本目录用于指导 XFVerse 的 UI 重构。

XFVerse 是一个基于 WPF + .NET 的 AI 智能影音库桌面软件。  
后端逻辑、扫描逻辑、WebDAV、播放器 mpv 接入、TMDB / OMDb / AI 接口等核心业务逻辑已经基本完成。  
当前任务是基于新的 UI 设计，重构现有简陋界面。

本目录中的文档用于说明：

- 全局视觉规范
- UI 资源策略
- Codex 实现约束
- 每个页面的布局结构
- 每个页面的交互规则
- 每个页面的滚动区域
- 每个页面的验收标准

---

## 2. 重要原则

Codex 在开始任何 UI 实现前，必须先阅读本文件和相关设计文档。

实现时必须遵守以下原则：

- 不要擅自修改后端业务逻辑
- 不要擅自删除已有功能
- 不要擅自重新设计页面结构
- 不要根据截图颜色实现 UI
- 截图只用于理解布局和区域关系
- 如果截图和 md 文档冲突，以 md 文档为准
- 如果页面文档和全局规范冲突，优先遵守全局规范，并在输出中说明冲突
- 优先复用现有 ViewModel、Service、Command 和后端逻辑
- 新 UI 可以新建一套 Views / Controls / Styles，不强行沿用旧 UI 文件
- 所有颜色、字体、圆角、间距、控件样式必须集中到 ResourceDictionary 或统一资源文件中
- 不要在各页面硬编码颜色、字号、间距和样式
- 基础控件优先使用 WPF UI
- 普通图标优先使用 WPF UI 默认图标
- 不混用风格差异明显的图标库
- 深色 / 浅色双主题必须支持
- 播放器区域永远保持深色沉浸式风格

---

## 3. 阅读顺序

Codex 应按以下顺序阅读文档。

### 第 1 组：全局规则

必须先读：

1. `DESIGN.md`
2. `resources-note.md`
3. `codex-ui-rules.md`

这三份文件定义全局视觉、资源策略和实现约束。

### 第 2 组：页面规格

然后阅读 `page-spec/` 目录下的页面说明。

建议阅读顺序：

1. `page-spec/global-shell.md`
2. `page-spec/global-dialogs.md`
3. `page-spec/home-page.md`
4. `page-spec/user-profile-dialog.md`
5. `page-spec/scan-task-page.md`
6. `page-spec/settings-page.md`
7. `page-spec/cache-management-page.md`
8. `page-spec/media-library-page.md`
9. `page-spec/media-library-special-items.md`
10. `page-spec/movie-detail-page.md`
11. `page-spec/tv-detail-page.md`
12. `page-spec/episode-detail-page.md`
13. `page-spec/correction-flow.md`
14. `page-spec/watch-history-page.md`
15. `page-spec/favorites-page.md`
16. `page-spec/movie-discovery-page.md`
17. `page-spec/recommendation-page.md`
18. `page-spec/player-page.md`
19. `page-spec/online-subtitle-search-page.md`
20. `page-spec/watch-insights-page.md`

### 第 3 组：截图辅助

如果存在 `screenshots/` 目录，截图只作为布局辅助参考。

截图使用规则：

- 只看布局、区域关系、内容位置
- 不参考截图颜色
- 不参考截图中的临时黑色占位块样式
- 黑色正方形通常表示语义相关图标
- 黑色长方形通常表示输入框、图表、占位区域或说明图例，需要结合页面 md 判断
- 如果截图与 md 文档冲突，以 md 文档为准

---

## 4. 推荐目录结构

建议将设计文档放置为以下结构：

```text
DesignDraft/
├─ UI-REBUILD-README.md
├─ DESIGN.md
├─ resources-note.md
├─ codex-ui-rules.md
├─ page-spec/
│  ├─ global-shell.md
│  ├─ global-dialogs.md
│  ├─ home-page.md
│  ├─ user-profile-dialog.md
│  ├─ scan-task-page.md
│  ├─ settings-page.md
│  ├─ cache-management-page.md
│  ├─ media-library-page.md
│  ├─ media-library-special-items.md
│  ├─ movie-detail-page.md
│  ├─ tv-detail-page.md
│  ├─ episode-detail-page.md
│  ├─ correction-flow.md
│  ├─ watch-history-page.md
│  ├─ favorites-page.md
│  ├─ movie-discovery-page.md
│  ├─ recommendation-page.md
│  ├─ player-page.md
│  ├─ online-subtitle-search-page.md
│  └─ watch-insights-page.md
└─ screenshots/
   ├─ home/
   ├─ user-profile/
   ├─ scan-task/
   ├─ settings/
   ├─ media-library/
   ├─ movie-detail/
   ├─ watch-history/
   ├─ favorites/
   ├─ movie-discovery/
   ├─ player/
   └─ watch-insights/
```

截图目录不是必须完整存在。  
如果截图缺失，以 md 文档为准。

---

## 5. 当前已覆盖模块

本设计文档已覆盖以下模块：

- 全局壳层、导航与账号菜单
- 全局确认弹窗及危险确认变体
- 首页
- 用户资料弹窗
- 扫描任务页面
- 设置页面
- 软件缓存管理、在线字幕孤立缓存保护与清理确认
- 媒体库页面
- 媒体库特殊媒体项、未识别季与人工聚合流程
- 影片详情页
- 电视剧剧详情页与季详情页
- 单集详情页
- 电影 / TV / Unknown 统一修正弹窗
- 观影历史页面
- 收藏夹页面
- 影片发现页面
- 影片发现内的 AI 推荐 Tab 与推荐偏好弹窗
- 播放器
- 在线字幕搜索窗口、字幕绑定与播放器内删除绑定轻量确认
- 观影洞察页面

其中观影洞察页面包含：

- 画像分析 Tab
- 观影统计 Tab

---

## 6. 全局导航规则

主页面通常支持：

- 展开导航栏状态
- 关闭导航栏状态

展开导航栏状态：

- 左侧显示完整导航栏
- 当前页面导航项高亮
- 导航栏右上角显示关闭导航栏按钮
- 点击后收起导航栏

关闭导航栏状态：

- 左侧导航栏隐藏或收缩
- 页面左上角显示导航栏展开按钮
- 点击后恢复展开导航栏状态
- 主内容区向左扩展

注意：

- 导航栏展开 / 关闭按钮不是 App Logo
- App Logo 只用于侧边栏品牌区、关于 XFVerse 行、应用图标等品牌位置
- 品牌区最终显示左侧图标和 `XFVerse`
- 不以 `智能影音库 / WebDAV 路径 · Metadata 识别 · AI` 作为最终品牌展示
- 主题切换使用旧设计图标化入口
- 展开态首页保留旧设计欢迎语，收起态按旧设计显示紧凑标题
- 隐藏路由只作为内部路由，不加入主导航

详细规格见 `page-spec/global-shell.md`。

---

## 6.1 Phase 7 实施前置

Phase 7 正式实施前必须先完成全局 token / 控件基线：

- 颜色、字体、圆角、间距
- 主按钮、次按钮、图标按钮、危险按钮
- 卡片、导航项、Popup、Dialog
- 主窗口与播放器的自定义标题栏及窗口按钮
- 播放器固定深色的 Menu、Popup、Popover、在线字幕搜索 Dialog
- EmptyState、Loading、Error、Disabled、ConfigMissing
- 设置页敏感字段输入、缓存卡、扫描日志卡与轻量警示 Popover
- 深色 / 浅色主题资源

Phase 6 只更新设计文档，不修改 UI 代码或资源字典。

---

## 7. 返回按钮规则

部分页面左上角是返回按钮，不是 Logo。

包括但不限于：

- 影片详情页
- 电视剧剧详情页
- 电视剧季详情页
- 单集详情页
- 播放器
- 从菜单进入的全屏功能页，如扫描任务、设置等，若设计文档中指定为返回按钮

返回按钮行为：

- 返回上一页
- 或关闭当前播放器 / 弹窗
- 具体逻辑按现有路由和后端逻辑处理

---

## 8. 影片状态规则

全局影片状态规则如下：

基础观看状态：

- 已看
- 未看

偏好状态：

- 喜爱
- 想看
- 不想看

规则：

- 已看影片才能标记喜爱
- 未看影片才能标记想看
- 已看和未看影片都可以标记不想看
- 喜爱 / 想看 / 不想看 三者互斥
- 任意两个不能共存
- 不想看会覆盖喜爱和想看

状态显示优先级：

已看影片：

1. 不想看
2. 喜爱
3. 已看

未看影片：

1. 不想看
2. 想看
3. 未看

---

## 9. 播放器规则

播放器区域规则：

- 播放器永远使用深色沉浸式风格
- 播放器不随普通页面浅色主题变成浅色
- 播放器和在线字幕搜索弹窗不使用原生标题栏作为最终 UI，且固定为深色
- 控制栏使用半透明深灰 / 黑灰浮层
- 播放进度使用主色
- 仅在 mpv 能可靠提供时展示独立缓冲进度；当前不把离线缓存作为播放器能力，也不伪造缓存进度
- 普通音量和亮度使用主色
- 音量增强使用低饱和橙色
- 音量范围为 0% - 200%
- 超过 100% 为音量增强
- 亮度范围为 0% - 100%
- 字幕菜单包含无字幕、内嵌、外挂、在线下载字幕，在线字幕绑定删除仅使用轻量确认并保留缓存文件
- 文件路径可以展示，完整 WebDAV URL 不裸露，长字段省略并支持 Tooltip 查看允许展示的完整内容

播放器具体规则以 `page-spec/player-page.md` 与 `page-spec/online-subtitle-search-page.md` 为准。

---

## 10. 资源策略摘要

实际需要准备的图片 / 矢量资源很少。

必须准备：

- `app-logo.svg`
- `app-logo.ico`
- `poster-placeholder.png`

可选准备：

- `app-logo.png`

不准备：

- 默认头像图片
- 播放器背景图片
- 空状态插画图片
- 小图标图片
- 按钮图片
- 输入框图片
- 卡片背景图片
- 播放器控件图片

默认头像、播放器背景、空状态、普通图标优先由 WPF / XAML / WPF UI 实现。

详细规则以 `resources-note.md` 为准。

---

## 11. 第一轮 Codex 任务要求

第一轮 Codex 不允许直接改代码。

第一轮只做：

1. 阅读当前项目结构
2. 阅读 `DesignDraft/` 下的所有设计文档
3. 识别现有 UI 文件、ViewModel、Service、Command、资源文件和后端逻辑
4. 输出 UI 重构总方案
5. 输出分阶段实施计划
6. 输出每个阶段可直接交给 Codex 执行的 prompt
7. 输出风险点和不确定项
8. 输出需要保留、复用、替换、新建的文件建议

第一轮禁止：

- 修改代码
- 新建代码文件
- 删除代码文件
- 重命名代码文件
- 修改项目结构
- 修改资源文件
- 修改后端逻辑
- 修改数据库 / 配置 / 服务逻辑

---

## 12. 阶段基线开发要求

后续每个 UI 重构阶段必须遵循阶段基线开发模式。

每个阶段只做指定目标，不做阶段外功能。

每个阶段输出必须包含：

- 本阶段目标完成情况
- 修改文件列表
- 明确未做事项
- 自动验证结果，例如 `dotnet build`
- 关键日志
- 人工验收矩阵
- Known Issues
  - Blocker
  - Deferred
  - Noise
- 是否建议进入下一阶段

阶段外发现的问题：

- 只记录
- 不默认修复
- 不直接扩大修改范围

遇到无法判断的问题：

- 先输出证据
- 再输出最小补丁计划
- 不直接大改

---

## 13. 第一轮 Codex 输出格式

第一轮 Codex 输出应包含以下内容：

```text
# XFVerse UI 重构总方案

## 1. 项目结构观察
- 当前 UI 技术栈
- 当前主窗口 / 页面 / 导航结构
- 当前资源文件结构
- 当前 ViewModel / Service / Command 情况

## 2. 设计文档理解摘要
- 全局视觉规范
- 资源策略
- 导航规则
- 状态规则
- 页面清单

## 3. UI 重构总体策略
- 新建 UI 还是改造旧 UI
- 资源文件如何组织
- 样式如何集中管理
- 页面如何分阶段替换
- 后端逻辑如何复用

## 4. 分阶段实施计划
- 阶段 1
- 阶段 2
- 阶段 3
- ...

## 5. 每阶段 Codex Prompt
- 每阶段一个可直接执行的 prompt
- 每阶段明确目标、范围、禁止事项、验证方式

## 6. 风险点和不确定项
- 需要人工确认的问题
- 可能影响后端的问题
- UI 与现有代码结构冲突的问题

## 7. 建议优先级
- 哪些先做
- 哪些后做
- 哪些必须等前置阶段完成
```

---

## 14. 重要提醒

Codex 不能只看单个页面 md 就开始实现。

任何实现前必须结合：

- `DESIGN.md`
- `resources-note.md`
- `codex-ui-rules.md`
- 对应页面 md
- 当前项目真实结构

如果页面文档中有业务字段或命令名称与项目现有代码不一致：

- 优先使用项目现有命名
- 不要为了匹配文档擅自改后端命名
- 必须在输出中说明映射关系

如果现有业务逻辑与 UI 文档冲突：

- 不要直接修改业务逻辑
- 先记录冲突
- 输出最小修改建议
- 等人工确认后再执行
