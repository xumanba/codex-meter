<div align="center">

# ✦ CodexMeter

### 原生 Windows / macOS Codex 用量可视化与额度监控浮窗

[English](README.en.md) · 简体中文

[![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=flat-square&logo=windows11&logoColor=white)](windows/README.zh-CN.md)
[![macOS 14+](https://img.shields.io/badge/macOS-14%2B-111827?style=flat-square&logo=apple&logoColor=white)](https://www.apple.com/macos/)
[![Swift 6](https://img.shields.io/badge/Swift-6-F05138?style=flat-square&logo=swift&logoColor=white)](https://www.swift.org/)
[![CodexBar](https://img.shields.io/badge/Powered%20by-CodexBar-0A84FF?style=flat-square)](https://github.com/steipete/CodexBar)
[![License: MIT](https://img.shields.io/badge/License-MIT-34C759?style=flat-square)](LICENSE)
[![下载 v0.2.0](https://img.shields.io/badge/Download-v0.2.0-0A84FF?style=flat-square&logo=github)](https://github.com/xumanba/codex-meter/releases/tag/v0.2.0)

优先面向中文用户，帮助你在工作区内查看 Codex 额度、token 使用和模型使用偏好。

<p>
  <img src="assets/CodexMeter-icon.png" alt="CodexMeter 应用图标" width="128">
</p>

</div>

> [!IMPORTANT]
> **Windows 和 macOS 都有对应版本。** Windows 当前是 v0.1.1，使用原生 WinForms 客户端和 Win-CodexBar CLI；macOS 当前是 v0.2.0，使用 SwiftUI/AppKit 客户端并内置已经验证的 CodexBar CLI。两端都不会保存账号凭证。

> [!NOTE]
> **统一主线。** `main` 现在同时包含 Windows v0.1.1 和 macOS v0.2.0 的完整源码；macOS 安装包仍可从 [v0.2.0 Release](https://github.com/xumanba/codex-meter/releases/tag/v0.2.0) 下载。

## 当前浮窗界面

| 深色界面 | 浅色界面 |
|:---:|:---:|
| <img src="assets/codexmeter-dark.png" alt="CodexMeter 深色界面" width="344"> | <img src="assets/codexmeter-light.png" alt="CodexMeter 浅色界面" width="344"> |

## 平台与版本

| 平台 | 原生界面 | 当前版本 / 安装包 | 数据来源 |
|---|---|---|---|
| Windows 10/11 | WinForms + DWM，Per-Monitor V2 DPI | [`Codex-Meter-Windows-portable-v0.1.1.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.1.1/Codex-Meter-Windows-portable-v0.1.1.zip) | [Win-CodexBar](https://github.com/Finesssee/Win-CodexBar) CLI |
| macOS 14+ | SwiftUI + AppKit | [`CodexMeter-macos-universal-0.2.0.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.2.0/CodexMeter-macos-universal-0.2.0.zip) | 内置 CodexBar CLI |

Windows 的构建、安装和故障排查请看 [`windows/README.zh-CN.md`](windows/README.zh-CN.md)。不同平台的版本差异请看 [`VERSION-GUIDE.md`](VERSION-GUIDE.md)。

## 功能概览

### Windows 主线开发版（正式下载仍为 v0.1.1）

- 聚焦显示每周主额度、重置时间和使用节奏，不再绘制单独的 Spark 整栏。
- 点击重置倒计时可查看最近 3 次重置、平均间隔，并展开到最近 10 次；历史日志推算与程序直接检测会明确区分。
- 默认使用简易卡片；点击节奏行展开近 7 天 token 与模型/推理强度偏好。
- 模型偏好颜色按使用占比从低饱和蓝灰连续增强为高饱和蓝色。
- 支持左右边缘吸附、悬停展开、托盘模式、开机自启动和多显示器 DPI。
- 支持点击“实时”立即同步、失败退避和系统总上传/下载速度提示。
- 使用本机 Win-CodexBar CLI，不把账号凭证写入应用。
- 本机 token 与重置历史缓存不保存提示词、回复或凭据；缺少模型元数据时明确标为“未标注模型”。

### macOS v0.2.0

- 每周额度优先显示剩余百分比、token 总量、重置时间和颜色进度条。
- 每周额度剩余百分比按 Codex 提供的整数精度显示，例如 `90%`，不估算小数。
- 每周额度进度条保留使用节奏提示：标出按时间应使用的位置，并显示已用/应使用比例及节奏正常、偏快或偏慢。
- 节奏信息行与进度条保留更舒适的间距，并在进度条下方区域对齐显示。
- 显示当前额度周期内近 7 天每天的 token 使用量和占周额度百分比。
- 近 7 天百分比按 Codex 官方周额度累计使用比例的日差值计算，token 数量只作为数量展示，不参与额度换算。
- 模型、思考强度和 Fast 模式分别统计，每行优先显示该组合的周额度估算占比（保留两位小数），再显示 token 数量。
- Fast 模式按 2.5 倍额度权重估算；模型目录中的 1.5 倍描述表示速度提升，不是额度倍率。
- 模型统计只纳入当前周额度窗口的会话；旧额度窗口的 token 不会留下一个 `0%` 的模型行。
- 思考强度按界面规范显示为 `None`、`Low`、`Medium`、`High`、`xHigh`、`Max` 等等级。
- 检测到额度刷新后，模型偏好自动清零，从刷新时刻重新统计，不会跨额度周期累积。
- 使用过的新模型自动增加一行，超过屏幕可用高度后浮窗内部滚动。
- 支持深色/浅色玻璃、固定桌面、跟随 Codex 和左右边缘吸附。
- 应用名称为 `CodexMeter`，浮窗顶部和安装包均使用 CodexMeter 应用图标。

## v0.2.0 macOS 更新记录

- 重做浮窗信息层级，重点突出每周额度剩余。
- 增加当前额度周期的近 7 天每日 token 统计。
- 修正每日额度百分比：改用官方累计 `usedPercent` 的日差值，避免不同模型计费差异造成虚高。
- 修正模型偏好额度占比：改用输入、缓存输入、缓存写入和输出 token 的模型价格权重，并按官方整体周额度已用百分比归一化，不再把一次整数额度快照直接归因给某个模型。
- Auto Review 暂按 Luna 价格权重估算；由于 Codex 未公开逐模型额度价格，模型区域标题标注为“额度估算”。
- 修正 Fast 模式额度倍率：使用 2.5 倍额度权重，不再将 1.5 倍速度提升误当作额度倍率。
- 修复跨额度窗口混入：额度刷新后，旧窗口的模型 token 不再混入当前周统计并显示为 `0%`。
- 将周额度进度条的节奏参考线改为橙色，提高与绿色剩余额度条的对比度。
- 恢复每周额度的使用节奏状态、按时间应使用标记和耗尽预测。
- 刷新额度后，刷新前的记录不混入当前额度周期；当天中途刷新时只统计刷新之后的 token。
- 增加模型、思考强度和 Fast 模式的独立偏好统计。
- 模型偏好绑定当前额度周期；检测到重置时间变化或已用比例回落后自动清零重算。
- 模型行按实际数量动态增高，超过屏幕高度后启用内部滚动。
- 优化圆角玻璃裁剪，修复浮窗外侧残留矩形阴影和半透明角落。
- 应用名称统一为 `CodexMeter`，并加入新图标和新的深色/浅色界面展示图。

后续每次功能、统计逻辑或界面修改，都应同步更新本 README、[`README.en.md`](README.en.md) 和对应的截图/资源，保持文档与实际版本一致。

## 安装

### Windows v0.1.1

1. 安装并登录 [Win-CodexBar](https://github.com/Finesssee/Win-CodexBar)。
2. 下载 [`Codex-Meter-Windows-portable-v0.1.1.zip`](https://github.com/xumanba/codex-meter/releases/download/v0.1.1/Codex-Meter-Windows-portable-v0.1.1.zip)。
3. 解压后打开 `Codex Meter Windows v0.1.1`，运行 `CodexMeter.exe`。
4. Windows 可能提示未知发布者，因为社区版本没有 Authenticode 签名；如有需要请审查源码或自行构建。

### macOS v0.2.0

1. 从 [v0.2.0 Release](https://github.com/xumanba/codex-meter/releases/tag/v0.2.0) 下载 `CodexMeter-macos-universal-0.2.0.zip`。
2. 解压后将 **CodexMeter.app** 拖到“应用程序”。
3. 当前版本是 ad-hoc 签名且没有 Apple notarization，第一次打开时请右键应用并选择“打开”；如果仍被拦截，请到“系统设置 → 隐私与安全性”中选择“仍要打开”。
4. 打开 CodexMeter；本机 Codex 登录有效时，浮窗会自动出现。

macOS v0.2.0 安装包同时支持 Apple 芯片和 Intel Mac，不需要单独安装 CodexBar，也不需要管理员密码。

## 从源码构建

### Windows

```powershell
.\windows\build.ps1
.\windows\dist\CodexMeter.Tests.exe
.\windows\package-release.ps1 -Version 0.1.1
```

### macOS v0.2.0

```bash
git clone https://github.com/xumanba/codex-meter.git
cd codex-meter
chmod +x install.sh uninstall.sh build-app.sh
./install.sh
```

构建通用 macOS 安装包：

```bash
./Scripts/package-release.sh
```

输出文件为 `.build/release/CodexMeter-macos-universal-0.2.0.zip` 及对应的 `.sha256` 校验文件。构建脚本会检查 arm64/x86_64 架构、深度签名、CodexBar 许可证和应用图标。

## 边缘吸附

将浮窗拖到屏幕左侧或右侧并释放，浮窗会收起为窄条；鼠标靠近边缘时展开，移开后自动收回。

```text
拖到边缘 → 收起为窄条 → 鼠标靠近 → 展开
移开鼠标 → 等待约 0.18 秒 → 平滑收回
```

macOS 使用 AppKit 鼠标追踪，Windows 使用 Per-Monitor V2 DPI 感知的边缘几何，不需要额外的 Accessibility 权限。

## 数据与隐私

macOS v0.2.0 通过本机 `127.0.0.1:18747` helper 获取额度；token 统计只读取本机 Codex session 记录，并按当前额度周期、日期、模型、思考强度和 Fast 模式在本机聚合，不会上传这些记录。Windows 每次额度刷新直接调用本机 `codexbar-cli.exe`，不开放监听端口；近 7 天与重置历史统计只读取本机 Codex rollout 的时间、模型、推理强度、token 汇总和结构化额度窗口字段，并把不含对话正文的增量缓存保存在 `%LOCALAPPDATA%\CodexMeter`。

CodexBar 读取你本机 Codex 配置中的 OAuth 会话并请求该账号的额度数据。CodexMeter 不复制、显示或打包密码、OAuth token、Cookie 或账号邮箱。

## 第三方组件与许可证

CodexMeter 是基于 [steipete/CodexBar](https://github.com/steipete/CodexBar) 的独立、非官方应用。macOS 发布包内置 CodexBar CLI v0.45.2，完整许可证位于 `ThirdPartyLicenses/` 和应用包内。

本项目使用 [MIT License](LICENSE)，与 CodexBar、OpenAI 或 Apple 没有隶属或官方背书关系。

---

<div align="center">
为希望随时看见 Codex 剩余额度的人而做。
</div>
