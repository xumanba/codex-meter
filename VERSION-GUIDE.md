# Codex Meter 版本选择 / Version guide

Codex Meter 的 Windows 与 macOS 安装包彼此独立，不能跨系统使用。每个 ZIP
都可以单独下载；无需再下载另一个 Codex Meter 版本。Windows 版本仍需本机
已安装并登录 Win-CodexBar。

Codex Meter packages for Windows and macOS are separate and are not
interchangeable. Each ZIP is an independent Codex Meter download. The Windows
build still requires an installed, signed-in Win-CodexBar.

## 下载选择 / Choose a download

| 系统 / System | 版本 / Version | 安装包 / Package | 建议 / Recommendation |
|---|---:|---|---|
| Windows 10/11 | v0.1.1 | [`Codex-Meter-Windows-portable-v0.1.1.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.1.1/Codex-Meter-Windows-portable-v0.1.1.zip) | Windows 用户推荐 / Recommended for Windows |
| Windows 10/11 | v0.1.0 | [`Codex-Meter-Windows-portable-v0.1.0.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.1.0/Codex-Meter-Windows-portable-v0.1.0.zip) | 保留的首发版本 / Preserved initial release |
| macOS 14+ | v0.1.0 | [`Codex-Meter-macos-universal-0.1.0.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.1.0/Codex-Meter-macos-universal-0.1.0.zip) | 当前 Mac 版本；Apple silicon 与 Intel 通用 / Current universal Mac build |

macOS 没有 v0.1.1 安装包。Windows v0.1.1 的开发和发布没有修改 macOS
v0.1.0 的程序、功能或安装包。

There is no macOS v0.1.1 package. The Windows v0.1.1 work does not modify the
macOS v0.1.0 application, features, or installation archive.

## Windows v0.1.1 与 v0.1.0 的区别

Windows v0.1.1 保留 v0.1.0 的额度卡片功能，并增加或改进：

- 实时上传/下载网速显示，以及最小化到系统托盘。
- 单实例恢复、可独立关闭置顶、F5 立即同步和 Esc 最小化到托盘。
- 每周额度与 Spark 额度统一使用 `xd xh 后重置`，并对齐重置时间。
- 悬停重置时间可查看准确本机日期；悬停“实时”可查看数据更新时间。
- 使用上游数据时间判断是否过期，避免把旧缓存错误显示为“实时”。
- 提供可选的当前用户安装/卸载脚本；升级时检测仍在运行的已安装程序。
- 增强错误信息脱敏、自测、界面截图回归和 Windows 安装包校验。

Windows v0.1.0 是首个跨平台正式版本，继续保留供回退和复现使用，但不包含
上述 v0.1.1 Windows 改进。

## Windows v0.1.1 compared with v0.1.0

Windows v0.1.1 keeps the v0.1.0 allowance card and adds or improves:

- Aggregate live upload/download speed and notification-area minimize/restore.
- Single-instance restore, independent always-on-top control, F5 refresh, and
  Esc minimize-to-tray.
- Consistent and aligned `xd xh until reset` text for weekly and Spark quotas.
- Exact reset time on hover and latest data-update time on status hover.
- Provider timestamp freshness checks so old cached data is not shown as live.
- Optional per-user install/uninstall scripts with a running-app upgrade guard.
- Error sanitization, self-tests, visual regression previews, and package checks.

Windows v0.1.0 remains available for rollback and reproduction, but does not
contain the Windows v0.1.1 improvements above.

## 安装与校验 / Install and verify

- Windows：解压所选 ZIP，运行 `CodexMeter.exe`。若使用 `install.ps1` 升级，
  请先从托盘退出旧程序。
- macOS：解压 v0.1.0 ZIP，把 **Codex Meter.app** 移到“应用程序”。
- 每个 Release 都提供对应的 SHA-256 文件。Windows 程序未进行
  Authenticode 签名；macOS v0.1.0 为 ad-hoc 签名且未经过 Apple 公证。

- Windows: extract the selected ZIP and run `CodexMeter.exe`. Exit an installed
  copy from the tray before upgrading with `install.ps1`.
- macOS: extract the v0.1.0 ZIP and move **Codex Meter.app** to Applications.
- Each Release includes matching SHA-256 information. Windows is not
  Authenticode-signed; macOS v0.1.0 is ad-hoc signed and not Apple-notarized.
