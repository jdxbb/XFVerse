# XFVerse 1.0.0 Release Candidate 验收报告

Last updated: 2026-06-21

## 1. 结论

不建议发布，存在以下 Blocker。

本轮 RC 已通过双架构构建、安装器生命周期、隔离首次启动、现有数据副本启动、数据库完整性、libmpv 原生加载、敏感信息抽查和发布制品校验，但尚未满足 GA 的全部 P0 条件。

## 2. 候选版标识

| 项目 | 值 |
| --- | --- |
| 产品 | XFVerse |
| 版本 | 1.0.0 |
| 通道 | RC |
| 源分支 | `main` |
| 源提交 | `204734e8b8705e093ee34dbdded6bde799b91710` |
| 工作区 | 非干净，包含 Phase 8 未提交修改 |
| 构建日期 | 2026-06-21 |
| RC 环境 | Windows 11 ARM64，系统构建 26200 |
| .NET SDK | 8.0.420 |
| Inno Setup | 6.7.1 |

当前提交号只能标识修改前的 Git 基点，不能单独重现未提交补丁，因此本轮产物不能作为可追踪 GA。

## 3. 发布制品

| 制品 | 大小 | SHA-256 | 签名 |
| --- | ---: | --- | --- |
| `XFVerse-Setup-1.0.0-win-x64.exe` | 266,950,555 bytes | `6D7641FBEB7E20FC282EE23BF81DF7ECA1CE81DFE6D4366ED1DE38D167F04A15` | NotSigned |
| `XFVerse-Setup-1.0.0-win-arm64.exe` | 240,257,892 bytes | `6C75397ADAD4CEDF6374CA936D367D989C817ABB0ADAF9A0A31ECD6DADA52BC8` | NotSigned |
| `XFVerse-1.0.0-corresponding-source-win-x64.zip` | 41,353,342 bytes | `295DD94628A74B0D85890882B14EC4A2F1D42015AF31D28A3E8018F84941D8E9` | 不适用 |
| `XFVerse-1.0.0-corresponding-source-win-arm64.zip` | 41,353,339 bytes | `7836F690311094DC3CDE280A53D09A4A0C7C1A68C417E3F8BC9ED5ACDE7883C6` | 不适用 |

根发布目录已生成：

- `SHA256SUMS.txt`
- `release-manifest.json`

清单包含四个制品，逐项重新计算 SHA-256 后全部一致。

## 4. 本轮发现与修复

### RC-BUG-01：关闭主窗口时发生 Closing 重入异常

现象：

- 双架构首次启动 smoke test 能打开主窗口。
- 请求正常关闭时，关闭事件中的异步流程可能在原 `Closing` 事件尚未退出前再次调用 `Close()`。
- WPF 抛出 `InvalidOperationException`，导致不能正常结束进程。

原因：

- `MainWindow.OnClosing` 是异步事件处理器。
- 第一次关闭被取消以执行退出前加载/保存流程。
- 当异步调用同步或快速完成时，第二次 `Close()` 仍处于原关闭事件调用栈中。

修复：

- 通过 WPF Dispatcher 排队执行第二次关闭。
- 未修改媒体业务逻辑、数据库结构、页面布局或用户可见功能。

回归：

- Release build：PASS，0 警告、0 错误。
- x64 仿真首次启动和正常退出：PASS。
- ARM64 原生首次启动和正常退出：PASS。
- ARM64 现有数据副本启动和正常退出：PASS。
- 修复后双架构安装包已重新构建并重新执行生命周期验证。

## 5. 自动与进程级验证

| 项目 | x64 | ARM64 | 结果说明 |
| --- | --- | --- | --- |
| Release build | PASS | PASS | 解决方案构建 0 警告、0 错误 |
| 正式 publish | PASS | PASS | self-contained 目标架构输出 |
| 安装器构建 | PASS | PASS | 正式 Inno Setup 链路 |
| 首次安装 | PASS | PASS | 程序文件与合规文档完整 |
| 同版本修复 | PASS | PASS | 安装生命周期脚本通过 |
| 默认卸载 | PASS | PASS | 用户数据保持不变 |
| 双向架构覆盖 | PASS | PASS | 目标架构文件存在，另一架构目录清理 |
| 干净首次启动 | PASS，ARM64 仿真 | PASS，原生 | 主窗口形成，空数据库创建，正常退出 |
| 现有数据副本启动 | 未执行原生 x64 | PASS | 原始数据只读，副本启动成功 |
| 数据库完整性 | PASS | PASS | `PRAGMA integrity_check = ok` |
| libmpv 原生加载 | PASS，x64 进程 | PASS，ARM64 进程 | 初始化、媒体载入和 duration 探测成功 |
| ffprobe 原生探测 | PASS | PASS | PE 架构、版本和 JSON duration 通过 |
| 敏感信息抽查 | PASS | PASS | 正式包和测试日志未发现凭据或完整私有路径 |
| 对应源代码 | PASS | PASS | 内部清单、对象映射和 SHA-256 通过 |

## 6. 首次启动与数据验证

### 6.1 干净数据目录

x64 和 ARM64 均使用隔离应用数据目录：

- 启动后进程持续运行并形成主窗口。
- 窗口标题与 XFVerse 一致。
- 自动创建数据库。
- 未发现预置用户媒体、历史或凭据。
- 请求关闭后均以退出码 0 正常结束。
- 标准输出和标准错误为空。

### 6.2 现有数据副本

ARM64 使用现有用户数据的临时副本启动：

- 原始数据库哈希在测试前后保持一致。
- 副本成功完成应用启动和正常退出。
- 数据库 WAL 在正常退出后归零。
- 日志未出现 exception、unhandled 或 fatal。
- 未发现完整用户目录、WebDAV URL 或凭据标签泄漏。

### 6.3 数据库结构与核心计数

干净 x64、干净 ARM64 和升级副本均满足：

- `PRAGMA integrity_check = ok`
- 22 张业务/框架表
- 27 条 EF Core migration 记录
- 最新 migration 为 `20260617073620_AddScanTaskLogHistorySnapshots`

升级前后核心业务表计数保持一致，未发现数据丢失。长期文档不记录私有媒体名称、路径或远端地址。

## 7. 原生播放核心验证

对 x64 与 ARM64 分别使用对应架构的最小验证程序加载随包 libmpv：

- 进程架构与目标架构一致。
- libmpv API 可读取。
- `config=no`、`terminal=no`、`vo=null`、`ao=null` 设置成功。
- `mpv_initialize` 返回成功。
- 测试媒体 `loadfile` 返回成功。
- 收到文件载入事件。
- 探测 duration 为 1 秒。

该验证证明原生库和基础媒体载入链路可用，不等同于播放器 UI、全屏、音量、轨道、字幕、WebDAV 或续播验收。

## 8. 安装、快捷方式与卸载

- 双架构安装器均完成首次安装、修复和默认卸载。
- ARM64 安装验证了开始菜单快捷方式与卸载注册项。
- 卸载后快捷方式和卸载注册项被移除。
- 默认卸载未修改现有用户数据库。
- 两个安装器均未签名，文档已明确未知发布者提示和 SHA-256 核对方式。

在 Windows ARM64 上运行 x64 仿真包后，少量 x64 运行时文件可能因仿真进程文件锁而延迟删除。该现象不代表原生 Windows x64 卸载结果，正式 ARM64 用户应使用 ARM64 安装包。

## 9. 未完成的验收

以下项目没有足够证据标记为通过：

- Windows 11 x64 原生干净安装、启动、播放和卸载。
- Windows 11 x64 原生现有数据升级。
- Windows 10 x64 安装、启动、播放和卸载。
- 损坏或 migration 失败数据库的恢复提示与恢复流程。
- 100%、125%、150% 显示缩放。
- 浅色、深色和跟随系统主题。
- HEVC 4K 与大体积 WebDAV 长时间播放。
- 最终 RC 脱敏截图。

当前 Windows 11 ARM64 设备的主导航、扫描、WebDAV、媒体库、详情、播放器、字幕、发现、状态和设置由用户人工确认无异常；该结论不作为其他操作系统环境的兼容性证据。

## 10. Blocker

### P8-B06：GA 构建不可追踪

- 当前工作区包含未提交 Phase 8 修改。
- 当前安装包无法只用记录的提交号重现。
- 需要在明确提交或等价不可变源码快照后，从干净工作区重新构建并复算校验值。

### P8-B07：Windows 11 x64 原生环境缺失

- 当前 x64 仅在 Windows ARM64 的 x64 仿真环境验证。
- 不能用仿真结果代替声明支持的原生 x64 环境。

### P8-B08：Windows 10 x64 环境缺失

- 当前文档仍把 Windows 10 列为目标平台。
- 必须完成该环境 P0 验收，或明确移除 Windows 10 正式支持并同步全部用户文档。

### 已关闭：P8-B09：完整 UI 与业务矩阵

- 用户于 2026-06-21 确认当前 Windows 11 ARM64 设备人工验收无异常。
- Windows UI 自动化运行环境仍不可用，本轮没有形成页面级自动化截图和逐项机器记录。
- 该人工结论仅关闭当前设备 UI 验收，不替代 Windows x64 和 Windows 10 环境验收。

## 11. Deferred

- 安装器 Authenticode 签名。
- 安装包进一步体积优化。
- 自动更新器。
- HEVC 4K 和 70GB 以上 WebDAV 高负载表现。
- 更多 Windows 版本、硬件和显示设备兼容性覆盖。

## 12. 发布前必做

1. 在 Windows 11 x64 原生环境执行 P0 验收。
2. 在 Windows 10 x64 执行 P0 验收，或缩减平台支持声明。
3. 处理并提交当前工作区修改，形成可追踪源码状态。
4. 从干净工作区重建 x64 和 ARM64 安装包。
5. 重新执行 build、安装/修复/卸载、升级副本、敏感扫描和对应源代码验证。
6. 重新生成 `SHA256SUMS.txt`、`release-manifest.json` 和发布说明。
7. 确认 Blocker 为零后，才可把通道从 RC 改为 GA。
