# Codex Meter 版本选择 / Version guide

CodexMeter 的 Windows 与 macOS 安装包彼此独立，不能跨系统使用。当前 `main`
同时维护两端完整源码；每个 ZIP 都可以单独下载。Windows v0.1.3 内置并校验
Win-CodexBar CLI 0.45.2，macOS v0.2.0 内置上游 CodexBar CLI。

CodexMeter packages for Windows and macOS are separate and are not
interchangeable. The `main` branch contains both source lines. Windows v0.1.3
bundles and verifies Win-CodexBar CLI 0.45.2; macOS v0.2.0 bundles the upstream
CodexBar CLI.

## 下载选择 / Choose a download

| 系统 / System | 版本 / Version | 安装包 / Package | 建议 / Recommendation |
|---|---:|---|---|
| Windows 10/11 | v0.1.3 | [`Codex-Meter-Windows-portable-v0.1.3.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.1.3/Codex-Meter-Windows-portable-v0.1.3.zip) | Windows 用户推荐 / Recommended for Windows |
| Windows 10/11 | v0.1.1 | [`Codex-Meter-Windows-portable-v0.1.1.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.1.1/Codex-Meter-Windows-portable-v0.1.1.zip) | 保留的上一版本 / Preserved previous release |
| Windows 10/11 | v0.1.0 | [`Codex-Meter-Windows-portable-v0.1.0.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.1.0/Codex-Meter-Windows-portable-v0.1.0.zip) | 保留的首发版本 / Preserved initial release |
| macOS 14+ | v0.2.0 | [`CodexMeter-macos-universal-0.2.0.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.2.0/CodexMeter-macos-universal-0.2.0.zip) | 当前 Mac 版本；Apple silicon 与 Intel 通用 / Current universal Mac build |

macOS 当前使用 v0.2.0 安装包。Windows v0.1.3 与 macOS v0.2.0 的源码现已
统一在 `main`，两端的安装包仍分别发布。

The current macOS package is v0.2.0. Windows v0.1.3 and macOS v0.2.0 source
lines are now unified on `main`, while their install packages remain separate.

> Windows v0.1.3 包含 `CodexMeter` 统一命名、中等蓝灰新图标、默认简易
> 卡片、可展开的近 7 天 token 与模型偏好，以及重置历史时间轴。Windows
> 与 macOS 安装包仍分别下载、分别安装。

> Windows v0.1.3 includes the unified `CodexMeter` identity, medium blue-gray
> icon, compact-by-default UI, expandable local seven-day/model details and the
> reset-history timeline. Windows and macOS packages remain separate downloads.

## Windows v0.1.3

- 默认使用简易卡片，点击节奏行可展开近 7 天 token、每日占比和模型/推理
  强度偏好。
- ZIP 内置固定版本 Win-CodexBar CLI 0.45.2，并在构建和执行前校验
  SHA-256；只需已有 Codex 登录，不需要另外安装 Win-CodexBar。
- 记录并回推额度重置时间，显示平均、最短和最长可靠间隔，以及可拖动的
  每日时间轴；低、中、高时间可信度分别使用红、蓝、绿。
- 将界面绘制、窗口置顶、贴边状态、刷新调度、历史交互和展示规则拆分为
  可独立测试的 Windows 模块。
- 使用原子缓存写入、脱敏轮转日志和后台增量扫描；保留原有设置与缓存路径。
- 保留托盘、开机启动、左右贴边、实时同步和同屏 Codex/ChatGPT 临时抬高。

## Windows v0.1.3 changes

- Starts with a compact card; the pace row expands local seven-day tokens, daily
  shares and model/reasoning-effort preferences.
- Bundles pinned Win-CodexBar CLI 0.45.2 and verifies its SHA-256 during the
  build and before execution; only an existing Codex login is required.
- Records and infers quota reset times with average, shortest and longest reliable
  intervals plus a draggable daily timeline; low, medium and high time confidence
  use red, blue and green.
- Separates drawing, topmost/window behavior, docking state, refresh coordination,
  history interaction and presentation rules into independently tested modules.
- Uses atomic cache writes, sanitized rotating logs and incremental background
  scans while preserving existing settings and cache locations.
- Retains tray, startup, left/right edge docking, live sync and temporary elevation
  over foreground Codex/ChatGPT on the same display.

## macOS v0.2.0

- Shows weekly remaining percentage, reset time and current-period token total.
- Shows daily token usage for the latest seven days in the current quota window.
- Separates model, reasoning effort and Fast mode preference rows.
- Clears model preference totals when a quota reset is detected and recounts only
  usage after the refresh point.
- Includes edge docking, dark/light glass themes and a universal macOS package.

## Windows v0.1.1 与 v0.1.0 的区别

Windows v0.1.1 保留 v0.1.0 的额度卡片功能，并增加或改进：

- 实时上传/下载网速显示，以及最小化到系统托盘。
- 单实例恢复、可通过“取消始终置顶”切换到普通窗口层级、F5 立即同步和
  Esc 最小化到托盘；Codex/ChatGPT 同屏前台时仍临时抬高。
- 每周额度与 Spark 额度统一使用 `xd xh 后重置`，并对齐重置时间。
- 悬停重置时间可查看准确本机日期；悬停“实时”可查看数据更新时间。
- 使用上游数据时间判断是否过期，避免把旧缓存错误显示为“实时”。
- 提供可选的当前用户安装/卸载脚本；升级时检测仍在运行的已安装程序。
- 增强错误信息脱敏、自测、界面截图回归和 Windows 安装包校验。
- v0.1.3 进一步增加本机近 7 天 token、模型/推理强度偏好、用量颜色梯度、
  节奏行详情折叠和重置历史时间轴。

Windows v0.1.0 是首个跨平台正式版本，继续保留供回退和复现使用，但不包含
上述 v0.1.1 Windows 改进。

## Windows v0.1.1 compared with v0.1.0

Windows v0.1.1 keeps the v0.1.0 allowance card and adds or improves:

- Aggregate live upload/download speed and notification-area minimize/restore.
- Single-instance restore, a “取消始终置顶” action for ordinary window ordering,
  F5 refresh and Esc minimize-to-tray; same-screen foreground Codex/ChatGPT still
  raises the card temporarily.
- Consistent and aligned `xd xh until reset` text for weekly and Spark quotas.
- Exact reset time on hover and latest data-update time on status hover.
- Provider timestamp freshness checks so old cached data is not shown as live.
- Optional per-user install/uninstall scripts with a running-app upgrade guard.
- Error sanitization, self-tests, visual regression previews, and package checks.
- v0.1.3 further adds local seven-day tokens, model/effort preference, a
  usage-share color gradient, pace-row detail expansion and reset-history timeline.

Windows v0.1.0 remains available for rollback and reproduction, but does not
contain the Windows v0.1.1 improvements above.

## 安装与校验 / Install and verify

- Windows：解压所选 ZIP，运行 `CodexMeter.exe`。若使用 `install.ps1` 升级，
  请先从托盘退出旧程序。
- macOS：解压 v0.2.0 ZIP，把 **CodexMeter.app** 移到“应用程序”。
- 每个 Release 都提供对应的 SHA-256 文件。Windows 程序未进行
  Authenticode 签名；macOS v0.2.0 为 ad-hoc 签名且未经过 Apple 公证。

- Windows: extract the selected ZIP and run `CodexMeter.exe`. Exit an installed
  copy from the tray before upgrading with `install.ps1`.
- macOS: extract the v0.2.0 ZIP and move **CodexMeter.app** to Applications.
- Each Release includes matching SHA-256 information. Windows is not
  Authenticode-signed; macOS v0.2.0 is ad-hoc signed and not Apple-notarized.
