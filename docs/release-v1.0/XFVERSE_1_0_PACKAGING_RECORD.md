# XFVerse 1.0 正式打包记录

## 1. 记录信息

| 项目 | 内容 |
| --- | --- |
| 所属阶段 | Phase 8.4；P8-B05 合规重建于 Phase 8.8；关闭修复重建于 Phase 8.9 |
| 产品版本 | 1.0.0 |
| 目标架构 | Windows x64、Windows ARM64 |
| 发布模式 | Release、self-contained |
| 打包状态 | 候选安装包已生成并通过静默生命周期、首次启动和正常退出验证；GA 仍被 RC Blocker 阻断 |
| 记录日期 | 2026-06-21 |

本文档记录 XFVerse 1.0 正式打包链路、产物、校验值、包内容、安装行为和当前限制。

## 2. 正式打包入口

对应源代码包命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Build-CorrespondingSource.ps1
```

正式打包命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Build-ReleaseInstaller.ps1 `
  -RuntimeIdentifier win-x64

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Build-ReleaseInstaller.ps1 `
  -RuntimeIdentifier win-arm64
```

安装生命周期验证命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Test-ReleaseInstaller.ps1 `
  -RuntimeIdentifier win-x64

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Test-ReleaseInstaller.ps1 `
  -RuntimeIdentifier win-arm64
```

正式链路与测试链路完全分离：

| 项目 | 正式链路 | 测试链路 |
| --- | --- | --- |
| 构建脚本 | `Build-ReleaseInstaller.ps1` | `Build-TestInstaller.ps1` |
| 安装定义 | `XFVerse.ReleaseInstaller.iss` | `XFVerse.TestInstaller*.iss` |
| AppId | `{C0D20576-16FD-4539-94AA-FA7041348EEB}` | 测试专用 AppId |
| 产物目录 | `artifacts/release/1.0.0/<RID>/` | `artifacts/test-installer/` |
| 产物名称 | `XFVerse-Setup-1.0.0-win-x64.exe`、`XFVerse-Setup-1.0.0-win-arm64.exe` | `XFVerse-TestSetup-*` |
| 用户数据输入 | 禁止 | 测试链路可能使用 seed data |
| 用户数据删除 | 禁止 | 测试安装定义存在覆盖行为 |

## 3. 构建参数

正式 publish 使用：

- Configuration：Release。
- RuntimeIdentifier：分别为 win-x64、win-arm64。
- SelfContained：true。
- PublishTrimmed：false。
- PublishSingleFile：false。
- PublishReadyToRun：false。
- DebugType：none。
- DebugSymbols：false。

未启用 trimming、单文件和 ReadyToRun，避免在 1.0 阶段引入未经验证的 WPF、反射或原生加载兼容风险。

Release 项目统一关闭 PDB 和 CodeView 路径，避免程序集携带开发机绝对路径。

## 4. 产物

### 4.1 安装包

| 架构 | 文件名 | 大小 | SHA-256 | 签名 |
| --- | --- | --- | --- | --- |
| x64 | `XFVerse-Setup-1.0.0-win-x64.exe` | 266,950,555 bytes / 254.58 MiB | `6D7641FBEB7E20FC282EE23BF81DF7ECA1CE81DFE6D4366ED1DE38D167F04A15` | 未签名 |
| ARM64 | `XFVerse-Setup-1.0.0-win-arm64.exe` | 240,257,892 bytes / 229.13 MiB | `6C75397ADAD4CEDF6374CA936D367D989C817ABB0ADAF9A0A31ECD6DADA52BC8` | 未签名 |

两个安装包的 FileVersion 均为 `1.0.0.0`，ProductVersion 均为 `1.0.0`。

安装包位于忽略版本控制的正式产物目录：

```text
artifacts/release/1.0.0/win-x64/output/
artifacts/release/1.0.0/win-arm64/output/
```

### 4.2 对应源代码包

| 架构 | 文件名 | 大小 | SHA-256 |
| --- | --- | --- | --- |
| x64 | `XFVerse-1.0.0-corresponding-source-win-x64.zip` | 41,353,342 bytes | `295DD94628A74B0D85890882B14EC4A2F1D42015AF31D28A3E8018F84941D8E9` |
| ARM64 | `XFVerse-1.0.0-corresponding-source-win-arm64.zip` | 41,353,339 bytes | `7836F690311094DC3CDE280A53D09A4A0C7C1A68C417E3F8BC9ED5ACDE7883C6` |

两个源码包内部 `SHA256SUMS.txt` 均通过逐文件校验，每包包含 9 个被校验文件，以及机器可读对象映射。

### 4.3 Staging

| 架构 | 文件数 | 总大小 | 非目标架构文件 | 数据库/日志/PDB | 私有路径/可疑秘密 |
| --- | --- | --- | --- | --- | --- |
| x64 | 550 | 583,333,102 bytes / 556.31 MiB | 0 | 0 | 0 |
| ARM64 | 549 | 491,489,782 bytes / 468.72 MiB | 0 | 0 | 0 |

两个架构分别保留 staging、reports 和 output。原始 publish 只作为中间输入，在 staging 完成后删除。

## 5. 体积治理

### 5.1 人格海报

实际人格海报 UI 区域约为 334×444，源图多数约为 3258×4344。

正式 staging 对宽度大于 1086 的 PNG 进行高质量等比缩放：

| 项目 | 值 |
| --- | --- |
| 优化文件数 | 50 |
| 优化前总大小 | 732,263,089 bytes |
| 优化后总大小 | 133,373,311 bytes |
| 目标上限 | 1086×约1448 |
| 源文件 | 不修改 |

目标图仍约为实际显示尺寸的 3.25 倍，能够支持高 DPI 和高质量缩放。

### 5.2 原生资源

每个正式 staging 只保留自身架构的：

- `mpv/<RID>/libmpv-2.dll`
- `tools/ffmpeg/<RID>/ffprobe.exe`

已移除：

- 另一架构的原生目录。
- mpv 头文件。
- mpv 导入库。
- 原生组件开发 README。
- PDB。
- 测试数据和 seed-data。

### 5.3 主要体积来源

| 文件或类别 | x64 | ARM64 |
| --- | --- | --- |
| libmpv-2.dll | 117,972,992 bytes | 79,081,984 bytes |
| ffprobe.exe | 142,620,672 bytes | 75,474,944 bytes |
| 优化后人格海报 | 133,373,311 bytes | 133,373,311 bytes |
| .NET/WPF 运行时及应用程序集 | 其余文件 | 其余文件 |

## 6. 原生架构与版本

| 架构 | 文件 | PE Machine | 版本或来源 | SHA-256 |
| --- | --- | --- | --- | --- |
| x64 | MediaLibrary.App.exe | 8664 | XFVerse 1.0.0 | `5621CE66A497A4AEE754040D9FBE7F82FDEBC0878EE129E7EE0604891E1ECD70` |
| x64 | libmpv-2.dll | 8664 | v0.41.0-514-g06f4ce75a | `AE7B7B6B3ECCC7AEEFF3CBEF30FD175F97B27F1B815F89D0BD0CCA926C8C2D99` |
| x64 | ffprobe.exe | 8664 | BtbN FFmpeg n8.1.2-20260620 | `5ADF6B558DA8CB60F15B67D6FF1E0B2B0EDCFEBAA1F47106A66F20506B43E769` |
| ARM64 | MediaLibrary.App.exe | AA64 | XFVerse 1.0.0 | `2CB3BA2E1125A306F65BC42F8AF610961A8316589E5045CECA76CEAD0401F71C` |
| ARM64 | libmpv-2.dll | AA64 | v0.41.0-514-g06f4ce75a | `37C731D685A164CBD3BA544EA0C0E8D9DDD02C12B615DEB33917EED60FC0CFB4` |
| ARM64 | ffprobe.exe | AA64 | BtbN FFmpeg n8.1.2-20260620 | `DA87820CAC84552A59B192EB0C99B5388068A387028060BA580A7D460B04C2CC` |

`8664` 表示 Windows x64 / AMD64，`AA64` 表示 Windows ARM64。

两个 staging 中的 ffprobe 均已执行 `-version` 并成功返回版本和 GPLv3 构建配置；对 1 秒 WAV 样本执行 JSON duration 探测均返回 `1.000000`、退出码 0。

## 7. 安装器行为

### 7.1 安装

- 当前用户安装。
- 不要求管理员权限。
- 安装、任务、进度和卸载的主要提示为简体中文。
- 默认安装目录：当前用户 LocalAppData 下的 Programs/XFVerse。
- 创建开始菜单快捷方式。
- 桌面快捷方式为可选任务，默认不勾选。
- 静默安装不自动启动应用。
- 安装包不包含用户数据库、用户配置、缓存或日志。

### 7.2 覆盖升级与修复

- 正式 AppId 固定。
- x64 与 ARM64 使用同一正式 AppId、版本号、安装目录和用户数据格式。
- 使用原安装目录。
- 覆盖程序文件。
- 架构切换时删除另一架构的 mpv 与 ffprobe 程序目录。
- 保留安装任务选择。
- 不读取或删除 XFVerse 用户数据目录。
- 当前候选包已通过同版本覆盖修复验证。

### 7.3 卸载

- 删除程序文件、开始菜单和由安装器创建的快捷方式。
- 默认保留用户数据。
- 不删除本地媒体文件。
- 不删除 WebDAV 文件。
- 当前版本不提供“同时删除用户数据”选项。

## 8. 安装生命周期验证

验证使用工作区临时安装目录，未启动应用，未执行数据库初始化。

| 检查 | x64 | ARM64 |
| --- | --- | --- |
| 首次静默安装 | 退出码 0 | 退出码 0 |
| 必需文件存在 | 通过 | 通过 |
| 安装后应用哈希与 staging 一致 | 通过 | 通过 |
| 同版本覆盖修复 | 退出码 0 | 退出码 0 |
| 静默卸载 | 退出码 0 | 退出码 0 |
| 程序目录删除 | 通过 | 通过 |
| 用户数据目录存在状态与时间戳不变 | 通过 | 通过 |
| 应用首次启动 | 未执行 | 未执行 |
| Database migration | 未执行 | 未执行 |

另执行 x64→ARM64 和 ARM64→x64 双向覆盖安装：目标架构文件存在、另一架构原生目录已删除、卸载退出码为 0，用户数据目录状态与时间戳保持不变。

## 9. 随包许可证

安装后的 `licenses` 目录包含：

- Apache 2.0。
- GNU GPL version 3。
- Inno Setup License。
- Microsoft .NET License。
- Microsoft .NET Third-Party Notices。
- Microsoft Windows Desktop License。
- SmartDate Notice。
- Phosphor Icons Notice。
- XFVerse 1.0 第三方组件清单。

第三方组件的版本、来源、许可证、修改状态和哈希见：

```text
docs/third-party/XFVERSE_1_0_THIRD_PARTY_INVENTORY.md
```

Phase 8.8 已新增 `THIRD_PARTY_NOTICES.md`、`CORRESPONDING_SOURCE.md` 和双架构对应源代码包。正式打包脚本与安装生命周期验证脚本会复制并校验：

- `licenses/THIRD-PARTY-NOTICES.txt`
- `licenses/CORRESPONDING-SOURCE.txt`
- `licenses/CORRESPONDING-SOURCE-SHA256.txt`

两个候选安装包已在 2026-06-21 重建，并确认上述文件实际进入 staging 和安装目录。

本次合规重建：

- 保留来自 `shinchiro/mpv-winbuild-cmake` 20260419 发布的双架构 libmpv。
- 将 x64 和 ARM64 ffprobe 统一替换为 BtbN `autobuild-2026-06-20-13-30` 的 FFmpeg 8.1.2 构建。
- 固定 mpv、FFmpeg 和两套构建系统的源码快照、补丁、发布校验和对象映射。
- 正式打包脚本固定校验四个原生输入的 SHA-256，缺失或不匹配时拒绝打包。
- 关闭 P8-B05；公开 GA 时必须把源码包与对应安装包同时上传。

## 10. 自动化报告

每个架构的 `artifacts/release/1.0.0/<RID>/reports/` 包含：

- `release-manifest.json`
- `file-inventory.txt`
- `size-report.json`
- `sha256.txt`
- `sensitive-scan-report.txt`
- `third-party-inventory.md`
- `installer-lifecycle-report.json`

版本根目录另包含：

- `cross-architecture-lifecycle-report.json`
- `SHA256SUMS.txt`
- `release-manifest.json`

报告使用相对路径，不记录完整开发机路径或秘密。

## 11. 工具链

| 工具 | 版本或标识 |
| --- | --- |
| .NET SDK | 8.0.420 |
| Inno Setup | 6.7.1 |
| Inno Setup compiler SHA-256 | `EB6F4410C8DB367A5F74127E8025AD2CCACC0AFABBE783959D237DF3050F97FB` |

Inno Setup 当前用于非商业课程项目构建。若改变分发性质，需要重新确认工具许可条件。

## 12. Phase 8.9 重建与后续验证

Phase 8.9 在候选版正常关闭验证中发现 WPF `Closing` 重入异常。修复方式为把第二次关闭排队到 Dispatcher，未改变媒体业务逻辑、数据库结构或页面布局。

修复后：

- Release build 通过，0 警告、0 错误。
- 双架构安装包重新构建。
- x64 仿真与 ARM64 原生首次启动、空数据库初始化和正常退出通过。
- ARM64 现有数据副本启动通过，原始数据库未修改。
- 双架构 libmpv 基础媒体载入与 ffprobe 探测通过。
- 双架构安装、修复、卸载和架构切换生命周期重新验证通过。
- 当前表格中的安装包大小和 SHA-256 即为修复后的最终 RC 值。

仍未执行：

- Windows 11 x64 原生设备或虚拟机 RC。
- Windows 10 x64 RC。
- 完整播放器 UI、WebDAV 播放、字幕和续播验收。
- 完整页面级业务矩阵、显示缩放和主题矩阵。
- 数字签名。
- 公共下载页面和对应源代码包的公开上传。

详细状态见 `XFVERSE_1_0_RC_REPORT.md`。
