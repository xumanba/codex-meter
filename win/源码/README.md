# Windows 开发源码

[Windows 下载入口](../README.md) · [仓库首页](../../README.md) · [完整使用与开发说明](README.zh-CN.md) · [架构说明](ARCHITECTURE.md)

原生 WinForms / C#，使用 .NET Framework 编译器，不需要 NuGet。此目录包含源码、资源、构建 / 安装脚本、固定版本的 CLI 及随包许可证。

## 构建和验证

在 Windows 的仓库根目录运行：

```powershell
.\win\源码\build.ps1
.\win\源码\dist\CodexMeter.Tests.exe
```

或进入此目录后运行 `.\build.ps1` 和 `.\dist\CodexMeter.Tests.exe`。

生成本地验证包（会再次构建、自测并校验包内容）：

```powershell
.\win\源码\package-release.ps1 -Version 0.1.4
```

编译产物位于 `dist/`，ZIP 与 SHA-256 文件生成在本目录。打包脚本拒绝覆盖已有同名包；本地重建包不代表覆盖 GitHub 上的历史 Release。

版本号仍为 v0.1.4；本次只是目录整理，没有改变 Windows 业务代码、用户设置或运行中的安装版本。
