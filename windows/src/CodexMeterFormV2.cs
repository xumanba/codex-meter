using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace CodexMeter
{
    internal sealed class CodexMeterFormV2 : Form
    {
        private const int DesignWidth = DashboardPresentation.DesignWidth;
        private const int HeaderHeight = DashboardPresentation.HeaderHeight;
        private const int MeterHeight = DashboardPresentation.MeterHeight;
        private const int PaceHeight = DashboardPresentation.PaceHeight;
        private const int DailyUsageHeight = DashboardPresentation.DailyUsageHeight;
        private const int DockStrip = 7;
        private const int NormalRefreshMilliseconds = 60000;
        private const int HiddenRefreshMilliseconds = 120000;
        private const int MaximumBackoffMilliseconds = 600000;
        private static readonly TimeSpan ResetHistoryImportInterval = TimeSpan.FromMinutes(30);

        private float uiScale;
        private readonly CodexBarClient client = new CodexBarClient();
        private readonly SettingsStore settingsStore = new SettingsStore();
        private readonly AppSettings settings;
        private readonly WinFormsTimer refreshTimer = new WinFormsTimer();
        private readonly WinFormsTimer foregroundTimer = new WinFormsTimer();
        private readonly WinFormsTimer dockTimer = new WinFormsTimer();
        private readonly WinFormsTimer statusTimer = new WinFormsTimer();
        private readonly WinFormsTimer networkTimer = new WinFormsTimer();
        private readonly NetworkSpeedMonitor networkSpeedMonitor = new NetworkSpeedMonitor();
        private readonly WeeklyUsageReader weeklyUsageReader = new WeeklyUsageReader();
        private readonly ResetHistoryStore resetHistoryStore = new ResetHistoryStore();
        private readonly RefreshCoordinator refreshCoordinator = new RefreshCoordinator();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly NotifyIcon trayIcon = new NotifyIcon();
        private readonly ToolTip contextualToolTip = new ToolTip();
        private readonly List<ResetHoverTarget> resetHoverTargets = new List<ResetHoverTarget>();
        private readonly EventWaitHandle showExistingEvent;
        private readonly DashboardState dashboardState;
        private readonly WindowBehaviorController windowBehavior;
        private readonly DockVisibilityState dockVisibility =
            new DockVisibilityState();

        private ToolStripMenuItem lightItem;
        private ToolStripMenuItem darkItem;
        private ToolStripMenuItem edgeItem;
        private ToolStripMenuItem cancelTopMostItem;
        private ToolStripMenuItem startupItem;
        private ToolStripMenuItem visibilityItem;
        private ResetHistoryPopup resetHistoryPopup;
        private bool isWeeklyUsageRefreshing;
        private bool isResetHistoryRefreshing;
        private bool isExiting;
        private bool manuallyHidden;
        private bool isDragging;
        private Point dragOffset;
        private Screen activeDockScreen;
        private Rectangle menuButtonBounds;
        private Rectangle syncButtonBounds;
        private Rectangle paceToggleBounds;
        private Rectangle resetHistoryButtonBounds;
        private Rectangle budgetMarkerBounds;
        private bool syncButtonHovered;
        private bool paceToggleHovered;
        private bool budgetMarkerHovered;
        private bool detailsExpanded;
        private float budgetMarkerDesignX;
        private string budgetToolTipText = String.Empty;
        private string activeResetToolTipText = String.Empty;
        private int designHeight = 126;
        private int scheduledRefreshMilliseconds = NormalRefreshMilliseconds;
        private DateTimeOffset? lastResetHistoryImportAttemptAt;
        private CancellationTokenSource refreshCancellation;
        private RegisteredWaitHandle showExistingWait;
        private readonly bool startedWithWindows;
        private readonly Bitmap appIconBitmap;

        private UsageSnapshot snapshot { get { return dashboardState.Snapshot; } }
        private WeeklyTokenReport weeklyUsage { get { return dashboardState.WeeklyUsage; } }
        private ResetHistoryReport resetHistory { get { return dashboardState.ResetHistory; } }
        private string lastError { get { return dashboardState.LastError; } }
        private bool isConnected { get { return dashboardState.IsConnected; } }
        private DateTimeOffset? lastSuccessfulRefreshAt
        {
            get { return dashboardState.LastSuccessfulRefreshAt; }
        }
        private NetworkSpeedSnapshot networkSpeed { get { return dashboardState.NetworkSpeed; } }

        public CodexMeterFormV2() : this(null, false)
        {
        }

        internal CodexMeterFormV2(EventWaitHandle showExistingEvent) : this(showExistingEvent, false)
        {
        }

        internal CodexMeterFormV2(EventWaitHandle showExistingEvent, bool startedWithWindows)
        {
            this.showExistingEvent = showExistingEvent;
            this.startedWithWindows = startedWithWindows;
            uiScale = Math.Max(1f, Math.Min(3f, NativeMethods.SystemScale()));
            settings = settingsStore.Load();
            dashboardState = new DashboardState(resetHistoryStore.Read());

            Text = "CodexMeter";
            Icon = NativeMethods.CreateAppIcon();
            appIconBitmap = Icon.ToBitmap();
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = settings.AlwaysOnTop;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            AccessibleName = "Codex 用量";
            AccessibleRole = AccessibleRole.Window;
            AccessibleDescription = "Codex 用量悬浮卡片。按 F5 立即同步，按 Esc 最小化到托盘。";
            BackColor = IsDark ? Color.FromArgb(23, 28, 39) : Color.FromArgb(232, 242, 248);
            Opacity = 0.995;
            ClientSize = new Size(S(DesignWidth), S(designHeight));
            windowBehavior = new WindowBehaviorController(
                this, settings, delegate { return S(18); });

            BuildMenus();
            ConfigureTimers();
            contextualToolTip.ShowAlways = true;
            contextualToolTip.AutoPopDelay = 8000;
            windowBehavior.RestorePosition();
            UpdateRoundedRegion();

            Shown += OnShown;
            FormClosing += OnFormClosing;
            MouseDown += OnCardMouseDown;
            MouseMove += OnCardMouseMove;
            MouseUp += OnCardMouseUp;
            MouseClick += OnCardMouseClick;
            KeyDown += OnShortcutKeyDown;
            MouseLeave += delegate
            {
                if (!isDragging)
                {
                    bool wasHovered = syncButtonHovered;
                    syncButtonHovered = false;
                    bool wasPaceHovered = paceToggleHovered;
                    paceToggleHovered = false;
                    bool wasBudgetHovered = budgetMarkerHovered;
                    budgetMarkerHovered = false;
                    activeResetToolTipText = String.Empty;
                    contextualToolTip.Hide(this);
                    Cursor = Cursors.Default;
                    if (wasHovered)
                        Invalidate();
                    if (wasPaceHovered)
                        Invalidate();
                    if (wasBudgetHovered)
                        Invalidate();
                }
            };
            Resize += delegate { UpdateRoundedRegion(); };
            Disposed += delegate
            {
                contextualToolTip.Dispose();
                appIconBitmap.Dispose();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return parameters;
            }
        }

        private bool IsDark
        {
            get { return String.Equals(settings.Theme, "dark", StringComparison.OrdinalIgnoreCase); }
        }

        private int S(float value)
        {
            return Math.Max(1, Convert.ToInt32(Math.Round(value * uiScale)));
        }

        private void BuildMenus()
        {
            visibilityItem = new ToolStripMenuItem("最小化到托盘");
            visibilityItem.Click += delegate { ToggleTrayVisibility(); };

            cancelTopMostItem = new ToolStripMenuItem("取消始终置顶");
            cancelTopMostItem.CheckOnClick = true;
            cancelTopMostItem.Checked = WindowBehaviorPolicy.CancelTopMostMenuChecked(
                settings.AlwaysOnTop);
            cancelTopMostItem.CheckedChanged += delegate
            {
                settings.AlwaysOnTop = WindowBehaviorPolicy.AlwaysOnTopFromCancelMenu(
                    cancelTopMostItem.Checked);
                windowBehavior.ApplyTopMost();
                SaveSettings();
            };

            startupItem = new ToolStripMenuItem("开机自启动");
            startupItem.Click += delegate { ToggleStartWithWindows(); };

            ToolStripMenuItem appearance = new ToolStripMenuItem("外观");
            lightItem = new ToolStripMenuItem("浅色玻璃");
            lightItem.Click += delegate { SetTheme("light"); };
            darkItem = new ToolStripMenuItem("深色玻璃");
            darkItem.Click += delegate { SetTheme("dark"); };
            appearance.DropDownItems.Add(lightItem);
            appearance.DropDownItems.Add(darkItem);

            edgeItem = new ToolStripMenuItem("贴边自动隐藏（左右边缘）");
            edgeItem.CheckOnClick = true;
            edgeItem.Checked = settings.EdgeAutoHide;
            edgeItem.CheckedChanged += delegate
            {
                settings.EdgeAutoHide = edgeItem.Checked;
                if (!settings.EdgeAutoHide)
                    ClearDock(true);
                ConfigureWindowTimers();
                ScheduleNextRefresh();
                SaveSettings();
            };

            ToolStripMenuItem refresh = new ToolStripMenuItem("立即同步");
            refresh.Click += delegate { RequestManualRefresh(); };
            ToolStripMenuItem openFolder = new ToolStripMenuItem("打开 CodexBar 目录");
            openFolder.Click += delegate { OpenCodexBarFolder(); };
            ToolStripMenuItem quit = new ToolStripMenuItem("退出");
            quit.Click += delegate
            {
                isExiting = true;
                Close();
            };

            menu.Items.Add(visibilityItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(cancelTopMostItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(appearance);
            menu.Items.Add(edgeItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(refresh);
            menu.Items.Add(openFolder);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(quit);
            menu.Opening += delegate { SyncMenuChecks(); };

            trayIcon.Icon = NativeMethods.CreateAppIcon();
            trayIcon.Text = "CodexMeter";
            trayIcon.Visible = true;
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate
            {
                if (Visible)
                    MinimizeToTray();
                else
                    RestoreFromTray();
            };

            SyncMenuChecks();
        }

        private void ConfigureTimers()
        {
            refreshTimer.Interval = NormalRefreshMilliseconds;
            refreshTimer.Tick += delegate
            {
                refreshTimer.Stop();
                RefreshNow();
            };
            foregroundTimer.Interval = 500;
            foregroundTimer.Tick += delegate { windowBehavior.ApplyTopMost(); };
            dockTimer.Interval = 50;
            dockTimer.Tick += delegate { UpdateDockPosition(); };
            statusTimer.Interval = 60000;
            statusTimer.Tick += delegate
            {
                if (Visible)
                    Invalidate();
                RefreshResetHistory(false);
            };
            networkTimer.Interval = 1000;
            networkTimer.Tick += delegate { UpdateNetworkSpeed(); };
        }

        private void OnShown(object sender, EventArgs eventArgs)
        {
            StartShowExistingWait();
            ApplyUiScale(NativeMethods.WindowScale(Handle));
            NativeMethods.ApplyWindowStyle(Handle, IsDark);
            RestoreDockIfNeeded();
            ConfigureWindowTimers();
            RevealDockForStartupIfNeeded();
            statusTimer.Start();
            SaveSettings();
            RefreshNow();
            RefreshResetHistory(true);
        }

        private void RevealDockForStartupIfNeeded()
        {
            if (!WindowBehaviorPolicy.ShouldRevealDockAtStartup(
                    startedWithWindows, settings.DockEdge, settings.EdgeAutoHide))
                return;

            manuallyHidden = false;
            dockVisibility.Reveal(DateTime.UtcNow, TimeSpan.FromSeconds(30));
            UpdateDockPosition();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            if (!isExiting && eventArgs.CloseReason == CloseReason.UserClosing)
            {
                eventArgs.Cancel = true;
                MinimizeToTray();
                return;
            }

            refreshTimer.Stop();
            refreshCoordinator.Cancel();
            foregroundTimer.Stop();
            dockTimer.Stop();
            statusTimer.Stop();
            networkTimer.Stop();
            networkSpeedMonitor.Reset();
            if (resetHistoryPopup != null)
            {
                ResetHistoryPopup popup = resetHistoryPopup;
                resetHistoryPopup = null;
                popup.Close();
            }
            if (showExistingWait != null)
            {
                showExistingWait.Unregister(null);
                showExistingWait = null;
            }
            if (refreshCancellation != null)
            {
                refreshCancellation.Cancel();
                refreshCancellation.Dispose();
                refreshCancellation = null;
            }
            SaveCurrentPosition();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            menu.Dispose();
        }

        private void RefreshNow()
        {
            if (!refreshCoordinator.TryBeginAutomaticRefresh())
                return;

            StartRefreshCore();
        }

        private void StartRefreshCore()
        {
            refreshTimer.Stop();
            RefreshWeeklyUsage();
            Invalidate();
            CancellationTokenSource requestCancellation = new CancellationTokenSource();
            refreshCancellation = requestCancellation;
            Task.Factory.StartNew(
                delegate
                {
                    UsageSnapshot refreshedSnapshot = client.Refresh(requestCancellation.Token);
                    requestCancellation.Token.ThrowIfCancellationRequested();
                    DateTimeOffset refreshedAt = DateTimeOffset.Now;
                    UsageWindow mainWindow = refreshedSnapshot == null
                        ? null : refreshedSnapshot.Weekly ?? refreshedSnapshot.Session;
                    ResetHistoryReport observedHistory = resetHistoryStore.Observe(
                        mainWindow, refreshedAt);
                    return new QuotaRefreshResult
                    {
                        Snapshot = refreshedSnapshot,
                        ResetHistory = observedHistory,
                        RefreshedAt = refreshedAt
                    };
                },
                requestCancellation.Token,
                TaskCreationOptions.None,
                TaskScheduler.Default)
                .ContinueWith(delegate(Task<QuotaRefreshResult> task)
                {
                    if (IsDisposed)
                    {
                        requestCancellation.Dispose();
                        return;
                    }

                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (task.IsFaulted)
                            {
                                dashboardState.ApplyQuotaFailure(FlattenError(task.Exception));
                                AppDiagnostics.Record("quota-refresh", task.Exception);
                                refreshCoordinator.RegisterFailure();
                            }
                            else if (task.IsCanceled)
                            {
                                dashboardState.ApplyQuotaFailure("同步已取消");
                            }
                            else
                            {
                                dashboardState.ApplyQuotaSuccess(task.Result);
                                refreshCoordinator.RegisterSuccess();
                            }

                            if (ReferenceEquals(refreshCancellation, requestCancellation))
                                refreshCancellation = null;
                            requestCancellation.Dispose();
                            trayIcon.Text = BuildTrayText();
                            UpdateAccessibleSummary();
                            ResizeForContent();
                            if (refreshCoordinator.FinishAndBeginQueuedRefresh(!isExiting))
                            {
                                StartRefreshCore();
                            }
                            else
                            {
                                ScheduleNextRefresh();
                            }
                            Invalidate();
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        // The application is already closing.
                    }
                });
        }

        private void RefreshResetHistory(bool force)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            if (!force && !BackgroundRefreshPolicy.ShouldRun(
                    isResetHistoryRefreshing, lastResetHistoryImportAttemptAt,
                    now, ResetHistoryImportInterval))
            {
                return;
            }
            if (isResetHistoryRefreshing)
                return;

            isResetHistoryRefreshing = true;
            lastResetHistoryImportAttemptAt = now;
            Task.Factory.StartNew(
                delegate { return resetHistoryStore.ImportLocalHistory(); },
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default)
                .ContinueWith(delegate(Task<ResetHistoryReport> task)
                {
                    if (IsDisposed)
                        return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            isResetHistoryRefreshing = false;
                            if (!task.IsFaulted && !task.IsCanceled && task.Result != null)
                                dashboardState.ApplyResetHistory(task.Result);
                            else if (task.IsFaulted)
                                AppDiagnostics.Record("reset-history-refresh", task.Exception);
                            UpdateAccessibleSummary();
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        // The application is already closing.
                    }
                });
        }

        private void RefreshWeeklyUsage()
        {
            if (isWeeklyUsageRefreshing)
                return;

            isWeeklyUsageRefreshing = true;
            Task.Factory.StartNew(
                delegate { return weeklyUsageReader.Read(DateTimeOffset.Now); },
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default)
                .ContinueWith(delegate(Task<WeeklyTokenReport> task)
                {
                    if (IsDisposed)
                        return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            isWeeklyUsageRefreshing = false;
                            if (!task.IsFaulted && !task.IsCanceled && task.Result != null)
                                dashboardState.ApplyWeeklyUsage(task.Result);
                            else if (task.IsFaulted)
                                AppDiagnostics.Record("weekly-usage-refresh", task.Exception);
                            ResizeForContent();
                            UpdateAccessibleSummary();
                            Invalidate();
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        // The application is already closing.
                    }
                });
        }

        private void RequestManualRefresh()
        {
            if (!refreshCoordinator.RequestManualRefresh())
            {
                Invalidate();
                return;
            }

            StartRefreshCore();
        }

        internal static bool ShouldStartManualRefresh(bool refreshRunning)
        {
            return RefreshCoordinator.ShouldStartManualRefresh(refreshRunning);
        }

        internal static bool ShouldQueueManualRefresh(bool refreshRunning)
        {
            return RefreshCoordinator.ShouldQueueManualRefresh(refreshRunning);
        }

        internal static int CardDesignWidth
        {
            get { return DesignWidth; }
        }

        private void ScheduleNextRefresh()
        {
            if (isExiting || IsDisposed)
                return;

            int interval;
            if (!Visible || manuallyHidden ||
                (!String.IsNullOrEmpty(settings.DockEdge) && !dockVisibility.Revealed))
                interval = HiddenRefreshMilliseconds;
            else
                interval = NormalRefreshMilliseconds;

            interval = refreshCoordinator.ApplyFailureBackoff(
                interval, MaximumBackoffMilliseconds);

            scheduledRefreshMilliseconds = interval;
            refreshTimer.Interval = interval;
            if (!refreshCoordinator.IsRefreshing)
                refreshTimer.Start();
        }

        private void ConfigureWindowTimers()
        {
            // This timer also monitors whether Codex/ChatGPT is foreground on
            // the card's screen, so it remains above that window when needed.
            foregroundTimer.Start();

            bool needsDockPolling = WindowBehaviorPolicy.ShouldPollDock(
                settings.EdgeAutoHide, settings.DockEdge, Visible);
            if (needsDockPolling)
            {
                dockTimer.Interval = 50;
                dockTimer.Start();
            }
            else
            {
                dockTimer.Stop();
            }

            bool shouldSampleNetwork = WindowBehaviorPolicy.ShouldSampleNetwork(
                Visible, manuallyHidden);
            if (shouldSampleNetwork && !networkTimer.Enabled)
            {
                networkSpeedMonitor.Reset();
                dashboardState.ApplyNetworkSpeed(networkSpeedMonitor.Sample());
                networkTimer.Start();
            }
            else if (!shouldSampleNetwork && networkTimer.Enabled)
            {
                networkTimer.Stop();
                networkSpeedMonitor.Reset();
                dashboardState.ApplyNetworkSpeed(new NetworkSpeedSnapshot(0, 0));
            }
        }

        private void UpdateNetworkSpeed()
        {
            if (!Visible || manuallyHidden)
                return;

            try
            {
                dashboardState.ApplyNetworkSpeed(networkSpeedMonitor.Sample());
            }
            catch
            {
                networkSpeedMonitor.Reset();
                dashboardState.ApplyNetworkSpeed(new NetworkSpeedSnapshot(0, 0));
            }
            UpdateAccessibleSummary();
            Invalidate(NetworkSpeedBounds);
        }

        private void ApplyUiScale(float newScale)
        {
            newScale = Math.Max(1f, Math.Min(3f, newScale));
            if (Math.Abs(newScale - uiScale) < 0.01f)
                return;

            uiScale = newScale;
            ClientSize = new Size(S(DesignWidth), S(designHeight));
            UpdateRoundedRegion();
            if (!String.IsNullOrEmpty(settings.DockEdge))
            {
                activeDockScreen = windowBehavior.FindDockScreen();
                RestoreDockIfNeeded();
            }
            else
            {
                windowBehavior.ClampToWorkingArea();
            }
            Invalidate();
        }

        private void ResizeForContent()
        {
            bool hasSnapshot = snapshot != null;
            bool hasWeeklyPace = hasSnapshot && snapshot.WeeklyPace != null;
            if (!hasWeeklyPace)
                detailsExpanded = false;

            int target = DashboardPresentation.ContentHeight(
                hasSnapshot, hasWeeklyPace, detailsExpanded);

            if (designHeight == target)
                return;

            designHeight = target;
            ClientSize = new Size(S(DesignWidth), S(designHeight));
            UpdateRoundedRegion();
            if (!String.IsNullOrEmpty(settings.DockEdge))
            {
                activeDockScreen = activeDockScreen ?? windowBehavior.FindDockScreen();
                if (activeDockScreen != null)
                    settings.DockTop = WindowBehaviorPolicy.ClampTop(
                        activeDockScreen.WorkingArea, Top, Height);
                UpdateDockPosition();
            }
            else
                windowBehavior.ClampToWorkingArea();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            Rectangle networkBounds = NetworkSpeedBounds;
            bool networkOnlyPaint = networkBounds.Contains(eventArgs.ClipRectangle);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.ScaleTransform(uiScale, uiScale);

            DashboardRenderer.DrawCard(graphics, designHeight, IsDark);
            if (networkOnlyPaint)
            {
                DashboardRenderer.DrawNetworkSpeed(graphics, networkSpeed, IsDark);
                return;
            }
            DashboardHeaderLayout headerLayout = DashboardRenderer.DrawHeader(
                graphics, appIconBitmap, networkSpeed, StatusText, StatusDotColor,
                syncButtonHovered, IsDark, uiScale);
            syncButtonBounds = headerLayout.SyncButtonBounds;
            menuButtonBounds = headerLayout.MenuButtonBounds;
            budgetMarkerBounds = Rectangle.Empty;
            paceToggleBounds = Rectangle.Empty;
            resetHistoryButtonBounds = Rectangle.Empty;
            budgetToolTipText = String.Empty;
            resetHoverTargets.Clear();

            if (snapshot == null)
            {
                DashboardRenderer.DrawEmptyState(graphics, lastError, IsDark);
                if (syncButtonHovered)
                    DashboardRenderer.DrawStatusToolTip(
                        graphics, StatusToolTipText(), 100, IsDark);
                return;
            }

            int y = HeaderHeight;
            UsageWindow main = snapshot.Weekly ?? snapshot.Session;
            string reset = ResetText(main);
            string weeklyTokens = weeklyUsage != null && weeklyUsage.TotalTokens > 0
                ? "本周 " + WeeklyUsageReader.FormatTokenCount(weeklyUsage.TotalTokens) + " token"
                : (isWeeklyUsageRefreshing ? "正在统计本周 token…" : "本周暂无本机记录");
            DashboardMeterLayout meterLayout = DashboardRenderer.DrawMeter(
                graphics, main, y, true, snapshot.WeeklyPace, reset,
                weeklyTokens, IsDark, uiScale);
            if (!meterLayout.ResetBounds.IsEmpty)
            {
                resetHoverTargets.Add(new ResetHoverTarget(
                    meterLayout.ResetBounds,
                    ResetToolTipText(main) + "\r\n点击查看重置历史"));
                resetHistoryButtonBounds = meterLayout.ResetBounds;
            }
            budgetMarkerBounds = meterLayout.BudgetMarkerBounds;
            budgetMarkerDesignX = meterLayout.BudgetMarkerDesignX;
            budgetToolTipText = meterLayout.BudgetToolTipText;
            y += MeterHeight;

            if (snapshot.WeeklyPace != null)
            {
                paceToggleBounds = DashboardRenderer.DrawPace(
                    graphics, snapshot.WeeklyPace, y, paceToggleHovered,
                    detailsExpanded, IsDark, uiScale);
                y += PaceHeight;
            }

            if (detailsExpanded)
            {
                double weeklyUsedPercent = snapshot.Weekly == null
                    ? 0
                    : snapshot.Weekly.UsedPercent;
                DashboardRenderer.DrawDailyUsage(graphics, weeklyUsage,
                    isWeeklyUsageRefreshing, weeklyUsedPercent, y, IsDark);
                y += DailyUsageHeight;
                DashboardRenderer.DrawModelUsage(graphics, weeklyUsage,
                    isWeeklyUsageRefreshing, y, IsDark);
            }

            if (budgetMarkerHovered && !String.IsNullOrEmpty(budgetToolTipText))
                DashboardRenderer.DrawBudgetToolTip(
                    graphics, budgetMarkerDesignX, budgetToolTipText, IsDark);
            if (syncButtonHovered)
            {
                UsageWindow statusWindow = snapshot.Weekly ?? snapshot.Session;
                DashboardRenderer.DrawStatusToolTip(graphics, StatusToolTipText(),
                    statusWindow == null ? 100 : statusWindow.RemainingPercent, IsDark);
            }
        }

        private Rectangle NetworkSpeedBounds
        {
            get { return DashboardRenderer.HeaderLayout(uiScale).NetworkSpeedBounds; }
        }

        private void OnCardMouseClick(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Right)
            {
                menu.Show(this, eventArgs.Location);
                return;
            }
            if (eventArgs.Button != MouseButtons.Left)
                return;

            DashboardAction action = DashboardInteractionPolicy.PrimaryActionAt(
                eventArgs.Location, syncButtonBounds, menuButtonBounds,
                resetHistoryButtonBounds, paceToggleBounds);
            switch (action)
            {
                case DashboardAction.Sync:
                    RequestManualRefresh();
                    break;
                case DashboardAction.Menu:
                    menu.Show(this,
                        new Point(menuButtonBounds.Right, menuButtonBounds.Bottom),
                        ToolStripDropDownDirection.BelowLeft);
                    break;
                case DashboardAction.ResetHistory:
                    ShowResetHistory();
                    break;
                case DashboardAction.ToggleDetails:
                    ToggleDetails();
                    break;
            }
        }

        private void ShowResetHistory()
        {
            if (resetHistoryPopup != null)
            {
                ResetHistoryPopup existing = resetHistoryPopup;
                resetHistoryPopup = null;
                existing.Close();
            }

            ResetHistoryPopup popup = new ResetHistoryPopup(
                resetHistory ?? resetHistoryStore.Read(),
                isResetHistoryRefreshing, IsDark, uiScale);
            resetHistoryPopup = popup;
            popup.Closed += delegate
            {
                if (ReferenceEquals(resetHistoryPopup, popup))
                    resetHistoryPopup = null;
                // ToolStripDropDown still executes its close pipeline after Closed.
                // Dispose only when the UI message has fully unwound.
                popup.DisposeAfterClose();
                dockVisibility.SuppressHide(
                    DateTime.UtcNow, TimeSpan.FromSeconds(2));
            };
            dockVisibility.SuppressHide(DateTime.UtcNow, TimeSpan.FromMinutes(5));
            popup.Show(this,
                new Point(resetHistoryButtonBounds.Right, resetHistoryButtonBounds.Bottom),
                ToolStripDropDownDirection.BelowLeft);
        }

        private void ToggleDetails()
        {
            if (snapshot == null || snapshot.WeeklyPace == null)
                return;

            detailsExpanded = !detailsExpanded;
            ResizeForContent();
            UpdateAccessibleSummary();
            Invalidate();
        }

        private void OnCardMouseDown(object sender, MouseEventArgs eventArgs)
        {
            DashboardAction action = DashboardInteractionPolicy.PrimaryActionAt(
                eventArgs.Location, syncButtonBounds, menuButtonBounds,
                resetHistoryButtonBounds, paceToggleBounds);
            if (eventArgs.Button != MouseButtons.Left ||
                DashboardInteractionPolicy.BlocksDrag(action))
                return;

            if (!String.IsNullOrEmpty(settings.DockEdge))
                ClearDock(false);

            isDragging = true;
            dragOffset = eventArgs.Location;
            Capture = true;
        }

        private void OnCardMouseMove(object sender, MouseEventArgs eventArgs)
        {
            if (!isDragging)
            {
                DashboardAction action = DashboardInteractionPolicy.PrimaryActionAt(
                    eventArgs.Location, syncButtonBounds, menuButtonBounds,
                    resetHistoryButtonBounds, paceToggleBounds);
                bool hovered = action == DashboardAction.Sync;
                bool paceHovered = action == DashboardAction.ToggleDetails;
                bool budgetHovered = budgetMarkerBounds.Contains(eventArgs.Location) &&
                    !String.IsNullOrEmpty(budgetToolTipText);
                string resetToolTipText = resetHoverTargets
                    .Where(delegate(ResetHoverTarget target) { return target.Bounds.Contains(eventArgs.Location); })
                    .Select(delegate(ResetHoverTarget target) { return target.Text; })
                    .FirstOrDefault() ?? String.Empty;
                if (String.IsNullOrEmpty(resetToolTipText) && paceHovered)
                    resetToolTipText = detailsExpanded
                        ? "收起近 7 天用量和模型偏好"
                        : "展开近 7 天用量和模型偏好";
                if (!String.Equals(resetToolTipText, activeResetToolTipText, StringComparison.Ordinal))
                {
                    contextualToolTip.Hide(this);
                    activeResetToolTipText = resetToolTipText;
                    if (!String.IsNullOrEmpty(activeResetToolTipText))
                    {
                        contextualToolTip.Show(activeResetToolTipText, this,
                            eventArgs.X, eventArgs.Y + S(18), 8000);
                    }
                }
                if (budgetHovered != budgetMarkerHovered)
                {
                    budgetMarkerHovered = budgetHovered;
                    Invalidate();
                }
                if (hovered != syncButtonHovered)
                {
                    syncButtonHovered = hovered;
                    Invalidate();
                }
                if (paceHovered != paceToggleHovered)
                {
                    paceToggleHovered = paceHovered;
                    Invalidate(paceToggleBounds);
                }
                DashboardCursorKind cursorKind = DashboardInteractionPolicy.CursorFor(
                    action, budgetHovered, !String.IsNullOrEmpty(resetToolTipText));
                Cursor = cursorKind == DashboardCursorKind.Hand
                    ? Cursors.Hand
                    : (cursorKind == DashboardCursorKind.Help
                        ? Cursors.Help : Cursors.Default);
            }

            if (!isDragging || (Control.MouseButtons & MouseButtons.Left) == 0)
                return;

            Point cursor = Cursor.Position;
            Location = new Point(cursor.X - dragOffset.X, cursor.Y - dragOffset.Y);
        }

        private void OnCardMouseUp(object sender, MouseEventArgs eventArgs)
        {
            if (!isDragging || eventArgs.Button != MouseButtons.Left)
                return;

            isDragging = false;
            Capture = false;
            EvaluateDockAfterMove();
            SaveCurrentPosition();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.ShowExistingInstanceMessage)
            {
                RestoreAndActivate();
                return;
            }

            base.WndProc(ref message);
            if (message.Msg == 0x02E0) // WM_DPICHANGED
            {
                int dpi = unchecked((int)((long)message.WParam & 0xFFFF));
                if (dpi >= 72 && dpi <= 480 && IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke((MethodInvoker)delegate { ApplyUiScale(dpi / 96f); });
                }
            }
            else if (message.Msg == 0x0232) // WM_EXITSIZEMOVE
            {
                EvaluateDockAfterMove();
                SaveCurrentPosition();
            }
        }

        private void EvaluateDockAfterMove()
        {
            if (!settings.EdgeAutoHide)
                return;

            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle area = screen.WorkingArea;
            int threshold = S(30);
            int leftDistance = Math.Min(Math.Abs(Left - area.Left), Math.Abs(Cursor.Position.X - area.Left));
            int rightDistance = Math.Min(Math.Abs(Right - area.Right), Math.Abs(Cursor.Position.X - area.Right));
            string dockEdge = WindowBehaviorPolicy.DockEdgeForDistances(
                leftDistance, rightDistance, threshold);
            if (String.IsNullOrEmpty(dockEdge))
                return;

            activeDockScreen = screen;
            settings.DockScreen = screen.DeviceName;
            settings.DockEdge = dockEdge;
            settings.DockTop = WindowBehaviorPolicy.ClampTop(area, Top, Height);
            dockVisibility.Hide(
                DateTime.UtcNow, TimeSpan.FromMilliseconds(550));
            SaveSettings();
            ConfigureWindowTimers();
            ScheduleNextRefresh();
            UpdateDockPosition();
        }

        private void UpdateDockPosition()
        {
            if (String.IsNullOrEmpty(settings.DockEdge) || !settings.EdgeAutoHide || !Visible)
                return;

            Screen screen = activeDockScreen ?? windowBehavior.FindDockScreen();
            if (screen == null)
                return;
            activeDockScreen = screen;
            Rectangle area = screen.WorkingArea;
            int top = settings.DockTop.HasValue ? settings.DockTop.Value : Top;
            top = WindowBehaviorPolicy.ClampTop(area, top, Height);

            Point cursor = Cursor.Position;
            bool cursorOnThisScreen = screen.Bounds.Contains(cursor);
            bool atStrip = cursorOnThisScreen && cursor.Y >= top - S(5) && cursor.Y <= top + Height + S(5) &&
                ((settings.DockEdge == "left" && cursor.X <= area.Left + S(11)) ||
                 (settings.DockEdge == "right" && cursor.X >= area.Right - S(11)));
            Rectangle hoverBounds = Bounds;
            hoverBounds.Inflate(S(8), S(8));
            bool overRevealedCard = dockVisibility.Revealed &&
                hoverBounds.Contains(cursor);
            dockVisibility.Evaluate(
                DateTime.UtcNow, atStrip, overRevealedCard, menu.Visible);

            int targetX = WindowBehaviorPolicy.DockTargetX(
                area, Width, S(DockStrip), S(7), settings.DockEdge,
                dockVisibility.Revealed);
            int nextX = WindowBehaviorPolicy.StepToward(Left, targetX, S(1), S(2));
            dockVisibility.SetAnimating(Left != nextX || Top != top);
            dockTimer.Interval = dockVisibility.IsAnimating ? 16 : 50;
            if (dockVisibility.IsAnimating)
                Location = new Point(nextX, top);
        }

        private void RestoreDockIfNeeded()
        {
            if (String.IsNullOrEmpty(settings.DockEdge) || !settings.EdgeAutoHide)
                return;

            activeDockScreen = windowBehavior.FindDockScreen();
            if (activeDockScreen == null)
            {
                ClearDock(true);
                return;
            }

            Rectangle area = activeDockScreen.WorkingArea;
            int top = settings.DockTop.HasValue ? settings.DockTop.Value : area.Top + S(20);
            top = WindowBehaviorPolicy.ClampTop(area, top, Height);
            Left = WindowBehaviorPolicy.DockTargetX(
                area, Width, S(DockStrip), S(7), settings.DockEdge, false);
            Top = top;
            dockVisibility.Hide(
                DateTime.UtcNow, TimeSpan.FromMilliseconds(450));
            ConfigureWindowTimers();
        }

        private void ClearDock(bool moveOnScreen)
        {
            if (String.IsNullOrEmpty(settings.DockEdge))
                return;

            string edge = settings.DockEdge;
            Screen screen = activeDockScreen ?? windowBehavior.FindDockScreen();
            settings.DockEdge = null;
            settings.DockTop = null;
            settings.DockScreen = null;
            activeDockScreen = null;
            dockVisibility.Clear();
            ConfigureWindowTimers();
            ScheduleNextRefresh();

            if (moveOnScreen && screen != null)
            {
                Rectangle area = screen.WorkingArea;
                int x = edge == "left" ? area.Left + S(12) : area.Right - Width - S(12);
                int y = WindowBehaviorPolicy.ClampTop(area, Top, Height);
                Location = new Point(x, y);
            }
        }

        private void SetTheme(string theme)
        {
            settings.Theme = theme;
            BackColor = IsDark ? Color.FromArgb(23, 28, 39) : Color.FromArgb(226, 233, 234);
            NativeMethods.ApplyWindowStyle(Handle, IsDark);
            SyncMenuChecks();
            SaveSettings();
            Invalidate();
        }

        private void ShowMeter()
        {
            manuallyHidden = false;
            Show();
            windowBehavior.ApplyTopMost();
            BringToFront();
            ConfigureWindowTimers();
            ScheduleNextRefresh();
        }

        private void ToggleTrayVisibility()
        {
            if (Visible)
                MinimizeToTray();
            else
                RestoreFromTray();
        }

        private void MinimizeToTray()
        {
            if (!Visible)
                return;

            SaveCurrentPosition();
            manuallyHidden = true;
            Hide();
            ConfigureWindowTimers();
            ScheduleNextRefresh();
            SyncMenuChecks();
        }

        private void RestoreFromTray()
        {
            manuallyHidden = false;
            if (!String.IsNullOrEmpty(settings.DockEdge) && settings.EdgeAutoHide)
            {
                dockVisibility.Reveal(
                    DateTime.UtcNow, TimeSpan.FromSeconds(2));
            }

            ShowMeter();
            if (!String.IsNullOrEmpty(settings.DockEdge))
                UpdateDockPosition();
            SyncMenuChecks();
        }

        private void RestoreAndActivate()
        {
            if (IsDisposed)
                return;

            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            RestoreFromTray();
            Activate();
            BringToFront();
        }

        private void StartShowExistingWait()
        {
            if (showExistingEvent == null || showExistingWait != null)
                return;

            showExistingWait = ThreadPool.RegisterWaitForSingleObject(
                showExistingEvent,
                delegate
                {
                    if (IsDisposed || Disposing)
                        return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { RestoreAndActivate(); });
                    }
                    catch (InvalidOperationException)
                    {
                        // The form may be closing while the event is delivered.
                    }
                },
                null,
                Timeout.Infinite,
                false);
        }

        private void OnShortcutKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode == Keys.F5)
            {
                RequestManualRefresh();
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
            }
            else if (eventArgs.KeyCode == Keys.Escape)
            {
                MinimizeToTray();
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
            }
        }

        private void SyncMenuChecks()
        {
            visibilityItem.Text = Visible ? "最小化到托盘" : "显示悬浮卡片";
            lightItem.Checked = !IsDark;
            darkItem.Checked = IsDark;
            edgeItem.Checked = settings.EdgeAutoHide;
            cancelTopMostItem.Checked = WindowBehaviorPolicy.CancelTopMostMenuChecked(
                settings.AlwaysOnTop);
            bool startupEnabled;
            string startupError;
            if (windowBehavior.TryReadStartup(out startupEnabled, out startupError))
            {
                startupItem.Checked = startupEnabled;
                startupItem.ToolTipText = "登录 Windows 后自动启动 CodexMeter";
            }
            else
            {
                startupItem.Checked = false;
                startupItem.ToolTipText = "无法读取 Windows 启动项：" +
                    Shorten(startupError, 120);
            }
        }

        private void ToggleStartWithWindows()
        {
            bool startupEnabled;
            string startupError;
            if (windowBehavior.TryToggleStartup(out startupEnabled, out startupError))
            {
                startupItem.Checked = startupEnabled;
            }
            else
            {
                SyncMenuChecks();
                MessageBox.Show(this,
                    "无法修改开机自启动：\r\n" + Shorten(startupError, 240),
                    "CodexMeter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateAccessibleSummary()
        {
            UsageWindow main = snapshot == null ? null : snapshot.Weekly ?? snapshot.Session;
            string usage = main == null
                ? "用量数据尚未载入"
                : "每周剩余 " + Math.Round(main.RemainingPercent).ToString("0") + "%";
            AccessibleDescription = usage + "。状态：" + StatusText +
                "。系统总网速，下载 " + NetworkSpeedMonitor.FormatRate(networkSpeed.DownloadBytesPerSecond) +
                "，上传 " + NetworkSpeedMonitor.FormatRate(networkSpeed.UploadBytesPerSecond) +
                "。网速不是 Codex 专属流量。点击重置倒计时可查看重置历史。" +
                "按 F5 立即同步，按 Esc 最小化到托盘。";
        }

        private void OpenCodexBarFolder()
        {
            string path = client.ExecutablePath;
            if (String.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show(this, "未找到 CodexBar CLI。", "CodexMeter",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        private void SaveCurrentPosition()
        {
            if (!windowBehavior.CapturePosition(dockVisibility.IsAnimating))
                return;
            SaveSettings();
        }

        private void SaveSettings()
        {
            try { settingsStore.Save(settings); }
            catch (Exception exception) { AppDiagnostics.Record("settings-write", exception); }
        }

        private void UpdateRoundedRegion()
        {
            using (GraphicsPath path = UiDrawing.RoundedRectangle(
                new RectangleF(0, 0, Width, Height), S(23)))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null)
                    old.Dispose();
            }
        }

        private string StatusToolTipText()
        {
            if (refreshCoordinator.ManualRefreshPending)
                return "正在同步数据；完成后将按本次点击再次同步";
            if (refreshCoordinator.IsRefreshing || isWeeklyUsageRefreshing)
                return "正在同步数据，请稍候…";
            if (snapshot != null)
            {
                DateTimeOffset displayTime = snapshot.UpdatedAt ?? lastSuccessfulRefreshAt ?? DateTimeOffset.Now;
                if (HasStaleData)
                    return String.IsNullOrWhiteSpace(lastError)
                        ? "数据已过期 · " + displayTime.ToLocalTime().ToString("HH:mm")
                        : "同步失败 · 保留 " + displayTime.ToLocalTime().ToString("HH:mm");
                return "数据已更新 · " + displayTime.ToLocalTime().ToString("HH:mm");
            }
            return String.IsNullOrWhiteSpace(lastError) ? "等待连接" : "同步失败，保留旧数据";
        }

        private string BuildTrayText()
        {
            UsageWindow main = snapshot == null ? null : snapshot.Weekly ?? snapshot.Session;
            if (main == null)
                return Shorten("CodexMeter - " + (lastError ?? "正在同步"), 63);
            string stale = HasStaleData ? "[过期] " : String.Empty;
            return Shorten("CodexMeter - " + stale + "每周剩余 " +
                Math.Round(main.RemainingPercent).ToString("0") + "%", 63);
        }

        private bool HasStaleData
        {
            get
            {
                return DashboardStatusPolicy.IsSnapshotStale(snapshot, isConnected, lastError,
                    lastSuccessfulRefreshAt, scheduledRefreshMilliseconds, DateTimeOffset.Now);
            }
        }

        private DashboardStatusKind CurrentStatusKind
        {
            get
            {
                return DashboardStatusPolicy.Determine(
                    refreshCoordinator.IsRefreshing || isWeeklyUsageRefreshing,
                    HasStaleData,
                    isConnected);
            }
        }

        private string StatusText
        {
            get
            {
                return DashboardStatusPolicy.Label(CurrentStatusKind);
            }
        }

        private Color StatusDotColor
        {
            get
            {
                switch (CurrentStatusKind)
                {
                    case DashboardStatusKind.Syncing:
                        return Color.FromArgb(0, 122, 255);
                    case DashboardStatusKind.Stale:
                        return Color.FromArgb(255, 159, 10);
                    case DashboardStatusKind.Live:
                        return Color.FromArgb(35, 205, 96);
                    default:
                        return Color.FromArgb(255, 149, 0);
                }
            }
        }

        internal static string ResetText(UsageWindow window)
        {
            if (window == null)
                return String.Empty;
            if (window.ResetsAt.HasValue)
                return ResetDuration((window.ResetsAt.Value - DateTimeOffset.Now).TotalSeconds) + " 后重置";
            if (!String.IsNullOrWhiteSpace(window.ResetDescription))
                return window.ResetDescription + " 后重置";
            return String.Empty;
        }

        private static string ResetToolTipText(UsageWindow window)
        {
            if (window == null)
                return String.Empty;
            if (window.ResetsAt.HasValue)
                return "当前预计重置：" + window.ResetsAt.Value.ToLocalTime().ToString("M月d日 HH:mm:ss") +
                    "（本机时间）";
            return window.ResetDescription ?? String.Empty;
        }

        internal static string ResetDuration(double seconds)
        {
            int totalMinutes = Math.Max(0, Convert.ToInt32(Math.Ceiling(seconds / 60)));
            if (totalMinutes <= 0)
                return "0m";
            if (totalMinutes < 60)
                return totalMinutes + "m";
            if (totalMinutes < 24 * 60)
            {
                int hours = totalMinutes / 60;
                int minutes = totalMinutes % 60;
                return minutes > 0 ? hours + "h " + minutes + "m" : hours + "h";
            }

            int totalHours = Convert.ToInt32(Math.Ceiling(totalMinutes / 60.0));
            int days = totalHours / 24;
            int remainingHours = totalHours % 24;
            return days + "d " + remainingHours + "h";
        }

        private sealed class ResetHoverTarget
        {
            public Rectangle Bounds { get; private set; }
            public string Text { get; private set; }

            public ResetHoverTarget(Rectangle bounds, string text)
            {
                Bounds = bounds;
                Text = text ?? String.Empty;
            }
        }

        private static string FlattenError(AggregateException aggregate)
        {
            if (aggregate == null)
                return "未知错误";
            AggregateException flattened = aggregate.Flatten();
            Exception error = flattened.InnerExceptions.Count > 0 ? flattened.InnerExceptions[0] : aggregate;
            return error.Message;
        }

        private static string Shorten(string value, int maximum)
        {
            if (String.IsNullOrEmpty(value) || value.Length <= maximum)
                return value;
            return value.Substring(0, maximum - 1) + "…";
        }

    }
}
