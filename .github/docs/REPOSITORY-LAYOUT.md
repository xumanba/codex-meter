# 仓库目录维护说明

[首页](../../README.md) · [Windows](../../win/README.md) · [Mac](../../mac/README.md)

## 分类原则

- 每个平台只维护一份当前源码，分别放在 `win/源码`、`mac/源码`。
- 每个正式发布版本有一个 `版本/vX.Y.Z/README.md`，说明功能区别、运行条件、安装包、SHA-256 和 Git tag 源码位置。
- 历史补发必须说明源码来源，不将重建源码冒充原始提交。Windows v0.1.2 已完成核验并上传原始 ZIP；其新建标签对应经二进制比对验证的重建源码，详情见 [v0.1.2](../../win/版本/v0.1.2/README.md)。
- ZIP / DMG 存在 GitHub Releases；不在每个版本文件夹复制大包或整套历史源码。
- 历史 tag 和 Release 保持原样。标签里的旧目录名不会跟随 main 移动。
- `.github` 保存自动构建与共享文档；根目录保留 MIT LICENSE 和 Git 忽略规则。

## 旧路径与新路径

| 原路径 | 整理后路径 |
| --- | --- |
| `Sources/`、`Tests/`、`Scripts/`、`assets/` | `mac/源码/` 下的同名目录 |
| `Package.swift`、`Info.plist`、Mac `.sh` 脚本 | `mac/源码/` 下的同名文件 |
| `windows/` | `win/源码/` |
| 根目录详细中英文 README、CHANGELOG、VERSION-GUIDE | `.github/docs/` |
| `release/v0.1.0`、`release/v0.1.1`、`release/v0.1.4` | `win/版本/` 下的同名目录 |
| v0.1.0 双平台原始说明与校验记录 | 保留在 `win/版本/v0.1.0`，并复制到 `mac/版本/v0.1.0` |
| `NOTICE`、`ThirdPartyLicenses/` | 移入 Mac 源码，并为 Windows 源码保留相同许可副本 |
| 根目录 `LICENSE` | 原位保留，两端源码各有相同副本 |

根 README 改为精简导航，原详细内容保存在 `.github/docs`，并修正相对链接和命令中的路径。已发布的历史说明按发布时内容保留；跨版本的后续变化以各版本索引为准。

Windows 构建脚本改为从其自身目录取随包许可，不再依赖仓库根目录。Mac 工程内部相对位置不变；Mac 代码、资源和原有脚本不因分类改变内容。当前维护源码与某个旧安装包不是同一概念。

## 验证与构建

在仓库根目录使用 PowerShell 7 运行目录检查：

```powershell
./.github/scripts/verify-layout.ps1
```

检查必需文件、版本索引、Markdown / HTML 本地链接、PowerShell 语法、许可副本与根目录分类。Windows 构建 / 自测 / 打包命令见 [Windows 源码](../../win/源码/README.md)。

Windows CI 已使用新路径；额外的目录 / macOS CI 检查目录和现有 Swift 检查程序。CI 不发布安装包、不创建 tag、不修改 Release。Mac 原生构建仍须在 Mac 或 macOS CI 中实际通过，不能仅以 Windows 端目录检查代替。

本次整理的源基线为 `f381d16aca7518670a1c84d6574d8e14deb5f2bb`（Windows v0.1.4 合并提交）。发布源基线和整理前的 Git 历史都保留，未重写历史。实际执行结果和未验证范围见 [目录整理验证记录](LAYOUT-VERIFICATION.md)。

## 后续发布

1. 在相应平台源码中开发、测试，保持另一平台独立。
2. 审核并更新该平台版本号，完成打包与安装验证。
3. 经确认后创建 tag / Release，上传该平台安装包及校验文件。
4. 新增该平台版本目录的 README，更新平台入口和仓库首页。

不要为了整理目录重打同一个历史版本的包，也不要移动旧 tag。旧 `main/windows/...` 一类源码路径会变化；历史 tag 链接及 `/releases/download/...` 安装包链接不受本次目录调整影响。
