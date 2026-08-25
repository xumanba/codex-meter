# CodexMeter for Windows v0.1.1（后续开发版）

这是 `xumanba/codex-meter` 的 Windows 原生改编版。它保留了上游的悬浮玻璃卡片交互，但将 SwiftUI/AppKit 界面替换成不需要额外 NuGet 包的 WinForms 单文件程序。

项目同时支持 Windows 与 macOS；跨平台总览和 macOS 使用方式见仓库根目录的 [`README.md`](../README.md)。本次 v0.1.1 只改进 Windows 客户端，macOS v0.1.0 的源码、功能和安装包保持不变。

## 已实现功能

- 聚焦显示每周主额度；仍兼容解析上游附加窗口，但不再绘制低价值的 Spark 整栏
- 剩余额度、重置倒计时、每周节奏标记和预计耗尽时间
- 本机近 7 天 token 总量、每日活动柱状图、按模型/推理强度统计的偏好占比，以及最喜欢的模型推荐
- 模型偏好颜色由使用占比连续决定：低占比为低饱和蓝灰，使用越多越接近醒目的高饱和蓝色；颜色不再固定代表某个模型
- 默认以简易模式显示标题、每周额度和节奏；点击“节奏正常/超额”整行可展开或收起近 7 天与模型偏好，程序下次启动仍默认收起
- 每日百分比按“当前已用周额度 × 当日 token / 近 7 天 token”折算；这是本机活动估算，不是账单或服务端额度明细
- 固定重置时间采用不提前归零的紧凑倒计时；鼠标悬停可查看准确的本机日期和时间
- 自适应刷新：普通显示约 60 秒、贴边/隐藏约 120 秒，失败时自动退避
- 深色/浅色玻璃主题
- 中等深度蓝灰冰晶用量柱图标用于 EXE、窗口和托盘；原深色及浅色源图均保留在仓库中
- Windows 卡片默认持续显示；可最小化到托盘或拖到左右边缘自动隐藏
- “始终置顶”可独立开关；即使关闭该开关，当 Codex/ChatGPT 在同一显示器前台时，卡片也会临时置顶，离开后恢复普通层级
- 菜单可直接勾选“开机自启动”，状态与当前 Windows 用户启动项同步；登录启动时，已贴边自动隐藏的卡片会先展开 30 秒，再恢复正常贴边隐藏
- 拖动位置记忆、左右贴边自动隐藏和鼠标触边唤出
- 顶部“实时”状态胶囊可直接点击立即同步；同步期间名称仍保持“实时”，蓝色状态点表示查询中，悬停显示“正在同步数据，请稍候…”；重复点击会合并到当前查询，避免连续执行两次慢查询
- 标题区每秒显示当前总下载/上传速度（`↓` / `↑`）；仅读取活跃网卡字节计数，不抓包、不分析访问内容，也不需要管理员权限
- 系统托盘菜单提供“最小化到托盘 / 显示悬浮卡片”切换，双击托盘图标也可隐藏或恢复卡片
- 已有实例在托盘或贴边隐藏时，再次运行 `CodexMeter.exe` 会唤回原窗口，不会重复启动
- 键盘快捷键：`F5` 立即同步，`Esc` 最小化到托盘
- Codex CLI 为可选依赖；没有 CLI 时仍显示本机近 7 天统计、模型偏好和最喜欢的模型推荐，额度区域显示“额度需 Codex CLI，本地统计可用”
- 安装并登录 Codex CLI 后，额度通过本机 `codex app-server --stdio` 读取；近 7 天统计只读取 Codex rollout 的时间、模型、强度和 token 字段，不保存登录凭据或对话正文

## 运行要求

- Windows 10/11
- Windows 自带的 .NET Framework 4.7.2 或更高版本
- 可选：已安装并登录 Codex CLI，用于显示每周额度剩余、重置时间和节奏预测

程序按以下顺序寻找 Codex CLI：

1. 用户显式设置的 `CODEX_CLI` 环境变量；
2. 与 `CodexMeter.exe` 同目录的 `codex.exe` 或 `codex.cmd`；
3. `PATH` 中的 `codex.exe` 或 `codex.cmd`；
4. `%APPDATA%\npm\codex.cmd`；
5. 常见当前用户安装目录中的 `codex.exe`。

出于路径劫持防护考虑，程序只接受文件名为 `codex.exe` 或 `codex.cmd` 的本机路径，不接受 UNC 网络路径，也会跳过 WindowsApps 中不可直接执行的 Codex 桌面应用内部路径。

## 直接运行

双击：

```text
windows\dist\CodexMeter.exe
```

也可以从 Releases 页面下载 `Codex-Meter-Windows-portable-v0.1.1.zip`。解压后进入 `Codex Meter Windows v0.1.1` 文件夹，再运行 `CodexMeter.exe`。

直接运行默认不会写注册表或添加开机启动项；只有在菜单中主动勾选“开机自启动”时，才会写入当前用户启动项。界面设置保存在：

```text
%LOCALAPPDATA%\CodexMeter\settings.ini
```

该设置文件只包含窗口位置、主题、贴边和置顶选项，不包含账号、令牌或 Cookie。旧版的 `mode` 字段会在新版启动后自动清理。

近 7 天统计的增量缓存位于 `%LOCALAPPDATA%\CodexMeter\weekly-usage-cache.json`，只包含文件偏移、日期、模型/强度和 token 汇总，不包含提示词、回复或凭据。日志缺少模型元数据时会明确显示“未标注模型”，不会猜测。

## 安装到当前用户

从仓库源码根目录运行：

```powershell
.\windows\install.ps1 -Launch
```

如果当前目录已经是解压后的 Windows ZIP，请运行 `./install.ps1 -Launch`。安装脚本把文件复制到 `%LOCALAPPDATA%\Programs\CodexMeter`，并创建开始菜单快捷方式，不请求管理员权限。只有显式增加 `-StartWithWindows` 时才写入当前用户的 Windows 启动项；它与菜单中的“开机自启动”使用同一个设置。源码根目录与解压目录分别使用：

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

用当前 Codex 登录做一次真实只读查询：

```powershell
.\windows\dist\CodexMeter.Tests.exe --live
```

只验证本机近 7 天日志统计和增量缓存：

```powershell
.\windows\dist\CodexMeter.Tests.exe --weekly-live
```

进行只读资源耐久采样（默认 10 分钟，结果写入 `windows\qa`）：

```powershell
.\windows\tools\measure-resource-soak.ps1 -Minutes 10
```

## 与 macOS 上游的差异

- Windows 版直接按分钟启动 `codex app-server --stdio` 并读取 `account/rateLimits/read`，不再需要 Win-CodexBar 或 `codexbar-cli.exe`，也不常驻占用 18747 端口。
- CLI 的 stdout/stderr 会异步读取，查询有 45 秒硬超时；退出主程序会取消正在进行的查询。
- 贴边轮询只在左右贴边状态运行；Windows 版已删除“跟随 Codex”显示模式，不再因 Codex 前后台状态自动隐藏卡片。
- 网速显示汇总所有已连接且非回环/隧道网卡的系统累计字节差，是整台电脑的总流量提示，并非 Codex 专属流量；多网卡、虚拟网卡或 VPN 分流可能使数值与任务管理器所选网卡不同。最小化到托盘时停止采样，恢复显示后重新建立基线。
- 支持按窗口 DPI 更新尺寸，跨不同缩放比例显示器时会重新计算卡片和贴边位置。
- 同步失败时保留最后一次成功数据，同时明确标记为“过期”；CLI 错误中的邮箱和常见令牌格式会先脱敏。
- 同时兼容 Codex app-server 的 `rateLimits` JSON、macOS 上游的 camelCase JSON 和旧 Win-CodexBar 0.45.x 的 snake_case JSON。
- 标准数据按 secondary 识别每周额度；Pro Lite 数据若只有 primary 带七天周期，则按窗口元数据把 primary 识别为每周额度，避免显示无周期的占位值。
- 缺少重置时间时不会虚构倒计时或节奏预测。
- 每周额度显示 `xd xh 后重置`；鼠标悬停可查看上游时间换算后的准确本机日期与时刻。Spark 附加窗口仍可被解析，但 Windows 界面不再显示该整栏。
- 超过一天的固定重置倒计时向上取整到小时，少于一天时显示分钟，避免实际还剩近一小时却被直接截掉。
- 耗尽时间采用当前额度周期内的累计平均消耗速度估算；周期开始不足 6 小时时显示“趋势不足，暂不预测”，因此它不是实时费用承诺或精确预测。
- 节奏行右侧预测文字使用限定区域和自适应字号，长文案不会越过节奏模块内框。
- 视觉效果使用 Windows DWM 与自绘半透明卡片，不依赖 macOS 的 `NSVisualEffectView`。
