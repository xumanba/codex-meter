CodexMeter for Windows v0.1.3
==================================================

中文安装说明
------------

1. 系统要求：Windows 10 或 Windows 11，.NET Framework 4.7.2 或更高版本。
2. 请先登录 Codex 桌面客户端，或运行 codex login。
3. 完整解压整个 ZIP，保持 CodexMeter.exe、codexbar-cli.exe 和许可证文件
   位于同一个文件夹；不需要另外安装 Win-CodexBar。
4. 双击 CodexMeter.exe。
5. CodexMeter 和内置 CLI 均未进行 Authenticode 签名。如果 Windows 显示
   “未知发布者”，请先核对 Release SHA-256，再选择“更多信息”→“仍要运行”。
6. 若窗口被隐藏，请双击系统托盘中的 CodexMeter 图标恢复。
7. 网速是整台电脑所有活动网卡的总流量，并非 Codex 专属流量。
8. 聚焦显示每周额度和“xd xh 后重置”；悬停可查看当前预计的本机日期
   和时间，点击可查看最近 3 次重置与平均间隔，并展开到最近 10 次。
   本机日志回推记录会明确标为“推算”；Spark 不再作为单独整栏显示。
9. 鼠标悬停在“实时”状态上，可通过与额度进度条同色系的圆角提示
   查看最近的数据更新时间。
10. 在“•••”菜单勾选“开机自启动”，可让 CodexMeter 在当前用户登录
    Windows 后自动启动；若卡片已贴边隐藏，登录时会先展开 30 秒，再恢复
    正常贴边隐藏；再次点击即可关闭。
11. 同步期间按钮名称仍保持“实时”，蓝色状态点表示查询中；重复点击会
    合并到当前查询，避免连续执行两次慢查询。
12. 默认始终置顶；勾选“取消始终置顶”后，其他程序可以覆盖卡片。
    Codex/ChatGPT 位于同一显示器并处于前台时仍会临时抬高卡片，离开后
    恢复普通层级。
13. 新增本机近 7 天 token、每日额度折算和模型/推理强度偏好。增量缓存
    不保存提示词、回复或凭据；缺少模型字段时显示“未标注模型”。
14. 默认采用简易模式；点击“节奏正常/超额”整行可展开或收起近 7 天和
    模型偏好。展开状态不跨启动保存。EXE、窗口和托盘使用中等深度
    蓝灰冰晶图标。
15. 模型偏好颜色按使用占比连续变化：低用量为低饱和蓝灰，使用越多
    越接近醒目的高饱和蓝色；颜色不再代表具体模型类别。

可选安装（当前用户）：在本目录运行 `powershell -ExecutionPolicy Bypass
-File .\install.ps1 -Launch`。仅在需要开机启动时再加
`-StartWithWindows`；它与菜单中的“开机自启动”使用同一个当前用户启动项。
升级已安装版本前，请先从托盘退出程序，再运行新版
`install.ps1`；脚本若检测到安装目录中的程序仍在运行，会停止安装并提示。
卸载前也应从托盘退出程序，再运行 `uninstall.ps1`；
卸载脚本会逐项请求确认，并默认保留界面设置。

Windows 独有功能：实时上传/下载网速，以及最小化到系统托盘。
本程序不抓取网络数据包，不保存账号密码、令牌或 Cookie。

English installation
--------------------

1. Requires Windows 10/11 and .NET Framework 4.7.2 or newer.
2. Sign in to the Codex desktop app first, or run `codex login`.
3. Fully extract the ZIP and keep CodexMeter.exe, codexbar-cli.exe and the
   license files together. No separate Win-CodexBar installation is required.
4. Run CodexMeter.exe.
5. CodexMeter and the bundled CLI are not Authenticode-signed. Windows may
   display an unknown-publisher warning; continue only after verifying the
   Release SHA-256.
6. If the card is hidden, double-click its notification-area icon to restore it.
7. Network speed is aggregate system traffic, not Codex-only traffic.
8. The focused Windows card shows the weekly allowance and its "xd xh until
   reset" text. Click it for the latest three reset records and average interval,
   with an expandable list of up to ten. Log-derived records are marked as
   estimates. Spark is no longer rendered as a separate row.
9. Hover the status pill to see the latest data-update time in a rounded prompt
   that uses the same color palette as the allowance progress bar.
10. Toggle "开机自启动" in the `•••` menu to start CodexMeter after the
    current user signs in to Windows. A docked card is revealed for 30 seconds
    at login before normal edge hiding resumes; toggle it again to disable startup.
11. The status label remains live while its blue dot indicates an active query.
    Repeated clicks are coalesced instead of starting back-to-back CLI queries.
12. The card is always on top by default. Check “取消始终置顶” to use ordinary
    window ordering so other applications can cover it. Foreground Codex/ChatGPT
    on the same display still raises the card temporarily, then ordinary ordering
    resumes when focus moves elsewhere.
13. The card adds local seven-day tokens, daily quota shares and model/reasoning-
    effort preferences. Its incremental cache stores no prompts, responses or credentials;
    missing model metadata is shown explicitly instead of guessed.
14. The card starts compact. Click the entire pace row to expand or collapse the
    seven-day and model details. Expansion is not persisted across launches, and the
    executable, window and tray use the balanced medium blue-gray ice-glass icon.
15. Model-preference color follows usage share continuously, from muted blue-gray
    at low usage to vivid blue at high usage; color no longer identifies a model type.

Optional per-user install: run `powershell -ExecutionPolicy Bypass -File
.\install.ps1 -Launch`. Add `-StartWithWindows` only to opt in; it uses the same
per-user startup entry as the menu toggle. Before upgrading
an installed copy, exit the app from the tray and then run the new
`install.ps1`; the script stops with a clear message if that installed copy is
still running. Exit the app before running `uninstall.ps1`; it asks for
confirmation and keeps interface settings by default.

Windows-only features: live aggregate upload/download speed and notification-
area minimize/restore. CodexMeter does not capture packets or store passwords,
tokens, cookies, or account credentials.

Project: https://github.com/xumanba/codex-meter
License: MIT
