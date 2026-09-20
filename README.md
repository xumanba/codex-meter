# CodexMeter

Codex 用量悬浮卡片，分别提供 **Windows** 与 **macOS** 版本。两端独立安装、独立编号，请按系统选择；不要把源码 ZIP 当作安装包。

| 系统 | 当前发布版本 | 下载与说明 | 开发源码 |
| --- | --- | --- | --- |
| Windows 10/11 | v0.1.4 | [Windows 版本入口](win/README.md) | [Windows 源码](win/源码/README.md) |
| macOS 14+（Apple silicon / Intel） | v0.2.0 | [Mac 版本入口](mac/README.md) | [Mac 源码](mac/源码/README.md) |

## 文件怎么找

```text
README.md
mac/
  README.md                 Mac 下载入口
  源码/                     当前维护的完整 macOS 工程
  版本/                     一个已发布版本一个文件夹
    v0.2.0/README.md         下载、校验、说明、对应源码标签
    v0.1.0/README.md
win/
  README.md                 Windows 下载入口
  源码/                     当前维护的完整 Windows 工程
  版本/
    v0.1.4/README.md
    v0.1.3/README.md
    v0.1.1/README.md
    v0.1.0/README.md
```

- **只想安装**：进入对应平台 → 版本 → 点击安装包下载链接。真正的 ZIP / DMG 保存在 [GitHub Releases](https://github.com/xumanba/codex-meter/releases)，不会在源码仓库中再复制一份。
- **想开发**：进入对应平台的“源码”。各历史版本页还提供对应 Git tag 的源码快照链接；不要用当前源码冒充历史版本。
- 版本目录只列已正式发布的版本。Windows 的本地开发版本号不一定对应公开 Release；Windows 与 Mac 的数字也不表示功能完全一致。
- `.github/` 保留自动构建和共用说明，`LICENSE`、`.gitignore` 是仓库必要文件，因此根目录不严格限制为三个条目。

## 安装注意

Windows v0.1.4 内置经过校验的 Win-CodexBar CLI，无需另外安装 CodexBar；需已有 Codex 登录。完整解压后运行 `CodexMeter.exe`，不要只复制一个 EXE。旧版 v0.1.0 / v0.1.1 的依赖不同，见各版本页。

macOS 使用其独立 ZIP / DMG。本次目录整理不更新 macOS 程序，也不替换任一历史 Release 安装包。当前维护源码可能包含发布标签之后的修复，安装包以对应 Release 为准。

程序包未提供商业代码签名 / Apple 公证；请核对来源和 SHA-256，不要忽略系统安全提示。

[完整中文介绍](.github/docs/README.zh-CN.md) · [English documentation](.github/docs/README.en.md) · [版本差异](.github/docs/VERSION-GUIDE.md) · [更新记录](.github/docs/CHANGELOG.md) · [目录维护说明](.github/docs/REPOSITORY-LAYOUT.md) · [MIT License](LICENSE)
