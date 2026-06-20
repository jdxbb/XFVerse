# Phase 8.4：正式打包与安装器工程

Last updated: 2026-06-20

## 阶段状态

- 状态：已完成。
- 完成日期：2026-06-20。
- 前置条件：Phase 8.3 核心发布加固已完成。
- 完成后下一阶段：Phase 8.5；Phase 8.6 可并行复核。

## 目标

建立可复现、与测试包完全分离、不会携带或破坏用户数据的 XFVerse 正式打包链路。

## 只做

1. 新增独立正式打包脚本和正式 Inno Setup 配置。
2. 使用干净 staging 和正式 artifacts 目录。
3. 构建支持架构的 Release publish。
4. 仅复制运行必需的 mpv、ffprobe、应用资产和许可证。
5. 裁剪头文件、导入库、错误架构、PDB、测试数据和不必要资源。
6. 优化人格海报等大资源，在不明显损害 UI 的前提下降低体积。
7. 实现安全的安装、升级、修复、卸载行为。
8. 生成 manifest、SHA-256、体积报告和敏感信息扫描报告。
9. 记录打包工具来源、版本和校验方式。

## 不做

- 不复用测试 seed-data。
- 不读取当前用户应用数据作为正式构建输入。
- 不删除用户数据目录。
- 不将开发机绝对路径写入脚本默认参数。
- 不把未验证架构打进正式包。
- 不为了减小体积启用未经验证的 trimming、单文件或 ReadyToRun 组合。

## 正式包与测试包隔离

建议命名：

- 正式脚本：`scripts/packaging/Build-ReleaseInstaller.ps1`
- 正式安装器：`scripts/packaging/XFVerse.ReleaseInstaller.iss`
- 正式 staging：`artifacts/release/1.0.0/staging/`
- 正式 publish：`artifacts/release/1.0.0/publish/win-x64/`
- 正式报告：`artifacts/release/1.0.0/reports/`
- 正式产物：`XFVerse-Setup-1.0.0-win-x64.exe`

正式脚本必须有显式断言：

- 不存在 seed-data 输入。
- 不存在 `.db`、`.db-wal`、`.db-shm`。
- 不存在已知 secrets/config 快照。
- 不存在完整开发机路径字符串。
- 仅包含目标架构的原生二进制。

## 安装器行为

### 安装

- 当前用户安装，默认不要求管理员权限。
- 默认目录建议保持 `{localappdata}\Programs\XFVerse`。
- 创建开始菜单入口。
- 桌面快捷方式应由用户选择，不依赖额外 PowerShell 脚本更稳妥。
- 安装完成可选择启动应用。

### 升级

- 使用稳定 AppId。
- 关闭运行中的应用后覆盖程序文件。
- 不删除用户数据目录。
- 保持快捷方式和卸载信息。
- 从 RC 到 GA 的升级行为必须提前定义。

### 卸载

- 删除程序文件和快捷方式。
- 默认保留用户数据。
- 如提供“同时删除用户数据”，必须是显式可选项、默认关闭，并说明不会删除真实本地或 WebDAV 媒体文件。

## 体积治理

必须生成主要体积来源列表。

优先安全裁剪：

- 非目标架构目录。
- mpv include、`.dll.a` 和开发 README。
- PDB。
- 不需要的发布语言资源；裁剪前验证 WPF 异常与系统资源显示。
- 不需要的 ffmpeg 文档文件。
- 未引用的重复人格海报或过高分辨率资产。

高风险优化需单独验证：

- `PublishTrimmed`
- `PublishSingleFile`
- `PublishReadyToRun`
- 原生组件 UPX 或其他二次压缩

默认不启用高风险优化。

## 第三方组件随包要求

至少审计：

- mpv / libmpv。
- ffmpeg / ffprobe。
- .NET 8 自包含运行时。
- SQLite / SQLitePCLRaw。
- Entity Framework Core。
- SmartDate 派生实现。
- 图标或其他第三方素材。

每项记录：

- 名称和版本。
- 来源。
- 许可证。
- 是否修改。
- 是否需要随包 LICENSE / NOTICE / source offer。
- 安装包中的落盘位置。

## 自动化报告

正式构建至少输出：

- `release-manifest.json`
- `file-inventory.txt`
- `size-report.json`
- `sha256.txt`
- `sensitive-scan-report.txt`
- `third-party-inventory.md` 或等价文件

报告不得包含秘密或完整私有路径。

## 验收矩阵

| ID | 检查项 |
| --- | --- |
| 8.4-A01 | 正式脚本与测试脚本文件、目录、产物名完全分离。 |
| 8.4-A02 | 正式脚本不读取用户 AppData，不调用测试数据注入。 |
| 8.4-A03 | 正式 publish 中没有数据库、用户配置、日志或缓存快照。 |
| 8.4-A04 | 正式安装器不删除用户数据目录。 |
| 8.4-A05 | 安装、覆盖升级、修复和卸载行为符合数据策略。 |
| 8.4-A06 | mpv 和 ffprobe 目标架构正确且可运行。 |
| 8.4-A07 | 不包含错误架构、头文件、导入库、PDB 和测试资产。 |
| 8.4-A08 | 第三方 LICENSE / NOTICE 满足审计结果。 |
| 8.4-A09 | 安装包、安装后目录和主要体积来源已记录。 |
| 8.4-A10 | SHA-256、manifest 和敏感扫描报告已生成。 |
| 8.4-A11 | `dotnet build`、`dotnet publish` 和 Inno 编译通过。 |
| 8.4-A12 | migration diff 状态明确。 |

## 完成时维护

- 本文件。
- `PHASE_8_STAGE_LOG.md`
- `PHASE_8_KNOWN_ISSUES.md`
- `docs/安装说明.md` 的实际安装行为输入。
- 第三方组件清单。

## 阶段执行记录

- 完成内容：
  - 新增与测试链路完全隔离的正式打包脚本和 Inno Setup 定义。
  - 正式版仅构建 win-x64 self-contained Release。
  - 建立干净 release root、临时 raw publish、验证后 staging、reports 和 output 目录。
  - raw publish 在验证和 staging 完成后删除，避免重复占用磁盘。
  - 正式 staging 排除 ARM64、PDB、mpv 头文件、导入库、开发 README、数据库、日志、缓存和测试数据。
  - 对 50 张超大人格海报进行 staging-only 高质量等比缩放，源文件不修改。
  - 新增包内容、架构、私有路径、敏感字段和必需文件自动检查。
  - 新增正式安装、同版本覆盖修复和卸载自动化测试脚本。
  - 建立固定正式 AppId、当前用户安装、可选桌面快捷方式和保留用户数据的卸载行为。
  - 生成 manifest、文件清单、体积、SHA-256、敏感扫描、第三方清单和安装生命周期报告。
  - 完成 mpv、FFmpeg、.NET、EF Core、SQLitePCLRaw、SmartDate、Phosphor Icons 和 Inno Setup 许可清单。
- 修改文件：
  - `Directory.Build.props`
  - `scripts/packaging/Build-TestInstaller.ps1`
  - 本阶段计划、Phase 8 总计划、阶段日志和 Known Issues。
- 新增文件：
  - `scripts/packaging/Build-ReleaseInstaller.ps1`
  - `scripts/packaging/Test-ReleaseInstaller.ps1`
  - `scripts/packaging/XFVerse.ReleaseInstaller.iss`
  - `docs/third-party/XFVERSE_1_0_THIRD_PARTY_INVENTORY.md`
  - `docs/third-party/PHOSPHOR_ICONS_NOTICE.md`
  - `docs/third-party/licenses/APACHE-2.0.txt`
  - `XFVERSE_1_0_PACKAGING_RECORD.md`
- 删除文件：无。
- 明确未做事项：
  - 未修改业务逻辑、XAML、导航、扫描识别、推荐或播放器功能。
  - 未启动安装后的应用。
  - 未执行数据库初始化、database update 或旧数据库升级。
  - 未在独立 Windows x64 设备或虚拟机执行 RC。
  - 未进行数字签名。
  - 未发布公共下载页面或 GPL 对应源代码归档。
  - 未 commit，未 push。
- build / publish / installer 结果：
  - Release publish：通过。
  - Inno Setup 6.7.1 编译：通过。
  - 首次静默安装：退出码 0。
  - 同版本覆盖修复：退出码 0。
  - 静默卸载：退出码 0。
- 产物、体积和 SHA-256：
  - 安装包：`XFVerse-Setup-1.0.0-win-x64.exe`。
  - 安装包大小：253,965,595 bytes，242.20 MiB。
  - 安装包 SHA-256：`50E20EE9890FD18539428B2C1F71380E69B97B0BE9FFF212DDAB396E515CE301`。
  - Staging：547 个文件，541,555,700 bytes，516.47 MiB。
  - 人格海报：732,263,089 bytes 降至 133,373,311 bytes。
- 敏感信息扫描结果：
  - 数据库、日志、PDB、ARM64 文件均为 0。
  - 私有路径和可疑秘密命中均为 0。
  - 安装文件哈希与 staging 一致。
- migration 状态：Git 范围检查确认 migration diff 为空。
- 人工验收矩阵结果：
  - 8.4-A01～A05：通过。
  - 8.4-A06：三个关键 PE 均为 x64，ffprobe 可运行；libmpv 实际播放加载转 RC。
  - 8.4-A07：通过。
  - 8.4-A08：许可文本和清单已随包；公开发布的 GPL 对应源代码获取方式转 8.8。
  - 8.4-A09～A11：通过。
  - 8.4-A12：通过，migration diff 为空。
- Known Issues：
  - Blocker：P8-B05 仍需在 8.8 为公开分发完成对应源代码获取安排。
  - Deferred：数字签名、独立 x64 RC、首次启动和旧数据库升级。
  - Noise：测试安装包和测试 seed-data 不进入正式产物；raw publish 为临时目录。
- `git diff --stat`：最终报告提供；artifacts 被 Git 忽略。
- 是否建议进入 Phase 8.5：建议进入。

## 建议 commit message

`build(release): add clean XFVerse 1.0 installer pipeline`
