# Windows v0.1.3

[Windows 所有版本](../../README.md) · [当前维护源码](../../源码/README.md)

保留的上一版本。内置并校验 Win-CodexBar CLI；支持简易 / 展开模式、近 7 天 token、模型偏好、重置历史时间轴，并完成 Windows 模块化重构。尚不包含 v0.1.4 的延伸至今天和距上次重置显示。

与后来补发的内置 CLI 版 [v0.1.2](../v0.1.2/README.md) 对比，两者核心程序相同，差别为版本标识和编译元数据；不应把两者描述为不同功能版本。

## 安装包

- [下载 Windows ZIP](https://github.com/xumanba/codex-meter/releases/download/v0.1.3/Codex-Meter-Windows-portable-v0.1.3.zip)
- [SHA-256 校验文件](https://github.com/xumanba/codex-meter/releases/download/v0.1.3/Codex-Meter-Windows-portable-v0.1.3.zip.sha256)
- [原 Release 页面](https://github.com/xumanba/codex-meter/releases/tag/v0.1.3)

SHA-256：

```text
bbf40d206000da0a5e1c84f88c236e215a8fd620c3157ae8c9bb0d58a8283592
```

完整解压后运行 `CodexMeter.exe`。支持 Windows 10/11；需要 .NET Framework 4.7.2 或更高版本及已有 Codex 登录，包内已包含 CLI，不需要另装 CodexBar。程序未进行 Authenticode 签名，请核实来源后再运行。升级前从托盘退出旧程序；可选安装脚本见 ZIP 内说明。

## 对应源码与说明

- [v0.1.3 标签中的 Windows 源码](https://github.com/xumanba/codex-meter/tree/v0.1.3/windows)
- [下载 v0.1.3 仓库源码 ZIP](https://github.com/xumanba/codex-meter/archive/refs/tags/v0.1.3.zip)：这是源码，不是安装包；Windows 工程在压缩包内的 `windows/`。
- [详细版本差异](../../../.github/docs/VERSION-GUIDE.md)

历史标签中的目录名不会随 main 的本次整理变化。本页只是下载索引，没有重打包或替换已发布文件。
