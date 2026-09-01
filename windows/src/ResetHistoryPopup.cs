using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CodexMeter
{
    internal sealed class ResetHistoryPopup : ToolStripDropDown
    {
        private readonly ResetHistorySurface surface;
        private readonly ToolStripControlHost host;
        private bool disposeScheduled;

        public ResetHistoryPopup(ResetHistoryReport report, bool loading, bool darkTheme, float scale)
        {
            AutoClose = true;
            AutoSize = false;
            Padding = Padding.Empty;
            Margin = Padding.Empty;
            DropShadowEnabled = true;
            BackColor = Color.Transparent;

            surface = new ResetHistorySurface(report, loading, darkTheme, scale);
            host = new ToolStripControlHost(surface);
            host.AutoSize = false;
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            host.Size = surface.Size;
            Items.Add(host);
            Size = surface.Size;

            surface.LayoutChanged += delegate
            {
                host.Size = surface.Size;
                Size = surface.Size;
                PerformLayout();
            };
            surface.CloseRequested += delegate { Close(ToolStripDropDownCloseReason.ItemClicked); };
        }

        internal void DisposeAfterClose()
        {
            if (IsDisposed || disposeScheduled)
                return;

            disposeScheduled = true;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed)
                        Dispose();
                });
            }
            catch (InvalidOperationException)
            {
                EventHandler disposeOnIdle = null;
                disposeOnIdle = delegate
                {
                    Application.Idle -= disposeOnIdle;
                    if (!IsDisposed)
                        Dispose();
                };
                Application.Idle += disposeOnIdle;
            }
        }
    }

    internal sealed class ResetHistorySurface : Control
    {
        private const int DesignWidth = 500;
        private const int HeaderHeight = 48;
        private const int StatisticsHeight = 64;
        private const int HoverDetailHeight = 44;
        private const int RowHeight = 40;
        private const int TimelineHeight = 174;
        private const int FooterHeight = 42;
        private const int BottomPadding = 12;
        private const int VisibleListRows = 3;
        internal const float TimelineAxisLeft = 32f;
        internal const float TimelineAxisRight = DesignWidth - 32f;
        internal const int TimelineViewportDays = 16;
        internal const float TimelineDayWidth =
            (TimelineAxisRight - TimelineAxisLeft) / TimelineViewportDays;

        private readonly ResetHistoryReport report;
        private readonly bool loading;
        private readonly bool darkTheme;
        private readonly float scale;
        private readonly List<TimelineHoverTarget> timelineTargets =
            new List<TimelineHoverTarget>();
        private readonly ResetHistoryInteractionState interaction =
            new ResetHistoryInteractionState();
        private RectangleF moreBounds;
        private RectangleF closeBounds;
        private RectangleF averageBounds;
        private RectangleF shortestBounds;
        private RectangleF longestBounds;
        private RectangleF timelineSliderBounds;
        private RectangleF timelineThumbBounds;
        private int activeHoverTarget = Int32.MinValue;

        public event EventHandler LayoutChanged;
        public event EventHandler CloseRequested;

        public ResetHistorySurface(ResetHistoryReport report, bool loading, bool darkTheme, float scale)
        {
            this.report = report ?? new ResetHistoryReport();
            this.loading = loading;
            this.darkTheme = darkTheme;
            this.scale = Math.Max(1f, Math.Min(3f, scale));
            DoubleBuffered = true;
            TabStop = true;
            AccessibleName = "重置历史";
            AccessibleRole = AccessibleRole.Pane;
            BackColor = darkTheme ? Color.FromArgb(25, 31, 44) : Color.FromArgb(240, 248, 253);
            UpdateSurfaceSize();
            MouseMove += OnSurfaceMouseMove;
            MouseLeave += delegate
            {
                Cursor = Cursors.Default;
                activeHoverTarget = Int32.MinValue;
                Invalidate();
            };
            MouseDown += OnSurfaceMouseDown;
            MouseClick += OnSurfaceMouseClick;
            MouseUp += OnSurfaceMouseUp;
            MouseWheel += OnSurfaceMouseWheel;
            MouseEnter += delegate
            {
                if (CanFocus)
                    Focus();
            };
            KeyDown += OnSurfaceKeyDown;
        }

        internal void ExpandTimeline()
        {
            interaction.Expand(TimelineDays(), TimelineViewportDays);
            UpdateSurfaceSize();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.ScaleTransform(scale, scale);

            int designHeight = Convert.ToInt32(Math.Round(Height / scale));
            RectangleF card = new RectangleF(0.5f, 0.5f, DesignWidth - 1f, designHeight - 1f);
            Color backgroundTop = darkTheme ? Color.FromArgb(250, 24, 31, 45) : Color.FromArgb(255, 250, 253, 255);
            Color backgroundBottom = darkTheme ? Color.FromArgb(250, 34, 43, 61) : Color.FromArgb(255, 228, 241, 249);
            Color primary = darkTheme ? Color.FromArgb(239, 246, 255) : Color.FromArgb(24, 43, 66);
            Color secondary = darkTheme ? Color.FromArgb(167, 185, 207) : Color.FromArgb(89, 112, 137);
            Color border = darkTheme ? Color.FromArgb(78, 88, 170, 224) : Color.FromArgb(74, 92, 157, 203);

            using (GraphicsPath path = RoundedRectangle(card, 18f))
            using (LinearGradientBrush fill = new LinearGradientBrush(card, backgroundTop, backgroundBottom, 90f))
            using (Pen outline = new Pen(border, 0.8f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
            }

            RectangleF titleBounds = new RectangleF(18, 8, 210, 31);
            closeBounds = new RectangleF(DesignWidth - 44, 8, 29, 29);
            using (Font titleFont = PixelFont(16.5f, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(primary))
                DrawText(graphics, "重置历史", titleFont, titleBrush, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);
            DrawCloseButton(graphics, closeBounds, secondary);

            DrawStatistics(graphics, primary, secondary);
            DrawHoverDetail(graphics, primary, secondary);

            List<ResetHistoryEntry> visible = VisibleEntries();
            int contentY = HeaderHeight + StatisticsHeight + HoverDetailHeight + 4;
            timelineTargets.Clear();
            timelineSliderBounds = RectangleF.Empty;
            timelineThumbBounds = RectangleF.Empty;
            if (visible.Count == 0)
            {
                string empty = loading
                    ? "正在整理本机历史日志…"
                    : (String.IsNullOrWhiteSpace(report.Error)
                        ? "尚未检测到可验证的历史重置"
                        : "历史日志暂时无法读取");
                using (Font emptyFont = PixelFont(12.5f, FontStyle.Regular))
                using (Brush emptyBrush = new SolidBrush(secondary))
                    DrawText(graphics, empty, emptyFont, emptyBrush,
                        new RectangleF(16, contentY, DesignWidth - 32, 44),
                        StringAlignment.Center, StringAlignment.Center);
                moreBounds = RectangleF.Empty;
                return;
            }

            int footerY;
            if (interaction.ShowAll && visible.Count > 1)
            {
                DrawTimeline(graphics, visible, contentY, primary, secondary);
                footerY = contentY + TimelineHeight;
            }
            else
            {
                for (int index = 0; index < visible.Count; index++)
                    DrawEntry(graphics, visible[index], index,
                        contentY + index * RowHeight, primary, secondary);
                DrawScrollIndicator(graphics, contentY, visible.Count);
                footerY = contentY + visible.Count * RowHeight;
            }

            if (report.Entries.Count > 1)
            {
                moreBounds = new RectangleF(14, footerY, DesignWidth - 28, FooterHeight - 3);
                string more = interaction.ShowAll
                    ? "收起至最近 3 次"
                    : "点击查看历史重置时间轴";
                using (Font moreFont = PixelFont(12f, FontStyle.Bold))
                using (Brush moreBrush = new SolidBrush(darkTheme
                    ? Color.FromArgb(86, 203, 255)
                    : Color.FromArgb(15, 117, 202)))
                    DrawText(graphics, more, moreFont, moreBrush, moreBounds,
                        StringAlignment.Center, StringAlignment.Center);
            }
            else
            {
                moreBounds = RectangleF.Empty;
            }
        }

        private void DrawStatistics(Graphics graphics, Color primary, Color secondary)
        {
            const float gap = 8f;
            float width = (DesignWidth - 28f - gap * 2f) / 3f;
            averageBounds = new RectangleF(14, HeaderHeight, width, StatisticsHeight - 6);
            shortestBounds = new RectangleF(14 + width + gap, HeaderHeight,
                width, StatisticsHeight - 5);
            longestBounds = new RectangleF(14 + (width + gap) * 2f, HeaderHeight,
                width, StatisticsHeight - 5);

            DrawStatisticCard(graphics, averageBounds, "平均间隔",
                ResetHistoryPresentation.IntervalText(
                    report == null ? null : report.AverageInterval),
                darkTheme ? Color.FromArgb(77, 198, 255) : Color.FromArgb(18, 127, 211),
                primary, secondary);
            DrawStatisticCard(graphics, shortestBounds, "最短间隔",
                ResetHistoryPresentation.IntervalText(
                    report == null ? null : report.ShortestInterval),
                darkTheme ? Color.FromArgb(67, 222, 160) : Color.FromArgb(13, 153, 103),
                primary, secondary);
            DrawStatisticCard(graphics, longestBounds, "最长间隔",
                ResetHistoryPresentation.IntervalText(
                    report == null ? null : report.LongestInterval),
                darkTheme ? Color.FromArgb(190, 135, 255) : Color.FromArgb(112, 82, 208),
                primary, secondary);
        }

        private void DrawStatisticCard(Graphics graphics, RectangleF bounds,
            string label, string value, Color accent, Color primary, Color secondary)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 12f))
            using (Brush fill = new SolidBrush(darkTheme
                ? Color.FromArgb(29, 255, 255, 255)
                : Color.FromArgb(156, 255, 255, 255)))
            using (Pen outline = new Pen(Color.FromArgb(darkTheme ? 34 : 30, accent), 0.7f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
            }

            using (Brush dot = new SolidBrush(accent))
                graphics.FillEllipse(dot, bounds.X + 12, bounds.Y + 12, 7, 7);
            using (Font labelFont = PixelFont(11f, FontStyle.Bold))
            using (Brush labelBrush = new SolidBrush(secondary))
                DrawText(graphics, label, labelFont, labelBrush,
                    new RectangleF(bounds.X + 25, bounds.Y + 5, bounds.Width - 33, 22),
                    StringAlignment.Near, StringAlignment.Center);
            using (Font valueFont = PixelFont(13.5f, FontStyle.Bold))
            using (Brush valueBrush = new SolidBrush(primary))
                DrawText(graphics, value, valueFont, valueBrush,
                    new RectangleF(bounds.X + 12, bounds.Y + 28, bounds.Width - 24, 25),
                    StringAlignment.Near, StringAlignment.Center);
        }

        private void DrawHoverDetail(Graphics graphics, Color primary, Color secondary)
        {
            RectangleF bounds = new RectangleF(
                14, HeaderHeight + StatisticsHeight, DesignWidth - 28, HoverDetailHeight - 6);
            using (GraphicsPath path = RoundedRectangle(bounds, 10f))
            using (Brush fill = new SolidBrush(darkTheme
                ? Color.FromArgb(22, 255, 255, 255)
                : Color.FromArgb(116, 255, 255, 255)))
            {
                graphics.FillPath(fill, path);
            }

            string detail = HoverDetail(activeHoverTarget);
            bool hasDetail = !String.IsNullOrWhiteSpace(detail);
            if (!hasDetail)
                detail = interaction.ShowAll
                    ? "悬停统计项查看预测 · 悬停刻度或蓝点查看日期与时间"
                    : "悬停统计项查看预测 · 滚轮浏览历史 · 点击下方查看时间轴";
            Color accent = activeHoverTarget == 1
                ? (darkTheme ? Color.FromArgb(67, 222, 160) : Color.FromArgb(13, 153, 103))
                : (activeHoverTarget == 2
                    ? (darkTheme ? Color.FromArgb(190, 135, 255) : Color.FromArgb(112, 82, 208))
                    : (darkTheme ? Color.FromArgb(77, 198, 255) : Color.FromArgb(18, 127, 211)));
            using (Brush dot = new SolidBrush(hasDetail ? accent : secondary))
                graphics.FillEllipse(dot, bounds.X + 13, bounds.Y + 15, 7, 7);
            using (Font font = PixelFont(hasDetail ? 11f : 10.5f,
                hasDetail ? FontStyle.Bold : FontStyle.Regular))
            using (Brush textBrush = new SolidBrush(hasDetail ? primary : secondary))
                DrawText(graphics, detail, font, textBrush,
                    new RectangleF(bounds.X + 27, bounds.Y + 3, bounds.Width - 40, bounds.Height - 6),
                    StringAlignment.Near, StringAlignment.Center);
        }

        private void DrawTimeline(Graphics graphics, IList<ResetHistoryEntry> entries,
            int y, Color primary, Color secondary)
        {
            List<ResetHistoryEntry> chronological = entries
                .Where(item => item != null)
                .OrderBy(item => item.ResetUnixSeconds)
                .ToList();
            RectangleF panel = new RectangleF(14, y, DesignWidth - 28, TimelineHeight - 4);
            using (GraphicsPath path = RoundedRectangle(panel, 14f))
            using (Brush fill = new SolidBrush(darkTheme
                ? Color.FromArgb(20, 255, 255, 255)
                : Color.FromArgb(104, 255, 255, 255)))
            using (Pen outline = new Pen(darkTheme
                ? Color.FromArgb(25, 109, 164, 211)
                : Color.FromArgb(25, 54, 118, 163), 0.7f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
            }

            using (Font titleFont = PixelFont(12f, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(primary))
                DrawText(graphics, "历史共 " + chronological.Count + " 次 · 每天一个刻度",
                    titleFont, titleBrush, new RectangleF(29, y + 7, DesignWidth - 58, 23),
                    StringAlignment.Near, StringAlignment.Center);

            float axisLeft = TimelineAxisLeft;
            float axisRight = TimelineAxisRight;
            float axisY = y + 63f;
            List<DateTimeOffset> dayTicks =
                ResetHistoryPresentation.TimelineDays(chronological);
            UpdateTimelineRange(dayTicks);
            float gridTop = axisY - 15f;
            float gridBottom = axisY + 15f;
            using (Pen dayGrid = new Pen(darkTheme
                ? Color.FromArgb(116, 145, 177, 211)
                : Color.FromArgb(108, 72, 126, 165), 1f))
            {
                int lastVisible = Math.Min(dayTicks.Count - 1,
                    interaction.TimelineStartDay + TimelineViewportDays);
                for (int index = interaction.TimelineStartDay; index <= lastVisible; index++)
                {
                    float x = axisLeft +
                        (index - interaction.TimelineStartDay) * TimelineDayWidth;
                    graphics.DrawLine(dayGrid, x, gridTop, x, gridBottom);
                    timelineTargets.Add(new TimelineHoverTarget
                    {
                        Bounds = new RectangleF(x - 6, gridTop - 4, 12,
                            gridBottom - gridTop + 8),
                        Day = dayTicks[index]
                    });
                }
            }
            using (Pen axis = new Pen(darkTheme
                ? Color.FromArgb(94, 147, 177, 211)
                : Color.FromArgb(82, 82, 123, 158), 1.2f))
            {
                axis.StartCap = LineCap.Round;
                axis.EndCap = LineCap.ArrowAnchor;
                graphics.DrawLine(axis, axisLeft, axisY, axisRight, axisY);
            }

            for (int index = 0; index < chronological.Count; index++)
            {
                ResetHistoryEntry entry = chronological[index];
                float coordinate = ResetHistoryPresentation.TimelineDayCoordinate(
                    entry.ResetAt, dayTicks);
                float x = axisLeft +
                    (coordinate - interaction.TimelineStartDay) * TimelineDayWidth;
                if (x < axisLeft - 5f || x > axisRight + 5f)
                    continue;
                Color pointColor = ResetHistoryPresentation.TimelineConfidenceColor(
                    entry, darkTheme);
                using (Brush point = new SolidBrush(pointColor))
                    graphics.FillEllipse(point, x - 4f, axisY - 4f, 8f, 8f);

                TimeSpan? previousInterval = index > 0
                    ? (TimeSpan?)(entry.ResetAt - chronological[index - 1].ResetAt)
                    : null;
                timelineTargets.Add(new TimelineHoverTarget
                {
                    Bounds = new RectangleF(x - 8, axisY - 10, 16, 20),
                    Entry = entry,
                    PreviousInterval = previousInterval
                });
            }

            using (Font dateFont = PixelFont(10f, FontStyle.Bold))
            using (Brush dateBrush = new SolidBrush(secondary))
            {
                DrawText(graphics,
                    dayTicks[interaction.TimelineStartDay].ToString("M月d日"),
                    dateFont, dateBrush,
                    new RectangleF(axisLeft - 1, axisY + 17, 68, 23),
                    StringAlignment.Near, StringAlignment.Center);
            }
            DrawTimelineHoverLabel(graphics, axisY, primary);
            using (Font hintFont = PixelFont(10.5f, FontStyle.Regular))
            using (Brush hintBrush = new SolidBrush(secondary))
                DrawText(graphics, "悬停刻度查看日期 · 悬停蓝点查看重置时间", hintFont, hintBrush,
                    new RectangleF(29, y + 101, DesignWidth - 58, 22),
                    StringAlignment.Center, StringAlignment.Center);
            DrawTimelineSlider(graphics, y, secondary);
        }

        private void DrawTimelineSlider(Graphics graphics, int y, Color secondary)
        {
            RectangleF track = new RectangleF(48, y + 133, DesignWidth - 96, 6);
            int totalDays = interaction.TimelineTotalDays;
            int visibleDays = Math.Min(TimelineViewportDays, Math.Max(1, totalDays));
            float thumbWidth = totalDays <= visibleDays
                ? track.Width
                : Math.Max(44f, track.Width * visibleDays / totalDays);
            float ratio = interaction.TimelineMaximumStartDay <= 0
                ? 0f
                : interaction.TimelineStartDay /
                    (float)interaction.TimelineMaximumStartDay;
            float thumbLeft = track.Left + (track.Width - thumbWidth) * ratio;
            timelineSliderBounds = new RectangleF(
                track.Left - 5, track.Top - 10, track.Width + 10, 27);
            timelineThumbBounds = new RectangleF(thumbLeft, track.Top - 3, thumbWidth, 12);

            using (GraphicsPath trackPath = RoundedRectangle(track, 3f))
            using (Brush trackFill = new SolidBrush(darkTheme
                ? Color.FromArgb(64, 255, 255, 255)
                : Color.FromArgb(44, 50, 91, 126)))
            using (GraphicsPath thumbPath = RoundedRectangle(timelineThumbBounds, 6f))
            using (Brush thumbFill = new LinearGradientBrush(timelineThumbBounds,
                darkTheme ? Color.FromArgb(78, 206, 255) : Color.FromArgb(38, 180, 231),
                darkTheme ? Color.FromArgb(111, 106, 255) : Color.FromArgb(80, 102, 245), 0f))
            {
                graphics.FillPath(trackFill, trackPath);
                graphics.FillPath(thumbFill, thumbPath);
            }

            using (Font font = PixelFont(10.5f, FontStyle.Bold))
            using (Brush brush = new SolidBrush(secondary))
                DrawText(graphics, "拖动滑块浏览全部历史", font, brush,
                    new RectangleF(48, y + 146, DesignWidth - 96, 19),
                    StringAlignment.Center, StringAlignment.Center);
        }

        private void DrawTimelineHoverLabel(Graphics graphics, float axisY, Color primary)
        {
            if (activeHoverTarget < 100 ||
                activeHoverTarget - 100 >= timelineTargets.Count)
                return;

            TimelineHoverTarget target = timelineTargets[activeHoverTarget - 100];
            string label;
            float width;
            if (target.Entry != null)
            {
                label = target.Entry.ResetAt.ToLocalTime().ToString("M月d日 HH:mm");
                width = 102f;
            }
            else if (target.Day.HasValue)
            {
                label = target.Day.Value.ToString("M月d日");
                width = 62f;
            }
            else
            {
                return;
            }

            float center = target.Bounds.Left + target.Bounds.Width / 2f;
            float left = Math.Max(15f, Math.Min(DesignWidth - 15f - width,
                center - width / 2f));
            RectangleF bounds = new RectangleF(left, axisY + 16f, width, 24f);
            using (GraphicsPath path = RoundedRectangle(bounds, 6f))
            using (Brush fill = new SolidBrush(darkTheme
                ? Color.FromArgb(235, 34, 50, 70)
                : Color.FromArgb(238, 248, 252, 255)))
            using (Pen outline = new Pen(darkTheme
                ? Color.FromArgb(94, 74, 199, 255)
                : Color.FromArgb(78, 20, 132, 218), 0.8f))
            using (Font font = PixelFont(10f, FontStyle.Bold))
            using (Brush text = new SolidBrush(primary))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
                DrawText(graphics, label, font, text, bounds,
                    StringAlignment.Center, StringAlignment.Center);
            }
        }

        private void DrawEntry(Graphics graphics, ResetHistoryEntry entry, int index,
            int y, Color primary, Color secondary)
        {
            if (index > 0)
            {
                using (Pen separator = new Pen(darkTheme
                    ? Color.FromArgb(24, 255, 255, 255)
                    : Color.FromArgb(22, 35, 73, 104), 0.7f))
                    graphics.DrawLine(separator, 24, y, DesignWidth - 24, y);
            }

            string date = entry.ResetAt.ToLocalTime().ToString("M月d日 HH:mm");
            string label = ResetHistoryPresentation.EntryStateText(entry);
            Color accent = EntryAccent(entry);

            using (Brush dot = new SolidBrush(accent))
                graphics.FillEllipse(dot, 22, y + 15, 8, 8);
            using (Font dateFont = PixelFont(13.5f, FontStyle.Bold))
            using (Brush dateBrush = new SolidBrush(primary))
                DrawText(graphics, date, dateFont, dateBrush,
                    new RectangleF(38, y + 2, 190, RowHeight - 4),
                    StringAlignment.Near, StringAlignment.Center);
            using (Font labelFont = PixelFont(11.5f, FontStyle.Bold))
            using (Brush labelBrush = new SolidBrush(accent))
                DrawText(graphics, label, labelFont, labelBrush,
                    new RectangleF(230, y + 2, DesignWidth - 250, RowHeight - 4),
                    StringAlignment.Far, StringAlignment.Center);
        }

        private void DrawScrollIndicator(Graphics graphics, int y, int visibleCount)
        {
            int total = report == null || report.Entries == null ? 0 : report.Entries.Count;
            if (total <= VisibleListRows || visibleCount <= 0)
                return;

            float trackTop = y + 8f;
            float trackHeight = visibleCount * RowHeight - 14f;
            float trackX = DesignWidth - 18f;
            float thumbHeight = Math.Max(18f, trackHeight * visibleCount / total);
            int maxOffset = Math.Max(1, total - VisibleListRows);
            float thumbTop = trackTop + (trackHeight - thumbHeight) *
                interaction.ListOffset / maxOffset;
            using (Pen track = new Pen(darkTheme
                ? Color.FromArgb(42, 255, 255, 255)
                : Color.FromArgb(30, 45, 91, 127), 2.2f))
            using (Pen thumb = new Pen(darkTheme
                ? Color.FromArgb(132, 86, 203, 255)
                : Color.FromArgb(126, 18, 127, 211), 2.8f))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                thumb.StartCap = LineCap.Round;
                thumb.EndCap = LineCap.Round;
                graphics.DrawLine(track, trackX, trackTop, trackX, trackTop + trackHeight);
                graphics.DrawLine(thumb, trackX, thumbTop,
                    trackX, thumbTop + thumbHeight);
            }
        }

        private void DrawCloseButton(Graphics graphics, RectangleF bounds, Color color)
        {
            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLine(pen, bounds.X + 8, bounds.Y + 8,
                    bounds.Right - 8, bounds.Bottom - 8);
                graphics.DrawLine(pen, bounds.Right - 8, bounds.Y + 8,
                    bounds.X + 8, bounds.Bottom - 8);
            }
        }

        private void OnSurfaceMouseMove(object sender, MouseEventArgs eventArgs)
        {
            PointF point = ToDesignPoint(eventArgs.Location);
            if (interaction.DraggingTimelineSlider)
            {
                UpdateTimelineFromSlider(point.X);
                Cursor = Cursors.SizeWE;
                return;
            }

            int hoverTarget = HoverTargetAt(point);
            bool action = closeBounds.Contains(point) || moreBounds.Contains(point);
            bool sliderAction = interaction.ShowAll &&
                interaction.TimelineMaximumStartDay > 0 &&
                timelineSliderBounds.Contains(point);
            Cursor = sliderAction
                ? Cursors.SizeWE
                : (action || hoverTarget >= 0
                    ? (action ? Cursors.Hand : Cursors.Help)
                    : Cursors.Default);

            if (hoverTarget == activeHoverTarget)
                return;

            activeHoverTarget = hoverTarget;
            Invalidate();
        }

        private void OnSurfaceMouseDown(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left)
                return;
            PointF point = ToDesignPoint(eventArgs.Location);
            if (!interaction.BeginTimelineDrag(
                    point, timelineSliderBounds, timelineThumbBounds))
                return;

            Capture = true;
            UpdateTimelineFromSlider(point.X);
        }

        private void OnSurfaceMouseUp(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left ||
                !interaction.DraggingTimelineSlider)
                return;
            interaction.EndTimelineDrag();
            Capture = false;
            Cursor = Cursors.SizeWE;
        }

        private void UpdateTimelineFromSlider(float pointerX)
        {
            const float trackLeft = 48f;
            const float trackWidth = DesignWidth - 96f;
            if (!interaction.UpdateTimelineFromSlider(
                    pointerX, trackLeft, trackWidth, timelineThumbBounds.Width))
                return;
            activeHoverTarget = Int32.MinValue;
            Invalidate();
        }

        private int HoverTargetAt(PointF point)
        {
            if (averageBounds.Contains(point))
                return 0;
            if (shortestBounds.Contains(point))
                return 1;
            if (longestBounds.Contains(point))
                return 2;

            for (int priority = 0; priority < 2; priority++)
            {
                bool resetPoint = priority == 0;
                int bestIndex = -1;
                float bestDistance = Single.MaxValue;
                for (int index = 0; index < timelineTargets.Count; index++)
                {
                    TimelineHoverTarget target = timelineTargets[index];
                    if ((target.Entry != null) != resetPoint ||
                        !target.Bounds.Contains(point))
                        continue;
                    float center = target.Bounds.Left + target.Bounds.Width / 2f;
                    float distance = Math.Abs(center - point.X);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = index;
                    }
                }
                if (bestIndex >= 0)
                    return 100 + bestIndex;
            }
            return -1;
        }

        private string HoverDetail(int target)
        {
            if (target == 0)
                return ResetHistoryPresentation.ForecastInlineText(
                    report, report == null ? null : report.AverageInterval,
                    "平均", DateTimeOffset.Now);
            if (target == 1)
                return ResetHistoryPresentation.ForecastInlineText(
                    report, report == null ? null : report.ShortestInterval,
                    "最短", DateTimeOffset.Now);
            if (target == 2)
                return ResetHistoryPresentation.ForecastInlineText(
                    report, report == null ? null : report.LongestInterval,
                    "最长", DateTimeOffset.Now);
            if (target < 100 || target - 100 >= timelineTargets.Count)
                return null;

            TimelineHoverTarget timeline = timelineTargets[target - 100];
            ResetHistoryEntry entry = timeline.Entry;
            if (entry == null && timeline.Day.HasValue)
                return timeline.Day.Value.ToString("M月d日") + " · 每日刻度";
            if (entry == null)
                return null;
            string detail = entry.ResetAt.ToLocalTime().ToString("M月d日 HH:mm:ss") +
                " · " + ResetHistoryPresentation.EntryStateText(entry);
            if (timeline.PreviousInterval.HasValue)
                detail += " · 距上一次 " +
                    ResetHistoryPresentation.IntervalText(timeline.PreviousInterval);
            return detail;
        }

        private void OnSurfaceMouseClick(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left)
                return;
            PointF point = ToDesignPoint(eventArgs.Location);
            if (closeBounds.Contains(point))
            {
                EventHandler handler = CloseRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
                return;
            }
            if (moreBounds.Contains(point))
            {
                interaction.Toggle(TimelineDays(), TimelineViewportDays);
                activeHoverTarget = Int32.MinValue;
                UpdateSurfaceSize();
                Invalidate();
                EventHandler handler = LayoutChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        private void OnSurfaceMouseWheel(object sender, MouseEventArgs eventArgs)
        {
            if (interaction.ShowAll || eventArgs.Delta == 0)
                return;

            int notches = Math.Max(1,
                Math.Abs(eventArgs.Delta) / SystemInformation.MouseWheelScrollDelta);
            ScrollHistory((eventArgs.Delta < 0 ? 1 : -1) * notches);
            HandledMouseEventArgs handled = eventArgs as HandledMouseEventArgs;
            if (handled != null)
                handled.Handled = true;
        }

        internal void ScrollHistory(int steps)
        {
            int total = report == null || report.Entries == null ? 0 : report.Entries.Count;
            if (!interaction.ScrollList(total, VisibleListRows, steps))
                return;
            activeHoverTarget = Int32.MinValue;
            Invalidate();
        }

        internal int ListOffset
        {
            get { return interaction.ListOffset; }
        }

        internal int TimelineStartDay
        {
            get { return interaction.TimelineStartDay; }
        }

        internal int TimelineMaximumStartDay
        {
            get { return interaction.TimelineMaximumStartDay; }
        }

        internal RectangleF TimelineSliderBounds
        {
            get { return timelineSliderBounds; }
        }

        private void OnSurfaceKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode == Keys.Escape)
            {
                EventHandler handler = CloseRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
                eventArgs.Handled = true;
            }
        }

        private void UpdateSurfaceSize()
        {
            int rows = VisibleEntries().Count;
            int emptyHeight = rows == 0 ? 52 : 0;
            int footer = report != null && report.Entries.Count > 1 ? FooterHeight : 0;
            int contentHeight = rows == 0
                ? emptyHeight
                : (interaction.ShowAll && rows > 1 ? TimelineHeight : rows * RowHeight);
            int designHeight = HeaderHeight + StatisticsHeight + HoverDetailHeight + 4 +
                contentHeight + footer + BottomPadding;
            Size = new Size(Px(DesignWidth), Px(designHeight));
        }

        private List<ResetHistoryEntry> VisibleEntries()
        {
            if (report == null || report.Entries == null)
                return new List<ResetHistoryEntry>();
            return interaction.VisibleEntries(report.Entries, VisibleListRows);
        }

        private List<DateTimeOffset> TimelineDays()
        {
            return ResetHistoryPresentation.TimelineDays(
                report == null ? null : report.Entries);
        }

        private void UpdateTimelineRange(IList<DateTimeOffset> days)
        {
            interaction.UpdateTimelineRange(days, TimelineViewportDays);
        }

        private Color EntryAccent(ResetHistoryEntry entry)
        {
            return ResetHistoryPresentation.TimelineConfidenceColor(entry, darkTheme);
        }

        private PointF ToDesignPoint(Point point)
        {
            return new PointF(point.X / scale, point.Y / scale);
        }

        private int Px(float value)
        {
            return Math.Max(1, Convert.ToInt32(Math.Round(value * scale)));
        }

        private static Font PixelFont(float size, FontStyle style)
        {
            return UiDrawing.PixelFont(size, style);
        }

        private static void DrawText(Graphics graphics, string text, Font font, Brush brush,
            RectangleF bounds, StringAlignment horizontal, StringAlignment vertical)
        {
            UiDrawing.DrawText(
                graphics, text, font, brush, bounds, horizontal, vertical);
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            return UiDrawing.RoundedRectangle(bounds, radius);
        }

        private sealed class TimelineHoverTarget
        {
            public RectangleF Bounds { get; set; }
            public ResetHistoryEntry Entry { get; set; }
            public DateTimeOffset? Day { get; set; }
            public TimeSpan? PreviousInterval { get; set; }
        }
    }
}
