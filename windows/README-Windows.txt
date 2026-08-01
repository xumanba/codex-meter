Codex Meter for Windows v0.1.1
================================

中文安装说明
------------

1. 系统要求：Windows 10 或 Windows 11，.NET Framework 4.7.2 或更高版本。
2. 请先安装并登录 Win-CodexBar：
   https://github.com/Finesssee/Win-CodexBar
3. 解压整个 ZIP，保持本文件夹内的文件在一起。
4. 双击 CodexMeter.exe。
5. 如果 Windows 显示“未知发布者”，请确认文件来自本项目 Release 后，
   选择“更多信息”→“仍要运行”。
6. 若窗口被隐藏，请双击系统托盘中的 Codex Meter 图标恢复。
7. 网速是整台电脑所有活动网卡的总流量，并非 Codex 专属流量。
8. 每周额度和 Spark 额度统一显示“xd xh 后重置”，悬停可查看准确
   的本机日期和时间。
9. 鼠标悬停在“实时”状态上，可通过与额度进度条同色系的圆角提示
   查看最近的数据更新时间。

可选安装（当前用户）：在本目录运行 `powershell -ExecutionPolicy Bypass
-File .\install.ps1 -Launch`。仅在需要开机启动时再加
`-StartWithWindows`。卸载前从托盘退出程序，再运行 `uninstall.ps1`；
卸载脚本会逐项请求确认，并默认保留界面设置。

Windows 独有功能：实时上传/下载网速，以及最小化到系统托盘。
本程序不抓取网络数据包，不保存账号密码、令牌或 Cookie。

English installation
--------------------

1. Requires Windows 10/11 and .NET Framework 4.7.2 or newer.
2. Install and sign in to Win-CodexBar first:
   https://github.com/Finesssee/Win-CodexBar
3. Extract the entire ZIP and keep all files in this folder together.
4. Run CodexMeter.exe.
5. The binary is not Authenticode-signed. Windows may display an
   unknown-publisher warning; continue only after verifying the download.
6. If the card is hidden, double-click its notification-area icon to restore it.
7. Network speed is aggregate system traffic, not Codex-only traffic.
8. Weekly and Spark allowances use the same "xd xh until reset" format. Hover
   either countdown to see the exact local date and time.
9. Hover the status pill to see the latest data-update time in a rounded prompt
   that uses the same color palette as the allowance progress bar.

Optional per-user install: run `powershell -ExecutionPolicy Bypass -File
.\install.ps1 -Launch`. Add `-StartWithWindows` only to opt in. Exit the app
from the tray before running `uninstall.ps1`; it asks for confirmation and
keeps interface settings by default.

Windows-only features: live aggregate upload/download speed and notification-
area minimize/restore. Codex Meter does not capture packets or store passwords,
tokens, cookies, or account credentials.

Project: https://github.com/xumanba/codex-meter
License: MIT
