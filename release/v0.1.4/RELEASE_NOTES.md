# CodexMeter for Windows v0.1.4

本次是 Windows 重置历史显示与交互更新，整合本地修订版 0.1.3.1、0.1.3.2 的改动。

## 更新内容

- 点击主卡片的重置倒计时，直接打开重置历史时间轴。
- 每日刻度延伸到今天；即使数天没有重置记录，也显示这些日期的刻度。
- 新增“距上次重置已过”，显示天、小时、分钟，每分钟刷新；悬停查看对应记录的时间、来源与可信度。
- 时间轴底部“查看历史时间”切换到可滚轮浏览的记录列表；“返回时间轴”切回时间轴。
- 面板打开期间跨天自动补刻度；浏览较早日期时保留当前位置。
- 只有一条重置记录也可以打开时间轴；没有记录时显示空状态，不虚构重置事件。

继续包含经过 SHA-256 校验的 Win-CodexBar CLI 0.45.2，无需另外安装 CodexBar。

## 下载与安装

1. 下载 `Codex-Meter-Windows-portable-v0.1.4.zip`，可用同名 `.sha256` 文件核对完整性。
2. 完整解压，进入 `CodexMeter Windows v0.1.4` 文件夹。
3. 已登录 Codex 桌面客户端或已运行 `codex login` 后，双击 `CodexMeter.exe`。
4. 如需安装到当前用户目录，运行包内 `install.ps1`；升级前请从托盘退出旧程序。

支持 Windows 10/11、.NET Framework 4.7.2 或更高版本。设置与重置历史继续使用原有目录。程序与内置 CLI 未进行 Authenticode 签名，Windows 首次运行可能提示未知发布者。

## 平台与旧版本

- 本 Release 仅提供 Windows 安装包。
- [Windows v0.1.3](https://github.com/xumanba/codex-meter/releases/tag/v0.1.3) 保留，可独立下载。
- macOS 继续使用现有 [v0.2.0 ZIP / DMG](https://github.com/xumanba/codex-meter/releases/tag/v0.2.0)，本次不修改 macOS 程序和安装包。
- 详细版本差异见仓库 [VERSION-GUIDE.md](https://github.com/xumanba/codex-meter/blob/main/VERSION-GUIDE.md)。

## 校验与验证

- Windows 程序文件版本：`0.1.4.0`。
- ZIP：6,280,654 字节，包含 10 个发布文件。
- ZIP SHA-256：`007FB74196ABBBE6DE7F801A54B1E5B99C77516F1779EB40633C3BE96E893DF0`。
- 211 项自动检查通过，严格编译（警告视为错误）通过。
- 已检查时间轴和记录列表的渲染预览，并验证 ZIP 内程序及 CLI 与构建产物哈希一致。
