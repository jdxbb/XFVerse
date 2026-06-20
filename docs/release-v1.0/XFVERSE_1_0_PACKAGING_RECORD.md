# XFVerse 1.0 正式打包记录

## 1. 记录信息

| 项目 | 内容 |
| --- | --- |
| 所属阶段 | Phase 8.4 |
| 产品版本 | 1.0.0 |
| 目标架构 | Windows x64 |
| 发布模式 | Release、self-contained |
| 打包状态 | 候选安装包已生成并通过静默生命周期验证 |
| 记录日期 | 2026-06-20 |

本文档记录 XFVerse 1.0 正式打包链路、产物、校验值、包内容、安装行为和当前限制。

## 2. 正式打包入口

正式打包命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Build-ReleaseInstaller.ps1
```

安装生命周期验证命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Test-ReleaseInstaller.ps1
```

正式链路与测试链路完全分离：

| 项目 | 正式链路 | 测试链路 |
| --- | --- | --- |
| 构建脚本 | `Build-ReleaseInstaller.ps1` | `Build-TestInstaller.ps1` |
| 安装定义 | `XFVerse.ReleaseInstaller.iss` | `XFVerse.TestInstaller*.iss` |
| AppId | `{C0D20576-16FD-4539-94AA-FA7041348EEB}` | 测试专用 AppId |
| 产物目录 | `artifacts/release/1.0.0/` | `artifacts/test-installer/` |
| 产物名称 | `XFVerse-Setup-1.0.0-win-x64.exe` | `XFVerse-TestSetup-*` |
| 用户数据输入 | 禁止 | 测试链路可能使用 seed data |
| 用户数据删除 | 禁止 | 测试安装定义存在覆盖行为 |

## 3. 构建参数

正式 publish 使用：

- Configuration：Release。
- RuntimeIdentifier：win-x64。
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

| 项目 | 值 |
| --- | --- |
| 文件名 | `XFVerse-Setup-1.0.0-win-x64.exe` |
| 大小 | 253,965,595 bytes |
| 大小（MiB） | 242.20 MiB |
| FileVersion | 1.0.0.0 |
| ProductVersion | 1.0.0 |
| SHA-256 | `50E20EE9890FD18539428B2C1F71380E69B97B0BE9FFF212DDAB396E515CE301` |
| 数字签名 | 未签名 |

安装包位于忽略版本控制的正式产物目录：

```text
artifacts/release/1.0.0/output/
```

### 4.2 Staging

| 项目 | 值 |
| --- | --- |
| 文件数 | 547 |
| 总大小 | 541,555,700 bytes |
| 总大小（MiB） | 516.47 MiB |
| 架构 | x64 |
| 数据库文件 | 0 |
| 日志文件 | 0 |
| PDB | 0 |
| ARM64 文件 | 0 |
| 私有路径命中 | 0 |
| 可疑秘密命中 | 0 |

原始 publish 只作为中间输入，验证后的 staging 被保留；原始 publish 副本在打包结束时删除，避免重复占用约 1 GB 磁盘。

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

正式 staging 只保留：

- `mpv/win-x64/libmpv-2.dll`
- `tools/ffmpeg/win-x64/ffprobe.exe`

已移除：

- ARM64 原生目录。
- mpv 头文件。
- mpv 导入库。
- 原生组件开发 README。
- PDB。
- 测试数据和 seed-data。

### 5.3 主要体积来源

| 文件或类别 | 大小 |
| --- | --- |
| libmpv-2.dll | 117,972,992 bytes |
| ffprobe.exe | 100,853,248 bytes |
| 优化后人格海报 | 133,373,311 bytes |
| .NET/WPF 运行时及应用程序集 | 其余约 189 MB |

## 6. 原生架构与版本

| 文件 | PE Machine | 版本或来源 | SHA-256 |
| --- | --- | --- | --- |
| MediaLibrary.App.exe | 8664 | XFVerse 1.0.0 | `5621CE66A497A4AEE754040D9FBE7F82FDEBC0878EE129E7EE0604891E1ECD70` |
| libmpv-2.dll | 8664 | v0.41.0-514-g06f4ce75a | `AE7B7B6B3ECCC7AEEFF3CBEF30FD175F97B27F1B815F89D0BD0CCA926C8C2D99` |
| ffprobe.exe | 8664 | FFmpeg 8.1 essentials | `54B944D6095C4588D7548424D7ACD5118390A9D4F01B1C92F841547FC9A7429C` |

`8664` 表示 Windows x64 / AMD64。

Staging 中的 ffprobe 已执行 `-version` 并成功返回版本和 GPLv3 构建配置。

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
- 使用原安装目录。
- 覆盖程序文件。
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

| 检查 | 结果 |
| --- | --- |
| 首次静默安装 | 退出码 0 |
| 必需文件存在 | 通过 |
| 安装后应用哈希与 staging 一致 | 通过 |
| 同版本覆盖修复 | 退出码 0 |
| 静默卸载 | 退出码 0 |
| 程序目录删除 | 通过 |
| 用户数据目录存在状态不变 | 通过 |
| 用户数据目录时间戳不变 | 通过 |
| 应用首次启动 | 未执行 |
| Database migration | 未执行 |

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

公开发布前仍需确保 GPL 对应源代码或等价源代码获取方式与安装包同时提供。

## 10. 自动化报告

正式报告目录包含：

- `release-manifest.json`
- `file-inventory.txt`
- `size-report.json`
- `sha256.txt`
- `sensitive-scan-report.txt`
- `third-party-inventory.md`
- `installer-lifecycle-report.json`

报告使用相对路径，不记录完整开发机路径或秘密。

## 11. 工具链

| 工具 | 版本或标识 |
| --- | --- |
| .NET SDK | 8.0.420 |
| Inno Setup | 6.7.1 |
| Inno Setup compiler SHA-256 | `EB6F4410C8DB367A5F74127E8025AD2CCACC0AFABBE783959D237DF3050F97FB` |

Inno Setup 当前用于非商业课程项目构建。若改变分发性质，需要重新确认工具许可条件。

## 12. 未执行与后续验证

本阶段未执行：

- 应用首次启动。
- 空数据目录数据库初始化。
- 旧数据库升级。
- 播放器实际播放。
- Windows x64 独立设备或虚拟机 RC。
- 数字签名。
- 公共下载页面和 GPL 对应源代码托管。

上述项目由 Phase 8.5～8.9 根据各自范围继续完成。
