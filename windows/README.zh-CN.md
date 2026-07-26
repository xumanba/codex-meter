# Codex Meter for Windows v1.0.0

这是 `xumanba/codex-meter` 的 Windows 原生改编版。它保留了上游的悬浮玻璃卡片交互，但将 SwiftUI/AppKit 界面替换成不需要额外 NuGet 包的 WinForms 单文件程序。

项目同时支持 Windows 与 macOS；跨平台总览和 macOS 使用方式见仓库根目录的 [`README.md`](../README.md)。Windows v1.0.0 便携包发布在 [GitHub Releases](https://github.com/xumanba/codex-meter/releases/tag/v1.0.0)。

## 已实现功能

- 自动识别每周主额度及 Codex Spark 附加窗口
- 剩余额度、重置倒计时、每周节奏标记和预计耗尽时间
- 自适应刷新：Codex 前台约 30 秒、普通显示约 60 秒、贴边/隐藏约 120 秒，失败时自动退避
- 深色/浅色玻璃主题
- 悬浮置顶、跟随 Codex 前台窗口
- 拖动位置记忆、左右贴边自动隐藏和鼠标触边唤出
- 顶部“实时”状态胶囊可直接点击立即同步；同步时会切换为蓝色“同步”状态并更新全部显示数据
- 系统托盘菜单提供“最小化到托盘 / 显示悬浮卡片”切换，双击托盘图标也可隐藏或恢复卡片
- 只调用本机 `codexbar-cli.exe`，不保存登录凭据

## 运行要求

- Windows 10/11
- Windows 自带的 .NET Framework 4.7.2 或更高版本
- 已安装并登录 [Win-CodexBar](https://github.com/Finesssee/Win-CodexBar)

程序按以下顺序寻找 CLI：

1. `%LOCALAPPDATA%\Programs\CodexBar\codexbar-cli.exe`；
2. `%LOCALAPPDATA%\Programs\Win-CodexBar\codexbar-cli.exe`；
3. 用户显式设置的 `CODEXBAR_CLI` 环境变量；
4. 与 `CodexMeter.exe` 同目录的 `codexbar-cli.exe`。

出于路径劫持防护考虑，程序不再自动执行 `PATH` 中名称相似的程序，也不接受 UNC 网络路径或 `codexbar.exe` 别名。

## 直接运行

双击：

```text
windows\dist\CodexMeter.exe
```

也可以从 v1.0.0 Release 下载 `CodexMeter-Windows-portable-v1.0.0.zip`，解压后直接运行 `CodexMeter.exe`。

这是便携版本，不会写注册表，也不会添加开机启动项。界面设置保存在：

```text
%LOCALAPPDATA%\CodexMeter\settings.ini
```

该设置文件只包含窗口位置、主题和显示模式，不包含账号、令牌或 Cookie。

## 安装到当前用户

在 PowerShell 中运行：

```powershell
.\windows\install.ps1 -Launch
```

安装脚本把已构建文件复制到 `%LOCALAPPDATA%\Programs\CodexMeter`，并创建开始菜单快捷方式；不会请求管理员权限，也不会创建开机启动项。

## 从源码构建

```powershell
.\windows\build.ps1
```

构建脚本使用 Windows 自带的 .NET Framework C# 编译器，不会下载依赖。输出位于 `windows\dist`。

生成经过测试并带 SHA-256 的 v1.0.0 Release 便携包：

```powershell
.\windows\package-release.ps1
```

验证解析器：

```powershell
.\windows\dist\CodexMeter.Tests.exe
```

用当前 CodexBar 登录做一次真实只读查询：

```powershell
.\windows\dist\CodexMeter.Tests.exe --live
```

进行只读资源耐久采样（默认 10 分钟，结果写入 `windows\qa`）：

```powershell
.\windows\tools\measure-resource-soak.ps1 -Minutes 10
```

## 与 macOS 上游的差异

- Windows 版直接按分钟调用 `codexbar-cli.exe usage ... --format json`，不常驻占用 18747 端口。
- CLI 的 stdout/stderr 会异步读取，查询有 45 秒硬超时；退出主程序会取消正在进行的查询。
- 贴边轮询只在贴边状态运行，跟随 Codex 的前台检测只在对应模式运行。
- 支持按窗口 DPI 更新尺寸，跨不同缩放比例显示器时会重新计算卡片和贴边位置。
- 同步失败时保留最后一次成功数据，同时明确标记为“过期”；CLI 错误中的邮箱和常见令牌格式会先脱敏。
- 同时兼容 macOS 上游的 camelCase JSON 和 Win-CodexBar 0.45.x 的 snake_case JSON。
- 标准数据按 secondary 识别每周额度；Pro Lite 数据若只有 primary 带七天周期，则按窗口元数据把 primary 识别为每周额度，避免显示无周期的占位值。
- 缺少重置时间时不会虚构倒计时或节奏预测。
- 视觉效果使用 Windows DWM 与自绘半透明卡片，不依赖 macOS 的 `NSVisualEffectView`。
