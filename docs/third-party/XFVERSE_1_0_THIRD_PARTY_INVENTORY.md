# XFVerse 1.0 Third-Party Inventory

Last updated: 2026-06-20

## Distribution scope

This inventory covers the XFVerse 1.0 Windows x64 self-contained installer.
The release pipeline copies this document and the applicable license texts to
the installed `licenses` directory.

## Runtime and libraries

| Component | Version | Source | License | Distribution note |
| --- | --- | --- | --- | --- |
| .NET Runtime | 8.0.26 | https://github.com/dotnet/runtime | MIT and bundled third-party notices | Self-contained x64 runtime; Microsoft license and notices are included. |
| WPF / Windows Desktop Runtime | 8.0.26 | https://github.com/dotnet/wpf | MIT | Self-contained x64 desktop runtime; Microsoft license is included. |
| Entity Framework Core | 8.0.11 | https://github.com/dotnet/efcore | MIT | Included through application publish output. |
| Microsoft.Data.Sqlite | 8.0.11 | https://github.com/dotnet/efcore | MIT | Included through EF Core SQLite provider. |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | https://github.com/dotnet/runtime | MIT | Included through application publish output. |
| System.Security.Cryptography.ProtectedData | 8.0.0 | https://github.com/dotnet/runtime | MIT | Provides Windows DPAPI access. |
| SQLitePCLRaw | 2.1.6 | https://github.com/ericsink/SQLitePCL.raw | Apache-2.0 | Apache 2.0 license text is included. |
| SQLite amalgamation | supplied by SQLitePCLRaw 2.1.6 | https://www.sqlite.org/ | Public Domain | Native e_sqlite3 runtime included by SQLitePCLRaw. |

## Native media tools

### mpv / libmpv

- Distributed file: `mpv/win-x64/libmpv-2.dll`.
- File version: `v0.41.0-514-g06f4ce75a`.
- SHA-256 of audited input:
  `AE7B7B6B3ECCC7AEEFF3CBEF30FD175F97B27F1B815F89D0BD0CCA926C8C2D99`.
- Upstream source: https://github.com/mpv-player/mpv
- Source revision indicated by the binary: `06f4ce75a`.
- Upstream copyright and license information:
  https://github.com/mpv-player/mpv/blob/master/Copyright
- Distribution treatment: GPL-3.0-or-later-compatible terms are applied
  conservatively because the supplied Windows binary is a combined native
  distribution and does not carry a standalone license manifest.
- Modifications: XFVerse does not modify the binary.

The release package includes the GNU GPL version 3 text and source retrieval
information. A future native-runtime refresh must preserve build provenance,
configuration and license files alongside the binary.

### FFmpeg / ffprobe

- Distributed file: `tools/ffmpeg/win-x64/ffprobe.exe`.
- Version: `8.1-essentials_build-www.gyan.dev`.
- SHA-256 of audited input:
  `54B944D6095C4588D7548424D7ACD5118390A9D4F01B1C92F841547FC9A7429C`.
- Upstream source: https://ffmpeg.org/
- Windows build source: https://www.gyan.dev/ffmpeg/builds/
- License: GPL-3.0-or-later.
- Evidence: the binary reports `--enable-gpl --enable-version3`.
- Modifications: XFVerse does not modify the binary.

The release package includes the GNU GPL version 3 text and records the exact
binary version, configuration and hash in the generated reports.

## UI and adapted components

| Component | Source | License | Notice |
| --- | --- | --- | --- |
| SmartDate-inspired date picker | https://github.com/JamesnetGroup/smartdate | MIT | `SMARTDATE_NOTICE.md` |
| Phosphor Icons | https://github.com/phosphor-icons/core | MIT | `PHOSPHOR_ICONS_NOTICE.md` |

## Packaging tool

| Component | Version | Source | License | Distribution note |
| --- | --- | --- | --- | --- |
| Inno Setup | 6.x local compiler | https://jrsoftware.org/isinfo.php | Inno Setup License | Installer bootstrap/uninstaller technology; license text is copied into the installed license bundle. |

## Release obligations

- Keep this inventory with the exact release version.
- Include the referenced MIT, Apache-2.0, GPL-3.0 and Inno Setup texts.
- Include Microsoft .NET third-party notices.
- Do not replace native binaries without updating version, hash, source and
  license evidence.
- Provide corresponding source or equivalent source access required by the
  applicable copyleft licenses when distributing the installer publicly.
- Do not describe third-party components as authored by XFVerse.
