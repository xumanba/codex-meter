# macOS 开发源码

[Mac 下载入口](../README.md) · [仓库首页](../../README.md)

这里是原仓库根目录中的完整 macOS 工程，SwiftUI / AppKit，Swift 工具链版本要求见 [Package.swift](Package.swift)。运行目标为 macOS 14+。

## 在 Mac 上构建

在仓库根目录打开终端：

```sh
cd mac/源码
swift build -c release
./build-app.sh
```

`swift build` 编译可执行程序；`build-app.sh` 生成 `.build/CodexMeter.app`，包含图标和下载的 CodexBar CLI，并执行 ad-hoc 签名。脚本需要联网获取上游组件。

可选安装（会更新本机应用和登录启动配置，执行前阅读脚本）：

```sh
./install.sh
```

创建 Apple silicon / Intel 通用 ZIP 与 DMG：

```sh
./Scripts/package-release.sh
```

产物位于本目录的 `.build/release/`。脚本内版本号维持原值；目录调整没有生成或发布新的 Mac 安装包。需要正式更新版本时，请单独审核版本号、构建、签名与发布流程。

## 工程入口

- [Sources/AppDelegate.swift](Sources/AppDelegate.swift)：应用与窗口入口。
- [Sources/MeterView.swift](Sources/MeterView.swift)：卡片显示。
- [Sources/CodexBarClient.swift](Sources/CodexBarClient.swift)：额度数据。
- [Sources/TokenUsageScanner.swift](Sources/TokenUsageScanner.swift)：本机 token 统计。
- [Tests](Tests)：现有独立 Swift 检查程序，不是 SwiftPM test target。
- [Scripts](Scripts)：依赖获取与打包脚本。
- [NOTICE](NOTICE)、[ThirdPartyLicenses](ThirdPartyLicenses)、[LICENSE](LICENSE)：许可与归属。

各相对路径保持平台工程内部闭合；不依赖 Windows 源码。Windows 机器无法完成 AppKit 原生运行验证。
