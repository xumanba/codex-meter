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
        private const int DesignWidth = 420;
        private const int HeaderHeight = 42;
        private const int StatisticsHeight = 54;
        private const int HoverDetailHeight = 38;
        private const int RowHeight = 34;
        private const int TimelineHeight = 126;
        private const int FooterHeight = 34;
        private const int BottomPadding = 9;

        private readonly ResetHistoryReport report;
        private readonly bool loading;
        private readonly bool darkTheme;
        private readonly float scale;
        private readonly List<TimelineHoverTarget> timelineTargets =
            new List<TimelineHoverTarget>();
        private bool showAll;
        private RectangleF moreBounds;
        private RectangleF closeBounds;
        private RectangleF averageBounds;
        private RectangleF shortestBounds;
        private RectangleF longestBounds;
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
            MouseClick += OnSurfaceMouseClick;
            KeyDown += OnSurfaceKeyDown;
        }

        internal void ExpandTimeline()
        {
            showAll = true;
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

            using (GraphicsPath path = RoundedRectangle(card, 16f))
            using (LinearGradientBrush fill = new LinearGradientBrush(card, backgroundTop, backgroundBottom, 90f))
            using (Pen outline = new Pen(border, 0.8f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
            }

            RectangleF titleBounds = new RectangleF(15, 7, 170, 27);
            closeBounds = new RectangleF(DesignWidth - 38, 7, 25, 25);
            using (Font titleFont = PixelFont(14f, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(primary))
                DrawText(graphics, "重置历史", titleFont, titleBrush, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);
            DrawCloseButton(graphics, closeBounds, secondary);

            DrawStatistics(graphics, primary, secondary);
            DrawHoverDetail(graphics, primary, secondary);

            List<ResetHistoryEntry> visible = VisibleEntries();
            int contentY = HeaderHeight + StatisticsHeight + HoverDetailHeight + 4;
            timelineTargets.Clear();
            if (visible.Count == 0)
            {
                string empty = loading
                    ? "正在整理本机历史日志…"
                    : (String.IsNullOrWhiteSpace(report.Error)
                        ? "尚未检测到可验证的历史重置"
                        : "历史日志暂时无法读取");
                using (Font emptyFont = PixelFont(11f, FontStyle.Regular))
                using (Brush emptyBrush = new SolidBrush(secondary))
                    DrawText(graphics, empty, emptyFont, emptyBrush,
                        new RectangleF(16, contentY, DesignWidth - 32, 44),
                        StringAlignment.Center, StringAlignment.Center);
                moreBounds = RectangleF.Empty;
                return;
            }

            int footerY;
            if (showAll && visible.Count > 1)
            {
                DrawTimeline(graphics, visible, contentY, primary, secondary);
                footerY = contentY + TimelineHeight;
            }
            else
            {
                for (int index = 0; index < visible.Count; index++)
                    DrawEntry(graphics, visible[index], index,
                        contentY + index * RowHeight, primary, secondary);
                footerY = contentY + visible.Count * RowHeight;
            }

            if (report.Entries.Count > 3)
            {
                moreBounds = new RectangleF(12, footerY, DesignWidth - 24, FooterHeight - 3);
                string more = showAll
                    ? "收起至最近 3 次"
                    : "在时间轴中查看最近 " + Math.Min(10, report.Entries.Count) + " 次";
                using (Font moreFont = PixelFont(10.5f, FontStyle.Bold))
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
            const float gap = 7f;
            float width = (DesignWidth - 24f - gap * 2f) / 3f;
            averageBounds = new RectangleF(12, HeaderHeight, width, StatisticsHeight - 5);
            shortestBounds = new RectangleF(12 + width + gap, HeaderHeight,
                width, StatisticsHeight - 5);
            longestBounds = new RectangleF(12 + (width + gap) * 2f, HeaderHeight,
                width, StatisticsHeight - 5);

            DrawStatisticCard(graphics, averageBounds, "平均间隔",
                IntervalText(report == null ? null : report.AverageInterval),
                darkTheme ? Color.FromArgb(77, 198, 255) : Color.FromArgb(18, 127, 211),
                primary, secondary);
            DrawStatisticCard(graphics, shortestBounds, "最短间隔",
                IntervalText(report == null ? null : report.ShortestInterval),
                darkTheme ? Color.FromArgb(67, 222, 160) : Color.FromArgb(13, 153, 103),
                primary, secondary);
            DrawStatisticCard(graphics, longestBounds, "最长间隔",
                IntervalText(report == null ? null : report.LongestInterval),
                darkTheme ? Color.FromArgb(190, 135, 255) : Color.FromArgb(112, 82, 208),
                primary, secondary);
        }

        private void DrawStatisticCard(Graphics graphics, RectangleF bounds,
            string label, string value, Color accent, Color primary, Color secondary)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 10f))
            using (Brush fill = new SolidBrush(darkTheme
                ? Color.FromArgb(29, 255, 255, 255)
                : Color.FromArgb(156, 255, 255, 255)))
            using (Pen outline = new Pen(Color.FromArgb(darkTheme ? 34 : 30, accent), 0.7f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
            }

            using (Brush dot = new SolidBrush(accent))
                graphics.FillEllipse(dot, bounds.X + 10, bounds.Y + 10, 6, 6);
            using (Font labelFont = PixelFont(9.5f, FontStyle.Bold))
            using (Brush labelBrush = new SolidBrush(secondary))
                DrawText(graphics, label, labelFont, labelBrush,
                    new RectangleF(bounds.X + 21, bounds.Y + 4, bounds.Width - 28, 19),
                    StringAlignment.Near, StringAlignment.Center);
            using (Font valueFont = PixelFont(11.5f, FontStyle.Bold))
            using (Brush valueBrush = new SolidBrush(primary))
                DrawText(graphics, value, valueFont, valueBrush,
                    new RectangleF(bounds.X + 10, bounds.Y + 22, bounds.Width - 20, 22),
                    StringAlignment.Near, StringAlignment.Center);
        }

        private void DrawHoverDetail(Graphics graphics, Color primary, Color secondary)
        {
            RectangleF bounds = new RectangleF(
                12, HeaderHeight + StatisticsHeight, DesignWidth - 24, HoverDetailHeight - 5);
            using (GraphicsPath path = RoundedRectangle(bounds, 9f))
            using (Brush fill = new SolidBrush(darkTheme
                ? Color.FromArgb(22, 255, 255, 255)
                : Color.FromArgb(116, 255, 255, 255)))
            {
                graphics.FillPath(fill, path);
            }

            string detail = HoverDetail(activeHoverTarget);
            bool hasDetail = !String.IsNullOrWhiteSpace(detail);
            if (!hasDetail)
                detail = "悬停上方统计项查看预测 · 悬停时间轴节点查看详情";
            Color accent = activeHoverTarget == 1
                ? (darkTheme ? Color.FromArgb(67, 222, 160) : Color.FromArgb(13, 153, 103))
                : (activeHoverTarget == 2
                    ? (darkTheme ? Color.FromArgb(190, 135, 255) : Color.FromArgb(112, 82, 208))
                    : (darkTheme ? Color.FromArgb(77, 198, 255) : Color.FromArgb(18, 127, 211)));
            using (Brush dot = new SolidBrush(hasDetail ? accent : secondary))
                graphics.FillEllipse(dot, bounds.X + 11, bounds.Y + 13, 6, 6);
            using (Font font = PixelFont(hasDetail ? 9.5f : 9f,
                hasDetail ? FontStyle.Bold : FontStyle.Regular))
            using (Brush textBrush = new SolidBrush(hasDetail ? primary : secondary))
                DrawText(graphics, detail, font, textBrush,
                    new RectangleF(bounds.X + 23, bounds.Y + 3, bounds.Width - 34, bounds.Height - 6),
                    StringAlignment.Near, StringAlignment.Center);
        }

        private void DrawTimeline(Graphics graphics, IList<ResetHistoryEntry> entries,
            int y, Color primary, Color secondary)
        {
            List<ResetHistoryEntry> chronological = entries
                .Where(item => item != null)
                .OrderBy(item => item.ResetUnixSeconds)
                .ToList();
            RectangleF panel = new RectangleF(12, y, DesignWidth - 24, TimelineHeight - 4);
            using (GraphicsPath path = RoundedRectangle(panel, 12f))
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

            using (Font titleFont = PixelFont(10.5f, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(primary))
                DrawText(graphics, "最近 " + chronological.Count + " 次 · 位置按实际时间比例",
                    titleFont, titleBrush, new RectangleF(24, y + 5, DesignWidth - 48, 20),
                    StringAlignment.Near, StringAlignment.Center);

            float axisLeft = 29f;
            float axisRight = DesignWidth - 29f;
            float axisY = y + 52f;
            using (Pen axis = new Pen(darkTheme
                ? Color.FromArgb(94, 147, 177, 211)
                : Color.FromArgb(82, 82, 123, 158), 1.2f))
            {
                axis.StartCap = LineCap.Round;
                axis.EndCap = LineCap.ArrowAnchor;
                graphics.DrawLine(axis, axisLeft, axisY, axisRight, axisY);
            }

            long oldest = chronological[0].ResetUnixSeconds;
            long newest = chronological[chronological.Count - 1].ResetUnixSeconds;
            for (int index = 0; index < chronological.Count; index++)
            {
                ResetHistoryEntry entry = chronological[index];
                float x = TimelineX(entry.ResetUnixSeconds, oldest, newest, axisLeft, axisRight);
                Color accent = EntryAccent(entry);
                using (Pen tick = new Pen(Color.FromArgb(darkTheme ? 155 : 135, accent), 1f))
                    graphics.DrawLine(tick, x, axisY - 8, x, axisY + 8);
                using (Brush halo = new SolidBrush(Color.FromArgb(darkTheme ? 54 : 43, accent)))
                    graphics.FillEllipse(halo, x - 7, axisY - 7, 14, 14);
                using (Brush point = new SolidBrush(accent))
                    graphics.FillEllipse(point, x - 3.5f, axisY - 3.5f, 7, 7);

                TimeSpan? previousInterval = index > 0
                    ? (TimeSpan?)(entry.ResetAt - chronological[index - 1].ResetAt)
                    : null;
                timelineTargets.Add(new TimelineHoverTarget
                {
                    Bounds = new RectangleF(x - 8, axisY - 15, 16, 30),
                    Entry = entry,
                    PreviousInterval = previousInterval
                });
            }

            string oldestText = chronological[0].ResetAt.ToLocalTime().ToString("M月d日");
            string newestText = chronological[chronological.Count - 1].ResetAt.ToLocalTime().ToString("M月d日");
            using (Font dateFont = PixelFont(9.5f, FontStyle.Bold))
            using (Brush dateBrush = new SolidBrush(secondary))
            {
                DrawText(graphics, oldestText, dateFont, dateBrush,
                    new RectangleF(axisLeft, axisY + 13, 90, 20),
                    StringAlignment.Near, StringAlignment.Center);
                DrawText(graphics, newestText, dateFont, dateBrush,
                    new RectangleF(axisRight - 90, axisY + 13, 90, 20),
                    StringAlignment.Far, StringAlignment.Center);
            }
            using (Font hintFont = PixelFont(9f, FontStyle.Regular))
            using (Brush hintBrush = new SolidBrush(secondary))
                DrawText(graphics, "悬停节点查看具体时间与相邻间隔", hintFont, hintBrush,
                    new RectangleF(24, y + 91, DesignWidth - 48, 20),
                    StringAlignment.Center, StringAlignment.Center);
        }

        private void DrawEntry(Graphics graphics, ResetHistoryEntry entry, int index,
            int y, Color primary, Color secondary)
        {
            if (index > 0)
            {
                using (Pen separator = new Pen(darkTheme
                    ? Color.FromArgb(24, 255, 255, 255)
                    : Color.FromArgb(22, 35, 73, 104), 0.7f))
                    graphics.DrawLine(separator, 20, y, DesignWidth - 20, y);
            }

            string date = entry.ResetAt.ToLocalTime().ToString("M月d日 HH:mm");
            string label = EntryStateText(entry);
            Color accent = EntryAccent(entry);

            using (Brush dot = new SolidBrush(accent))
                graphics.FillEllipse(dot, 18, y + 12, 7, 7);
            using (Font dateFont = PixelFont(11.5f, FontStyle.Bold))
            using (Brush dateBrush = new SolidBrush(primary))
                DrawText(graphics, date, dateFont, dateBrush,
                    new RectangleF(31, y + 2, 154, RowHeight - 4),
                    StringAlignment.Near, StringAlignment.Center);
            using (Font labelFont = PixelFont(10f, FontStyle.Bold))
            using (Brush labelBrush = new SolidBrush(accent))
                DrawText(graphics, label, labelFont, labelBrush,
                    new RectangleF(188, y + 2, DesignWidth - 204, RowHeight - 4),
                    StringAlignment.Far, StringAlignment.Center);
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
            int hoverTarget = HoverTargetAt(point);
            bool action = closeBounds.Contains(point) || moreBounds.Contains(point);
            Cursor = action || hoverTarget >= 0
                ? (action ? Cursors.Hand : Cursors.Help)
                : Cursors.Default;

            if (hoverTarget == activeHoverTarget)
                return;

            activeHoverTarget = hoverTarget;
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

            int bestIndex = -1;
            float bestDistance = Single.MaxValue;
            for (int index = 0; index < timelineTargets.Count; index++)
            {
                TimelineHoverTarget target = timelineTargets[index];
                if (!target.Bounds.Contains(point))
                    continue;
                float center = target.Bounds.Left + target.Bounds.Width / 2f;
                float distance = Math.Abs(center - point.X);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = index;
                }
            }
            return bestIndex >= 0 ? 100 + bestIndex : -1;
        }

        private string HoverDetail(int target)
        {
            if (target == 0)
                return ForecastInlineText(report, report == null ? null : report.AverageInterval,
                    "平均", DateTimeOffset.Now);
            if (target == 1)
                return ForecastInlineText(report, report == null ? null : report.ShortestInterval,
                    "最短", DateTimeOffset.Now);
            if (target == 2)
                return ForecastInlineText(report, report == null ? null : report.LongestInterval,
                    "最长", DateTimeOffset.Now);
            if (target < 100 || target - 100 >= timelineTargets.Count)
                return null;

            TimelineHoverTarget timeline = timelineTargets[target - 100];
            ResetHistoryEntry entry = timeline.Entry;
            string detail = entry.ResetAt.ToLocalTime().ToString("M月d日 HH:mm:ss") +
                " · " + EntryStateText(entry);
            if (timeline.PreviousInterval.HasValue)
                detail += " · 距上一次 " + IntervalText(timeline.PreviousInterval);
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
                showAll = !showAll;
                activeHoverTarget = Int32.MinValue;
                UpdateSurfaceSize();
                Invalidate();
                EventHandler handler = LayoutChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
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
            int emptyHeight = rows == 0 ? 44 : 0;
            int footer = report != null && report.Entries.Count > 3 ? FooterHeight : 0;
            int contentHeight = rows == 0
                ? emptyHeight
                : (showAll && rows > 1 ? TimelineHeight : rows * RowHeight);
            int designHeight = HeaderHeight + StatisticsHeight + HoverDetailHeight + 4 +
                contentHeight + footer + BottomPadding;
            Size = new Size(Px(DesignWidth), Px(designHeight));
        }

        private List<ResetHistoryEntry> VisibleEntries()
        {
            if (report == null || report.Entries == null)
                return new List<ResetHistoryEntry>();
            int count = showAll ? 10 : 3;
            return report.Entries.Take(count).ToList();
        }

        internal static string AverageText(ResetHistoryReport value)
        {
            if (value == null || !value.AverageInterval.HasValue || value.AverageIntervalCount <= 0)
                return "平均间隔：样本不足";

            return "平均间隔 " + IntervalText(value.AverageInterval) + " · " +
                value.AverageIntervalCount + " 个区间";
        }

        internal static string IntervalText(TimeSpan? value)
        {
            if (!value.HasValue)
                return "样本不足";

            int totalMinutes = Math.Max(0,
                Convert.ToInt32(Math.Round(Math.Abs(value.Value.TotalMinutes))));
            int days = totalMinutes / (24 * 60);
            int hours = (totalMinutes / 60) % 24;
            int minutes = totalMinutes % 60;
            if (days > 0)
                return days + "天" + (hours > 0 ? hours + "小时" : String.Empty);
            if (hours > 0)
                return hours + "小时" + (minutes > 0 ? minutes + "分钟" : String.Empty);
            return minutes + "分钟";
        }

        internal static string ForecastText(ResetHistoryReport value, TimeSpan? interval,
            string label, DateTimeOffset now)
        {
            if (value == null || !interval.HasValue || value.Entries == null)
                return label + "间隔：样本不足";

            ResetHistoryEntry latest = value.Entries
                .Where(item => item != null &&
                    item.Confidence >= (int)ResetConfidence.Medium)
                .OrderByDescending(item => item.ResetUnixSeconds)
                .FirstOrDefault();
            if (latest == null)
                return label + "间隔：样本不足";

            DateTimeOffset predicted = latest.ResetAt.Add(interval.Value);
            TimeSpan remaining = predicted - now;
            string timing = remaining >= TimeSpan.Zero
                ? "预计还有 " + IntervalText(remaining)
                : "预计时间已过 " + IntervalText(remaining.Duration()) +
                    "，尚未检测到新重置";
            return "按" + label + "间隔推算\r\n预计重置：" +
                predicted.ToLocalTime().ToString("M月d日 HH:mm") + "\r\n" + timing;
        }

        internal static string ForecastInlineText(ResetHistoryReport value, TimeSpan? interval,
            string label, DateTimeOffset now)
        {
            if (value == null || !interval.HasValue || value.Entries == null)
                return label + "间隔：样本不足";

            ResetHistoryEntry latest = value.Entries
                .Where(item => item != null &&
                    item.Confidence >= (int)ResetConfidence.Medium)
                .OrderByDescending(item => item.ResetUnixSeconds)
                .FirstOrDefault();
            if (latest == null)
                return label + "间隔：样本不足";

            DateTimeOffset predicted = latest.ResetAt.Add(interval.Value);
            TimeSpan remaining = predicted - now;
            string timing = remaining >= TimeSpan.Zero
                ? "还有 " + IntervalText(remaining)
                : "已过 " + IntervalText(remaining.Duration()) + "，尚未检测到新重置";
            return label + "推算：" + predicted.ToLocalTime().ToString("M月d日 HH:mm") +
                " · " + timing;
        }

        internal static float TimelineX(long timestamp, long oldest, long newest,
            float left, float right)
        {
            if (newest <= oldest)
                return (left + right) / 2f;
            double ratio = (timestamp - oldest) / (double)(newest - oldest);
            ratio = Math.Max(0, Math.Min(1, ratio));
            return left + Convert.ToSingle((right - left) * ratio);
        }

        private Color EntryAccent(ResetHistoryEntry entry)
        {
            if (entry != null && !entry.IsEstimated)
                return darkTheme ? Color.FromArgb(62, 222, 145) : Color.FromArgb(18, 158, 103);
            if (entry != null && entry.Confidence < (int)ResetConfidence.Medium)
                return darkTheme ? Color.FromArgb(255, 190, 88) : Color.FromArgb(211, 119, 20);
            return darkTheme ? Color.FromArgb(86, 203, 255) : Color.FromArgb(18, 127, 211);
        }

        private static string EntryStateText(ResetHistoryEntry entry)
        {
            if (entry == null)
                return String.Empty;
            string state = entry.IsEstimated ? "推算" : "检测";
            string confidence = entry.Confidence >= (int)ResetConfidence.High
                ? "高" : (entry.Confidence >= (int)ResetConfidence.Medium ? "中" : "低");
            return state + " · " + confidence;
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
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Pixel);
        }

        private static void DrawText(Graphics graphics, string text, Font font, Brush brush,
            RectangleF bounds, StringAlignment horizontal, StringAlignment vertical)
        {
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment = horizontal;
                format.LineAlignment = vertical;
                format.FormatFlags |= StringFormatFlags.NoWrap;
                format.Trimming = StringTrimming.EllipsisCharacter;
                graphics.DrawString(text ?? String.Empty, font, brush, bounds, format);
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class TimelineHoverTarget
        {
            public RectangleF Bounds { get; set; }
            public ResetHistoryEntry Entry { get; set; }
            public TimeSpan? PreviousInterval { get; set; }
        }
    }
}
