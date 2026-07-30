# Codex Meter for Windows v0.1.1

这是 `xumanba/codex-meter` 的 Windows 原生改编版。它保留了上游的悬浮玻璃卡片交互，但将 SwiftUI/AppKit 界面替换成不需要额外 NuGet 包的 WinForms 单文件程序。

项目同时支持 Windows 与 macOS；跨平台总览和 macOS 使用方式见仓库根目录的 [`README.md`](../README.md)。本次 v0.1.1 只改进 Windows 客户端，macOS v0.1.0 的源码、功能和安装包保持不变。

## 已实现功能

- 自动识别每周主额度及 Codex Spark 附加窗口
- 剩余额度、重置倒计时、每周节奏标记和预计耗尽时间
- 固定重置时间采用不提前归零的紧凑倒计时；鼠标悬停可查看准确的本机日期和时间
- 自适应刷新：Codex 前台约 30 秒、普通显示约 60 秒、贴边/隐藏约 120 秒，失败时自动退避
- 深色/浅色玻璃主题
- 悬浮置顶、跟随 Codex 前台窗口
- “始终置顶”可独立开关，不再与固定/跟随显示模式绑定
- 拖动位置记忆、左右贴边自动隐藏和鼠标触边唤出
- 顶部“实时”状态胶囊可直接点击立即同步；同步时会切换为蓝色“同步”状态并更新全部显示数据
- 标题区每秒显示当前总下载/上传速度（`↓` / `↑`）；仅读取活跃网卡字节计数，不抓包、不分析访问内容，也不需要管理员权限
- 系统托盘菜单提供“最小化到托盘 / 显示悬浮卡片”切换，双击托盘图标也可隐藏或恢复卡片
- 已有实例在托盘或贴边隐藏时，再次运行 `CodexMeter.exe` 会唤回原窗口，不会重复启动
- 键盘快捷键：`F5` 立即同步，`Esc` 最小化到托盘
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

也可以从 Releases 页面下载 `Codex-Meter-Windows-portable-v0.1.1.zip`。解压后进入 `Codex Meter Windows v0.1.1` 文件夹，再运行 `CodexMeter.exe`。

直接运行是便携模式，不会写注册表，也不会添加开机启动项。界面设置保存在：

```text
%LOCALAPPDATA%\CodexMeter\settings.ini
```

该设置文件只包含窗口位置、主题、显示模式和置顶选项，不包含账号、令牌或 Cookie。

## 安装到当前用户

从仓库源码根目录运行：

```powershell
.\windows\install.ps1 -Launch
```

如果当前目录已经是解压后的 Windows ZIP，请运行 `./install.ps1 -Launch`。安装脚本把文件复制到 `%LOCALAPPDATA%\Programs\CodexMeter`，并创建开始菜单快捷方式，不请求管理员权限。只有显式增加 `-StartWithWindows` 时才创建当前用户的开机启动快捷方式；源码根目录与解压目录分别使用：

```powershell
.\windows\install.ps1 -Launch -StartWithWindows
# 或在解压目录：
.\install.ps1 -Launch -StartWithWindows
```

卸载前先从托盘退出程序，然后运行安装目录或解压目录中的：

```powershell
.\uninstall.ps1
```

脚本会逐项确认要移除的程序目录和快捷方式，默认保留 `%LOCALAPPDATA%\CodexMeter` 的界面设置；显式加 `-RemoveSettings` 才会连同设置移除。

## 从源码构建

```powershell
.\windows\build.ps1
```

构建脚本使用 Windows 自带的 .NET Framework C# 编译器，不会下载依赖。输出位于 `windows\dist`。

生成经过测试、带顶层文件夹并附 SHA-256 的 v0.1.1 Release 便携包：

```powershell
.\windows\package-release.ps1 -Version 0.1.1
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
- 网速显示汇总所有已连接且非回环/隧道网卡的系统累计字节差，是整台电脑的总流量提示，并非 Codex 专属流量；多网卡、虚拟网卡或 VPN 分流可能使数值与任务管理器所选网卡不同。最小化到托盘或被“跟随 Codex”模式隐藏时停止采样，恢复显示后重新建立基线。
- 支持按窗口 DPI 更新尺寸，跨不同缩放比例显示器时会重新计算卡片和贴边位置。
- 同步失败时保留最后一次成功数据，同时明确标记为“过期”；CLI 错误中的邮箱和常见令牌格式会先脱敏。
- 同时兼容 macOS 上游的 camelCase JSON 和 Win-CodexBar 0.45.x 的 snake_case JSON。
- 标准数据按 secondary 识别每周额度；Pro Lite 数据若只有 primary 带七天周期，则按窗口元数据把 primary 识别为每周额度，避免显示无周期的占位值。
- 缺少重置时间时不会虚构倒计时或节奏预测。
- Spark 尚未消耗且上游每次同步都把重置时间顺延为“当前时间 + 完整周期”时，按官方用量页面的方式显示绝对日期（例如“8月7日重置”），不再误报成固定的 `6d 23h` 倒计时。
- 超过一天的固定重置倒计时向上取整到小时，少于一天时显示分钟，避免实际还剩近一小时却被直接截掉。
- 耗尽时间采用当前额度周期内的累计平均消耗速度估算；周期开始不足 6 小时时显示“趋势不足，暂不预测”，因此它不是实时费用承诺或精确预测。
- 视觉效果使用 Windows DWM 与自绘半透明卡片，不依赖 macOS 的 `NSVisualEffectView`。
