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
        private const int DesignWidth = 328;
        private const int HeaderHeight = 58;
        private const int MeterHeight = 72;
        private const int PaceHeight = 30;
        private const int DailyUsageHeight = 116;
        private const int ModelHeaderHeight = 26;
        private const int ModelRowHeight = 36;
        private const int MaximumModelRows = 4;
        private const int BottomPadding = 10;
        private const int DockStrip = 7;
        private const float SectionTitleFontSize = 14f;
        private const float SupportingTextFontSize = 12.5f;
        private const int NormalRefreshMilliseconds = 60000;
        private const int HiddenRefreshMilliseconds = 120000;
        private const int MaximumBackoffMilliseconds = 600000;

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
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly NotifyIcon trayIcon = new NotifyIcon();
        private readonly ToolTip contextualToolTip = new ToolTip();
        private readonly List<ResetHoverTarget> resetHoverTargets = new List<ResetHoverTarget>();
        private readonly EventWaitHandle showExistingEvent;

        private ToolStripMenuItem lightItem;
        private ToolStripMenuItem darkItem;
        private ToolStripMenuItem edgeItem;
        private ToolStripMenuItem cancelTopMostItem;
        private ToolStripMenuItem startupItem;
        private ToolStripMenuItem visibilityItem;
        private UsageSnapshot snapshot;
        private WeeklyTokenReport weeklyUsage;
        private string lastError;
        private bool isConnected;
        private bool isRefreshing;
        private bool isWeeklyUsageRefreshing;
        private bool isExiting;
        private bool manuallyHidden;
        private bool dockRevealed;
        private bool isDockAnimating;
        private bool isDragging;
        private Point dragOffset;
        private DateTime? pointerLeftAt;
        private DateTime suppressRevealUntil;
        private DateTime suppressHideUntil;
        private Screen activeDockScreen;
        private Rectangle menuButtonBounds;
        private Rectangle syncButtonBounds;
        private Rectangle paceToggleBounds;
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
        private int consecutiveFailures;
        private DateTimeOffset? lastSuccessfulRefreshAt;
        private CancellationTokenSource refreshCancellation;
        private RegisteredWaitHandle showExistingWait;
        private NetworkSpeedSnapshot networkSpeed;
        private readonly bool startedWithWindows;
        private readonly Bitmap appIconBitmap;

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

            BuildMenus();
            ConfigureTimers();
            contextualToolTip.ShowAlways = true;
            contextualToolTip.AutoPopDelay = 8000;
            RestorePosition();
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
            cancelTopMostItem.Checked = CancelTopMostMenuChecked(settings.AlwaysOnTop);
            cancelTopMostItem.CheckedChanged += delegate
            {
                settings.AlwaysOnTop = AlwaysOnTopFromCancelMenu(cancelTopMostItem.Checked);
                TopMost = ShouldBeTopMost(settings.AlwaysOnTop, IsCodexForegroundOnSameScreen());
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
            foregroundTimer.Tick += delegate { UpdateForegroundTopMost(); };
            dockTimer.Interval = 50;
            dockTimer.Tick += delegate { UpdateDockPosition(); };
            statusTimer.Interval = 60000;
            statusTimer.Tick += delegate
            {
                if (Visible)
                    Invalidate();
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
        }

        private void RevealDockForStartupIfNeeded()
        {
            if (!ShouldRevealDockAtStartup(
                    startedWithWindows, settings.DockEdge, settings.EdgeAutoHide))
                return;

            manuallyHidden = false;
            dockRevealed = true;
            pointerLeftAt = null;
            suppressHideUntil = DateTime.UtcNow.AddSeconds(30);
            UpdateDockPosition();
        }

        internal static bool ShouldRevealDockAtStartup(
            bool startupLaunch, string dockEdge, bool edgeAutoHide)
        {
            return startupLaunch && !String.IsNullOrEmpty(dockEdge) && edgeAutoHide;
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
            foregroundTimer.Stop();
            dockTimer.Stop();
            statusTimer.Stop();
            networkTimer.Stop();
            networkSpeedMonitor.Reset();
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
            if (isRefreshing)
                return;

            refreshTimer.Stop();
            isRefreshing = true;
            RefreshWeeklyUsage();
            Invalidate();
            CancellationTokenSource requestCancellation = new CancellationTokenSource();
            refreshCancellation = requestCancellation;
            Task.Factory.StartNew(
                delegate { return client.Refresh(requestCancellation.Token); },
                requestCancellation.Token,
                TaskCreationOptions.None,
                TaskScheduler.Default)
                .ContinueWith(delegate(Task<UsageSnapshot> task)
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
                            isRefreshing = false;
                            if (task.IsFaulted)
                            {
                                isConnected = false;
                                lastError = FlattenError(task.Exception);
                                consecutiveFailures++;
                            }
                            else if (task.IsCanceled)
                            {
                                isConnected = false;
                                lastError = "同步已取消";
                            }
                            else
                            {
                                snapshot = task.Result;
                                isConnected = true;
                                lastError = null;
                                consecutiveFailures = 0;
                                lastSuccessfulRefreshAt = DateTimeOffset.Now;
                            }

                            if (ReferenceEquals(refreshCancellation, requestCancellation))
                                refreshCancellation = null;
                            requestCancellation.Dispose();
                            trayIcon.Text = BuildTrayText();
                            UpdateAccessibleSummary();
                            ResizeForContent();
                            ScheduleNextRefresh();
                            Invalidate();
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
                                weeklyUsage = task.Result;
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
            if (!ShouldStartManualRefresh(isRefreshing))
            {
                Invalidate();
                return;
            }

            RefreshNow();
        }

        internal static bool ShouldStartManualRefresh(bool refreshRunning)
        {
            return !refreshRunning;
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
            if (!Visible || manuallyHidden || (!String.IsNullOrEmpty(settings.DockEdge) && !dockRevealed))
                interval = HiddenRefreshMilliseconds;
            else
                interval = NormalRefreshMilliseconds;

            if (consecutiveFailures > 0)
            {
                int multiplier = 1 << Math.Min(consecutiveFailures, 3);
                interval = Math.Min(MaximumBackoffMilliseconds, interval * multiplier);
            }

            scheduledRefreshMilliseconds = interval;
            refreshTimer.Interval = interval;
            if (!isRefreshing)
                refreshTimer.Start();
        }

        private void ConfigureWindowTimers()
        {
            // This timer also monitors whether Codex/ChatGPT is foreground on
            // the card's screen, so it remains above that window when needed.
            foregroundTimer.Start();

            bool needsDockPolling = ShouldPollDock(
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

            bool shouldSampleNetwork = Visible && !manuallyHidden;
            if (shouldSampleNetwork && !networkTimer.Enabled)
            {
                networkSpeedMonitor.Reset();
                networkSpeed = networkSpeedMonitor.Sample();
                networkTimer.Start();
            }
            else if (!shouldSampleNetwork && networkTimer.Enabled)
            {
                networkTimer.Stop();
                networkSpeedMonitor.Reset();
                networkSpeed = new NetworkSpeedSnapshot(0, 0);
            }
        }

        private void UpdateNetworkSpeed()
        {
            if (!Visible || manuallyHidden)
                return;

            try
            {
                networkSpeed = networkSpeedMonitor.Sample();
            }
            catch
            {
                networkSpeedMonitor.Reset();
                networkSpeed = new NetworkSpeedSnapshot(0, 0);
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
                activeDockScreen = FindDockScreen();
                RestoreDockIfNeeded();
            }
            else
            {
                ClampToWorkingArea();
            }
            Invalidate();
        }

        private void ResizeForContent()
        {
            bool hasSnapshot = snapshot != null;
            bool hasWeeklyPace = hasSnapshot && snapshot.WeeklyPace != null;
            if (!hasWeeklyPace)
                detailsExpanded = false;

            int target = ContentHeight(hasSnapshot, hasWeeklyPace, detailsExpanded);

            if (designHeight == target)
                return;

            designHeight = target;
            ClientSize = new Size(S(DesignWidth), S(designHeight));
            UpdateRoundedRegion();
            if (!String.IsNullOrEmpty(settings.DockEdge))
            {
                activeDockScreen = activeDockScreen ?? FindDockScreen();
                if (activeDockScreen != null)
                    settings.DockTop = Math.Max(activeDockScreen.WorkingArea.Top,
                        Math.Min(Top, activeDockScreen.WorkingArea.Bottom - Height));
                UpdateDockPosition();
            }
            else
                ClampToWorkingArea();
        }

        internal static int ContentHeight(bool hasSnapshot, bool hasWeeklyPace,
            bool detailsExpanded)
        {
            if (!hasSnapshot)
                return 126;

            int compactHeight = HeaderHeight + MeterHeight + BottomPadding +
                (hasWeeklyPace ? PaceHeight : 0);
            if (!detailsExpanded)
                return compactHeight;

            return compactHeight + DailyUsageHeight + ModelHeaderHeight +
                (MaximumModelRows * ModelRowHeight);
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

            DrawGlassCard(graphics);
            if (networkOnlyPaint)
            {
                DrawNetworkSpeed(graphics);
                return;
            }
            DrawHeader(graphics);
            budgetMarkerBounds = Rectangle.Empty;
            paceToggleBounds = Rectangle.Empty;
            budgetToolTipText = String.Empty;
            resetHoverTargets.Clear();

            if (snapshot == null)
            {
                DrawEmptyState(graphics);
                if (syncButtonHovered)
                    DrawStatusToolTip(graphics);
                return;
            }

            int y = HeaderHeight;
            UsageWindow main = snapshot.Weekly ?? snapshot.Session;
            DrawMeter(graphics, main, y, true, snapshot.WeeklyPace);
            y += MeterHeight;

            if (snapshot.WeeklyPace != null)
            {
                DrawPace(graphics, snapshot.WeeklyPace, y);
                y += PaceHeight;
            }

            if (detailsExpanded)
            {
                DrawDailyUsage(graphics, y);
                y += DailyUsageHeight;
                DrawModelUsage(graphics, y);
            }

            if (budgetMarkerHovered && !String.IsNullOrEmpty(budgetToolTipText))
                DrawBudgetToolTip(graphics);
            if (syncButtonHovered)
                DrawStatusToolTip(graphics);
        }

        private void DrawGlassCard(Graphics graphics)
        {
            RectangleF bounds = new RectangleF(0.5f, 0.5f, DesignWidth - 1f, designHeight - 1f);
            using (GraphicsPath path = RoundedRectangle(bounds, 22f))
            using (LinearGradientBrush baseBrush = new LinearGradientBrush(
                bounds,
                IsDark ? Color.FromArgb(250, 22, 29, 43) : Color.FromArgb(255, 250, 253, 255),
                IsDark ? Color.FromArgb(250, 35, 43, 61) : Color.FromArgb(255, 225, 239, 248),
                28f))
            {
                graphics.FillPath(baseBrush, path);

                GraphicsState state = graphics.Save();
                graphics.SetClip(path);
                DrawAmbientGlow(graphics, new RectangleF(-74, -78, 245, 176),
                    IsDark ? Color.FromArgb(54, 21, 176, 255) : Color.FromArgb(74, 46, 198, 255));
                DrawAmbientGlow(graphics, new RectangleF(210, designHeight - 118, 208, 168),
                    IsDark ? Color.FromArgb(40, 126, 84, 255) : Color.FromArgb(48, 111, 84, 255));
                DrawTechGrid(graphics);

                using (LinearGradientBrush highlight = new LinearGradientBrush(
                    new RectangleF(0, 0, DesignWidth, Math.Max(80, designHeight)),
                    IsDark ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(188, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    graphics.FillPath(highlight, path);
                }
                graphics.Restore(state);

                using (Pen border = new Pen(
                    IsDark ? Color.FromArgb(88, 150, 214, 255) : Color.FromArgb(220, 255, 255, 255), 1f))
                {
                    graphics.DrawPath(border, path);
                }

                using (Pen innerBorder = new Pen(
                    IsDark ? Color.FromArgb(30, 104, 186, 255) : Color.FromArgb(58, 96, 178, 222), 0.7f))
                using (GraphicsPath innerPath = RoundedRectangle(
                    new RectangleF(2.5f, 2.5f, DesignWidth - 5f, designHeight - 5f), 20f))
                    graphics.DrawPath(innerBorder, innerPath);

                using (LinearGradientBrush accent = new LinearGradientBrush(
                    new RectangleF(20, 1, DesignWidth - 40, 2),
                    Color.FromArgb(0, 24, 193, 255), Color.FromArgb(190, 103, 82, 255), 0f))
                    graphics.FillRectangle(accent, 20, 1, DesignWidth - 40, 1.4f);
            }
        }

        internal static bool ShouldPollDock(bool edgeAutoHide, string dockEdge, bool visible)
        {
            return edgeAutoHide && !String.IsNullOrEmpty(dockEdge) && visible;
        }

        private void DrawAmbientGlow(Graphics graphics, RectangleF bounds, Color centerColor)
        {
            using (GraphicsPath ellipse = new GraphicsPath())
            {
                ellipse.AddEllipse(bounds);
                using (PathGradientBrush glow = new PathGradientBrush(ellipse))
                {
                    glow.CenterColor = centerColor;
                    glow.SurroundColors = new Color[] { Color.FromArgb(0, centerColor) };
                    graphics.FillPath(glow, ellipse);
                }
            }
        }

        private void DrawTechGrid(Graphics graphics)
        {
            Color gridColor = IsDark ? Color.FromArgb(12, 107, 204, 255) : Color.FromArgb(11, 19, 112, 170);
            using (Pen grid = new Pen(gridColor, 0.5f))
            {
                for (int x = 20; x < DesignWidth; x += 28)
                    graphics.DrawLine(grid, x, HeaderHeight, x, designHeight - 12);
                for (int y = HeaderHeight + 16; y < designHeight; y += 24)
                    graphics.DrawLine(grid, 12, y, DesignWidth - 12, y);
            }
        }

        private void DrawHeader(Graphics graphics)
        {
            RectangleF iconGlow = new RectangleF(10, 5, 50, 50);
            DrawAmbientGlow(graphics, iconGlow,
                IsDark ? Color.FromArgb(72, 31, 185, 255) : Color.FromArgb(62, 0, 157, 255));

            RectangleF icon = new RectangleF(17, 11, 36, 36);
            GraphicsState iconState = graphics.Save();
            using (GraphicsPath iconPath = RoundedRectangle(icon, 10f))
            {
                graphics.SetClip(iconPath);
                graphics.DrawImage(appIconBitmap, icon);
            }
            graphics.Restore(iconState);
            using (GraphicsPath iconBorderPath = RoundedRectangle(icon, 10f))
            using (Pen iconBorder = new Pen(Color.FromArgb(118, 255, 255, 255), 0.8f))
                graphics.DrawPath(iconBorder, iconBorderPath);

            RectangleF titleBounds = new RectangleF(62, 14, 94, 28);
            using (Font titleFont = FittedPixelFont(graphics, "Codex 用量", titleBounds, 18f, 15.5f, FontStyle.Bold))
            using (Brush primary = new SolidBrush(PrimaryText))
            {
                DrawText(graphics, "Codex 用量", titleFont, primary, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);
            }

            RectangleF networkTile = new RectangleF(160, 8, 70, 41);
            using (GraphicsPath tile = RoundedRectangle(networkTile, 10f))
            using (Brush tileBrush = new SolidBrush(IsDark
                ? Color.FromArgb(28, 255, 255, 255)
                : Color.FromArgb(126, 255, 255, 255)))
            using (Pen tileBorder = new Pen(IsDark
                ? Color.FromArgb(36, 98, 190, 255)
                : Color.FromArgb(50, 69, 151, 205), 0.7f))
            {
                graphics.FillPath(tileBrush, tile);
                graphics.DrawPath(tileBorder, tile);
            }
            DrawNetworkSpeed(graphics);

            RectangleF status = new RectangleF(234, 15, 58, 26);
            syncButtonBounds = new Rectangle(S(status.X), S(status.Y), S(status.Width), S(status.Height));
            using (GraphicsPath pill = RoundedRectangle(status, 13f))
            using (Brush pillBrush = new SolidBrush(syncButtonHovered
                ? (IsDark ? Color.FromArgb(55, 58, 180, 255) : Color.FromArgb(44, 0, 147, 255))
                : (IsDark ? Color.FromArgb(31, 255, 255, 255) : Color.FromArgb(137, 255, 255, 255))))
            using (Pen pillBorder = new Pen(IsDark
                ? Color.FromArgb(48, 85, 196, 255)
                : Color.FromArgb(58, 40, 152, 211), 0.7f))
            {
                graphics.FillPath(pillBrush, pill);
                graphics.DrawPath(pillBorder, pill);
            }

            Color dotColor = StatusDotColor;
            using (Brush dotGlow = new SolidBrush(Color.FromArgb(45, dotColor)))
                graphics.FillEllipse(dotGlow, 239, 20, 12, 12);
            using (Brush dot = new SolidBrush(dotColor))
                graphics.FillEllipse(dot, 242, 23, 6, 6);
            RectangleF statusTextBounds = new RectangleF(250, 17, 38, 21);
            using (Font statusFont = FittedPixelFont(
                graphics, StatusText, statusTextBounds, SectionTitleFontSize,
                SupportingTextFontSize, FontStyle.Bold))
            using (Brush secondary = new SolidBrush(SecondaryText))
                DrawText(graphics, StatusText, statusFont, secondary, statusTextBounds,
                    StringAlignment.Center, StringAlignment.Center);

            RectangleF menuSurface = new RectangleF(297, 15, 24, 26);
            menuButtonBounds = new Rectangle(S(menuSurface.X), S(menuSurface.Y), S(menuSurface.Width), S(menuSurface.Height));
            using (GraphicsPath menuPath = RoundedRectangle(menuSurface, 9f))
            using (Brush menuBrush = new SolidBrush(IsDark
                ? Color.FromArgb(24, 255, 255, 255)
                : Color.FromArgb(94, 255, 255, 255)))
                graphics.FillPath(menuBrush, menuPath);
            using (Brush dots = new SolidBrush(PrimaryText))
            {
                graphics.FillEllipse(dots, 302, 27, 2.2f, 2.2f);
                graphics.FillEllipse(dots, 308, 27, 2.2f, 2.2f);
                graphics.FillEllipse(dots, 314, 27, 2.2f, 2.2f);
            }

            using (Pen divider = new Pen(IsDark
                ? Color.FromArgb(28, 108, 184, 240)
                : Color.FromArgb(32, 51, 119, 162), 0.6f))
                graphics.DrawLine(divider, 18, HeaderHeight - 1, DesignWidth - 18, HeaderHeight - 1);
        }

        private Rectangle NetworkSpeedBounds
        {
            get { return new Rectangle(S(160), S(8), S(70), S(41)); }
        }

        private void DrawNetworkSpeed(Graphics graphics)
        {
            string download = "↓ " + NetworkSpeedMonitor.FormatRate(networkSpeed.DownloadBytesPerSecond);
            string upload = "↑ " + NetworkSpeedMonitor.FormatRate(networkSpeed.UploadBytesPerSecond);
            Color downloadColor = IsDark ? Color.FromArgb(92, 211, 255) : Color.FromArgb(0, 125, 204);
            Color uploadColor = IsDark ? Color.FromArgb(179, 157, 255) : Color.FromArgb(102, 74, 207);

            RectangleF downloadBounds = new RectangleF(165, 9, 62, 18);
            RectangleF uploadBounds = new RectangleF(165, 29, 62, 18);
            float speedSize = Math.Min(
                FittedPixelFontSize(graphics, download, downloadBounds, 11f, 7.8f, FontStyle.Bold),
                FittedPixelFontSize(graphics, upload, uploadBounds, 11f, 7.8f, FontStyle.Bold));
            using (Font speedFont = PixelFont(speedSize, FontStyle.Bold))
            using (Brush downloadBrush = new SolidBrush(downloadColor))
            using (Brush uploadBrush = new SolidBrush(uploadColor))
            {
                DrawText(graphics, download, speedFont, downloadBrush, downloadBounds,
                    StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, upload, speedFont, uploadBrush, uploadBounds,
                    StringAlignment.Near, StringAlignment.Center);
            }
        }

        private void DrawEmptyState(Graphics graphics)
        {
            string message = String.IsNullOrWhiteSpace(lastError) ? "正在连接 CodexBar…" : lastError;
            RectangleF surface = new RectangleF(14, 66, DesignWidth - 28, 40);
            using (GraphicsPath path = RoundedRectangle(surface, 12f))
            using (Brush fill = new SolidBrush(IsDark
                ? Color.FromArgb(24, 255, 255, 255)
                : Color.FromArgb(132, 255, 255, 255)))
            using (Pen border = new Pen(IsDark
                ? Color.FromArgb(34, 91, 178, 236)
                : Color.FromArgb(42, 61, 143, 199), 0.7f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            RectangleF messageBounds = new RectangleF(27, 66, DesignWidth - 54, 40);
            using (Font font = FittedPixelFont(graphics, message, messageBounds, 13f, 10f, FontStyle.Regular))
            using (Brush brush = new SolidBrush(String.IsNullOrWhiteSpace(lastError) ? SecondaryText : Color.FromArgb(218, 112, 0)))
                DrawText(graphics, message, font, brush, messageBounds,
                    StringAlignment.Near, StringAlignment.Center);
        }

        private void DrawMeter(Graphics graphics, UsageWindow window, int y, bool prominent, PaceInfo pace)
        {
            if (window == null)
                return;

            string title = prominent
                ? "每周额度"
                : (String.Equals(window.Title, "Spark", StringComparison.OrdinalIgnoreCase)
                    ? "Spark额度"
                    : window.Title);
            string reset = ResetText(window);
            string remaining = "剩余 " + Math.Round(window.RemainingPercent).ToString("0") + "%";
            string weeklyTokens = weeklyUsage != null && weeklyUsage.TotalTokens > 0
                ? "本周 " + WeeklyUsageReader.FormatTokenCount(weeklyUsage.TotalTokens) + " token"
                : (isWeeklyUsageRefreshing ? "正在统计本周 token…" : "本周暂无本机记录");

            RectangleF panel = new RectangleF(10, y + 1, DesignWidth - 20, MeterHeight - 3);
            using (GraphicsPath panelPath = RoundedRectangle(panel, 14f))
            using (Brush panelBrush = new SolidBrush(IsDark
                ? Color.FromArgb(prominent ? 31 : 21, 255, 255, 255)
                : Color.FromArgb(prominent ? 164 : 105, 255, 255, 255)))
            using (Pen panelBorder = new Pen(IsDark
                ? Color.FromArgb(prominent ? 44 : 28, 91, 178, 236)
                : Color.FromArgb(prominent ? 48 : 30, 59, 136, 191), 0.7f))
            {
                graphics.FillPath(panelBrush, panelPath);
                graphics.DrawPath(panelBorder, panelPath);
            }

            RectangleF titleBounds = new RectangleF(20, y + 4, 112, 22);
            RectangleF resetBounds = new RectangleF(160, y + 4, 148, 22);
            RectangleF remainingBounds = new RectangleF(20, y + 27, 124, 25);
            RectangleF tokenBounds = new RectangleF(139, y + 29, 169, 22);
            if (!String.IsNullOrEmpty(reset))
            {
                resetHoverTargets.Add(new ResetHoverTarget(
                    new Rectangle(S(resetBounds.X), S(resetBounds.Y),
                        S(resetBounds.Width), S(resetBounds.Height)),
                    ResetToolTipText(window)));
            }
            using (Font titleFont = PixelFont(SectionTitleFontSize, FontStyle.Bold))
            using (Font resetFont = PixelFont(SupportingTextFontSize, FontStyle.Regular))
            using (Font remainingFont = FittedPixelFont(
                graphics, remaining, remainingBounds, 19f, 15f, FontStyle.Bold))
            using (Font tokenFont = FittedPixelFont(
                graphics, weeklyTokens, tokenBounds, 11.5f, 8.5f, FontStyle.Bold))
            using (Brush primary = new SolidBrush(PrimaryText))
            using (Brush secondary = new SolidBrush(prominent ? TertiaryText : SecondaryText))
            {
                DrawText(graphics, title, titleFont, prominent ? primary : secondary, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, remaining, remainingFont, primary, remainingBounds,
                    StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, weeklyTokens, tokenFont, secondary, tokenBounds,
                    StringAlignment.Far, StringAlignment.Center);
                if (!String.IsNullOrEmpty(reset))
                {
                    DrawText(graphics, reset, resetFont, secondary, resetBounds,
                        StringAlignment.Far, StringAlignment.Center);
                }
            }

            RectangleF track = new RectangleF(20, y + 57, DesignWidth - 40, prominent ? 9f : 7f);
            using (GraphicsPath path = RoundedRectangle(track, track.Height / 2f))
            using (Brush trackBrush = new SolidBrush(IsDark
                ? Color.FromArgb(38, 255, 255, 255)
                : Color.FromArgb(28, 29, 74, 105)))
                graphics.FillPath(trackBrush, path);

            float fillWidth = Math.Max(4f, (float)(track.Width * window.RemainingPercent / 100.0));
            fillWidth = Math.Min(track.Width, fillWidth);
            RectangleF fill = new RectangleF(track.X, track.Y, fillWidth, track.Height);
            Color start;
            Color end;
            BarColors(window.RemainingPercent, prominent, out start, out end);
            RectangleF glowBounds = new RectangleF(fill.X - 1, fill.Y - 1.5f, fill.Width + 2, fill.Height + 3);
            using (GraphicsPath glowPath = RoundedRectangle(glowBounds, glowBounds.Height / 2f))
            using (Brush glowBrush = new SolidBrush(Color.FromArgb(IsDark ? 34 : 26, end)))
                graphics.FillPath(glowBrush, glowPath);
            using (GraphicsPath fillPath = RoundedRectangle(fill, fill.Height / 2f))
            using (LinearGradientBrush fillBrush = new LinearGradientBrush(fill, start, end, 0f))
                graphics.FillPath(fillBrush, fillPath);
            using (Pen highlight = new Pen(Color.FromArgb(92, 255, 255, 255), 0.7f))
                graphics.DrawLine(highlight, track.X + 4, track.Y + 1, track.X + Math.Max(4, fillWidth - 4), track.Y + 1);

            if (prominent && pace != null)
            {
                double expectedRemaining = Math.Max(0, Math.Min(100, 100 - pace.ExpectedUsedPercent));
                float markerX = track.X + (float)(track.Width * expectedRemaining / 100.0);
                bool overBudget = pace.DeltaPercent > 0.5;
                Color markerColor = overBudget ? Color.FromArgb(255, 116, 48) : Color.FromArgb(105, 76, 255);
                using (Pen markerGlow = new Pen(Color.FromArgb(46, markerColor), 7f))
                    graphics.DrawLine(markerGlow, markerX, track.Y - 3, markerX, track.Bottom + 3);
                using (Pen marker = new Pen(markerColor, 2f))
                    graphics.DrawLine(marker, markerX, track.Y - 4, markerX, track.Bottom + 4);
                PointF[] pointer = new PointF[]
                {
                    new PointF(markerX, track.Y - 1),
                    new PointF(markerX - 3.4f, track.Y - 5.6f),
                    new PointF(markerX + 3.4f, track.Y - 5.6f)
                };
                using (Brush pointerBrush = new SolidBrush(markerColor))
                    graphics.FillPolygon(pointerBrush, pointer);

                budgetMarkerBounds = new Rectangle(
                    S(markerX - 8f), S(track.Y - 10f), S(16f), S(track.Height + 20f));
                budgetMarkerDesignX = markerX;
                budgetToolTipText = "预算线 " + Math.Round(pace.ExpectedUsedPercent).ToString("0") + "%";
            }

        }

        private void DrawDailyUsage(Graphics graphics, int y)
        {
            RectangleF titleBounds = new RectangleF(20, y + 2, 170, 22);
            using (Font titleFont = PixelFont(SectionTitleFontSize, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(PrimaryText))
                DrawText(graphics, "近7天每日", titleFont, titleBrush, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);

            string stateText = null;
            if (weeklyUsage == null && isWeeklyUsageRefreshing)
                stateText = "正在读取本机会话统计…";
            else if (weeklyUsage != null && !String.IsNullOrWhiteSpace(weeklyUsage.Error))
                stateText = "统计暂不可用";

            if (!String.IsNullOrEmpty(stateText))
            {
                RectangleF stateBounds = new RectangleF(176, y + 3, 132, 20);
                using (Font stateFont = FittedPixelFont(
                    graphics, stateText, stateBounds, 10f, 8f, FontStyle.Regular))
                using (Brush stateBrush = new SolidBrush(TertiaryText))
                    DrawText(graphics, stateText, stateFont, stateBrush, stateBounds,
                        StringAlignment.Far, StringAlignment.Center);
            }

            List<DailyTokenUsage> days = DisplayDays();
            long maximum = days.Count == 0 ? 0 : days.Max(item => item.Tokens);
            double usedPercent = snapshot == null || snapshot.Weekly == null
                ? 0
                : snapshot.Weekly.UsedPercent;
            long total = weeklyUsage == null ? 0 : weeklyUsage.TotalTokens;

            for (int index = 0; index < 7; index++)
            {
                DailyTokenUsage day = days[index];
                float x = 20f + (index * 42f);
                RectangleF percentBounds = new RectangleF(x - 3f, y + 23, 38f, 17f);
                string percent = DailyQuotaPercent(day.Tokens, total, usedPercent)
                    .ToString("0.0") + "%";
                using (Font percentFont = FittedPixelFont(
                    graphics, percent, percentBounds, 10.5f, 8f, FontStyle.Bold))
                using (Brush percentBrush = new SolidBrush(PrimaryText))
                    DrawText(graphics, percent, percentFont, percentBrush, percentBounds,
                        StringAlignment.Center, StringAlignment.Center);

                RectangleF track = new RectangleF(x, y + 42, 32, 43);
                using (GraphicsPath trackPath = RoundedRectangle(track, 15f))
                using (Brush trackBrush = new SolidBrush(IsDark
                    ? Color.FromArgb(30, 255, 255, 255)
                    : Color.FromArgb(24, 26, 58, 83)))
                    graphics.FillPath(trackBrush, trackPath);

                if (day.Tokens > 0 && maximum > 0)
                {
                    float fillHeight = Math.Max(3f, (float)(track.Height * day.Tokens / (double)maximum));
                    RectangleF fill = new RectangleF(track.X, track.Bottom - fillHeight,
                        track.Width, fillHeight);
                    using (GraphicsPath fillPath = RoundedRectangle(fill,
                        Math.Min(15f, Math.Max(1.5f, fillHeight / 2f))))
                    using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                        fill, Color.FromArgb(65, 211, 239), Color.FromArgb(86, 102, 255), 90f))
                    {
                        graphics.FillPath(fillBrush, fillPath);
                    }
                }

                RectangleF accent = new RectangleF(track.X, track.Bottom - 2.5f, track.Width, 2.5f);
                using (LinearGradientBrush accentBrush = new LinearGradientBrush(
                    accent, Color.FromArgb(53, 207, 235), Color.FromArgb(91, 86, 255), 0f))
                    graphics.FillRectangle(accentBrush, accent);

                string dayLabel = day.Day.Date == DateTime.Now.Date
                    ? "今"
                    : ChineseWeekday(day.Day.DayOfWeek);
                RectangleF dayBounds = new RectangleF(x - 2f, y + 87, 36f, 16f);
                RectangleF tokenBounds = new RectangleF(x - 4f, y + 101, 40f, 14f);
                using (Font dayFont = PixelFont(10.5f, FontStyle.Bold))
                using (Font tokenFont = FittedPixelFont(
                    graphics, WeeklyUsageReader.FormatTokenCount(day.Tokens), tokenBounds,
                    8.5f, 7f, FontStyle.Regular))
                using (Brush dayBrush = new SolidBrush(SecondaryText))
                using (Brush tokenBrush = new SolidBrush(TertiaryText))
                {
                    DrawText(graphics, dayLabel, dayFont, dayBrush, dayBounds,
                        StringAlignment.Center, StringAlignment.Center);
                    DrawText(graphics, WeeklyUsageReader.FormatTokenCount(day.Tokens), tokenFont,
                        tokenBrush, tokenBounds, StringAlignment.Center, StringAlignment.Center);
                }
            }
        }

        private void DrawModelUsage(Graphics graphics, int y)
        {
            RectangleF titleBounds = new RectangleF(20, y + 1, 150, 23);
            using (Font titleFont = PixelFont(SectionTitleFontSize, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(PrimaryText))
                DrawText(graphics, "模型偏好", titleFont, titleBrush, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);

            List<ModelTokenUsage> rows = VisibleModelRows(
                weeklyUsage == null ? null : weeklyUsage.Models, MaximumModelRows);
            if (rows.Count == 0 || weeklyUsage == null || weeklyUsage.TotalTokens <= 0)
            {
                RectangleF empty = new RectangleF(14, y + ModelHeaderHeight,
                    DesignWidth - 28, (MaximumModelRows * ModelRowHeight) - 8);
                using (GraphicsPath path = RoundedRectangle(empty, 14f))
                using (Brush fill = new SolidBrush(IsDark
                    ? Color.FromArgb(24, 255, 255, 255)
                    : Color.FromArgb(116, 255, 255, 255)))
                using (Pen border = new Pen(IsDark
                    ? Color.FromArgb(28, 91, 178, 236)
                    : Color.FromArgb(32, 59, 136, 191), 0.7f))
                {
                    graphics.FillPath(fill, path);
                    graphics.DrawPath(border, path);
                }
                string message = weeklyUsage != null && !String.IsNullOrWhiteSpace(weeklyUsage.Error)
                    ? weeklyUsage.Error
                    : (isWeeklyUsageRefreshing ? "正在统计模型与推理强度…" : "近 7 天暂无本机会话记录");
                RectangleF messageBounds = new RectangleF(empty.X + 12, empty.Y,
                    empty.Width - 24, empty.Height);
                using (Font font = FittedPixelFont(
                    graphics, message, messageBounds, 11f, 8.5f, FontStyle.Regular))
                using (Brush brush = new SolidBrush(TertiaryText))
                    DrawText(graphics, message, font, brush, messageBounds,
                        StringAlignment.Center, StringAlignment.Center);
                return;
            }

            for (int index = 0; index < rows.Count; index++)
            {
                ModelTokenUsage row = rows[index];
                float rowY = y + ModelHeaderHeight + (index * ModelRowHeight) + 2;
                RectangleF surface = new RectangleF(14, rowY, DesignWidth - 28, ModelRowHeight - 5);
                using (GraphicsPath path = RoundedRectangle(surface, 13f))
                using (Brush fill = new SolidBrush(IsDark
                    ? Color.FromArgb(30, 255, 255, 255)
                    : Color.FromArgb(128, 255, 255, 255)))
                using (Pen border = new Pen(IsDark
                    ? Color.FromArgb(24, 108, 184, 240)
                    : Color.FromArgb(28, 59, 136, 191), 0.6f))
                {
                    graphics.FillPath(fill, path);
                    graphics.DrawPath(border, path);
                }

                double sharePercent = row.Tokens * 100d / weeklyUsage.TotalTokens;
                Color accent = UsageAccent(sharePercent, IsDark);
                string label = ModelLabel(row);
                string percentage = sharePercent.ToString("0.0") + "%";
                string tokens = WeeklyUsageReader.FormatTokenCount(row.Tokens);
                RectangleF labelBounds = new RectangleF(25, rowY + 1, 174, surface.Height - 2);
                RectangleF percentBounds = new RectangleF(197, rowY + 1, 57, surface.Height - 2);
                RectangleF tokenBounds = new RectangleF(258, rowY + 1, 47, surface.Height - 2);
                using (Font labelFont = FittedPixelFont(
                    graphics, label, labelBounds, 11.5f, 8.5f, FontStyle.Bold))
                using (Font percentFont = FittedPixelFont(
                    graphics, percentage, percentBounds, 15f, 11f, FontStyle.Bold))
                using (Font tokenFont = FittedPixelFont(
                    graphics, tokens, tokenBounds, 10.5f, 8f, FontStyle.Bold))
                using (Brush labelBrush = new SolidBrush(accent))
                using (Brush percentBrush = new SolidBrush(accent))
                using (Brush tokenBrush = new SolidBrush(Color.FromArgb(210, accent)))
                {
                    DrawText(graphics, label, labelFont, labelBrush, labelBounds,
                        StringAlignment.Near, StringAlignment.Center);
                    DrawText(graphics, percentage, percentFont, percentBrush, percentBounds,
                        StringAlignment.Far, StringAlignment.Center);
                    DrawText(graphics, tokens, tokenFont, tokenBrush, tokenBounds,
                        StringAlignment.Far, StringAlignment.Center);
                }
            }
        }

        private List<DailyTokenUsage> DisplayDays()
        {
            if (weeklyUsage != null && weeklyUsage.Days != null && weeklyUsage.Days.Count == 7)
                return weeklyUsage.Days;

            List<DailyTokenUsage> days = new List<DailyTokenUsage>();
            DateTime firstDay = DateTime.Now.Date.AddDays(-6);
            for (int offset = 0; offset < 7; offset++)
                days.Add(new DailyTokenUsage { Day = firstDay.AddDays(offset), Tokens = 0 });
            return days;
        }

        internal static double DailyQuotaPercent(long dailyTokens, long totalTokens, double usedPercent)
        {
            if (dailyTokens <= 0 || totalTokens <= 0 || usedPercent <= 0)
                return 0;
            return Math.Max(0, usedPercent * dailyTokens / totalTokens);
        }

        internal static List<ModelTokenUsage> VisibleModelRows(
            IEnumerable<ModelTokenUsage> models, int maximumRows)
        {
            List<ModelTokenUsage> sorted = (models ?? new ModelTokenUsage[0])
                .Where(item => item != null && item.Tokens > 0)
                .OrderByDescending(item => item.Tokens)
                .ToList();
            if (maximumRows <= 0)
                return new List<ModelTokenUsage>();
            if (sorted.Count <= maximumRows)
                return sorted;

            List<ModelTokenUsage> visible = sorted.Take(maximumRows - 1).ToList();
            visible.Add(new ModelTokenUsage
            {
                Model = "other",
                Tokens = sorted.Skip(maximumRows - 1).Sum(item => item.Tokens)
            });
            return visible;
        }

        internal static string ModelLabel(ModelTokenUsage usage)
        {
            if (usage == null)
                return String.Empty;
            if (String.Equals(usage.Model, "other", StringComparison.OrdinalIgnoreCase))
                return "其他";

            List<string> parts = new List<string>();
            parts.Add(WeeklyUsageReader.DisplayModelName(usage.Model));
            string mode = DisplayCollaborationMode(usage.CollaborationMode);
            if (!String.IsNullOrEmpty(mode) &&
                !String.Equals(mode, "Default", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(mode);
            }
            string effort = WeeklyUsageReader.DisplayEffort(usage.Effort);
            if (!String.IsNullOrEmpty(effort))
                parts.Add(effort);
            return String.Join(" · ", parts.ToArray());
        }

        private static string DisplayCollaborationMode(string mode)
        {
            if (String.IsNullOrWhiteSpace(mode))
                return String.Empty;
            string[] words = mode.Replace('-', ' ').Replace('_', ' ')
                .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < words.Length; index++)
                words[index] = Char.ToUpperInvariant(words[index][0]) + words[index].Substring(1);
            return String.Join(" ", words);
        }

        private static string ChineseWeekday(DayOfWeek day)
        {
            string[] labels = new string[] { "日", "一", "二", "三", "四", "五", "六" };
            return labels[(int)day];
        }

        internal static Color UsageAccent(double percentage, bool darkTheme)
        {
            double clamped = Math.Max(0d, Math.Min(100d, percentage));
            Color low = darkTheme
                ? Color.FromArgb(136, 150, 166)
                : Color.FromArgb(100, 119, 139);
            Color middle = darkTheme
                ? Color.FromArgb(67, 177, 223)
                : Color.FromArgb(39, 157, 210);
            Color high = darkTheme
                ? Color.FromArgb(67, 215, 255)
                : Color.FromArgb(20, 92, 245);

            if (clamped <= 40d)
                return InterpolateColor(low, middle, clamped / 40d);
            return InterpolateColor(middle, high, (clamped - 40d) / 60d);
        }

        private static Color InterpolateColor(Color start, Color end, double amount)
        {
            amount = Math.Max(0d, Math.Min(1d, amount));
            return Color.FromArgb(
                Convert.ToInt32(Math.Round(start.R + ((end.R - start.R) * amount))),
                Convert.ToInt32(Math.Round(start.G + ((end.G - start.G) * amount))),
                Convert.ToInt32(Math.Round(start.B + ((end.B - start.B) * amount))));
        }

        private void DrawBudgetToolTip(Graphics graphics)
        {
            const float width = 88f;
            const float height = 24f;
            float x = Math.Max(14f, Math.Min(DesignWidth - width - 14f, budgetMarkerDesignX - width / 2f));
            float y = HeaderHeight + 3f;
            RectangleF bounds = new RectangleF(x, y, width, height);

            using (GraphicsPath path = RoundedRectangle(bounds, 9f))
            using (Brush background = new SolidBrush(IsDark
                ? Color.FromArgb(35, 49, 76)
                : Color.FromArgb(35, 54, 82)))
            using (Pen border = new Pen(Color.FromArgb(170, 105, 154, 255), 0.8f))
            {
                graphics.FillPath(background, path);
                graphics.DrawPath(border, path);
            }

            PointF[] pointer = new PointF[]
            {
                new PointF(budgetMarkerDesignX, bounds.Bottom + 5f),
                new PointF(budgetMarkerDesignX - 4f, bounds.Bottom - 0.5f),
                new PointF(budgetMarkerDesignX + 4f, bounds.Bottom - 0.5f)
            };
            using (Brush pointerBrush = new SolidBrush(IsDark
                ? Color.FromArgb(35, 49, 76)
                : Color.FromArgb(35, 54, 82)))
                graphics.FillPolygon(pointerBrush, pointer);

            using (Font font = PixelFont(11f, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
                DrawText(graphics, budgetToolTipText, font, textBrush, bounds,
                    StringAlignment.Center, StringAlignment.Center);
        }

        private void DrawStatusToolTip(Graphics graphics)
        {
            string text = StatusToolTipText();
            if (String.IsNullOrWhiteSpace(text))
                return;

            const float width = 124f;
            const float height = 24f;
            const float anchorX = 263f;
            RectangleF bounds = new RectangleF(DesignWidth - width - 14f, 50f, width, height);

            UsageWindow main = snapshot == null ? null : snapshot.Weekly ?? snapshot.Session;
            Color start;
            Color end;
            BarColors(main == null ? 100 : main.RemainingPercent, true, out start, out end);
            Color fillStart = Color.FromArgb(IsDark ? 238 : 226, start);
            Color fillEnd = Color.FromArgb(IsDark ? 238 : 226, end);

            PointF[] pointer = new PointF[]
            {
                new PointF(anchorX, bounds.Y - 5f),
                new PointF(anchorX - 4.5f, bounds.Y + 0.8f),
                new PointF(anchorX + 4.5f, bounds.Y + 0.8f)
            };
            using (Brush pointerBrush = new SolidBrush(fillEnd))
                graphics.FillPolygon(pointerBrush, pointer);

            using (GraphicsPath shadowPath = RoundedRectangle(
                new RectangleF(bounds.X, bounds.Y + 1.8f, bounds.Width, bounds.Height), 10f))
            using (Brush shadow = new SolidBrush(Color.FromArgb(IsDark ? 72 : 42, 12, 28, 50)))
                graphics.FillPath(shadow, shadowPath);

            using (GraphicsPath path = RoundedRectangle(bounds, 10f))
            using (LinearGradientBrush background = new LinearGradientBrush(
                bounds, fillStart, fillEnd, 0f))
            using (Pen border = new Pen(Color.FromArgb(150, 255, 255, 255), 0.8f))
            {
                graphics.FillPath(background, path);
                graphics.DrawPath(border, path);
            }

            RectangleF textBounds = new RectangleF(bounds.X + 6f, bounds.Y, bounds.Width - 12f, bounds.Height);
            using (Font font = FittedPixelFont(graphics, text, textBounds, 11.5f, 9.5f, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
                DrawText(graphics, text, font, textBrush, textBounds,
                    StringAlignment.Center, StringAlignment.Center);
        }

        private void DrawPace(Graphics graphics, PaceInfo pace, int y)
        {
            string left = pace.DeltaPercent > 0.5
                ? "超额 " + Math.Round(pace.DeltaPercent).ToString("0") + "%"
                : "节奏正常";
            string right;
            if (!pace.IsTrendStable)
                right = "趋势不足，暂不预测";
            else if (pace.WillLastToReset)
                right = "平均估算可用至重置";
            else if (pace.EtaSeconds.HasValue)
                right = "平均估算 " + Duration(pace.EtaSeconds.Value) + " 后耗尽";
            else
                right = "暂无消耗趋势";

            bool overBudget = pace.DeltaPercent > 0.5;
            Color stateColor = overBudget ? Color.FromArgb(255, 132, 38) : Color.FromArgb(18, 183, 127);
            RectangleF surface = new RectangleF(14, y + 2, DesignWidth - 28, 25);
            paceToggleBounds = new Rectangle(
                S(surface.X), S(surface.Y), S(surface.Width), S(surface.Height));
            using (GraphicsPath surfacePath = RoundedRectangle(surface, 12.5f))
            using (Brush surfaceBrush = new SolidBrush(paceToggleHovered
                ? (IsDark ? Color.FromArgb(43, 80, 174, 235) : Color.FromArgb(178, 255, 255, 255))
                : (IsDark ? Color.FromArgb(23, 255, 255, 255) : Color.FromArgb(116, 255, 255, 255))))
            using (Pen surfaceBorder = new Pen(IsDark
                ? Color.FromArgb(30, 91, 178, 236)
                : Color.FromArgb(35, 59, 136, 191), 0.7f))
            {
                graphics.FillPath(surfaceBrush, surfacePath);
                graphics.DrawPath(surfaceBorder, surfacePath);
            }

            using (Brush dotGlow = new SolidBrush(Color.FromArgb(38, stateColor)))
                graphics.FillEllipse(dotGlow, 20, y + 8, 12, 12);
            using (Brush dot = new SolidBrush(stateColor))
                graphics.FillEllipse(dot, 23, y + 11, 6, 6);

            RectangleF stateBounds = new RectangleF(35, y + 3, 96, 23);
            RectangleF forecastBounds = PaceForecastBounds(y);
            using (Font font = PixelFont(SupportingTextFontSize, FontStyle.Bold))
            using (Font forecastFont = FittedPixelFont(
                graphics, right, forecastBounds, SupportingTextFontSize, 10.5f, FontStyle.Regular))
            using (Brush leftBrush = new SolidBrush(overBudget ? stateColor : PrimaryText))
            using (Brush rightBrush = new SolidBrush(SecondaryText))
            {
                DrawText(graphics, left, font, leftBrush, stateBounds,
                    StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, right, forecastFont, rightBrush, forecastBounds,
                    StringAlignment.Far, StringAlignment.Center);
            }

            float chevronY = y + 14.5f;
            using (Pen chevron = new Pen(paceToggleHovered ? stateColor : SecondaryText, 1.6f))
            {
                chevron.StartCap = LineCap.Round;
                chevron.EndCap = LineCap.Round;
                if (detailsExpanded)
                {
                    graphics.DrawLine(chevron, 302f, chevronY + 2f, 306f, chevronY - 2f);
                    graphics.DrawLine(chevron, 306f, chevronY - 2f, 310f, chevronY + 2f);
                }
                else
                {
                    graphics.DrawLine(chevron, 302f, chevronY - 2f, 306f, chevronY + 2f);
                    graphics.DrawLine(chevron, 306f, chevronY + 2f, 310f, chevronY - 2f);
                }
            }
        }

        internal static RectangleF PaceForecastBounds(int y)
        {
            return new RectangleF(132, y + 3, DesignWidth - 166, 23);
        }

        private void OnCardMouseClick(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left && syncButtonBounds.Contains(eventArgs.Location))
                RequestManualRefresh();
            else if (eventArgs.Button == MouseButtons.Left && menuButtonBounds.Contains(eventArgs.Location))
                menu.Show(this, new Point(menuButtonBounds.Right, menuButtonBounds.Bottom), ToolStripDropDownDirection.BelowLeft);
            else if (eventArgs.Button == MouseButtons.Left && paceToggleBounds.Contains(eventArgs.Location))
                ToggleDetails();
            else if (eventArgs.Button == MouseButtons.Right)
                menu.Show(this, eventArgs.Location);
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
            if (eventArgs.Button != MouseButtons.Left || menuButtonBounds.Contains(eventArgs.Location) ||
                syncButtonBounds.Contains(eventArgs.Location) || paceToggleBounds.Contains(eventArgs.Location))
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
                bool hovered = syncButtonBounds.Contains(eventArgs.Location);
                bool paceHovered = paceToggleBounds.Contains(eventArgs.Location);
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
                Cursor = hovered || paceHovered || menuButtonBounds.Contains(eventArgs.Location)
                    ? Cursors.Hand
                    : (budgetHovered || !String.IsNullOrEmpty(resetToolTipText)
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
            if (leftDistance > threshold && rightDistance > threshold)
                return;

            activeDockScreen = screen;
            settings.DockScreen = screen.DeviceName;
            settings.DockEdge = leftDistance <= rightDistance ? "left" : "right";
            settings.DockTop = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
            dockRevealed = false;
            suppressRevealUntil = DateTime.UtcNow.AddMilliseconds(550);
            pointerLeftAt = null;
            SaveSettings();
            ConfigureWindowTimers();
            ScheduleNextRefresh();
            UpdateDockPosition();
        }

        private void UpdateDockPosition()
        {
            if (String.IsNullOrEmpty(settings.DockEdge) || !settings.EdgeAutoHide || !Visible)
                return;

            Screen screen = activeDockScreen ?? FindDockScreen();
            if (screen == null)
                return;
            activeDockScreen = screen;
            Rectangle area = screen.WorkingArea;
            int top = settings.DockTop.HasValue ? settings.DockTop.Value : Top;
            top = Math.Max(area.Top, Math.Min(top, area.Bottom - Height));

            Point cursor = Cursor.Position;
            bool cursorOnThisScreen = screen.Bounds.Contains(cursor);
            bool atStrip = cursorOnThisScreen && cursor.Y >= top - S(5) && cursor.Y <= top + Height + S(5) &&
                ((settings.DockEdge == "left" && cursor.X <= area.Left + S(11)) ||
                 (settings.DockEdge == "right" && cursor.X >= area.Right - S(11)));
            Rectangle hoverBounds = Bounds;
            hoverBounds.Inflate(S(8), S(8));
            bool overRevealedCard = dockRevealed && hoverBounds.Contains(cursor);

            if (!dockRevealed && DateTime.UtcNow >= suppressRevealUntil && atStrip)
            {
                dockRevealed = true;
                pointerLeftAt = null;
            }
            else if (dockRevealed && DateTime.UtcNow < suppressHideUntil)
            {
                pointerLeftAt = null;
            }
            else if (dockRevealed && (overRevealedCard || atStrip || menu.Visible))
            {
                pointerLeftAt = null;
            }
            else if (dockRevealed)
            {
                if (!pointerLeftAt.HasValue)
                    pointerLeftAt = DateTime.UtcNow;
                else if ((DateTime.UtcNow - pointerLeftAt.Value).TotalMilliseconds >= 220)
                {
                    dockRevealed = false;
                    pointerLeftAt = null;
                }
            }

            int targetX;
            if (settings.DockEdge == "left")
                targetX = dockRevealed ? area.Left + S(7) : area.Left - Width + S(DockStrip);
            else
                targetX = dockRevealed ? area.Right - Width - S(7) : area.Right - S(DockStrip);

            int delta = targetX - Left;
            int nextX = Math.Abs(delta) <= S(1) ? targetX : Left + Math.Sign(delta) * Math.Max(S(2), Math.Abs(delta) / 3);
            isDockAnimating = Left != nextX || Top != top;
            dockTimer.Interval = isDockAnimating ? 16 : 50;
            if (isDockAnimating)
                Location = new Point(nextX, top);
        }

        private void RestoreDockIfNeeded()
        {
            if (String.IsNullOrEmpty(settings.DockEdge) || !settings.EdgeAutoHide)
                return;

            activeDockScreen = FindDockScreen();
            if (activeDockScreen == null)
            {
                ClearDock(true);
                return;
            }

            Rectangle area = activeDockScreen.WorkingArea;
            int top = settings.DockTop.HasValue ? settings.DockTop.Value : area.Top + S(20);
            top = Math.Max(area.Top, Math.Min(top, area.Bottom - Height));
            Left = settings.DockEdge == "left" ? area.Left - Width + S(DockStrip) : area.Right - S(DockStrip);
            Top = top;
            dockRevealed = false;
            suppressRevealUntil = DateTime.UtcNow.AddMilliseconds(450);
            ConfigureWindowTimers();
        }

        private Screen FindDockScreen()
        {
            if (!String.IsNullOrEmpty(settings.DockScreen))
            {
                Screen match = Screen.AllScreens.FirstOrDefault(delegate(Screen item)
                {
                    return String.Equals(item.DeviceName, settings.DockScreen, StringComparison.OrdinalIgnoreCase);
                });
                if (match != null)
                    return match;
            }
            return Screen.FromRectangle(Bounds);
        }

        private void ClearDock(bool moveOnScreen)
        {
            if (String.IsNullOrEmpty(settings.DockEdge))
                return;

            string edge = settings.DockEdge;
            Screen screen = activeDockScreen ?? FindDockScreen();
            settings.DockEdge = null;
            settings.DockTop = null;
            settings.DockScreen = null;
            activeDockScreen = null;
            dockRevealed = false;
            isDockAnimating = false;
            pointerLeftAt = null;
            ConfigureWindowTimers();
            ScheduleNextRefresh();

            if (moveOnScreen && screen != null)
            {
                Rectangle area = screen.WorkingArea;
                int x = edge == "left" ? area.Left + S(12) : area.Right - Width - S(12);
                int y = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
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

        private void UpdateForegroundTopMost()
        {
            bool codexOnSameScreen = IsCodexForegroundOnSameScreen();
            bool effectiveTopMost = ShouldBeTopMost(settings.AlwaysOnTop, codexOnSameScreen);
            if (TopMost != effectiveTopMost)
                TopMost = effectiveTopMost;
        }

        private void ShowMeter()
        {
            manuallyHidden = false;
            Show();
            TopMost = ShouldBeTopMost(settings.AlwaysOnTop, IsCodexForegroundOnSameScreen());
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
                dockRevealed = true;
                pointerLeftAt = null;
                suppressHideUntil = DateTime.UtcNow.AddSeconds(2);
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

        private bool IsCodexForegroundOnSameScreen()
        {
            IntPtr codexWindow = NativeMethods.ForegroundCodexWindow();
            if (codexWindow == IntPtr.Zero || !Visible)
                return false;

            Screen codexScreen = Screen.FromHandle(codexWindow);
            Screen meterScreen = Screen.FromRectangle(Bounds);
            return codexScreen != null && meterScreen != null &&
                String.Equals(codexScreen.DeviceName, meterScreen.DeviceName,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldBeTopMost(bool alwaysOnTop, bool codexForegroundOnSameScreen)
        {
            return alwaysOnTop || codexForegroundOnSameScreen;
        }

        internal static bool CancelTopMostMenuChecked(bool alwaysOnTop)
        {
            return !alwaysOnTop;
        }

        internal static bool AlwaysOnTopFromCancelMenu(bool cancelTopMost)
        {
            return !cancelTopMost;
        }

        private void SyncMenuChecks()
        {
            visibilityItem.Text = Visible ? "最小化到托盘" : "显示悬浮卡片";
            lightItem.Checked = !IsDark;
            darkItem.Checked = IsDark;
            edgeItem.Checked = settings.EdgeAutoHide;
            cancelTopMostItem.Checked = CancelTopMostMenuChecked(settings.AlwaysOnTop);
            try
            {
                startupItem.Checked = StartupRegistration.IsEnabled();
                startupItem.ToolTipText = "登录 Windows 后自动启动 CodexMeter";
            }
            catch (Exception ex)
            {
                startupItem.Checked = false;
                startupItem.ToolTipText = "无法读取 Windows 启动项：" + Shorten(ex.Message, 120);
            }
        }

        private void ToggleStartWithWindows()
        {
            try
            {
                bool enable = !StartupRegistration.IsEnabled();
                StartupRegistration.SetEnabled(enable);
                startupItem.Checked = StartupRegistration.IsEnabled();
            }
            catch (Exception ex)
            {
                SyncMenuChecks();
                MessageBox.Show(this,
                    "无法修改开机自启动：\r\n" + Shorten(ex.Message, 240),
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
                "。网速不是 Codex 专属流量。按 F5 立即同步，按 Esc 最小化到托盘。";
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

        private void RestorePosition()
        {
            if (settings.Left.HasValue && settings.Top.HasValue)
            {
                Location = new Point(settings.Left.Value, settings.Top.Value);
                ClampToWorkingArea();
                return;
            }

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - S(18), area.Top + S(18));
        }

        private void ClampToWorkingArea()
        {
            if (!String.IsNullOrEmpty(settings.DockEdge))
                return;
            Screen screen = Screen.FromRectangle(Bounds);
            Rectangle area = screen.WorkingArea;
            Location = new Point(
                Math.Max(area.Left, Math.Min(Left, area.Right - Width)),
                Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
        }

        private void SaveCurrentPosition()
        {
            if (isDockAnimating)
                return;
            if (String.IsNullOrEmpty(settings.DockEdge))
            {
                settings.Left = Left;
                settings.Top = Top;
            }
            else
            {
                settings.DockTop = Top;
            }
            SaveSettings();
        }

        private void SaveSettings()
        {
            try { settingsStore.Save(settings); }
            catch { }
        }

        private void UpdateRoundedRegion()
        {
            using (GraphicsPath path = RoundedRectangle(
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
            if (isRefreshing || isWeeklyUsageRefreshing)
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
                return IsSnapshotStale(snapshot, isConnected, lastError,
                    lastSuccessfulRefreshAt, scheduledRefreshMilliseconds, DateTimeOffset.Now);
            }
        }

        internal static bool IsSnapshotStale(
            UsageSnapshot currentSnapshot,
            bool connected,
            string error,
            DateTimeOffset? successfulRefreshAt,
            int refreshMilliseconds,
            DateTimeOffset now)
        {
            if (currentSnapshot == null)
                return false;
            if (!connected || !String.IsNullOrWhiteSpace(error) || !successfulRefreshAt.HasValue)
                return true;

            DateTimeOffset effectiveFreshness = successfulRefreshAt.Value;
            if (currentSnapshot.UpdatedAt.HasValue &&
                currentSnapshot.UpdatedAt.Value <= now.AddMinutes(5) &&
                currentSnapshot.UpdatedAt.Value < effectiveFreshness)
            {
                effectiveFreshness = currentSnapshot.UpdatedAt.Value;
            }

            double staleAfterMilliseconds = Math.Max(180000,
                Math.Max(1000, refreshMilliseconds) * 2.5);
            return (now - effectiveFreshness).TotalMilliseconds > staleAfterMilliseconds;
        }

        private string StatusText
        {
            get
            {
                if (isRefreshing || isWeeklyUsageRefreshing)
                    return "实时";
                if (HasStaleData)
                    return "过期";
                if (isConnected)
                    return "实时";
                return "离线";
            }
        }

        private Color StatusDotColor
        {
            get
            {
                if (isRefreshing || isWeeklyUsageRefreshing)
                    return Color.FromArgb(0, 122, 255);
                if (HasStaleData)
                    return Color.FromArgb(255, 159, 10);
                if (isConnected)
                    return Color.FromArgb(35, 205, 96);
                return Color.FromArgb(255, 149, 0);
            }
        }

        internal static string ResetText(UsageWindow window)
        {
            if (window.ResetsAt.HasValue)
                return ResetDuration((window.ResetsAt.Value - DateTimeOffset.Now).TotalSeconds) + " 后重置";
            if (!String.IsNullOrWhiteSpace(window.ResetDescription))
                return window.ResetDescription + " 后重置";
            return String.Empty;
        }

        private static string ResetToolTipText(UsageWindow window)
        {
            if (window.ResetsAt.HasValue)
                return "准确重置：" + window.ResetsAt.Value.ToLocalTime().ToString("M月d日 HH:mm:ss") +
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

        private static string Duration(double seconds)
        {
            int totalHours = Math.Max(0, Convert.ToInt32(Math.Floor(seconds / 3600)));
            int days = totalHours / 24;
            int hours = totalHours % 24;
            return days > 0 ? days + "d " + hours + "h" : hours + "h";
        }

        private static void BarColors(double remaining, bool prominent, out Color start, out Color end)
        {
            if (remaining < 15)
            {
                start = Color.FromArgb(255, 59, 48);
                end = Color.FromArgb(255, 69, 58);
            }
            else if (remaining < 35)
            {
                start = Color.FromArgb(255, 149, 0);
                end = Color.FromArgb(255, 204, 0);
            }
            else if (prominent)
            {
                start = Color.FromArgb(30, 205, 235);
                end = Color.FromArgb(74, 88, 255);
            }
            else
            {
                start = Color.FromArgb(74, 194, 223);
                end = Color.FromArgb(119, 91, 245);
            }
        }

        private static void DrawSparkle(Graphics graphics, float x, float y, float radius)
        {
            PointF[] points = new PointF[]
            {
                new PointF(x, y - radius),
                new PointF(x + radius * 0.28f, y - radius * 0.28f),
                new PointF(x + radius, y),
                new PointF(x + radius * 0.28f, y + radius * 0.28f),
                new PointF(x, y + radius),
                new PointF(x - radius * 0.28f, y + radius * 0.28f),
                new PointF(x - radius, y),
                new PointF(x - radius * 0.28f, y - radius * 0.28f)
            };
            using (Brush white = new SolidBrush(Color.White))
                graphics.FillPolygon(white, points);
        }

        private static Font PixelFont(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Pixel);
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

        private static Font FittedPixelFont(Graphics graphics, string text, RectangleF bounds,
            float preferredSize, float minimumSize, FontStyle style)
        {
            return PixelFont(
                FittedPixelFontSize(graphics, text, bounds, preferredSize, minimumSize, style), style);
        }

        private static float FittedPixelFontSize(Graphics graphics, string text, RectangleF bounds,
            float preferredSize, float minimumSize, FontStyle style)
        {
            float size = Math.Max(minimumSize, preferredSize);
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
                while (size > minimumSize)
                {
                    using (Font font = PixelFont(size, style))
                    {
                        SizeF measured = graphics.MeasureString(text ?? String.Empty, font,
                            new SizeF(10000f, bounds.Height), format);
                        if (measured.Width <= bounds.Width - 1f && measured.Height <= bounds.Height + 1f)
                            return size;
                    }
                    size = Math.Max(minimumSize, size - 0.25f);
                }
            }
            return minimumSize;
        }

        private static void DrawText(Graphics graphics, string text, Font font, Brush brush,
            RectangleF bounds, StringAlignment horizontal, StringAlignment vertical)
        {
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment = horizontal;
                format.LineAlignment = vertical;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags |= StringFormatFlags.NoWrap;
                graphics.DrawString(text, font, brush, bounds, format);
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = Math.Max(1, radius * 2);
            RectangleF arc = new RectangleF(rectangle.X, rectangle.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
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

        private Color PrimaryText
        {
            get { return IsDark ? Color.FromArgb(246, 248, 252) : Color.FromArgb(21, 36, 55); }
        }

        private Color SecondaryText
        {
            get { return IsDark ? Color.FromArgb(190, 199, 213) : Color.FromArgb(65, 84, 105); }
        }

        private Color TertiaryText
        {
            get { return IsDark ? Color.FromArgb(150, 161, 176) : Color.FromArgb(101, 119, 139); }
        }
    }
}
