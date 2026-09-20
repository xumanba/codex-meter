# Windows v0.1.2 · 历史补发

[Windows 所有版本](../../README.md) · [最新推荐 v0.1.4](../v0.1.4/README.md)

2026-09-20 补发的本地历史安装包，保留原始 ZIP，未将最新版重新编号成旧版。现在 Windows v0.1.0 至 v0.1.4 共 5 个版本均独立提供下载。

## 下载与安装

- [下载 Windows ZIP](https://github.com/xumanba/codex-meter/releases/download/v0.1.2/Codex-Meter-Windows-portable-v0.1.2.zip)
- [SHA-256 校验文件](https://github.com/xumanba/codex-meter/releases/download/v0.1.2/Codex-Meter-Windows-portable-v0.1.2.zip.sha256)
- [Release 与完整发布说明](https://github.com/xumanba/codex-meter/releases/tag/v0.1.2)

ZIP 为 6,058,636 字节，SHA-256：

```text
29D836DCE5577D110DB50F0666A5A3BD8CE24ADDF49B2586323CEE6532CE0409
```

支持 Windows 10/11、.NET Framework 4.7.2 或更高版本。需已有 Codex 登录；包内已包含 Win-CodexBar CLI 0.45.2，不必额外安装。完整解压后运行 `CodexMeter.exe`，可选安装脚本见包内说明。程序和 CLI 未进行 Authenticode 签名，请核对来源与校验文件。

## 功能与版本关系

包含周额度、网速、托盘、自启动、贴边、可折叠的近 7 天用量与模型偏好、重置历史。此内置 CLI 历史包与已发布 v0.1.3 的核心程序相同，差异为版本标识及编译元数据；不虚构功能增量。v0.1.4 才进一步增加时间轴延伸至今天、距上次重置已过多久、时间轴优先导航。

较早的 `pre-bundled` 中间包缺少 CLI，仍保留在本地，不作为第二份同版本安装包发布。

## 对应源码与核验边界

- [v0.1.2 Windows 源码](https://github.com/xumanba/codex-meter/tree/v0.1.2/windows)
- [下载标签源码 ZIP](https://github.com/xumanba/codex-meter/archive/refs/tags/v0.1.2.zip)：完整仓库源码，Windows 工程位于 `windows/`，不是安装包。
- [源码重建过程与复核脚本说明](https://github.com/xumanba/codex-meter/blob/v0.1.2/WINDOWS-v0.1.2-ARCHIVE.md)
- [该标签的 Windows CI](https://github.com/xumanba/codex-meter/actions/runs/35496627978)

这个标签是本次建立的**经验证重建源码快照**（提交 `2c155f15ac3be7c17bf9a399d1f4b1f65e0db4f4`），不是声称存在当年的原始 Git 提交。由 v0.1.3 保存源码恢复 Windows 版本标识后，编译得到的 EXE 仅排除 COFF 时间戳 4 字节及 MVID 16 字节，完整文件 SHA-256 与原 EXE 一致；其余 9 个随包文件重建后内容一致，部分文本仅有换行符差别。

已执行：确认原 EXE 版本 0.1.2.0、固定 CLI 哈希、10 文件包结构、源码重建与二进制对应性、195 项自测、打包检查、云端 Windows CI，并查看测试数据渲染预览。发布上传的是原始 ZIP，而非重建 ZIP。

发布后已通过公开下载链接重新下载 ZIP 及校验文件，文件大小与上述 SHA-256 一致；GitHub 的最新版本仍为 v0.1.4。另核对原有 4 个 Windows ZIP、2 个 Mac ZIP 和 1 个 Mac DMG 的 Release 资源摘要，均未改变。

没有进行新的账号在线同步、开机启动或全新 Windows 安装测试；没有替换本机正在运行的软件。其他 Windows 和 Mac 安装包及标签保持不变。
