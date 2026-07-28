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
        private const int DesignWidth = 334;
        private const int HeaderHeight = 55;
        private const int MeterHeight = 47;
        private const int PaceHeight = 27;
        private const int BottomPadding = 11;
        private const int DockStrip = 7;
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

        private ToolStripMenuItem fixedItem;
        private ToolStripMenuItem followItem;
        private ToolStripMenuItem lightItem;
        private ToolStripMenuItem darkItem;
        private ToolStripMenuItem edgeItem;
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
        private bool syncButtonHovered;
        private int designHeight = 118;
        private int scheduledRefreshMilliseconds = NormalRefreshMilliseconds;
        private int consecutiveFailures;
        private DateTimeOffset? lastSuccessfulRefreshAt;
        private CancellationTokenSource refreshCancellation;
        private NetworkSpeedSnapshot networkSpeed;

        public CodexMeterFormV2()
        {
            uiScale = Math.Max(1f, Math.Min(3f, NativeMethods.SystemScale()));
            settings = settingsStore.Load();

            Text = "Codex Meter";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = IsDark ? Color.FromArgb(23, 28, 39) : Color.FromArgb(226, 233, 234);
            Opacity = 0.985;
            ClientSize = new Size(S(DesignWidth), S(designHeight));

            BuildMenus();
            ConfigureTimers();
            RestorePosition();
            UpdateRoundedRegion();

            Shown += OnShown;
            FormClosing += OnFormClosing;
            MouseDown += OnCardMouseDown;
            MouseMove += OnCardMouseMove;
            MouseUp += OnCardMouseUp;
            MouseClick += OnCardMouseClick;
            MouseLeave += delegate
            {
                if (!isDragging)
                {
                    bool wasHovered = syncButtonHovered;
                    syncButtonHovered = false;
                    Cursor = Cursors.Default;
                    if (wasHovered)
                        Invalidate(syncButtonBounds);
                }
            };
            Resize += delegate { UpdateRoundedRegion(); };
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
                target = 118;
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
        }

        private void DrawGlassCard(Graphics graphics)
        {
            RectangleF bounds = new RectangleF(0.5f, 0.5f, DesignWidth - 1f, designHeight - 1f);
            using (GraphicsPath path = RoundedRectangle(bounds, 23f))
            using (LinearGradientBrush baseBrush = new LinearGradientBrush(
                bounds,
                IsDark ? Color.FromArgb(248, 25, 31, 43) : Color.FromArgb(250, 241, 246, 246),
                IsDark ? Color.FromArgb(248, 38, 45, 59) : Color.FromArgb(248, 215, 224, 226),
                22f))
            {
                graphics.FillPath(baseBrush, path);

                using (LinearGradientBrush highlight = new LinearGradientBrush(
                    new RectangleF(0, 0, DesignWidth, designHeight),
                    IsDark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(150, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    graphics.FillPath(highlight, path);
                }

                using (Pen border = new Pen(
                    IsDark ? Color.FromArgb(74, 255, 255, 255) : Color.FromArgb(235, 255, 255, 255), 0.8f))
                {
                    graphics.DrawPath(border, path);
                }
            }
        }

        private void DrawHeader(Graphics graphics)
        {
            RectangleF icon = new RectangleF(18, 13, 31, 31);
            using (GraphicsPath iconPath = new GraphicsPath())
            using (LinearGradientBrush iconBrush = new LinearGradientBrush(
                icon, Color.FromArgb(53, 195, 255), Color.FromArgb(0, 112, 255), 55f))
            {
                iconPath.AddEllipse(icon);
                graphics.FillPath(iconBrush, iconPath);
            }
            DrawSparkle(graphics, 33.5f, 28.5f, 5.3f);
            DrawSparkle(graphics, 40.5f, 20f, 2.3f);
            DrawSparkle(graphics, 27.5f, 21.5f, 1.7f);

            using (Font titleFont = PixelFont(16f, FontStyle.Bold))
            using (Font subtitleFont = PixelFont(10f, FontStyle.Bold))
            using (Brush primary = new SolidBrush(PrimaryText))
            using (Brush secondary = new SolidBrush(SecondaryText))
            {
                DrawText(graphics, "Codex 用量", titleFont, primary,
                    new RectangleF(59, 9, 108, 24), StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, HeaderSubtitle(), subtitleFont, secondary,
                    new RectangleF(59, 31, 108, 17), StringAlignment.Near, StringAlignment.Center);
            }

            DrawNetworkSpeed(graphics);

            RectangleF status = new RectangleF(233, 15, 54, 24);
            syncButtonBounds = new Rectangle(S(status.X), S(status.Y), S(status.Width), S(status.Height));
            using (GraphicsPath pill = RoundedRectangle(status, 12f))
            using (Brush pillBrush = new SolidBrush(syncButtonHovered
                ? (IsDark ? Color.FromArgb(48, 80, 174, 255) : Color.FromArgb(35, 0, 122, 255))
                : (IsDark ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(19, 50, 61, 65))))
            {
                graphics.FillPath(pillBrush, pill);
            }

            Color dotColor = StatusDotColor;
            using (Brush dot = new SolidBrush(dotColor))
                graphics.FillEllipse(dot, 241, 23, 6, 6);
            using (Font statusFont = PixelFont(9.5f, FontStyle.Bold))
            using (Brush secondary = new SolidBrush(SecondaryText))
                DrawText(graphics, StatusText, statusFont, secondary,
                    new RectangleF(250, 16, 33, 21), StringAlignment.Center, StringAlignment.Center);

            menuButtonBounds = new Rectangle(S(302), S(14), S(22), S(27));
            using (Brush dots = new SolidBrush(PrimaryText))
            {
                graphics.FillEllipse(dots, 305, 27, 2.4f, 2.4f);
                graphics.FillEllipse(dots, 312, 27, 2.4f, 2.4f);
                graphics.FillEllipse(dots, 319, 27, 2.4f, 2.4f);
            }
        }

        private Rectangle NetworkSpeedBounds
        {
            get { return new Rectangle(S(169), S(7), S(63), S(42)); }
        }

        private void DrawNetworkSpeed(Graphics graphics)
        {
            string download = "↓ " + NetworkSpeedMonitor.FormatRate(networkSpeed.DownloadBytesPerSecond);
            string upload = "↑ " + NetworkSpeedMonitor.FormatRate(networkSpeed.UploadBytesPerSecond);
            Color downloadColor = IsDark ? Color.FromArgb(92, 205, 255) : Color.FromArgb(0, 126, 214);
            Color uploadColor = IsDark ? Color.FromArgb(175, 153, 255) : Color.FromArgb(112, 82, 210);

            using (Font speedFont = PixelFont(8f, FontStyle.Bold))
            using (Brush downloadBrush = new SolidBrush(downloadColor))
            using (Brush uploadBrush = new SolidBrush(uploadColor))
            {
                DrawText(graphics, download, speedFont, downloadBrush,
                    new RectangleF(169, 8, 63, 19), StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, upload, speedFont, uploadBrush,
                    new RectangleF(169, 27, 63, 19), StringAlignment.Near, StringAlignment.Center);
            }
        }

        private void DrawEmptyState(Graphics graphics)
        {
            string message = String.IsNullOrWhiteSpace(lastError) ? "正在连接 CodexBar…" : lastError;
            using (Font font = PixelFont(10f, FontStyle.Regular))
            using (Brush brush = new SolidBrush(String.IsNullOrWhiteSpace(lastError) ? SecondaryText : Color.FromArgb(218, 112, 0)))
                DrawText(graphics, message, font, brush, new RectangleF(18, 60, 298, 42),
                    StringAlignment.Near, StringAlignment.Near);
        }

        private void DrawMeter(Graphics graphics, UsageWindow window, int y, bool prominent, PaceInfo pace)
        {
            if (window == null)
                return;

            string title = prominent ? "每周额度" : window.Title;
            string reset = ResetText(window);
            string remaining = "剩余 " + Math.Round(window.RemainingPercent).ToString("0") + "%";

            using (Font titleFont = PixelFont(prominent ? 12f : 11f, FontStyle.Bold))
            using (Font resetFont = PixelFont(9f, FontStyle.Regular))
            using (Font percentFont = PixelFont(prominent ? 16f : 12f, FontStyle.Bold))
            using (Brush primary = new SolidBrush(PrimaryText))
            using (Brush secondary = new SolidBrush(prominent ? TertiaryText : SecondaryText))
            {
                DrawText(graphics, title, titleFont, prominent ? primary : secondary,
                    new RectangleF(18, y + 1, 102, 24), StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, remaining, percentFont, primary,
                    new RectangleF(229, y - 1, 88, 28), StringAlignment.Far, StringAlignment.Center);
                if (!String.IsNullOrEmpty(reset))
                {
                    DrawText(graphics, reset, resetFont, secondary,
                        new RectangleF(102, y + 2, 124, 22), StringAlignment.Far, StringAlignment.Center);
                }
            }

            RectangleF track = new RectangleF(18, y + 31, 298, prominent ? 9f : 7f);
            using (GraphicsPath path = RoundedRectangle(track, track.Height / 2f))
            using (Brush trackBrush = new SolidBrush(IsDark ? Color.FromArgb(31, 255, 255, 255) : Color.FromArgb(22, 31, 42, 48)))
                graphics.FillPath(trackBrush, path);

            float fillWidth = Math.Max(4f, (float)(track.Width * window.RemainingPercent / 100.0));
            fillWidth = Math.Min(track.Width, fillWidth);
            RectangleF fill = new RectangleF(track.X, track.Y, fillWidth, track.Height);
            Color start;
            Color end;
            BarColors(window.RemainingPercent, prominent, out start, out end);
            using (GraphicsPath fillPath = RoundedRectangle(fill, fill.Height / 2f))
            using (LinearGradientBrush fillBrush = new LinearGradientBrush(fill, start, end, 0f))
                graphics.FillPath(fillBrush, fillPath);

            if (pace != null)
            {
                double expectedRemaining = Math.Max(0, Math.Min(100, 100 - pace.ExpectedUsedPercent));
                float markerX = track.X + (float)(track.Width * expectedRemaining / 100.0);
                using (Pen marker = new Pen(Color.FromArgb(255, 59, 48), 3f))
                    graphics.DrawLine(marker, markerX, track.Y - 3, markerX, track.Bottom + 3);
            }
        }

        private void DrawPace(Graphics graphics, PaceInfo pace, int y)
        {
            string left = pace.DeltaPercent > 0.5
                ? "超额 " + Math.Round(pace.DeltaPercent).ToString("0") + "%"
                : "节奏正常";
            string right;
            if (pace.WillLastToReset)
                right = "预计可用至重置";
            else if (pace.EtaSeconds.HasValue)
                right = "预计 " + Duration(pace.EtaSeconds.Value) + " 后耗尽";
            else
                right = "暂无消耗趋势";

            using (Font font = PixelFont(9f, FontStyle.Bold))
            using (Brush leftBrush = new SolidBrush(pace.DeltaPercent > 0.5 ? Color.FromArgb(255, 139, 0) : SecondaryText))
            using (Brush rightBrush = new SolidBrush(SecondaryText))
            {
                DrawText(graphics, left, font, leftBrush, new RectangleF(18, y + 1, 120, 22),
                    StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, right, font, rightBrush, new RectangleF(132, y + 1, 188, 22),
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
                if (hovered != syncButtonHovered)
                {
                    syncButtonHovered = hovered;
                    Cursor = hovered || menuButtonBounds.Contains(eventArgs.Location) ? Cursors.Hand : Cursors.Default;
                    Invalidate(syncButtonBounds);
                }
                else
                {
                    Cursor = hovered || menuButtonBounds.Contains(eventArgs.Location) ? Cursors.Hand : Cursors.Default;
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
            TopMost = true;
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

        private void SyncMenuChecks()
        {
            visibilityItem.Text = Visible ? "最小化到托盘" : "显示悬浮卡片";
            fixedItem.Checked = !IsFollowMode;
            followItem.Checked = IsFollowMode;
            lightItem.Checked = !IsDark;
            darkItem.Checked = IsDark;
            edgeItem.Checked = settings.EdgeAutoHide;
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

        private static string ResetText(UsageWindow window)
        {
            if (window.ResetsAt.HasValue)
                return Duration((window.ResetsAt.Value - DateTimeOffset.Now).TotalSeconds) + " 后重置";
            if (!String.IsNullOrWhiteSpace(window.ResetDescription))
                return window.ResetDescription + " 后重置";
            return String.Empty;
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
                start = Color.FromArgb(57, 199, 248);
                end = Color.FromArgb(0, 122, 255);
            }
            else
            {
                start = Color.FromArgb(86, 204, 235);
                end = Color.FromArgb(91, 111, 255);
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
            get { return IsDark ? Color.FromArgb(246, 248, 252) : Color.FromArgb(35, 39, 42); }
        }

        private Color SecondaryText
        {
            get { return IsDark ? Color.FromArgb(190, 199, 213) : Color.FromArgb(85, 91, 94); }
        }

        private Color TertiaryText
        {
            get { return IsDark ? Color.FromArgb(150, 161, 176) : Color.FromArgb(105, 110, 112); }
        }
    }
}
