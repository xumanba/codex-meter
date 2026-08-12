<div align="center">

# ✦ CodexMeter

### 原生 macOS Codex 用量可视化、额度监控与边缘吸附浮窗

[English](README.en.md) · 简体中文

[![macOS 14+](https://img.shields.io/badge/macOS-14%2B-111827?style=flat-square&logo=apple&logoColor=white)](https://www.apple.com/macos/)
[![Swift 6](https://img.shields.io/badge/Swift-6-F05138?style=flat-square&logo=swift&logoColor=white)](https://www.swift.org/)
[![CodexBar](https://img.shields.io/badge/Powered%20by-CodexBar-0A84FF?style=flat-square)](https://github.com/steipete/CodexBar)
[![License: MIT](https://img.shields.io/badge/License-MIT-34C759?style=flat-square)](LICENSE)
[![Download v0.2.0](https://img.shields.io/badge/Download-v0.2.0-0A84FF?style=flat-square&logo=github)](https://github.com/xumanba/codex-meter/releases/tag/v0.2.0)

在不离开工作区的情况下，查看 Codex 周额度剩余、当前额度周期内的 token 使用、近 7 天每日使用情况和模型偏好。

<p>
  <img src="assets/CodexMeter-icon.png" alt="CodexMeter 应用图标" width="128">
</p>

</div>

> [!IMPORTANT]
> **一个安装包即可使用，不需要单独安装 CodexBar。** 通用安装包内置了已经验证过的 CodexBar CLI，同时支持 Apple 芯片和 Intel Mac。应用使用你本机已有的 Codex 登录状态，不会打包账号凭证。

> [!NOTE]
> **v0.2.0 仅支持 macOS。** 需要 macOS 14 Sonoma 或更高版本；Windows 暂不支持此版本。

## 当前浮窗界面

| 深色界面 | 浅色界面 |
|:---:|:---:|
| <img src="assets/codexmeter-dark.png" alt="CodexMeter 深色界面" width="344"> | <img src="assets/codexmeter-light.png" alt="CodexMeter 浅色界面" width="344"> |

## 功能概览

- **每周额度**：显示剩余百分比、重置时间、当前额度周期 token 总量和颜色对应的进度条。
- **整数精度**：每周额度剩余百分比按 Codex 可提供的整数精度显示，例如 `90%`，不伪造小数位。
- **近 7 天每日用量**：按当前周额度周期展示每日 token 数量和当天占周额度的百分比。
- **模型偏好**：模型、思考强度和 Fast 模式分别统计，每一行优先显示百分比，再显示 token 数量。
- **动态高度**：使用过的新模型会自动增加一行；内容超过屏幕可用高度后，浮窗内部滚动。
- **边缘吸附**：拖到屏幕左侧或右侧后收起为窄条，鼠标靠近边缘即可展开。
- **双主题**：支持深色玻璃和浅色玻璃，记住上次选择。
- **窗口跟随**：可固定在桌面，也可以跟随 Codex 窗口显示。
- **本地优先**：通过本机 `127.0.0.1` helper 获取额度，不上传本地会话记录。

## v0.2.0 更新内容

- 重做浮窗信息层级，重点突出每周额度剩余，而不是已使用比例。
- 每周额度剩余百分比改为整数显示；Codex 没有提供可靠的小数额度，因此不进行估算。
- 增加当前额度周期内的近 7 天每日 token 使用量和百分比。
- 刷新额度后，刷新前的记录不混入当前额度周期；当天中途刷新时只统计刷新之后的 token。
- 增加模型、思考强度和 Fast 模式的独立偏好统计。
- 模型行根据实际使用数量动态增高，超过屏幕高度后才启用内部滚动。
- 优化圆角玻璃裁剪，修复浮窗外侧残留矩形阴影和半透明角落。
- 应用名称统一为 `CodexMeter`，并加入新的 macOS 应用图标。
- 更新 README 展示图、中文主文档和英文版互链。

后续每次功能、统计逻辑或界面修改，都会同步补充本节和对应的界面说明，保持 README 与实际版本一致。

## 安装

### 直接下载安装包

1. 从 [GitHub Releases](https://github.com/xumanba/codex-meter/releases/tag/v0.2.0) 下载 `CodexMeter-macos-universal-0.2.0.zip`。
2. 解压后将 **CodexMeter.app** 拖到“应用程序”。
3. 由于当前版本是 ad-hoc 签名、没有 Apple notarization，第一次打开时请右键应用并选择“打开”。如果仍被拦截，请到“系统设置 → 隐私与安全性”中选择“仍要打开”。
4. 打开 CodexMeter；本机 Codex 登录有效时，浮窗会自动出现。

不需要单独安装 CodexBar，也不需要管理员密码。

### 从源码安装

```bash
chmod +x install.sh
./install.sh
```

安装脚本会构建应用、安装到 `/Applications/CodexMeter.app`、配置当前用户的 LaunchAgent 并启动浮窗。旧版带空格的 `Codex Meter.app` 如存在，会被移动到废纸篓以避免两个实例互相抢占。

## 手动构建

```bash
chmod +x build-app.sh
./build-app.sh
open ".build/CodexMeter.app"
```

构建通用 macOS 包：

```bash
./Scripts/package-release.sh
```

输出文件位于：

- `.build/release/CodexMeter-macos-universal-0.2.0.zip`
- `.build/release/CodexMeter-macos-universal-0.2.0.zip.sha256`

构建脚本会验证 arm64 和 x86_64 架构、深度签名、内置 CodexBar 许可证和应用图标资源。

## 数据与隐私

CodexMeter 启动内置的 `codexbar serve` helper，并通过 `127.0.0.1:18747` 获取 Codex 额度。token 统计扫描本机 Codex session 记录，只在本机按当前额度周期、日期、模型、思考强度和 Fast 模式聚合，不会上传这些记录。

应用不会复制、显示或打包密码、OAuth token、Cookie 或账号邮箱。浮窗不会把账号凭证写入仓库或安装包。

## 卸载

如果是手动拖入“应用程序”的安装包，请退出 CodexMeter 后将 `/Applications/CodexMeter.app` 移到废纸篓。

如果使用过安装脚本：

```bash
./uninstall.sh
```

卸载脚本会停止 helper、移除 LaunchAgent，并将应用移动到废纸篓；历史统计数据不会被主动清除。

## 第三方组件与许可证

CodexMeter 是基于 CodexBar 的独立、非官方 macOS 应用。内置 CodexBar CLI 按 MIT License 发布，完整许可文件位于 `ThirdPartyLicenses/` 和应用包内的 `Contents/Resources/ThirdPartyLicenses/`。

本项目使用 [MIT License](LICENSE)。本项目与 CodexBar、OpenAI 或 Apple 没有隶属或官方背书关系。
