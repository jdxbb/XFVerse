# Phase 8 Stage Log

Last updated: 2026-06-20

## 2026-06-20 - Phase 8.4 正式打包与安装器工程

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

- 文件名：`XFVerse-Setup-1.0.0-win-x64.exe`。
- 大小：253,965,595 bytes，242.20 MiB。
- SHA-256：`50E20EE9890FD18539428B2C1F71380E69B97B0BE9FFF212DDAB396E515CE301`。
- Staging：547 个文件，516.47 MiB。
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
