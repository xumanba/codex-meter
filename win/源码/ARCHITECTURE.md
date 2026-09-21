# CodexMeter Windows 架构

本文描述 Windows 客户端的当前模块边界。macOS 使用独立的 SwiftUI/AppKit 实现，不由本目录的构建脚本编译。

## 数据流

```text
codexbar-cli.exe ──> CodexBarClient ───────────────┐
                                                   │
本机 .codex/*.jsonl ─> CodexRolloutFiles           ├─> CodexMeterFormV2 ─> 悬浮卡片/托盘
                         └─> RolloutJsonFields      │
                              ├─> WeeklyUsageReader ┤
                              └─> ResetHistoryStore ┘

NetworkSpeedMonitor ────────────────────────────────> 系统总上传/下载速度
SettingsStore / AtomicFileStore ───────────────────> 设置、缓存与备份
RefreshCoordinator ────────────────────────────────> 同步排队、失败计数与退避
BackgroundRefreshPolicy ───────────────────────────> 重置历史低频增量补扫
DashboardState ────────────────────────────────────> 额度、历史、周用量与网速的统一状态
WindowBehaviorPolicy / WindowBehaviorController ─> 窗口判定、屏幕、置顶与启动项
DockVisibilityState ──────────────────────────────> 贴边显示、抑制窗口与动画状态
DashboardStatusPolicy ────────────────────────────> 实时、同步、过期与离线状态
DashboardPresentation / ResetHistoryPresentation ─> 纯布局、文案、颜色与时间轴计算
DashboardInteractionPolicy / UiDrawing ───────────> 命中优先级与共享绘图基础设施
DashboardRenderer ─────────────────────────────────> 主卡片全部可见内容与命中区域
ResetHistoryInteractionState ─────────────────────> 历史列表、时间轴与滑块导航状态
```

## 模块职责

- `CodexBarClient`：定位并调用 Win-CodexBar CLI，只负责额度快照，不保存账号凭证。
- `RefreshCoordinator`：保证额度查询单路执行；同步期间的手动点击只排队一次；维护失败退避状态。
- `QuotaRefreshResult`：把后台额度查询、历史观察和完成时间作为一次不可变结果交回界面线程。
- `BackgroundRefreshPolicy`：控制重置历史补扫的最小间隔并阻止重叠任务；程序运行期间默认每 30 分钟检查一次。
- `DashboardState`：统一保存额度、周用量、重置历史、网速、连接状态和最后成功时间；一次额度成功作为整体应用，失败只改变连接状态并保留最后一次有效数据。
- `CodexRolloutFiles`：统一发现 sessions 与 archived_sessions 中的最新同名 rollout。
- `RolloutJsonFields`：统一提取 JSONL 中经过选择的结构化字段，避免两套解析逻辑漂移。
- `WeeklyUsageReader`：按日期、模型、协作模式和推理强度聚合最近七天 token。
- `ResetHistoryStore`：识别重置事件、记录来源和时间可信度、计算可靠间隔统计。
- `AtomicFileStore`：通过临时文件和原子替换保存缓存，并保留上一代 `.bak`。
- `AppDiagnostics`：写入脱敏、限长、单代轮换的本地诊断日志。
- `WindowBehaviorPolicy`：集中判定启动唤出、贴边轮询、置顶、菜单反向语义、网速采样、屏幕范围限制、贴边目标和动画步进；纯策略覆盖负坐标副屏测试。
- `WindowBehaviorController`：封装窗口位置恢复与保存、多显示器解析、同屏 Codex/ChatGPT 检测、有效置顶状态及当前用户启动项读写；窗体不再直接散布这些原生边界。
- `DockVisibilityState`：保存贴边展开、动画、触边抑制、弹窗期间禁止隐藏和鼠标离开宽限状态；时间推进逻辑可脱离窗口独立测试。
- `DashboardStatusPolicy`：集中判定同步中、实时、过期和离线状态，并统一界面标签。
- `DashboardPresentation`：集中主卡片尺寸、展开高度、节奏文案区域、每日额度折算、模型行合并、模型标签与用量颜色梯度。
- `ResetHistoryPresentation`：集中间隔和预测文案、可信度颜色、来源标签、每日时间轴刻度及时间点坐标换算。
- `DashboardInteractionPolicy`：集中同步、菜单、重置历史、详情开关和可拖动背景的命中优先级，并统一手形、帮助和默认光标判定。
- `UiDrawing`：由主卡片和重置历史窗口共享微软雅黑像素字体、自适应字号、单行省略文字与圆角路径实现。
- `DashboardRenderer`：绘制玻璃卡片背景、标题栏、网速、同步状态、周额度、预算线、节奏行、近 7 天、模型偏好、悬停提示和空状态；同时返回经 DPI 缩放的标题栏、重置历史、预算线和详情折叠命中区域。
- `ResetHistoryInteractionState`：管理折叠列表偏移、展开/收起、时间轴视口和滑块拖动；`ResetHistorySurface` 只负责绘制及把鼠标、滚轮和键盘事件转成状态操作。
- `CodexMeterFormV2`：作为组合根保留菜单、托盘、计时器、鼠标事件和异步任务编排；不再包含主卡片内容绘制、历史导航算法或直接的屏幕/启动项判定。耗时的额度查询、日志读取、历史合并与写盘均在后台完成。

## 重置记录语义

`Source` 和 `Confidence` 表示不同概念：

- `provider_window`：根据服务返回的七天额度窗口边界反推；来源是推算，但时间精度为高。
- `live_transition`：程序运行期间跨越重置边界并观察到额度回落。
- `local_log_transition`：从本机历史日志的相邻结构化快照推算。
- `legacy`：旧版缓存缺少来源字段，保留原置信度，不冒充新证据。

低、中、高时间可信度分别使用红、蓝、绿。间隔统计仅使用中及以上记录，并把 24 小时至 14 天之间的相邻间隔视为有效样本。

## 持久化边界

所有 Windows 数据位于 `%LOCALAPPDATA%\CodexMeter`：

- `settings.ini`：位置、主题、置顶与贴边设置。
- `weekly-usage-cache.json`：文件偏移和 token 聚合。
- `reset-history.json`：重置事件、来源、置信度和扫描检查点。
- `*.bak`：对应 JSON 缓存的上一代备份。
- `codexmeter.log` / `.old`：脱敏诊断日志，单文件上限约 1 MB。

这些文件不保存提示词、回复正文、Cookie 或账号令牌。

## 当前边界与后续原则

本轮结构收口已经覆盖主卡片绘制、窗口行为、贴边状态和重置历史导航。后续新增功能应优先扩展对应的 Presentation、State、Policy、Renderer 或 Controller，避免把计算和绘制重新堆回窗体。

当前仍使用仓库内经过验证的 `build.ps1` 和单独测试入口，目的是兼容 Windows 自带的 .NET Framework 编译器且不引入下载依赖。只有在具备可验证的 MSBuild/.NET SDK 发布环境时，才考虑迁移为独立应用与测试项目；迁移不得改变缓存路径、设置语义、单实例、启动项或现有 Release 运行要求。
