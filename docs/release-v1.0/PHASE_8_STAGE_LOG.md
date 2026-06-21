# Phase 8 Stage Log

Last updated: 2026-06-21

## 2026-06-21 - GitHub 仓库官网入口同步

### 完成内容

- 将 GitHub 仓库的 Homepage 设置为 `https://xfverse.fun/`。
- 在根 README 首屏新增官网、官网文档中心、安装与升级、使用指南、帮助与排障入口。
- 在 README 文档表中补充官网和官网文档中心的定位。
- 保留仓库内详细 Markdown 文档，官网文档作为面向最终用户的精简版本。

### 验证结果

- GitHub 仓库元数据返回的 `homepageUrl` 为 `https://xfverse.fun/`。
- README 中官网链接均使用 HTTPS。
- 官网及文档中心在线访问正常。

### 明确未做

- 未修改软件业务代码、XAML、数据库、migration、打包脚本或安装器。
- 未将 RC 状态改为 GA，未新增正式下载链接。

## 2026-06-21 - 官网文档同步与官网 UI 收口

### 完成内容

- 连接 `xfverse.fun` 生产服务器，确认官网由 Nginx 托管静态文件。
- 在替换前创建完整站点压缩备份，并保留带时间戳的服务器回滚副本。
- 将仓库详细文档整理为面向最终用户的官网版本，区分以下定位：
  - 文档首页：说明文档分工和快速开始。
  - 安装与升级：说明架构选择、安装、首次启动、升级、卸载和数据保留。
  - 使用指南：说明媒体来源、扫描、媒体库、详情、播放、字幕、状态和洞察操作。
  - 帮助与排障：按异常现象说明检查、备份、安全恢复和脱敏反馈方法。
  - 发布与隐私：说明 RC 状态、候选包校验、平台验证、数据位置、外部服务和限制。
- 保留旧 `docs.html` 入口，并将其跳转到新的文档中心。
- 新增 `robots.txt`、`sitemap.xml` 和静态 404 页面。
- 重新设计官网首页，使视觉结构更接近 Hanzo 参考站：
  - 双行大标题和嵌入式产品界面元素。
  - 首屏主要操作、产品信任标签和大幅软件预览。
  - 功能卡片、三步流程、界面截图、文档入口、隐私说明和 FAQ。
  - 桌面端与窄屏响应式布局。
- 官网明确保持 RC 表述，不提供虚假的 GA 下载按钮。
- 官网明确新用户首次启动为空数据库；同一 Windows 用户曾运行测试版时会继续读取保留数据。

### 验证结果

- 本地站点共 17 个文件、8 个 HTML 页面，内部链接和静态资源检查通过。
- JavaScript 语法检查通过，CSS 花括号数量一致。
- 敏感信息和错误表述扫描通过，未写入服务器地址、登录凭据、私有路径或虚假下载链接。
- 线上主页、文档首页、四个文档子页面、robots 和 sitemap 均返回 HTTP 200。
- HTTP 到 HTTPS 的 301 跳转通过。
- 线上 CSS 与 JavaScript 返回 HTTP 200。
- 线上桌面首页、窄屏首页和文档首页完成 Chrome 截图复核。
- Nginx 配置检查和重载通过。

### 明确未做

- 未修改 XFVerse C#、XAML、数据库、migration、打包脚本或安装器。
- 未在官网声明 1.0 已 GA，未发布候选安装包下载链接。
- 未关闭 P8-B06、P8-B07 或 P8-B08。
- 未 commit，未 push。

### Known Issues

- Blocker：GA 构建追踪和 Windows 原生平台验收仍沿用 P8-B06、P8-B07、P8-B08。
- Deferred：正式 GA 时需要同步替换官网 RC 状态、最终校验值和正式下载入口。
- Noise：服务器保留的时间戳备份和回滚副本不属于网站公开内容。

## 2026-06-21 - Phase 8.9 RC 自动与进程级验收

### 完成内容

- 在 Windows 11 ARM64 环境执行 x64 仿真与 ARM64 原生候选版验收。
- 双架构在隔离应用数据目录完成首次启动、空数据库创建、主窗口形成和正常退出。
- ARM64 使用现有用户数据副本完成启动验证，原始数据库哈希保持不变。
- 三个测试数据库均通过 `PRAGMA integrity_check`，包含 22 张表和 27 条 migration 记录。
- x64 与 ARM64 libmpv 均完成原生进程加载、初始化、测试媒体载入和 duration 探测。
- 验证开始菜单快捷方式、卸载注册项、默认卸载和双向架构覆盖安装。
- 复核正式包、运行日志、第三方声明、对应源代码包和敏感信息。
- 生成根发布目录 `SHA256SUMS.txt` 与 `release-manifest.json`，四个制品哈希复算一致。
- 新增 `XFVERSE_1_0_RC_REPORT.md`，记录已通过证据、未执行矩阵和 GA Blocker。

### RC 发现与修复

- 发现 `MainWindow.OnClosing` 在异步关闭流程快速完成时可能重入 WPF `Closing` 事件。
- 异常会阻止应用正常退出，属于发布阻断问题。
- 将第二次关闭改为通过 Dispatcher 排队执行，避免在原关闭事件调用栈中再次调用 `Close()`。
- 未改变媒体业务语义、数据库结构、页面布局或用户可见功能。
- 修复后 Release build、双架构首次启动、现有数据副本启动和正常退出均通过。
- 修复后重新构建 x64 与 ARM64 安装包，并重新执行安装/修复/卸载生命周期。

### 最终候选制品

- x64 安装包：266,950,555 bytes，SHA-256 `6D7641FBEB7E20FC282EE23BF81DF7ECA1CE81DFE6D4366ED1DE38D167F04A15`。
- ARM64 安装包：240,257,892 bytes，SHA-256 `6C75397ADAD4CEDF6374CA936D367D989C817ABB0ADAF9A0A31ECD6DADA52BC8`。
- x64 对应源代码包：41,353,342 bytes，SHA-256 `295DD94628A74B0D85890882B14EC4A2F1D42015AF31D28A3E8018F84941D8E9`。
- ARM64 对应源代码包：41,353,339 bytes，SHA-256 `7836F690311094DC3CDE280A53D09A4A0C7C1A68C417E3F8BC9ED5ACDE7883C6`。
- 两个安装包 Authenticode 状态均为 `NotSigned`。

### 未完成事项

- 未取得 Windows 11 x64 原生 RC 环境。
- 未取得 Windows 10 x64 RC 环境。
- Windows UI 自动化运行环境不可用；用户随后确认当前 Windows 11 ARM64 设备人工验收无异常，未形成自动化截图记录。
- 未执行损坏数据库恢复、显示缩放、深色主题、HEVC 4K 和大体积 WebDAV 长时播放。
- 未 commit、未 push、未执行 database update、未新增 migration。

### Known Issues

- Blocker：P8-B06 GA 构建不可追踪。
- Blocker：P8-B07 Windows 11 x64 原生环境缺失。
- Blocker：P8-B08 Windows 10 x64 环境缺失。
- 已关闭：P8-B09，用户确认当前设备人工验收通过。
- Deferred：数字签名、体积优化、自动更新器和高负载播放场景。
- Noise：Windows ARM64 上运行 x64 仿真包后，少量运行时文件可能因仿真进程锁定而延迟删除。

### 结论

- 当前候选制品保持 RC 状态，不标记为 GA。
- 不建议发布，存在以下 Blocker。
- 详细证据见 `XFVERSE_1_0_RC_REPORT.md` 和 `XFVERSE_1_0_RC_ENVIRONMENT_MATRIX.md`。

## 2026-06-21 - P8-B05 关闭与双架构合规重建

### 完成内容

- 用户授权替换来源不可验证的原生 ffprobe。
- 将 x64 和 ARM64 ffprobe 统一替换为 BtbN `autobuild-2026-06-20-13-30` 的 FFmpeg 8.1.2 GPL 构建。
- 两个原始发布包均通过上游 `checksums.sha256` 验证。
- 新增 `Build-CorrespondingSource.ps1`，固定 mpv、FFmpeg、mpv-winbuild-cmake 和 FFmpeg-Builds 源码/构建快照。
- 生成 x64 与 ARM64 对应源代码包、对象映射、内部文件清单和 SHA-256。
- 正式打包脚本要求源码包校验清单存在，并将其复制到安装目录。
- 安装生命周期脚本新增第三方声明、对应源代码说明和源码包校验文件检查。
- 重建 x64 与 ARM64 正式候选安装包，更新 README、安装说明、发布说明、第三方文档和打包记录。

### 产物

- x64 安装包：266,950,555 bytes，SHA-256 `6D7641FBEB7E20FC282EE23BF81DF7ECA1CE81DFE6D4366ED1DE38D167F04A15`。
- ARM64 安装包：240,257,892 bytes，SHA-256 `6C75397ADAD4CEDF6374CA936D367D989C817ABB0ADAF9A0A31ECD6DADA52BC8`。
- x64 源码包：41,353,342 bytes，SHA-256 `295DD94628A74B0D85890882B14EC4A2F1D42015AF31D28A3E8018F84941D8E9`。
- ARM64 源码包：41,353,339 bytes，SHA-256 `7836F690311094DC3CDE280A53D09A4A0C7C1A68C417E3F8BC9ED5ACDE7883C6`。

### 验证结果

- x64 ffprobe：PE `8664`，ARM64 ffprobe：PE `AA64`。
- 两个 ffprobe 均报告 `n8.1.2-20260620`，并对 1 秒 WAV 返回 JSON duration `1.000000`、退出码 0。
- 两个源码包内部 9 个文件的 SHA-256 全部通过。
- 双架构 publish、安装器编译、禁止文件检查和敏感信息扫描通过。
- 双架构首次安装、同版本修复、卸载和合规文件安装检查通过。
- Authenticode 仍为 `NotSigned`。
- Migration diff 和 XAML diff 均为空。

### 结论

- P8-B05 已关闭。
- 当前没有已确认发布 Blocker。
- 可以进入 Phase 8.9 全量应用级 RC；尚未宣布 GA。

## 2026-06-21 - Phase 8.8 README、发布说明与合规文档

### 完成内容

- 重写根 README，准确说明 XFVerse 1.0.0 候选状态、双架构安装包、核心能力、文档导航、构建打包入口、数据语义、隐私和许可证状态。
- 新增 1.0.0 发布说明，记录 x64/ARM64 文件名、大小、SHA-256、未签名状态、升级、回滚、外部服务和已知限制。
- 新增第三方声明和 GPL 对应源代码状态文档，并同步第三方清单。
- 确认两个 libmpv 来自 `shinchiro/mpv-winbuild-cmake` 20260419 发布，x64 ffprobe 来自 Gyan FFmpeg 8.1。
- 正式打包脚本和安装生命周期验证脚本增加 `THIRD-PARTY-NOTICES.txt` 与 `CORRESPONDING-SOURCE.txt`。
- 更新正式打包记录、文档信息架构、Phase 8 总计划、8.9 进入条件和 Known Issues。

### 验证结果

- `dotnet restore MediaLibrary.sln` 通过。
- Release build 通过，0 警告、0 错误。
- 两个打包相关 PowerShell 脚本语法检查通过。
- 正式文档本地相对链接检查和敏感信息模式扫描通过。
- README 与发布说明中的双架构 SHA-256 和 Phase 8.4 正式打包记录一致。
- Migration diff 和 XAML diff 均为空。
- 8.8-A01～A06、A08～A11 通过；8.8-A07 因完整对应源代码归档未完成而阻断。

### 明确未做与 Known Issues

- 未修改业务逻辑、ViewModel、XAML、数据库模型或 migration。
- 未重建安装包；现有候选安装包尚不包含两个新增声明文件。
- 未执行应用级 RC、首次启动、数据库初始化、旧数据升级或实际播放。
- Blocker：P8-B05。ARM64 ffprobe 原始构建提供方和全部 GPL 原生组件的完整对应源代码归档尚未闭环。
- Deferred：双架构应用级 RC、数字签名、最终截图和文案实机复核。
- Noise：`git diff --check` 仅有 LF/CRLF 转换提示。
- 结论：Phase 8.8 文档产物完成，但不建议正式进入 Phase 8.9；先关闭 P8-B05。

## 2026-06-21 - Phase 8.7 帮助文档与故障排查

### 完成内容

- 建立 `docs/help/README.md` 帮助中心索引和 8 篇主题帮助文档，共 9 个文件、2313 行。
- 按安装启动、扫描识别、媒体库数据操作、播放字幕、影片发现外部服务、历史收藏洞察、缓存备份恢复、诊断隐私拆分问题。
- 每篇按症状、影响、原因、检查、修复、禁止事项、诊断信息和相关完整章节组织，不复制整本使用说明书。
- 覆盖 WebDAV、TMDB、OMDb、OpenSubtitles、AI 的配置、认证、网络、额度、模型和降级。
- 覆盖播放器黑屏、Range、远程卡顿、音轨、嵌入/外挂/在线字幕、多源、续播和 Episode 导航。
- 明确移出媒体库、删除记录、删除扫描路径、缓存清理、卸载和完全清除的软件数据与真实文件边界。
- 建立 XFVerse 版本、Windows 架构、SHA-256、日志目录、日志文件、脱敏和最小问题报告模板。
- 安装说明、正式使用说明书和旧兼容入口已链接到正式帮助中心及对应主题。
- 修正文档信息架构中关于历史 Base64 状态的陈旧表述，并同步功能矩阵、RC 矩阵和 Known Issues。

### 修改与新增

- 新增：`docs/help/README.md` 和 8 篇主题帮助文档。
- 修改：`docs/安装说明.md`、`docs/使用说明书.md`、`docs/使用说明.md`。
- 修改：Phase 8.7 阶段文档、Phase 8 总计划、阶段日志、Known Issues、文档信息架构、功能矩阵和 RC 矩阵。
- 删除：无。

### 验证结果

- 9 个帮助文件逐篇写入并检查；单篇最多 322 行，未超过单次 400 行限制。
- 帮助中心共 2313 行，全部相对链接有效，无空链接。
- Markdown 代码围栏总数为 34，数量成对。
- 未发现完整私有本地用户路径、真实凭据、完整私有 URL 或真实样本。
- Windows 版本、系统架构、日志列表、日志尾部读取和 `Get-FileHash` 命令已执行验证。
- x64 安装包 SHA-256 命令结果与正式打包记录一致。
- 8.7-A01～A10 全部通过文档和命令检查。
- Migration diff 为空。
- 本阶段为纯文档阶段，未执行 build。

### 明确未做与 Known Issues

- 未修改业务逻辑、ViewModel、XAML、项目文件、安装器或数据库结构。
- 未启动应用执行业务功能，未执行扫描、播放、外部服务请求或 database update。
- 未执行 publish、安装器构建、commit 或 push。
- 未添加含私有数据的截图；最终脱敏 RC 截图、错误提示和日志一致性由 Phase 8.9 复核。
- Blocker：P8-B05 GPL 对应源代码公开获取安排仍待 Phase 8.8。
- Deferred：最终 x64/ARM64 RC 的错误提示、日志名称和排障步骤实机复核。
- Noise：部分日志只在对应功能运行后生成，目录中不存在所有日志是正常现象。
- 建议进入 Phase 8.8 README、发布说明与合规文档。

## 2026-06-21 - Phase 8.6 软件使用说明书

### 完成内容

- 新建 `docs/使用说明书.md`，形成适用于 XFVerse 1.0.0 的正式软件使用说明书。
- 正文共 13 章、4 个附录，覆盖产品概念、主导航、首次使用、本地/WebDAV 来源、扫描识别、媒体库、Movie/TV 详情、播放器、字幕、发现、状态、历史、收藏、洞察、设置、数据安全和依赖边界。
- 明确移出媒体库、删除记录、删除来源配置和清理缓存均不删除真实本地或 WebDAV 媒体文件。
- 明确 Movie、Series、Season、Episode 的层级、状态和 1.0 Movie-only 洞察/AI 推荐边界。
- 按代码记录播放器快捷键、多播放源、音轨、嵌入/外挂/在线字幕、续播和 WebDAV 缓存语义。
- 按实际设置页区分 TMDB、OMDb、OpenSubtitles、AI、WebDAV、主题、关闭行为、自动全屏、自动扫描和缓存管理。
- 记录 `%LOCALAPPDATA%\MediaLibrary` 用户数据结构、备份、恢复、卸载保留和凭据本机保护边界。
- 将旧 `docs/使用说明.md` 改为兼容跳转页，避免历史链接静默失效。
- 将安装说明中的功能文档引用更新为正式 `使用说明书.md`。
- 修正功能矩阵中 Phase 8.3 已解决的凭据保护、统一版本源和日志目录旧状态。

### 修改与新增

- 新增：`docs/使用说明书.md`。
- 修改：`docs/使用说明.md`、`docs/安装说明.md`。
- 修改：Phase 8.6 阶段文档、Phase 8 总计划、阶段日志、Known Issues、文档信息架构和功能矩阵。
- 删除：无。

### 验证结果

- 正式说明书共 993 行；分 349、355、289 行三批写入，每批均未超过 400 行，并在每批后重新读取检查。
- 旧兼容入口共 10 行，不再重复维护过期功能正文。
- 13 个主章节和 4 个附录均存在，Markdown 代码围栏数量成对，未发现空链接。
- 使用说明书、兼容入口、安装说明交叉链接和阶段文档均通过 `git diff --check` 内容检查；仅存在 Git 的 LF/CRLF 工作区提示。
- 8.6-A01～A08、A10、A12 的代码/文档检查通过。
- 8.6-A09 的脱敏 RC 截图和 8.6-A11 的最终 RC 实机复核纳入 Phase 8.9。
- Migration diff 为空。
- 本阶段为纯文档阶段，未执行 build。

### 明确未做与 Known Issues

- 未修改业务逻辑、ViewModel、XAML、项目文件、安装器或数据库结构。
- 未启动应用，未执行 database update、publish、安装器构建或实际播放。
- 未添加截图，避免使用历史 DesignDraft 冒充最终 RC；由 Phase 8.9 使用脱敏 RC 截图复核。
- 未编写 Phase 8.7 帮助文档、Phase 8.8 README 或发布说明。
- Blocker：Phase 8 总 Blocker P8-B05 的 GPL 对应源代码公开获取安排仍待 Phase 8.8。
- Deferred：帮助中心、双架构应用级 RC、使用说明书截图和最终文案一致性复核。
- Noise：旧 `docs/使用说明.md` 仅为兼容入口，不是第二份说明书事实源。
- 建议进入 Phase 8.7 帮助文档。

## 2026-06-21 - Phase 8.5 正式安装说明

### 完成内容

- 将原有开发/测试安装说明更新为面向终端用户的 XFVerse 1.0 正式安装说明。
- 删除 Visual Studio、额外 .NET Runtime、测试打包脚本、测试数据库快照和用户数据覆盖等正式用户不应执行的流程。
- 明确安装说明只负责部署生命周期；功能教程转 Phase 8.6，完整故障排查转 Phase 8.7。
- 记录 Windows x64 与 ARM64 安装包选择方法、正式文件名、大小和 SHA-256。
- 记录 self-contained 运行时、磁盘空间、网络和外部服务要求。
- 按正式 Inno Setup 中文文案记录安装位置、附加任务、安装和完成步骤。
- 记录未签名安装包的 SmartScreen/未知发布者处理原则，不要求关闭安全软件。
- 根据代码记录首次启动的数据目录、SQLite migration、主题和应用服务初始化行为。
- 记录程序目录、用户数据库、视频/海报/字幕缓存、日志和资料的默认位置。
- 记录升级、修复、x64/ARM64 双向切换、默认卸载和完全清理流程。
- 明确安装、升级、修复、卸载和软件数据清理均不会删除本地或 WebDAV 媒体文件。

### 修改与新增

- 修改：`docs/安装说明.md`。
- 修改：Phase 8.5 阶段文档、Phase 8 总计划、阶段日志和 Known Issues。
- 新增：无。
- 删除：无。

### 验证结果

- 正式安装说明共 321 行，单次写入未超过 400 行，写入后已重新读取检查。
- 文件名、大小和 SHA-256 与两个正式 release manifest 一致。
- 安装按钮、默认目录、桌面快捷方式默认状态、完成页启动选项与正式 `.iss` 一致。
- x64/ARM64 安装、同版本修复、卸载和跨架构生命周期报告均为 PASS。
- Authenticode 状态为 NotSigned，文档已如实披露。
- 用户数据、数据库、缓存和日志位置与 `AppPaths` 和数据库初始化代码一致。
- 文档内相对链接指向现有软件使用说明、设计说明书和正式打包记录。
- Migration diff 为空。

### 明确未做与 Known Issues

- 未修改业务逻辑、项目运行行为、XAML、安装器或数据库结构。
- 未重新 build、publish 或编译安装器。
- 未启动应用，未执行 database update、数据库初始化或旧数据升级。
- Blocker：Phase 8 总 Blocker P8-B05 的 GPL 对应源代码公开获取安排仍待 Phase 8.8。
- Deferred：Phase 8.7 完整帮助中心链接、Phase 8.9 应用首次启动和干净环境 RC、数字签名。
- Noise：未添加安装器截图；正式安装器流程可由稳定中文文案完整说明。
- 建议进入 Phase 8.6 软件使用说明书。

## 2026-06-20 - Phase 8.4 ARM64 独立正式包补充

### 完成内容

- 用户将原 x64 单架构决策更新为 x64 与 ARM64 两个独立正式候选安装包。
- 正式打包脚本增加 `win-x64`、`win-arm64` 参数化构建，按 RID 隔离 publish、staging、reports 和 output。
- ARM64 包只携带 ARM64 应用入口、libmpv 与 ffprobe；x64 包继续只携带 x64 原生文件。
- 发布打包不再把原始人格海报和双架构原生目录先复制到 publish，降低峰值磁盘占用；普通开发构建资源行为不变。
- 安装器使用同一稳定正式 AppId，架构切换时清理另一架构的 mpv 与 ffprobe 程序目录。
- 重建 x64 包并生成 ARM64 包，分别完成安装、同版本修复和卸载。
- 完成 x64→ARM64、ARM64→x64 双向覆盖安装验证。

### 产物

- x64：`XFVerse-Setup-1.0.0-win-x64.exe`，253,966,095 bytes，SHA-256 `164AA04901D00FDFDB7A5B1D92013B4E534F2837052EA2A07F469345C0A9DB3A`。
- ARM64：`XFVerse-Setup-1.0.0-win-arm64.exe`，245,878,081 bytes，SHA-256 `8C8A29DAAC8C36F3CBE68E668CCC46FAB93C382184B453C84C18E4653C2D268E`。
- x64 staging：547 个文件，541,551,120 bytes。
- ARM64 staging：546 个文件，549,843,224 bytes。
- 两个架构分别保留 manifest、文件清单、体积、SHA-256、敏感扫描、第三方清单和安装生命周期报告。

### 验证结果

- x64 应用入口、libmpv、ffprobe 的 PE Machine 均为 `8664`。
- ARM64 应用入口、libmpv、ffprobe 的 PE Machine 均为 `AA64`。
- 两个 ffprobe 均能执行 `-version`。
- 两架构首次安装、同版本修复和卸载退出码均为 0。
- 双向架构切换后，目标架构文件存在，另一架构原生程序目录已删除。
- 用户数据目录存在状态和时间戳不变。
- 两个 staging 的数据库、日志、PDB、非目标架构、私有路径和可疑秘密命中均为 0。
- Migration diff 为空。

### 明确未做与 Known Issues

- 未修改业务逻辑、XAML、导航或功能行为。
- 未启动安装后的应用，未执行数据库初始化、database update、旧数据库升级或实际播放。
- Blocker：公开分发前仍需完成 GPL 对应源代码获取安排。
- Deferred：双架构干净环境应用级 RC、数字签名、首次启动和旧数据库升级。
- Noise：旧单目录 x64 生成物已删除，正式产物只保留按 RID 隔离的新目录。

## 2026-06-20 - Phase 8.4 初始 x64 候选（已由上方双架构记录取代）

### 完成内容

- 新增正式版独立打包脚本、安装器定义和安装生命周期测试脚本。
- 正式链路不读取用户 AppData，不调用测试 seed-data，不复用测试安装器 AppId。
- 建立固定正式 AppId `{C0D20576-16FD-4539-94AA-FA7041348EEB}`。
- 构建 XFVerse 1.0.0 Windows x64 self-contained Release staging。
- Release 二进制禁用 PDB/CodeView，敏感扫描确认不含开发机绝对路径。
- 排除 ARM64、PDB、mpv 头文件、导入库、开发 README、数据库、日志、缓存和测试资产。
- 将 50 张超大人格海报在 staging 中等比缩放至最大宽度 1086，源文件不修改。
- 人格海报总大小从 732,263,089 bytes 降至 133,373,311 bytes。
- 生成 242.20 MiB 正式候选安装包。
- 生成 manifest、文件清单、体积、SHA-256、敏感扫描、第三方清单和安装生命周期报告。
- 完成许可文本随包和第三方组件来源、版本、许可证、哈希审计。
- 完成首次安装、同版本覆盖修复和卸载验证。

### 修改与新增

- 新增：`Build-ReleaseInstaller.ps1`、`Test-ReleaseInstaller.ps1`、`XFVerse.ReleaseInstaller.iss`。
- 新增：正式打包记录、第三方清单、Phosphor Notice 和 Apache 2.0 文本。
- 修改：Release 调试信息策略、测试脚本版本读取及 Phase 8 阶段文档。
- 删除：无。

### 产物

初始 x64 候选已被双架构重建产物取代，不再保留其旧校验值；当前值以上方 ARM64 补充记录和正式打包记录为准。

- 文件名：`XFVerse-Setup-1.0.0-win-x64.exe`。
- 大小、SHA-256 和 staging：已废弃，见当前双架构记录。
- FileVersion：1.0.0.0。
- ProductVersion：1.0.0。
- 数字签名：未签名。

### 明确未做

- 未修改业务逻辑或 XAML。
- 未启动安装后的应用。
- 未执行数据库初始化、database update 或旧数据库升级。
- 未执行播放器实际播放。
- 未在独立 Windows x64 RC 环境验证。
- 未提供公共 GPL 对应源代码归档。
- 未 commit，未 push。

### 验证结果

- Release build/publish 通过。
- Inno Setup 6.7.1 编译通过。
- 三个关键 PE 均为 x64。
- ffprobe 8.1 可运行。
- 首次安装、覆盖修复、卸载退出码均为 0。
- 安装文件与 staging 哈希一致。
- 卸载后程序目录删除。
- 用户数据目录存在状态和时间戳不变。
- 数据库、日志、PDB、ARM64、私有路径、可疑秘密扫描均为 0。
- Migration diff 为空。
- 建议进入 Phase 8.5。

### Known Issues

- Blocker：P8-B05 的公共 GPL 对应源代码获取安排由 Phase 8.8 完成。
- Deferred：数字签名、应用首次启动、旧数据库升级、播放器和独立 x64 RC。
- Noise：raw publish 为临时输入，正式保留 staging、安装包和报告。

## 2026-06-20 - Phase 8.3 核心发布加固实施

### 完成内容

- 用户澄清只禁止改变业务逻辑、界面和功能结果，允许修改发布、安全和诊断基础设施。
- 新增根级统一版本源，应用程序集、文件属性和信息版本统一为 XFVerse 1.0.0。
- 测试打包脚本默认读取统一版本，不再以日期作为默认产品版本。
- 移除测试打包脚本中的私人 Python 默认路径，改为显式参数或环境命令发现。
- 将敏感值写入升级为 Windows DPAPI CurrentUser，并增加 `xfv1:dpapi-cu:` 格式版本。
- 保持旧 Base64 凭据兼容读取；现有保存入口自动写入新格式。
- 新扫描历史快照透明加密，旧明文扫描快照继续按原内容显示。
- 统一诊断日志到当前用户应用数据目录的 `Logs` 子目录。
- 新增公共日志脱敏，覆盖凭据、账号、URL、Windows 绝对路径和 UNC 路径。
- 移除 mpv 原生加载诊断中的完整路径和可能携带路径的异常消息。

### 修改与新增

- 新增：`Directory.Build.props`、`DiagnosticLogSanitizer.cs`。
- 修改：Core 项目依赖、SecretProtector、AppPaths、诊断组件、扫描历史快照读写、mpv 诊断和测试打包脚本。
- 修改：8.3 设计、计划、Phase 8 总计划、阶段日志和 Known Issues。
- 删除：无。

### 明确未做

- 未修改任何 XAML。
- 未改变导航、页面布局、扫描识别、推荐、播放器或媒体库业务语义。
- 未新增 migration，未执行 database update。
- 未生成正式安装包，未执行安装、升级、修复或卸载。
- 未 commit，未 push。

### 验证结果

- Debug build 通过，0 警告、0 错误。
- Release build 通过，0 警告、0 错误。
- Release EXE/DLL：FileVersion `1.0.0.0`，ProductVersion `1.0.0`。
- DPAPI 往返、旧 Base64 读取、旧诊断快照和日志脱敏专项验证通过。
- PowerShell 打包脚本语法检查通过。
- XAML diff 为空。
- Migration diff 为空。
- 建议进入 Phase 8.4。

### Known Issues

- Blocker：P8-B01、P8-B02、P8-B05、P8-B06 转 Phase 8.4。
- Resolved：P8-B03、P8-B04、P8-B07 已完成代码整改，仍需在 RC 中复核。
- Deferred：跨 Windows 用户 DPAPI、全新初始化和完整升级在隔离 RC 环境验证。
- Noise：行尾转换警告不影响构建结果。

## 2026-06-20 - Phase 8.3 发布加固设计与现状审计

### 完成内容

- 审计统一版本来源、设置页版本、测试包版本和安装程序身份现状。
- 审计 WebDAV、字幕服务等敏感配置的现有 Base64 保存方式。
- 审计应用数据目录、日志路径、原生组件路径输出和扫描任务隐私快照。
- 新增 XFVerse 1.0 发布加固设计。
- 设计统一版本源及程序集、UI、打包脚本、安装器和发布说明的消费关系。
- 设计基于 Windows DPAPI CurrentUser 的带版本密文格式。
- 设计旧 Base64 配置兼容读取、保存时迁移和损坏配置处理规则。
- 建立安装、首次启动、升级、修复、卸载和完整清理的数据生命周期矩阵。
- 设计日志目录、脱敏、保留、清理和诊断包导出边界。
- 明确正式包白名单、禁止内容、正式 AppId 和安全卸载要求。
- 建立后续实施影响面、顺序、验收矩阵、风险和回滚原则。

### 修改与新增

- 新增：`XFVERSE_1_0_RELEASE_HARDENING_DESIGN.md`。
- 修改：8.3 计划、Phase 8 总计划、阶段日志和 Known Issues。
- 删除：无。

### 明确未做

- 未修改任何 C#、XAML、项目文件、资源、脚本或安装器。
- 未建立实际版本源。
- 未实现 DPAPI 和旧配置迁移。
- 未整改现有日志输出。
- 未执行 build、publish 或 installer。
- 未执行安装、首次启动、升级、修复和卸载验收。
- 未新增 migration，未执行 database update。
- 未 commit，未 push。

### 验证结果

- 发布加固设计共 814 行、16 个二级章节。
- Markdown 标题和代码围栏检查通过。
- 8.3 文档方案完成，但 8.3-A01～A09 的实施验收未通过或未执行。
- Migration diff 为空。
- 不建议进入 Phase 8.4 正式打包实施；仅可在相同限制下继续只读审计或打包方案文档。

### Known Issues

- Blocker：测试包数据、覆盖删除、版本源、敏感配置、许可、安装身份和日志隐私问题仍未解除。
- Deferred：ARM64、签名、包体积、自动更新和高负载播放保持原计划。
- Noise：历史测试产物、开发日志位置和内部打包工具不作为正式发布输入。

## 2026-06-20 - Phase 8.2 软件设计说明书

### 完成内容

- 新增 XFVerse 1.0 软件设计说明书。
- 设计说明覆盖产品上下文、总体架构、模块、数据、关键流程、外部服务、安全、性能、诊断、部署和 UI/UX。
- 建立核心实体关系、媒体来源生命周期、扫描识别、播放和字幕流程图。
- 记录当前 Windows/.NET 8/C#/WPF/EF Core/SQLite 实际技术栈。
- 记录 UI 既有“先草图和页面规格、后实际 WPF 实现、再运行收口”的设计流程。
- 建立主要草图、页面规格、实际 View 和差异原因的追溯矩阵。
- 明确 `DesignDraft` 中 WPF UI 库目标与当前原生 WPF 实现的差异。
- 明确 Movie/TV 边界、非破坏性文件语义和正式发布设计约束。

### 修改与新增

- 新增：`XFVERSE_1_0_SOFTWARE_DESIGN.md`。
- 修改：8.2 计划、Phase 8 总计划、阶段日志和 Known Issues。
- 删除：无。

### 明确未做

- 未修改任何 C#、XAML、项目文件、资源、脚本或安装器。
- 未重新绘制草图，未修改实际 UI。
- 未执行 build、publish、installer 或运行截图验收。
- 未新增 migration，未执行 database update。
- 未编写课程设计报告。
- 未 commit，未 push。

### 验证结果

- 8.2-A01～A12 全部通过。
- 设计说明书共 1696 行，UTF-8 检查通过。
- Migration diff 为空。
- 建议进入 Phase 8.3。

### Known Issues

- Blocker：沿用正式打包、版本、凭据、许可和安装器隔离问题。
- Deferred：历史 UI 规格与当前实现存在部分文档漂移，当前设计说明书以代码为准。
- Noise：历史草图的临时颜色、占位块和已替换入口不代表当前实现。

## 2026-06-20 - Phase 8.1 发布基线与文档信息架构审计

### 完成内容

- 完成只读产品和发布审计，未修改代码。
- 建立 XFVerse 1.0 功能证据矩阵，覆盖主导航、隐藏详情页、弹窗、播放器、扫描、识别、外部服务、设置、用户数据和安装发布边界。
- 冻结 1.0 GA 为 Windows 10/11 x64、self-contained、当前用户单架构安装器。
- ARM64、X86、Portable 和自动更新不进入 1.0；数字签名不作为 1.0 功能承诺。
- 冻结覆盖升级、修复和默认卸载均保留用户数据。
- 正式安装器必须使用新 AppId、独立脚本和空 staging，不能复用测试 seed-data 链路。
- 建立 README、安装说明、软件使用说明书、帮助文档、软件设计说明书、发布说明和第三方声明的职责边界。
- 建立 RC 环境矩阵，覆盖操作系统、安装生命周期、扫描、播放、数据、安全和合规。
- 确认 Movie/TV 支持边界以及移出媒体库、删除记录、删除扫描路径和清理缓存的语义。

### 新增文件

- `XFVERSE_1_0_FEATURE_MATRIX.md`
- `XFVERSE_1_0_RELEASE_DECISIONS.md`
- `XFVERSE_1_0_DOCUMENTATION_ARCHITECTURE.md`
- `XFVERSE_1_0_RC_ENVIRONMENT_MATRIX.md`

### 明确未做

- 未修改业务代码、项目文件、资源、脚本或安装器。
- 未执行 build、publish、installer 或 RC 实机验收。
- 未新增 migration，未执行 database update。
- 未开始软件设计说明书、README、安装说明、使用说明书和帮助文档正文。
- 未 commit，未 push。

### 验证结果

- 8.1 验收矩阵 8.1-A01～A10 的文档基线项均完成。
- Migration diff 为空。
- 标准 `git diff --stat` 不显示当前未跟踪的 Phase 8 文档目录；工作区状态以 `git status --short` 和阶段文件清单为准。

### 下一步

- 进入 Phase 8.2，依据功能矩阵和发布决策编写软件整体设计说明书。
- UI 章节记录项目既有“先绘制草图和页面规格，再依据草图修改实际 UI”的设计流程，不重新绘制草图，也不修改实际 UI。

### Known Issues

- Blocker：测试打包链路、用户数据删除、版本源、凭据保护、第三方许可和正式安装器身份隔离仍待 8.3/8.4 解决。
- Deferred：ARM64、自动更新、签名承诺和包体积优化不阻断 8.2。
- Noise：历史测试包和本地 artifacts 不纳入正式发布输入。

## 2026-06-20 - Phase 8 planning baseline

### 完成内容

- 建立 Phase 8 总计划。
- 将正式版工作拆分为 8.1～8.9 九个小阶段。
- 新增独立的软件设计说明书阶段。
- 明确 UI 相关要求是记录既有设计流程：先绘制草图和页面规格，再依据草图修改实际 UI；Phase 8 不重新绘制草图，也不新增 UI 改造阶段。
- 明确区分 README、安装说明、软件使用说明书、帮助文档、发布说明和第三方声明的定位。
- 确认正式包必须与现有测试包链路分离。
- 记录当前发布审计中发现的初始风险：
  - 测试包复制用户数据并覆盖用户数据目录。
  - 测试包可能携带凭据和私有记录。
  - 应用尚无明确统一版本源。
  - 当前敏感字段保护仅为 Base64。
  - 当前 x64 自包含测试发布目录和安装包体积较大。
  - 现有安装说明、使用说明和 README 不满足正式版要求。

### 明确未做

- 未修改业务代码。
- 未修改打包脚本或安装器。
- 未重写现有安装说明、使用说明和 README。
- 未生成正式包。
- 未执行 build、publish 或 installer。
- 未新增 migration，未执行 database update。
- 未 commit，未 push。

### 下一步

- 进入 Phase 8.1，建立代码—UI—文档—RC 证据矩阵，并冻结 1.0 支持范围。

### Known Issues

- Blocker：现有测试包不能作为正式包发布。
- Deferred：ARM64 是否进入 1.0 尚未决定。
- Noise：仓库根目录存在历史测试安装包和本地 artifacts，但当前未纳入本次文档变更。
