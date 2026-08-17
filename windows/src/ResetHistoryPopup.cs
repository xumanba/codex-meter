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
        private const int DesignWidth = 292;
        private const int HeaderHeight = 40;
        private const int SummaryHeight = 31;
        private const int RowHeight = 32;
        private const int FooterHeight = 31;
        private const int BottomPadding = 8;

        private readonly ResetHistoryReport report;
        private readonly bool loading;
        private readonly bool darkTheme;
        private readonly float scale;
        private bool showAll;
        private RectangleF moreBounds;
        private RectangleF closeBounds;

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
            MouseLeave += delegate { Cursor = Cursors.Default; };
            MouseClick += OnSurfaceMouseClick;
            KeyDown += OnSurfaceKeyDown;
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

            RectangleF summaryBounds = new RectangleF(12, HeaderHeight, DesignWidth - 24, SummaryHeight - 3);
            using (GraphicsPath summaryPath = RoundedRectangle(summaryBounds, 10f))
            using (Brush summaryFill = new SolidBrush(darkTheme
                ? Color.FromArgb(27, 255, 255, 255)
                : Color.FromArgb(145, 255, 255, 255)))
            {
                graphics.FillPath(summaryFill, summaryPath);
            }

            string average = AverageText(report);
            using (Font summaryFont = PixelFont(11.5f, FontStyle.Bold))
            using (Brush summaryBrush = new SolidBrush(primary))
                DrawText(graphics, average, summaryFont, summaryBrush,
                    new RectangleF(22, HeaderHeight, DesignWidth - 44, SummaryHeight - 3),
                    StringAlignment.Near, StringAlignment.Center);

            List<ResetHistoryEntry> visible = VisibleEntries();
            int rowY = HeaderHeight + SummaryHeight + 2;
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
                        new RectangleF(16, rowY, DesignWidth - 32, 42),
                        StringAlignment.Center, StringAlignment.Center);
                moreBounds = RectangleF.Empty;
                return;
            }

            for (int index = 0; index < visible.Count; index++)
                DrawEntry(graphics, visible[index], index, rowY + index * RowHeight, primary, secondary);

            int footerY = rowY + visible.Count * RowHeight;
            if (report.Entries.Count > 3)
            {
                moreBounds = new RectangleF(12, footerY, DesignWidth - 24, FooterHeight - 3);
                string more = showAll
                    ? "收起至最近 3 次"
                    : "查看最近 " + Math.Min(10, report.Entries.Count) + " 次";
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
            string state = entry.IsEstimated ? "推算" : "检测";
            string confidence = entry.Confidence >= (int)ResetConfidence.High
                ? "高" : (entry.Confidence >= (int)ResetConfidence.Medium ? "中" : "低");
            string label = state + " · " + confidence;
            Color accent = entry.IsEstimated
                ? (darkTheme ? Color.FromArgb(255, 190, 88) : Color.FromArgb(211, 119, 20))
                : (darkTheme ? Color.FromArgb(62, 222, 145) : Color.FromArgb(18, 158, 103));

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
            Cursor = closeBounds.Contains(point) || moreBounds.Contains(point)
                ? Cursors.Hand : Cursors.Default;
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
            int designHeight = HeaderHeight + SummaryHeight + 2 +
                (rows * RowHeight) + emptyHeight + footer + BottomPadding;
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

            TimeSpan interval = value.AverageInterval.Value;
            int totalHours = Math.Max(0, Convert.ToInt32(Math.Round(interval.TotalHours)));
            int days = totalHours / 24;
            int hours = totalHours % 24;
            string duration = days > 0
                ? days + "天" + (hours > 0 ? hours + "小时" : String.Empty)
                : hours + "小时";
            return "平均间隔 " + duration + " · " + value.AverageIntervalCount + " 个区间";
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
    }
}
