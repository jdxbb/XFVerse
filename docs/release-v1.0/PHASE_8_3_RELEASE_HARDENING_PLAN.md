# Phase 8.3：正式版安全、版本与数据生命周期收口

Last updated: 2026-06-20

## 阶段状态

- 状态：8.3 核心发布加固已完成。
- 前置条件：Phase 8.1 和 Phase 8.2 完成。
- 实施边界：只修改版本、安全存储、诊断日志和发布基础设施，不改变业务逻辑、界面和功能结果。
- 完成后下一阶段：Phase 8.4 正式打包与安装器工程。

## 目标

在不改变核心产品语义的前提下，解决会阻止公开发布的安全、版本、隐私、升级和卸载问题。

## 只做

1. 建立 1.0.0 单一版本源。
2. 同步程序集版本、EXE 文件属性、设置页、安装器参数和发布文件名。
3. 设计并实现本机凭据保护方案。
4. 保持对现有 Base64 数据的兼容读取，并在安全保存时迁移到新格式。
5. 明确数据库、配置、缓存和日志的数据生命周期。
6. 审计日志、错误信息和诊断输出中的敏感字段。
7. 为正式打包提供可验证的干净应用初始化行为。
8. 修复会导致正式安装、首次启动、升级或卸载不安全的最小问题。

## 不做

- 不新增与发布无关的业务功能。
- 不重构扫描、推荐或播放器架构。
- 不执行 database update。
- 未经授权不新增 migration。
- 不实现在线账号系统或云同步。
- 不把 Base64 描述为加密。

## 重点议题

### 1. 版本单一来源

候选方案应满足：

- 项目构建读取同一版本值。
- `SettingsViewModel.AppVersionText` 与产品版本一致。
- 安装器 `AppVersion` 与产品版本一致。
- 文件名可包含 `XFVerse-Setup-1.0.0-win-x64.exe`。
- RC 可使用 `1.0.0-rc.N` 信息版本，但安装升级排序必须明确。
- GA 不使用日期字符串替代语义化产品版本。

### 2. 凭据保护

优先评估 Windows DPAPI 的当前用户范围保护：

- 仅本机当前 Windows 用户可解密。
- 新格式具备可识别前缀或版本。
- 旧 Base64 数据可兼容读取。
- 下次保存自动转为新格式。
- 解密失败只影响对应配置，不导致整个应用无法启动。
- 日志不得输出明文。

如果需要数据库字段变化，必须暂停并报告最小 migration 方案；优先选择不改 schema 的版本化字符串格式。

### 3. 数据生命周期

必须形成并验证：

- 安装：不写入预置用户数据库。
- 首次启动：应用创建数据目录并迁移数据库。
- 升级：保留数据库、设置、状态和缓存。
- 修复：只恢复程序文件。
- 卸载：默认保留用户数据。
- 完全清除：如提供，必须显式选择并二次确认。
- 删除记录：只删软件记录，不删真实文件。
- 移出媒体库：只隐藏，不清用户状态和 metadata。

### 4. 诊断隐私

- 所有正式日志路径统一落在明确的应用数据日志目录，避免依赖仓库根目录。
- 路径、URL、账号和密钥按既有规则脱敏。
- 帮助文档只能要求用户提供脱敏日志。

## 验收矩阵

| ID | 检查项 |
| --- | --- |
| 8.3-A01 | 设置页、EXE、安装器输入和发布说明可读取同一 1.0.0 版本。 |
| 8.3-A02 | 新保存的 WebDAV 密码和 OpenSubtitles 敏感字段不再是可直接 Base64 还原的明文编码。 |
| 8.3-A03 | 旧 Base64 配置仍可加载，重新保存后迁移到新格式。 |
| 8.3-A04 | 无效或跨用户复制的密文不会导致应用整体启动失败。 |
| 8.3-A05 | 日志不输出完整 URL、路径、账号、Token、Password 或 API Key。 |
| 8.3-A06 | 首次启动空数据目录可创建数据库。 |
| 8.3-A07 | 旧数据库副本升级后核心数据保留。 |
| 8.3-A08 | 安装、升级、修复和卸载的数据策略已形成代码/安装器约束。 |
| 8.3-A09 | `dotnet build MediaLibrary.sln` 通过。 |
| 8.3-A10 | migration diff 为空；如不为空，存在用户明确授权和完整说明。 |

## 主要风险

- DPAPI 会使凭据不可跨 Windows 用户或机器复制；文档必须准确说明。
- 版本字段选择不当会影响安装器升级比较。
- 升级验证必须使用数据库副本，不能直接操作用户真实数据库。
- 日志路径调整不得丢失关键诊断能力。

## 完成时维护

- 本文件。
- `PHASE_8_STAGE_LOG.md`
- `PHASE_8_KNOWN_ISSUES.md`
- 版本与数据生命周期决策记录。
- 相关帮助文档草案中的安全说明。

## 阶段执行记录

- 完成内容：
  - 审计现有版本来源、设置页版本显示、测试打包版本和安装身份。
  - 审计现有 Base64 敏感配置保护和 WebDAV、字幕服务敏感字段。
  - 审计应用数据目录、诊断日志位置、原生组件路径日志和扫描任务快照。
  - 完成统一版本源、DPAPI CurrentUser、旧格式兼容、数据生命周期、日志隐私和正式包边界设计。
  - 新增根级 `Directory.Build.props`，统一 Version、AssemblyVersion、FileVersion 和 InformationalVersion。
  - 测试打包脚本默认读取统一版本源，并移除私人 Python 默认路径。
  - 使用带版本前缀的 DPAPI CurrentUser 格式保护新保存的 WebDAV 和 OpenSubtitles 凭据。
  - 保持旧 Base64 凭据读取兼容，用户再次保存时自动写入新格式。
  - 新扫描历史的 URL、用户名、路径和显示名快照透明加密，旧明文快照继续可读。
  - 诊断日志统一写入用户数据 `Logs` 目录。
  - 增加公共日志脱敏组件，并移除 mpv 诊断中的完整路径和异常消息。
  - 建立后续正式安装、升级、卸载的验收矩阵和回滚边界。
- 修改文件：
  - `Directory.Build.props`
  - `scripts/packaging/Build-TestInstaller.ps1`
  - `src/MediaLibrary.Core/MediaLibrary.Core.csproj`
  - `src/MediaLibrary.Core/Helpers/AppPaths.cs`
  - `src/MediaLibrary.Core/Helpers/SecretProtector.cs`
  - `src/MediaLibrary.Core/Diagnostics/*`
  - `src/MediaLibrary.Core/Services/Implementations/MediaScanService.cs`
  - `src/MediaLibrary.Core/Services/Implementations/LocalMediaScanService.cs`
  - `src/MediaLibrary.Core/Services/Implementations/RecommendationService.cs`
  - `src/MediaLibrary.App/Helpers/*Diagnostics.cs`
  - `src/MediaLibrary.App/Playback/Mpv/MpvNativeLoader.cs`
  - `src/MediaLibrary.App/ViewModels/Pages/RecommendationsViewModel.cs`
  - `PHASE_8_3_RELEASE_HARDENING_PLAN.md`
  - `PHASE_8_PLAN.md`
  - `PHASE_8_STAGE_LOG.md`
  - `PHASE_8_KNOWN_ISSUES.md`
- 新增文件：
  - `Directory.Build.props`
  - `src/MediaLibrary.Core/Diagnostics/DiagnosticLogSanitizer.cs`
  - `XFVERSE_1_0_RELEASE_HARDENING_DESIGN.md`
- 删除文件：无。
- 明确未做事项：
  - 未修改 XAML、页面布局、导航、扫描识别规则、播放器功能或媒体库业务语义。
  - 未新增正式安装器；该工作属于 Phase 8.4。
  - 未执行 publish、安装、升级、修复或卸载实机验收。
  - 未生成正式安装包。
  - 未执行 database update，未新增 migration。
  - 未 commit，未 push。
- build 结果：
  - `dotnet build MediaLibrary.sln --no-restore`：通过，0 警告、0 错误。
  - `dotnet build MediaLibrary.sln -c Release --no-restore`：通过，0 警告、0 错误。
- 关键验证结果：
  - MSBuild Version、AssemblyVersion、FileVersion、InformationalVersion 和 Product 输出符合 1.0.0 设计。
  - Release EXE 和 DLL 的 FileVersion 为 `1.0.0.0`，ProductVersion 为 `1.0.0`。
  - DPAPI 新格式往返、旧 Base64 读取、旧诊断快照兼容和日志脱敏专项验证通过。
  - PowerShell 打包脚本语法检查通过，脚本中不再存在私人绝对 Python 路径。
  - 发布加固设计文档共 814 行、16 个二级章节。
  - Markdown 代码围栏成对。
- migration 状态：Git 范围检查确认 migration diff 为空。
- 人工验收矩阵结果：
  - 8.3-A01：核心版本消费通过，正式安装器和发布说明待后续阶段。
  - 8.3-A02～A03：通过。
  - 8.3-A04：代码路径通过，跨 Windows 用户实机待 RC。
  - 8.3-A05：通过。
  - 8.3-A06：未执行 database update，转 Phase 8.4 隔离环境验证。
  - 8.3-A07：无 schema 变化，旧格式兼容通过；完整升级待 8.4。
  - 8.3-A08：生命周期规则完成，安装器约束待 8.4。
  - 8.3-A09：通过。
  - 8.3-A10：通过，migration diff 为空。
- Known Issues：
  - Blocker：P8-B03、P8-B04、P8-B07 已完成代码整改；P8-B01、P8-B02、P8-B05、P8-B06 待 Phase 8.4。
  - Deferred：ARM64、数字签名、包体积、自动更新和高负载播放等保持原状态。
  - Noise：历史测试包和旧仓库日志不作为正式发布输入。
- `git diff --stat`：最终报告提供；未跟踪 Phase 8 文档不会计入标准统计。
- 是否建议进入 Phase 8.4：建议进入，正式包、正式 AppId、数据生命周期和第三方许可必须在 8.4 完成。

## 建议 commit message

`feat(release): harden versioning secrets and diagnostics`
