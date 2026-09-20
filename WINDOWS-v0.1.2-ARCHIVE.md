# Windows v0.1.2 历史补发与源码核验

此 Git 快照于 2026-09-20 为补发本地历史 Windows v0.1.2 安装包而建立，不是伪造或回填的原始开发提交。当前最新 Windows 版本仍是 v0.1.4；本标签只对应 Windows 历史归档，不更新任何 Mac 安装包。

## 安装包

- 原始文件：`Codex-Meter-Windows-portable-v0.1.2.zip`，6,058,636 字节。
- ZIP SHA-256：`29D836DCE5577D110DB50F0666A5A3BD8CE24ADDF49B2586323CEE6532CE0409`。
- 原 EXE 版本：`0.1.2.0`。
- 原 EXE SHA-256：`6A2AFBE3BA2257392CE6600D8924006557D378F970BDE65AE4F3BFF7FB6380A0`。
- 内置 CLI SHA-256：`C0B737E1B36E0D90524AA6FAB169D718EBB9E54F00656695E340522D284ADFAD`。

此次上传原 ZIP，不用新编译的 ZIP 覆盖它，也不同时发布缺少 CLI 的 pre-bundled 中间包。

## 源码如何确认

1. 以已保存的 v0.1.3 源码提交 `a1a3058a03de796695f579f8b77c19953d664c28` 为基线，仅恢复 Windows assembly / manifest 版本标识、打包默认版本和有关文档 / CI 版本号。
2. 未改变 Windows 业务 C# 代码。Mac 源文件、资源和构建 / 安装脚本保持该基线原样。
3. 执行 `windows/build.ps1` 和 `windows/dist/CodexMeter.Tests.exe`，195 项自测通过。
4. 将重建 EXE 和历史 EXE 比较，仅将 COFF 编译时间戳的 4 字节、MVID 的 16 字节归零后，完整 524,800 字节文件的 SHA-256 一致：

```text
70DF1F9EB3975627994634BF9339FEC6D78A570B30B845AF981DC6E9BF4A3D41
```

因此这里是可核验对应历史二进制的重建源码，而不是声称找到了原始 v0.1.2 提交。完整原始 EXE 哈希与重新编译 EXE 哈希不同是正常的，不得混用。

## 复核

从此标签的仓库根目录在 Windows 上运行：

```powershell
.\windows\build.ps1
.\windows\dist\CodexMeter.Tests.exe
```

使用 PowerShell 7 对比下载包中的原 EXE 与重建 EXE：

```powershell
.\windows\tools\Test-HistoricalExecutable.ps1 -HistoricalExe '路径\CodexMeter.exe' -RebuiltExe '.\windows\dist\CodexMeter.exe'
```

检查程序只解析文件，不启动目标 EXE。若除这两个非确定字段外任何字节不同，验证失败。

## 版本差别与限制

- 本地保留的内置 CLI 版 v0.1.2 与已发布 v0.1.3 的运行程序差异限于版本标识和编译元数据；不虚构两者之间的功能增量。随包安装 / 卸载脚本、配置和 CLI 相同，说明 / NOTICE 仅有版本号差异。
- v0.1.4 在此基础上增加时间轴延伸至今天、距上次重置已过多久以及时间轴优先导航。
- 本次为离线自测和二进制完整性 / 对应性验证，没有替换本机常驻程序，也没有进行新的账号在线同步、开机启动或全新 Windows 安装测试。
- 仓库继承的其他文档可能描述基线版本 v0.1.3；本归档及随包 README 才是此 Windows 历史包的版本说明。Mac 历史内容不属于此次补发范围。
