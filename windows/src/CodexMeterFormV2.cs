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
        private const int DesignWidth = 344;
        private const int HeaderHeight = 58;
        private const int MeterHeight = 50;
        private const int PaceHeight = 30;
        private const int BottomPadding = 10;
        private const int DockStrip = 7;
        private const float SectionTitleFontSize = 14f;
        private const float SupportingTextFontSize = 12.5f;
        private const int ActiveRefreshMilliseconds = 30000;
        private const int NormalRefreshMilliseconds = 60000;
        private const int HiddenRefreshMilliseconds = 120000;
        private const int MaximumBackoffMilliseconds = 600000;

        private float uiScale;
        private readonly CodexBarClient client = new CodexBarClient();
        private readonly SettingsStore settingsStore = new SettingsStore();
        private readonly AppSettings settings;
        private readonly WinFormsTimer refreshTimer = new WinFormsTimer();
        private readonly WinFormsTimer visibilityTimer = new WinFormsTimer();
        private readonly WinFormsTimer dockTimer = new WinFormsTimer();
        private readonly WinFormsTimer statusTimer = new WinFormsTimer();
        private readonly WinFormsTimer networkTimer = new WinFormsTimer();
        private readonly NetworkSpeedMonitor networkSpeedMonitor = new NetworkSpeedMonitor();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly NotifyIcon trayIcon = new NotifyIcon();
        private readonly ToolTip contextualToolTip = new ToolTip();
        private readonly List<ResetHoverTarget> resetHoverTargets = new List<ResetHoverTarget>();
        private readonly EventWaitHandle showExistingEvent;

        private ToolStripMenuItem fixedItem;
        private ToolStripMenuItem followItem;
        private ToolStripMenuItem lightItem;
        private ToolStripMenuItem darkItem;
        private ToolStripMenuItem edgeItem;
        private ToolStripMenuItem topMostItem;
        private ToolStripMenuItem visibilityItem;
        private UsageSnapshot snapshot;
        private string lastError;
        private bool isConnected;
        private bool isRefreshing;
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
        private Rectangle budgetMarkerBounds;
        private bool syncButtonHovered;
        private bool budgetMarkerHovered;
        private float budgetMarkerDesignX;
        private string budgetToolTipText = String.Empty;
        private string activeResetToolTipText = String.Empty;
        private int designHeight = 122;
        private int scheduledRefreshMilliseconds = NormalRefreshMilliseconds;
        private int consecutiveFailures;
        private DateTimeOffset? lastSuccessfulRefreshAt;
        private CancellationTokenSource refreshCancellation;
        private RegisteredWaitHandle showExistingWait;
        private NetworkSpeedSnapshot networkSpeed;

        public CodexMeterFormV2() : this(null)
        {
        }

        internal CodexMeterFormV2(EventWaitHandle showExistingEvent)
        {
            this.showExistingEvent = showExistingEvent;
            uiScale = Math.Max(1f, Math.Min(3f, NativeMethods.SystemScale()));
            settings = settingsStore.Load();

            Text = "Codex Meter";
            Icon = NativeMethods.CreateAppIcon();
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
                    bool wasBudgetHovered = budgetMarkerHovered;
                    budgetMarkerHovered = false;
                    activeResetToolTipText = String.Empty;
                    contextualToolTip.Hide(this);
                    Cursor = Cursors.Default;
                    if (wasHovered)
                        Invalidate(syncButtonBounds);
                    if (wasBudgetHovered)
                        Invalidate();
                }
            };
            Resize += delegate { UpdateRoundedRegion(); };
            Disposed += delegate { contextualToolTip.Dispose(); };
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

        private bool IsFollowMode
        {
            get { return String.Equals(settings.Mode, "follow", StringComparison.OrdinalIgnoreCase); }
        }

        private int S(float value)
        {
            return Math.Max(1, Convert.ToInt32(Math.Round(value * uiScale)));
        }

        private void BuildMenus()
        {
            visibilityItem = new ToolStripMenuItem("最小化到托盘");
            visibilityItem.Click += delegate { ToggleTrayVisibility(); };

            fixedItem = new ToolStripMenuItem("固定在桌面");
            fixedItem.Click += delegate { SetMode("fixed"); };
            followItem = new ToolStripMenuItem("跟随 Codex");
            followItem.Click += delegate { SetMode("follow"); };

            topMostItem = new ToolStripMenuItem("始终置顶");
            topMostItem.CheckOnClick = true;
            topMostItem.Checked = settings.AlwaysOnTop;
            topMostItem.CheckedChanged += delegate
            {
                settings.AlwaysOnTop = topMostItem.Checked;
                TopMost = settings.AlwaysOnTop;
                SaveSettings();
            };

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
                ConfigureModeTimers();
                ScheduleNextRefresh();
                SaveSettings();
            };

            ToolStripMenuItem refresh = new ToolStripMenuItem("立即同步");
            refresh.Click += delegate { RefreshNow(); };
            ToolStripMenuItem networkInfo = new ToolStripMenuItem("网速为系统总流量（非 Codex 专属）");
            networkInfo.Enabled = false;
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
            menu.Items.Add(fixedItem);
            menu.Items.Add(followItem);
            menu.Items.Add(topMostItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(appearance);
            menu.Items.Add(edgeItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(refresh);
            menu.Items.Add(networkInfo);
            menu.Items.Add(openFolder);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(quit);
            menu.Opening += delegate { SyncMenuChecks(); };

            trayIcon.Icon = NativeMethods.CreateAppIcon();
            trayIcon.Text = "Codex Meter";
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
            visibilityTimer.Interval = 500;
            visibilityTimer.Tick += delegate { UpdateFollowVisibility(); };
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
            ConfigureModeTimers();
            statusTimer.Start();
            RefreshNow();
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
            visibilityTimer.Stop();
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

        private void ScheduleNextRefresh()
        {
            if (isExiting || IsDisposed)
                return;

            int interval;
            if (!Visible || manuallyHidden || (!String.IsNullOrEmpty(settings.DockEdge) && !dockRevealed))
                interval = HiddenRefreshMilliseconds;
            else if (IsFollowMode && NativeMethods.IsCodexForeground())
                interval = ActiveRefreshMilliseconds;
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

        private void ConfigureModeTimers()
        {
            if (IsFollowMode)
                visibilityTimer.Start();
            else
                visibilityTimer.Stop();

            bool needsDockPolling = settings.EdgeAutoHide &&
                !String.IsNullOrEmpty(settings.DockEdge) && !IsFollowMode && Visible;
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
            int target;
            if (snapshot == null)
            {
                target = 122;
            }
            else
            {
                int meterCount = 1 + snapshot.Extras.Count;
                target = HeaderHeight + meterCount * MeterHeight + BottomPadding;
                if (snapshot.WeeklyPace != null)
                    target += PaceHeight;
            }

            if (designHeight == target)
                return;

            designHeight = target;
            ClientSize = new Size(S(DesignWidth), S(designHeight));
            UpdateRoundedRegion();
            if (activeDockScreen != null)
                settings.DockTop = Math.Max(activeDockScreen.WorkingArea.Top,
                    Math.Min(Top, activeDockScreen.WorkingArea.Bottom - Height));
            else
                ClampToWorkingArea();
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
            budgetToolTipText = String.Empty;
            resetHoverTargets.Clear();

            if (snapshot == null)
            {
                DrawEmptyState(graphics);
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

            foreach (UsageWindow extra in snapshot.Extras)
            {
                DrawMeter(graphics, extra, y, false, null);
                y += MeterHeight;
            }

            if (budgetMarkerHovered && !String.IsNullOrEmpty(budgetToolTipText))
                DrawBudgetToolTip(graphics);
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
                    new RectangleF(0, 0, DesignWidth, Math.Max(80, designHeight * 0.72f)),
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
            using (GraphicsPath iconPath = RoundedRectangle(icon, 11f))
            using (LinearGradientBrush iconBrush = new LinearGradientBrush(
                icon, Color.FromArgb(38, 211, 239), Color.FromArgb(77, 91, 255), 55f))
            {
                graphics.FillPath(iconBrush, iconPath);
                using (Pen iconBorder = new Pen(Color.FromArgb(118, 255, 255, 255), 0.8f))
                    graphics.DrawPath(iconBorder, iconPath);
            }
            DrawSparkle(graphics, 34.5f, 29f, 5.7f);
            DrawSparkle(graphics, 43f, 19.5f, 2.2f);
            DrawSparkle(graphics, 25.8f, 20.7f, 1.6f);

            RectangleF titleBounds = new RectangleF(62, 8, 106, 25);
            RectangleF subtitleBounds = new RectangleF(62, 32, 108, 16);
            string subtitle = HeaderSubtitle();
            using (Font titleFont = FittedPixelFont(graphics, "Codex 用量", titleBounds, 18f, 15.5f, FontStyle.Bold))
            using (Font subtitleFont = PixelFont(SupportingTextFontSize, FontStyle.Regular))
            using (Brush primary = new SolidBrush(PrimaryText))
            using (Brush secondary = new SolidBrush(SecondaryText))
            {
                DrawText(graphics, "Codex 用量", titleFont, primary, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, subtitle, subtitleFont, secondary, subtitleBounds,
                    StringAlignment.Near, StringAlignment.Center);
            }

            RectangleF networkTile = new RectangleF(171, 8, 73, 41);
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

            RectangleF status = new RectangleF(249, 15, 58, 26);
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
                graphics.FillEllipse(dotGlow, 254, 20, 12, 12);
            using (Brush dot = new SolidBrush(dotColor))
                graphics.FillEllipse(dot, 257, 23, 6, 6);
            RectangleF statusTextBounds = new RectangleF(265, 17, 38, 21);
            using (Font statusFont = FittedPixelFont(
                graphics, StatusText, statusTextBounds, SectionTitleFontSize,
                SupportingTextFontSize, FontStyle.Bold))
            using (Brush secondary = new SolidBrush(SecondaryText))
                DrawText(graphics, StatusText, statusFont, secondary, statusTextBounds,
                    StringAlignment.Center, StringAlignment.Center);

            RectangleF menuSurface = new RectangleF(313, 15, 24, 26);
            menuButtonBounds = new Rectangle(S(menuSurface.X), S(menuSurface.Y), S(menuSurface.Width), S(menuSurface.Height));
            using (GraphicsPath menuPath = RoundedRectangle(menuSurface, 9f))
            using (Brush menuBrush = new SolidBrush(IsDark
                ? Color.FromArgb(24, 255, 255, 255)
                : Color.FromArgb(94, 255, 255, 255)))
                graphics.FillPath(menuBrush, menuPath);
            using (Brush dots = new SolidBrush(PrimaryText))
            {
                graphics.FillEllipse(dots, 318, 27, 2.2f, 2.2f);
                graphics.FillEllipse(dots, 324, 27, 2.2f, 2.2f);
                graphics.FillEllipse(dots, 330, 27, 2.2f, 2.2f);
            }

            using (Pen divider = new Pen(IsDark
                ? Color.FromArgb(28, 108, 184, 240)
                : Color.FromArgb(32, 51, 119, 162), 0.6f))
                graphics.DrawLine(divider, 18, HeaderHeight - 1, DesignWidth - 18, HeaderHeight - 1);
        }

        private Rectangle NetworkSpeedBounds
        {
            get { return new Rectangle(S(171), S(8), S(73), S(41)); }
        }

        private void DrawNetworkSpeed(Graphics graphics)
        {
            string download = "↓ " + NetworkSpeedMonitor.FormatRate(networkSpeed.DownloadBytesPerSecond);
            string upload = "↑ " + NetworkSpeedMonitor.FormatRate(networkSpeed.UploadBytesPerSecond);
            Color downloadColor = IsDark ? Color.FromArgb(92, 211, 255) : Color.FromArgb(0, 125, 204);
            Color uploadColor = IsDark ? Color.FromArgb(179, 157, 255) : Color.FromArgb(102, 74, 207);

            RectangleF downloadBounds = new RectangleF(177, 9, 64, 18);
            RectangleF uploadBounds = new RectangleF(177, 29, 64, 18);
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

            RectangleF titleBounds = new RectangleF(20, y + 2, 94, 24);
            RectangleF remainingBounds = new RectangleF(226, y + 2, 98, 24);
            RectangleF resetBounds = new RectangleF(104, y + 3, 122, 22);
            if (!String.IsNullOrEmpty(reset))
            {
                resetHoverTargets.Add(new ResetHoverTarget(
                    new Rectangle(S(resetBounds.X), S(resetBounds.Y),
                        S(resetBounds.Width), S(resetBounds.Height)),
                    ResetToolTipText(window)));
            }
            using (Font titleFont = PixelFont(SectionTitleFontSize, FontStyle.Bold))
            using (Font resetFont = PixelFont(SupportingTextFontSize, FontStyle.Regular))
            using (Brush primary = new SolidBrush(PrimaryText))
            using (Brush secondary = new SolidBrush(prominent ? TertiaryText : SecondaryText))
            {
                DrawText(graphics, title, titleFont, prominent ? primary : secondary, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, remaining, titleFont, primary, remainingBounds,
                    StringAlignment.Far, StringAlignment.Center);
                if (!String.IsNullOrEmpty(reset))
                {
                    DrawText(graphics, reset, resetFont, secondary, resetBounds,
                        StringAlignment.Far, StringAlignment.Center);
                }
            }

            RectangleF track = new RectangleF(20, y + 34, DesignWidth - 40, prominent ? 9f : 7f);
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
            using (GraphicsPath surfacePath = RoundedRectangle(surface, 12.5f))
            using (Brush surfaceBrush = new SolidBrush(IsDark
                ? Color.FromArgb(23, 255, 255, 255)
                : Color.FromArgb(116, 255, 255, 255)))
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

            RectangleF stateBounds = new RectangleF(35, y + 3, 108, 23);
            RectangleF forecastBounds = new RectangleF(140, y + 3, 183, 23);
            using (Font font = PixelFont(SupportingTextFontSize, FontStyle.Bold))
            using (Font forecastFont = PixelFont(SupportingTextFontSize, FontStyle.Regular))
            using (Brush leftBrush = new SolidBrush(overBudget ? stateColor : PrimaryText))
            using (Brush rightBrush = new SolidBrush(SecondaryText))
            {
                DrawText(graphics, left, font, leftBrush, stateBounds,
                    StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, right, forecastFont, rightBrush, forecastBounds,
                    StringAlignment.Far, StringAlignment.Center);
            }
        }

        private void OnCardMouseClick(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left && syncButtonBounds.Contains(eventArgs.Location))
                RefreshNow();
            else if (eventArgs.Button == MouseButtons.Left && menuButtonBounds.Contains(eventArgs.Location))
                menu.Show(this, new Point(menuButtonBounds.Right, menuButtonBounds.Bottom), ToolStripDropDownDirection.BelowLeft);
            else if (eventArgs.Button == MouseButtons.Right)
                menu.Show(this, eventArgs.Location);
        }

        private void OnCardMouseDown(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left || menuButtonBounds.Contains(eventArgs.Location) ||
                syncButtonBounds.Contains(eventArgs.Location))
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
                bool budgetHovered = budgetMarkerBounds.Contains(eventArgs.Location) &&
                    !String.IsNullOrEmpty(budgetToolTipText);
                string resetToolTipText = resetHoverTargets
                    .Where(delegate(ResetHoverTarget target) { return target.Bounds.Contains(eventArgs.Location); })
                    .Select(delegate(ResetHoverTarget target) { return target.Text; })
                    .FirstOrDefault() ?? String.Empty;
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
                    Cursor = hovered || menuButtonBounds.Contains(eventArgs.Location)
                        ? Cursors.Hand
                        : (budgetHovered || !String.IsNullOrEmpty(resetToolTipText)
                            ? Cursors.Help : Cursors.Default);
                    Invalidate(syncButtonBounds);
                }
                else
                {
                    Cursor = hovered || menuButtonBounds.Contains(eventArgs.Location)
                        ? Cursors.Hand
                        : (budgetHovered || !String.IsNullOrEmpty(resetToolTipText)
                            ? Cursors.Help : Cursors.Default);
                }
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
            if (!settings.EdgeAutoHide || IsFollowMode)
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
            ConfigureModeTimers();
            ScheduleNextRefresh();
            UpdateDockPosition();
        }

        private void UpdateDockPosition()
        {
            if (String.IsNullOrEmpty(settings.DockEdge) || !settings.EdgeAutoHide || IsFollowMode || !Visible)
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
            if (String.IsNullOrEmpty(settings.DockEdge) || !settings.EdgeAutoHide || IsFollowMode)
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
            ConfigureModeTimers();
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
            ConfigureModeTimers();
            ScheduleNextRefresh();

            if (moveOnScreen && screen != null)
            {
                Rectangle area = screen.WorkingArea;
                int x = edge == "left" ? area.Left + S(12) : area.Right - Width - S(12);
                int y = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
                Location = new Point(x, y);
            }
        }

        private void SetMode(string mode)
        {
            settings.Mode = mode;
            if (IsFollowMode)
                ClearDock(true);
            else
                ShowMeter();
            SyncMenuChecks();
            ConfigureModeTimers();
            ScheduleNextRefresh();
            SaveSettings();
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

        private void UpdateFollowVisibility()
        {
            if (!IsFollowMode)
            {
                if (!Visible && !manuallyHidden)
                    ShowMeter();
                return;
            }

            bool shouldShow = NativeMethods.IsCodexForeground() || menu.Visible || ContainsFocus;
            if (shouldShow && !Visible)
            {
                ShowMeter();
                ScheduleNextRefresh();
            }
            else if (!shouldShow && Visible)
            {
                Hide();
                ConfigureModeTimers();
                ScheduleNextRefresh();
            }
        }

        private void ShowMeter()
        {
            manuallyHidden = false;
            Show();
            TopMost = settings.AlwaysOnTop;
            BringToFront();
            ConfigureModeTimers();
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
            ConfigureModeTimers();
            ScheduleNextRefresh();
            SyncMenuChecks();
        }

        private void RestoreFromTray()
        {
            manuallyHidden = false;
            if (!String.IsNullOrEmpty(settings.DockEdge) && settings.EdgeAutoHide && !IsFollowMode)
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
                RefreshNow();
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
            fixedItem.Checked = !IsFollowMode;
            followItem.Checked = IsFollowMode;
            lightItem.Checked = !IsDark;
            darkItem.Checked = IsDark;
            edgeItem.Checked = settings.EdgeAutoHide;
            topMostItem.Checked = settings.AlwaysOnTop;
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
                MessageBox.Show(this, "未找到 CodexBar CLI。", "Codex Meter",
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

        private string HeaderSubtitle()
        {
            if (isRefreshing && snapshot == null)
                return "正在同步数据…";
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
                return Shorten("Codex Meter - " + (lastError ?? "正在同步"), 63);
            string stale = HasStaleData ? "[过期] " : String.Empty;
            return Shorten("Codex Meter - " + stale + "每周剩余 " +
                Math.Round(main.RemainingPercent).ToString("0") + "%", 63);
        }

        private bool HasStaleData
        {
            get
            {
                if (snapshot == null)
                    return false;
                if (!isConnected || !String.IsNullOrWhiteSpace(lastError) || !lastSuccessfulRefreshAt.HasValue)
                    return true;
                double staleAfterMilliseconds = Math.Max(180000, scheduledRefreshMilliseconds * 2.5);
                return (DateTimeOffset.Now - lastSuccessfulRefreshAt.Value).TotalMilliseconds > staleAfterMilliseconds;
            }
        }

        private string StatusText
        {
            get
            {
                if (isRefreshing)
                    return "同步";
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
                if (isRefreshing)
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
            if (window.DisplayResetAsDate && window.ResetsAt.HasValue)
                return window.ResetsAt.Value.ToLocalTime().ToString("M月d日") + "重置";
            if (window.ResetsAt.HasValue)
                return ResetDuration((window.ResetsAt.Value - DateTimeOffset.Now).TotalSeconds) + " 后重置";
            if (!String.IsNullOrWhiteSpace(window.ResetDescription))
                return window.ResetDescription + " 后重置";
            return String.Empty;
        }

        private static string ResetToolTipText(UsageWindow window)
        {
            if (window.DisplayResetAsDate && window.ResetsAt.HasValue)
                return "重置日期：" + window.ResetsAt.Value.ToLocalTime().ToString("M月d日") +
                    "（以上游当前返回为准）";
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
