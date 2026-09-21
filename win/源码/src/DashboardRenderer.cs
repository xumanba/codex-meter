using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace CodexMeter
{
    internal sealed class DashboardHeaderLayout
    {
        internal Rectangle NetworkSpeedBounds { get; set; }
        internal Rectangle SyncButtonBounds { get; set; }
        internal Rectangle MenuButtonBounds { get; set; }
    }

    internal sealed class DashboardMeterLayout
    {
        internal Rectangle ResetBounds { get; set; }
        internal Rectangle BudgetMarkerBounds { get; set; }
        internal float BudgetMarkerDesignX { get; set; }
        internal string BudgetToolTipText { get; set; }
    }

    internal static class DashboardRenderer
    {
        private const float SectionTitleFontSize = 14f;
        private const float SupportingTextFontSize = 12.5f;

        internal static DashboardHeaderLayout HeaderLayout(float scale)
        {
            scale = Math.Max(1f, Math.Min(3f, scale));
            return new DashboardHeaderLayout
            {
                NetworkSpeedBounds = ScaleRectangle(new RectangleF(160, 8, 70, 41), scale),
                SyncButtonBounds = ScaleRectangle(new RectangleF(234, 15, 58, 26), scale),
                MenuButtonBounds = ScaleRectangle(new RectangleF(297, 15, 24, 26), scale)
            };
        }

        internal static void DrawCard(Graphics graphics, int designHeight, bool darkTheme)
        {
            int designWidth = DashboardPresentation.DesignWidth;
            RectangleF bounds = new RectangleF(0.5f, 0.5f,
                designWidth - 1f, designHeight - 1f);
            using (GraphicsPath path = UiDrawing.RoundedRectangle(bounds, 22f))
            using (LinearGradientBrush baseBrush = new LinearGradientBrush(
                bounds,
                darkTheme ? Color.FromArgb(250, 22, 29, 43) : Color.FromArgb(255, 250, 253, 255),
                darkTheme ? Color.FromArgb(250, 35, 43, 61) : Color.FromArgb(255, 225, 239, 248),
                28f))
            {
                graphics.FillPath(baseBrush, path);

                GraphicsState state = graphics.Save();
                graphics.SetClip(path);
                DrawAmbientGlow(graphics, new RectangleF(-74, -78, 245, 176),
                    darkTheme ? Color.FromArgb(54, 21, 176, 255) : Color.FromArgb(74, 46, 198, 255));
                DrawAmbientGlow(graphics,
                    new RectangleF(210, designHeight - 118, 208, 168),
                    darkTheme ? Color.FromArgb(40, 126, 84, 255) : Color.FromArgb(48, 111, 84, 255));
                DrawTechGrid(graphics, designHeight, darkTheme);

                using (LinearGradientBrush highlight = new LinearGradientBrush(
                    new RectangleF(0, 0, designWidth, Math.Max(80, designHeight)),
                    darkTheme ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(188, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    graphics.FillPath(highlight, path);
                }
                graphics.Restore(state);

                using (Pen border = new Pen(
                    darkTheme ? Color.FromArgb(88, 150, 214, 255) : Color.FromArgb(220, 255, 255, 255), 1f))
                    graphics.DrawPath(border, path);

                using (Pen innerBorder = new Pen(
                    darkTheme ? Color.FromArgb(30, 104, 186, 255) : Color.FromArgb(58, 96, 178, 222), 0.7f))
                using (GraphicsPath innerPath = UiDrawing.RoundedRectangle(
                    new RectangleF(2.5f, 2.5f, designWidth - 5f, designHeight - 5f), 20f))
                    graphics.DrawPath(innerBorder, innerPath);

                using (LinearGradientBrush accent = new LinearGradientBrush(
                    new RectangleF(20, 1, designWidth - 40, 2),
                    Color.FromArgb(0, 24, 193, 255),
                    Color.FromArgb(190, 103, 82, 255), 0f))
                {
                    graphics.FillRectangle(accent, 20, 1, designWidth - 40, 1.4f);
                }
            }
        }

        internal static DashboardHeaderLayout DrawHeader(Graphics graphics, Bitmap appIcon,
            NetworkSpeedSnapshot networkSpeed, string statusText, Color statusDotColor,
            bool syncButtonHovered, bool darkTheme, float scale)
        {
            int designWidth = DashboardPresentation.DesignWidth;
            int headerHeight = DashboardPresentation.HeaderHeight;
            DashboardHeaderLayout layout = HeaderLayout(scale);

            RectangleF iconGlow = new RectangleF(10, 5, 50, 50);
            DrawAmbientGlow(graphics, iconGlow,
                darkTheme ? Color.FromArgb(72, 31, 185, 255) : Color.FromArgb(62, 0, 157, 255));

            RectangleF icon = new RectangleF(17, 11, 36, 36);
            if (appIcon != null)
            {
                GraphicsState iconState = graphics.Save();
                using (GraphicsPath iconPath = UiDrawing.RoundedRectangle(icon, 10f))
                {
                    graphics.SetClip(iconPath);
                    graphics.DrawImage(appIcon, icon);
                }
                graphics.Restore(iconState);
            }
            using (GraphicsPath iconBorderPath = UiDrawing.RoundedRectangle(icon, 10f))
            using (Pen iconBorder = new Pen(Color.FromArgb(118, 255, 255, 255), 0.8f))
                graphics.DrawPath(iconBorder, iconBorderPath);

            RectangleF titleBounds = new RectangleF(62, 14, 94, 28);
            using (Font titleFont = UiDrawing.FittedPixelFont(
                graphics, "Codex 用量", titleBounds, 18f, 15.5f, FontStyle.Bold))
            using (Brush primary = new SolidBrush(PrimaryText(darkTheme)))
            {
                UiDrawing.DrawText(graphics, "Codex 用量", titleFont, primary, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);
            }

            RectangleF networkTile = new RectangleF(160, 8, 70, 41);
            using (GraphicsPath tile = UiDrawing.RoundedRectangle(networkTile, 10f))
            using (Brush tileBrush = new SolidBrush(darkTheme
                ? Color.FromArgb(28, 255, 255, 255)
                : Color.FromArgb(126, 255, 255, 255)))
            using (Pen tileBorder = new Pen(darkTheme
                ? Color.FromArgb(36, 98, 190, 255)
                : Color.FromArgb(50, 69, 151, 205), 0.7f))
            {
                graphics.FillPath(tileBrush, tile);
                graphics.DrawPath(tileBorder, tile);
            }
            DrawNetworkSpeed(graphics, networkSpeed, darkTheme);

            RectangleF status = new RectangleF(234, 15, 58, 26);
            using (GraphicsPath pill = UiDrawing.RoundedRectangle(status, 13f))
            using (Brush pillBrush = new SolidBrush(syncButtonHovered
                ? (darkTheme ? Color.FromArgb(55, 58, 180, 255) : Color.FromArgb(44, 0, 147, 255))
                : (darkTheme ? Color.FromArgb(31, 255, 255, 255) : Color.FromArgb(137, 255, 255, 255))))
            using (Pen pillBorder = new Pen(darkTheme
                ? Color.FromArgb(48, 85, 196, 255)
                : Color.FromArgb(58, 40, 152, 211), 0.7f))
            {
                graphics.FillPath(pillBrush, pill);
                graphics.DrawPath(pillBorder, pill);
            }

            using (Brush dotGlow = new SolidBrush(Color.FromArgb(45, statusDotColor)))
                graphics.FillEllipse(dotGlow, 239, 20, 12, 12);
            using (Brush dot = new SolidBrush(statusDotColor))
                graphics.FillEllipse(dot, 242, 23, 6, 6);
            RectangleF statusTextBounds = new RectangleF(250, 17, 38, 21);
            using (Font statusFont = UiDrawing.FittedPixelFont(
                graphics, statusText, statusTextBounds, SectionTitleFontSize,
                SupportingTextFontSize, FontStyle.Bold))
            using (Brush secondary = new SolidBrush(SecondaryText(darkTheme)))
            {
                UiDrawing.DrawText(graphics, statusText, statusFont, secondary,
                    statusTextBounds, StringAlignment.Center, StringAlignment.Center);
            }

            RectangleF menuSurface = new RectangleF(297, 15, 24, 26);
            using (GraphicsPath menuPath = UiDrawing.RoundedRectangle(menuSurface, 9f))
            using (Brush menuBrush = new SolidBrush(darkTheme
                ? Color.FromArgb(24, 255, 255, 255)
                : Color.FromArgb(94, 255, 255, 255)))
                graphics.FillPath(menuBrush, menuPath);
            using (Brush dots = new SolidBrush(PrimaryText(darkTheme)))
            {
                graphics.FillEllipse(dots, 302, 27, 2.2f, 2.2f);
                graphics.FillEllipse(dots, 308, 27, 2.2f, 2.2f);
                graphics.FillEllipse(dots, 314, 27, 2.2f, 2.2f);
            }

            using (Pen divider = new Pen(darkTheme
                ? Color.FromArgb(28, 108, 184, 240)
                : Color.FromArgb(32, 51, 119, 162), 0.6f))
                graphics.DrawLine(divider, 18, headerHeight - 1,
                    designWidth - 18, headerHeight - 1);

            return layout;
        }

        internal static void DrawNetworkSpeed(Graphics graphics,
            NetworkSpeedSnapshot networkSpeed, bool darkTheme)
        {
            string download = "↓ " +
                NetworkSpeedMonitor.FormatRate(networkSpeed.DownloadBytesPerSecond);
            string upload = "↑ " +
                NetworkSpeedMonitor.FormatRate(networkSpeed.UploadBytesPerSecond);
            Color downloadColor = darkTheme
                ? Color.FromArgb(92, 211, 255) : Color.FromArgb(0, 125, 204);
            Color uploadColor = darkTheme
                ? Color.FromArgb(179, 157, 255) : Color.FromArgb(102, 74, 207);

            RectangleF downloadBounds = new RectangleF(165, 9, 62, 18);
            RectangleF uploadBounds = new RectangleF(165, 29, 62, 18);
            float speedSize = Math.Min(
                UiDrawing.FittedPixelFontSize(
                    graphics, download, downloadBounds, 11f, 7.8f, FontStyle.Bold),
                UiDrawing.FittedPixelFontSize(
                    graphics, upload, uploadBounds, 11f, 7.8f, FontStyle.Bold));
            using (Font speedFont = UiDrawing.PixelFont(speedSize, FontStyle.Bold))
            using (Brush downloadBrush = new SolidBrush(downloadColor))
            using (Brush uploadBrush = new SolidBrush(uploadColor))
            {
                UiDrawing.DrawText(graphics, download, speedFont, downloadBrush,
                    downloadBounds, StringAlignment.Near, StringAlignment.Center);
                UiDrawing.DrawText(graphics, upload, speedFont, uploadBrush,
                    uploadBounds, StringAlignment.Near, StringAlignment.Center);
            }
        }

        internal static DashboardMeterLayout DrawMeter(Graphics graphics,
            UsageWindow window, int y, bool prominent, PaceInfo pace,
            string resetText, string weeklyTokens, bool darkTheme, float scale)
        {
            DashboardMeterLayout layout = new DashboardMeterLayout
            {
                ResetBounds = Rectangle.Empty,
                BudgetMarkerBounds = Rectangle.Empty,
                BudgetToolTipText = String.Empty
            };
            if (window == null)
                return layout;

            string title = prominent
                ? "每周额度"
                : (String.Equals(window.Title, "Spark", StringComparison.OrdinalIgnoreCase)
                    ? "Spark额度"
                    : window.Title);
            string remaining = "剩余 " + Math.Round(window.RemainingPercent).ToString("0") + "%";
            weeklyTokens = weeklyTokens ?? String.Empty;
            resetText = resetText ?? String.Empty;

            RectangleF panel = new RectangleF(10, y + 1,
                DashboardPresentation.DesignWidth - 20,
                DashboardPresentation.MeterHeight - 3);
            using (GraphicsPath panelPath = UiDrawing.RoundedRectangle(panel, 14f))
            using (Brush panelBrush = new SolidBrush(darkTheme
                ? Color.FromArgb(prominent ? 31 : 21, 255, 255, 255)
                : Color.FromArgb(prominent ? 164 : 105, 255, 255, 255)))
            using (Pen panelBorder = new Pen(darkTheme
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
            if (!String.IsNullOrEmpty(resetText))
                layout.ResetBounds = ScaleRectangle(resetBounds, scale);

            using (Font titleFont = UiDrawing.PixelFont(SectionTitleFontSize, FontStyle.Bold))
            using (Font resetFont = UiDrawing.PixelFont(SupportingTextFontSize, FontStyle.Regular))
            using (Font remainingFont = UiDrawing.FittedPixelFont(
                graphics, remaining, remainingBounds, 19f, 15f, FontStyle.Bold))
            using (Font tokenFont = UiDrawing.FittedPixelFont(
                graphics, weeklyTokens, tokenBounds, 11.5f, 8.5f, FontStyle.Bold))
            using (Brush primary = new SolidBrush(PrimaryText(darkTheme)))
            using (Brush secondary = new SolidBrush(
                prominent ? TertiaryText(darkTheme) : SecondaryText(darkTheme)))
            {
                UiDrawing.DrawText(graphics, title, titleFont,
                    prominent ? primary : secondary, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);
                UiDrawing.DrawText(graphics, remaining, remainingFont, primary,
                    remainingBounds, StringAlignment.Near, StringAlignment.Center);
                UiDrawing.DrawText(graphics, weeklyTokens, tokenFont, secondary,
                    tokenBounds, StringAlignment.Far, StringAlignment.Center);
                if (!String.IsNullOrEmpty(resetText))
                {
                    UiDrawing.DrawText(graphics, resetText, resetFont, secondary,
                        resetBounds, StringAlignment.Far, StringAlignment.Center);
                }
            }

            RectangleF track = new RectangleF(20, y + 57,
                DashboardPresentation.DesignWidth - 40, prominent ? 9f : 7f);
            using (GraphicsPath path = UiDrawing.RoundedRectangle(track, track.Height / 2f))
            using (Brush trackBrush = new SolidBrush(darkTheme
                ? Color.FromArgb(38, 255, 255, 255)
                : Color.FromArgb(28, 29, 74, 105)))
                graphics.FillPath(trackBrush, path);

            float fillWidth = Math.Max(4f,
                (float)(track.Width * window.RemainingPercent / 100.0));
            fillWidth = Math.Min(track.Width, fillWidth);
            RectangleF fill = new RectangleF(track.X, track.Y, fillWidth, track.Height);
            Color start;
            Color end;
            BarColors(window.RemainingPercent, prominent, out start, out end);
            RectangleF glowBounds = new RectangleF(
                fill.X - 1, fill.Y - 1.5f, fill.Width + 2, fill.Height + 3);
            using (GraphicsPath glowPath = UiDrawing.RoundedRectangle(
                glowBounds, glowBounds.Height / 2f))
            using (Brush glowBrush = new SolidBrush(Color.FromArgb(darkTheme ? 34 : 26, end)))
                graphics.FillPath(glowBrush, glowPath);
            using (GraphicsPath fillPath = UiDrawing.RoundedRectangle(fill, fill.Height / 2f))
            using (LinearGradientBrush fillBrush = new LinearGradientBrush(fill, start, end, 0f))
                graphics.FillPath(fillBrush, fillPath);
            using (Pen highlight = new Pen(Color.FromArgb(92, 255, 255, 255), 0.7f))
                graphics.DrawLine(highlight, track.X + 4, track.Y + 1,
                    track.X + Math.Max(4, fillWidth - 4), track.Y + 1);

            if (prominent && pace != null)
            {
                double expectedRemaining = Math.Max(0,
                    Math.Min(100, 100 - pace.ExpectedUsedPercent));
                float markerX = track.X +
                    (float)(track.Width * expectedRemaining / 100.0);
                bool overBudget = pace.DeltaPercent > 0.5;
                Color markerColor = overBudget
                    ? Color.FromArgb(255, 116, 48)
                    : Color.FromArgb(105, 76, 255);
                using (Pen markerGlow = new Pen(Color.FromArgb(46, markerColor), 7f))
                    graphics.DrawLine(markerGlow, markerX, track.Y - 3,
                        markerX, track.Bottom + 3);
                using (Pen marker = new Pen(markerColor, 2f))
                    graphics.DrawLine(marker, markerX, track.Y - 4,
                        markerX, track.Bottom + 4);
                PointF[] pointer = new PointF[]
                {
                    new PointF(markerX, track.Y - 1),
                    new PointF(markerX - 3.4f, track.Y - 5.6f),
                    new PointF(markerX + 3.4f, track.Y - 5.6f)
                };
                using (Brush pointerBrush = new SolidBrush(markerColor))
                    graphics.FillPolygon(pointerBrush, pointer);

                layout.BudgetMarkerBounds = ScaleRectangle(
                    new RectangleF(markerX - 8f, track.Y - 10f,
                        16f, track.Height + 20f), scale);
                layout.BudgetMarkerDesignX = markerX;
                layout.BudgetToolTipText = "预算线 " +
                    Math.Round(pace.ExpectedUsedPercent).ToString("0") + "%";
            }

            return layout;
        }

        internal static Rectangle DrawPace(Graphics graphics, PaceInfo pace, int y,
            bool hovered, bool expanded, bool darkTheme, float scale)
        {
            if (pace == null)
                return Rectangle.Empty;

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
            Color stateColor = overBudget
                ? Color.FromArgb(255, 132, 38)
                : Color.FromArgb(18, 183, 127);
            RectangleF surface = new RectangleF(14, y + 2,
                DashboardPresentation.DesignWidth - 28, 25);
            Rectangle toggleBounds = ScaleRectangle(surface, scale);
            using (GraphicsPath surfacePath = UiDrawing.RoundedRectangle(surface, 12.5f))
            using (Brush surfaceBrush = new SolidBrush(hovered
                ? (darkTheme
                    ? Color.FromArgb(43, 80, 174, 235)
                    : Color.FromArgb(178, 255, 255, 255))
                : (darkTheme
                    ? Color.FromArgb(23, 255, 255, 255)
                    : Color.FromArgb(116, 255, 255, 255))))
            using (Pen surfaceBorder = new Pen(darkTheme
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
            RectangleF forecastBounds = DashboardPresentation.PaceForecastBounds(y);
            using (Font font = UiDrawing.PixelFont(SupportingTextFontSize, FontStyle.Bold))
            using (Font forecastFont = UiDrawing.FittedPixelFont(
                graphics, right, forecastBounds, SupportingTextFontSize,
                10.5f, FontStyle.Regular))
            using (Brush leftBrush = new SolidBrush(overBudget
                ? stateColor : PrimaryText(darkTheme)))
            using (Brush rightBrush = new SolidBrush(SecondaryText(darkTheme)))
            {
                UiDrawing.DrawText(graphics, left, font, leftBrush, stateBounds,
                    StringAlignment.Near, StringAlignment.Center);
                UiDrawing.DrawText(graphics, right, forecastFont, rightBrush,
                    forecastBounds, StringAlignment.Far, StringAlignment.Center);
            }

            float chevronY = y + 14.5f;
            using (Pen chevron = new Pen(
                hovered ? stateColor : SecondaryText(darkTheme), 1.6f))
            {
                chevron.StartCap = LineCap.Round;
                chevron.EndCap = LineCap.Round;
                if (expanded)
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

            return toggleBounds;
        }

        internal static void DrawDailyUsage(Graphics graphics, WeeklyTokenReport weeklyUsage,
            bool isRefreshing, double usedPercent, int y, bool darkTheme)
        {
            RectangleF titleBounds = new RectangleF(20, y + 2, 170, 22);
            using (Font titleFont = UiDrawing.PixelFont(SectionTitleFontSize, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(PrimaryText(darkTheme)))
                UiDrawing.DrawText(graphics, "近7天每日", titleFont, titleBrush, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);

            string stateText = null;
            if (weeklyUsage == null && isRefreshing)
                stateText = "正在读取本机会话统计…";
            else if (weeklyUsage != null && !String.IsNullOrWhiteSpace(weeklyUsage.Error))
                stateText = "统计暂不可用";

            if (!String.IsNullOrEmpty(stateText))
            {
                RectangleF stateBounds = new RectangleF(176, y + 3, 132, 20);
                using (Font stateFont = UiDrawing.FittedPixelFont(
                    graphics, stateText, stateBounds, 10f, 8f, FontStyle.Regular))
                using (Brush stateBrush = new SolidBrush(TertiaryText(darkTheme)))
                    UiDrawing.DrawText(graphics, stateText, stateFont, stateBrush, stateBounds,
                        StringAlignment.Far, StringAlignment.Center);
            }

            List<DailyTokenUsage> days = DisplayDays(weeklyUsage);
            long maximum = days.Count == 0 ? 0 : days.Max(item => item.Tokens);
            long total = weeklyUsage == null ? 0 : weeklyUsage.TotalTokens;

            for (int index = 0; index < 7; index++)
            {
                DailyTokenUsage day = days[index];
                float x = 20f + (index * 42f);
                RectangleF percentBounds = new RectangleF(x - 3f, y + 23, 38f, 17f);
                string percent = DashboardPresentation.DailyQuotaPercent(
                    day.Tokens, total, usedPercent).ToString("0.0") + "%";
                using (Font percentFont = UiDrawing.FittedPixelFont(
                    graphics, percent, percentBounds, 10.5f, 8f, FontStyle.Bold))
                using (Brush percentBrush = new SolidBrush(PrimaryText(darkTheme)))
                    UiDrawing.DrawText(graphics, percent, percentFont, percentBrush,
                        percentBounds, StringAlignment.Center, StringAlignment.Center);

                RectangleF track = new RectangleF(x, y + 42, 32, 43);
                using (GraphicsPath trackPath = UiDrawing.RoundedRectangle(track, 15f))
                using (Brush trackBrush = new SolidBrush(darkTheme
                    ? Color.FromArgb(30, 255, 255, 255)
                    : Color.FromArgb(24, 26, 58, 83)))
                    graphics.FillPath(trackBrush, trackPath);

                if (day.Tokens > 0 && maximum > 0)
                {
                    float fillHeight = Math.Max(3f,
                        (float)(track.Height * day.Tokens / (double)maximum));
                    RectangleF fill = new RectangleF(track.X, track.Bottom - fillHeight,
                        track.Width, fillHeight);
                    using (GraphicsPath fillPath = UiDrawing.RoundedRectangle(fill,
                        Math.Min(15f, Math.Max(1.5f, fillHeight / 2f))))
                    using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                        fill, Color.FromArgb(65, 211, 239),
                        Color.FromArgb(86, 102, 255), 90f))
                    {
                        graphics.FillPath(fillBrush, fillPath);
                    }
                }

                RectangleF accent = new RectangleF(
                    track.X, track.Bottom - 2.5f, track.Width, 2.5f);
                using (LinearGradientBrush accentBrush = new LinearGradientBrush(
                    accent, Color.FromArgb(53, 207, 235),
                    Color.FromArgb(91, 86, 255), 0f))
                    graphics.FillRectangle(accentBrush, accent);

                string dayLabel = day.Day.Date == DateTime.Now.Date
                    ? "今"
                    : DashboardPresentation.ChineseWeekday(day.Day.DayOfWeek);
                string tokenText = WeeklyUsageReader.FormatTokenCount(day.Tokens);
                RectangleF dayBounds = new RectangleF(x - 2f, y + 87, 36f, 16f);
                RectangleF tokenBounds = new RectangleF(x - 4f, y + 101, 40f, 14f);
                using (Font dayFont = UiDrawing.PixelFont(10.5f, FontStyle.Bold))
                using (Font tokenFont = UiDrawing.FittedPixelFont(
                    graphics, tokenText, tokenBounds, 8.5f, 7f, FontStyle.Regular))
                using (Brush dayBrush = new SolidBrush(SecondaryText(darkTheme)))
                using (Brush tokenBrush = new SolidBrush(TertiaryText(darkTheme)))
                {
                    UiDrawing.DrawText(graphics, dayLabel, dayFont, dayBrush, dayBounds,
                        StringAlignment.Center, StringAlignment.Center);
                    UiDrawing.DrawText(graphics, tokenText, tokenFont, tokenBrush, tokenBounds,
                        StringAlignment.Center, StringAlignment.Center);
                }
            }
        }

        internal static void DrawModelUsage(Graphics graphics, WeeklyTokenReport weeklyUsage,
            bool isRefreshing, int y, bool darkTheme)
        {
            RectangleF titleBounds = new RectangleF(20, y + 1, 150, 23);
            using (Font titleFont = UiDrawing.PixelFont(SectionTitleFontSize, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(PrimaryText(darkTheme)))
                UiDrawing.DrawText(graphics, "模型偏好", titleFont, titleBrush, titleBounds,
                    StringAlignment.Near, StringAlignment.Center);

            List<ModelTokenUsage> rows = DashboardPresentation.VisibleModelRows(
                weeklyUsage == null ? null : weeklyUsage.Models,
                DashboardPresentation.MaximumModelRows);
            if (rows.Count == 0 || weeklyUsage == null || weeklyUsage.TotalTokens <= 0)
            {
                RectangleF empty = new RectangleF(14,
                    y + DashboardPresentation.ModelHeaderHeight,
                    DashboardPresentation.DesignWidth - 28,
                    (DashboardPresentation.MaximumModelRows *
                        DashboardPresentation.ModelRowHeight) - 8);
                using (GraphicsPath path = UiDrawing.RoundedRectangle(empty, 14f))
                using (Brush fill = new SolidBrush(darkTheme
                    ? Color.FromArgb(24, 255, 255, 255)
                    : Color.FromArgb(116, 255, 255, 255)))
                using (Pen border = new Pen(darkTheme
                    ? Color.FromArgb(28, 91, 178, 236)
                    : Color.FromArgb(32, 59, 136, 191), 0.7f))
                {
                    graphics.FillPath(fill, path);
                    graphics.DrawPath(border, path);
                }
                string message = weeklyUsage != null &&
                    !String.IsNullOrWhiteSpace(weeklyUsage.Error)
                    ? weeklyUsage.Error
                    : (isRefreshing
                        ? "正在统计模型与推理强度…"
                        : "近 7 天暂无本机会话记录");
                RectangleF messageBounds = new RectangleF(
                    empty.X + 12, empty.Y, empty.Width - 24, empty.Height);
                using (Font font = UiDrawing.FittedPixelFont(
                    graphics, message, messageBounds, 11f, 8.5f, FontStyle.Regular))
                using (Brush brush = new SolidBrush(TertiaryText(darkTheme)))
                    UiDrawing.DrawText(graphics, message, font, brush, messageBounds,
                        StringAlignment.Center, StringAlignment.Center);
                return;
            }

            for (int index = 0; index < rows.Count; index++)
            {
                ModelTokenUsage row = rows[index];
                float rowY = y + DashboardPresentation.ModelHeaderHeight +
                    (index * DashboardPresentation.ModelRowHeight) + 2;
                RectangleF surface = new RectangleF(14, rowY,
                    DashboardPresentation.DesignWidth - 28,
                    DashboardPresentation.ModelRowHeight - 5);
                using (GraphicsPath path = UiDrawing.RoundedRectangle(surface, 13f))
                using (Brush fill = new SolidBrush(darkTheme
                    ? Color.FromArgb(30, 255, 255, 255)
                    : Color.FromArgb(128, 255, 255, 255)))
                using (Pen border = new Pen(darkTheme
                    ? Color.FromArgb(24, 108, 184, 240)
                    : Color.FromArgb(28, 59, 136, 191), 0.6f))
                {
                    graphics.FillPath(fill, path);
                    graphics.DrawPath(border, path);
                }

                double sharePercent = row.Tokens * 100d / weeklyUsage.TotalTokens;
                Color accent = DashboardPresentation.UsageAccent(sharePercent, darkTheme);
                string label = DashboardPresentation.ModelLabel(row);
                string percentage = sharePercent.ToString("0.0") + "%";
                string tokens = WeeklyUsageReader.FormatTokenCount(row.Tokens);
                RectangleF labelBounds = new RectangleF(25, rowY + 1, 174, surface.Height - 2);
                RectangleF percentBounds = new RectangleF(197, rowY + 1, 57, surface.Height - 2);
                RectangleF tokenBounds = new RectangleF(258, rowY + 1, 47, surface.Height - 2);
                using (Font labelFont = UiDrawing.FittedPixelFont(
                    graphics, label, labelBounds, 11.5f, 8.5f, FontStyle.Bold))
                using (Font percentFont = UiDrawing.FittedPixelFont(
                    graphics, percentage, percentBounds, 15f, 11f, FontStyle.Bold))
                using (Font tokenFont = UiDrawing.FittedPixelFont(
                    graphics, tokens, tokenBounds, 10.5f, 8f, FontStyle.Bold))
                using (Brush labelBrush = new SolidBrush(accent))
                using (Brush percentBrush = new SolidBrush(accent))
                using (Brush tokenBrush = new SolidBrush(Color.FromArgb(210, accent)))
                {
                    UiDrawing.DrawText(graphics, label, labelFont, labelBrush, labelBounds,
                        StringAlignment.Near, StringAlignment.Center);
                    UiDrawing.DrawText(graphics, percentage, percentFont, percentBrush,
                        percentBounds, StringAlignment.Far, StringAlignment.Center);
                    UiDrawing.DrawText(graphics, tokens, tokenFont, tokenBrush, tokenBounds,
                        StringAlignment.Far, StringAlignment.Center);
                }
            }
        }

        internal static void DrawBudgetToolTip(Graphics graphics,
            float markerDesignX, string text, bool darkTheme)
        {
            if (String.IsNullOrWhiteSpace(text))
                return;
            const float width = 88f;
            const float height = 24f;
            float x = Math.Max(14f, Math.Min(
                DashboardPresentation.DesignWidth - width - 14f,
                markerDesignX - width / 2f));
            float y = DashboardPresentation.HeaderHeight + 3f;
            RectangleF bounds = new RectangleF(x, y, width, height);

            using (GraphicsPath path = UiDrawing.RoundedRectangle(bounds, 9f))
            using (Brush background = new SolidBrush(darkTheme
                ? Color.FromArgb(35, 49, 76)
                : Color.FromArgb(35, 54, 82)))
            using (Pen border = new Pen(Color.FromArgb(170, 105, 154, 255), 0.8f))
            {
                graphics.FillPath(background, path);
                graphics.DrawPath(border, path);
            }

            PointF[] pointer = new PointF[]
            {
                new PointF(markerDesignX, bounds.Bottom + 5f),
                new PointF(markerDesignX - 4f, bounds.Bottom - 0.5f),
                new PointF(markerDesignX + 4f, bounds.Bottom - 0.5f)
            };
            using (Brush pointerBrush = new SolidBrush(darkTheme
                ? Color.FromArgb(35, 49, 76)
                : Color.FromArgb(35, 54, 82)))
                graphics.FillPolygon(pointerBrush, pointer);

            using (Font font = UiDrawing.PixelFont(11f, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
                UiDrawing.DrawText(graphics, text, font, textBrush, bounds,
                    StringAlignment.Center, StringAlignment.Center);
        }

        internal static void DrawStatusToolTip(Graphics graphics, string text,
            double remainingPercent, bool darkTheme)
        {
            if (String.IsNullOrWhiteSpace(text))
                return;
            const float width = 124f;
            const float height = 24f;
            const float anchorX = 263f;
            RectangleF bounds = new RectangleF(
                DashboardPresentation.DesignWidth - width - 14f,
                50f, width, height);

            Color start;
            Color end;
            BarColors(remainingPercent, true, out start, out end);
            Color fillStart = Color.FromArgb(darkTheme ? 238 : 226, start);
            Color fillEnd = Color.FromArgb(darkTheme ? 238 : 226, end);

            PointF[] pointer = new PointF[]
            {
                new PointF(anchorX, bounds.Y - 5f),
                new PointF(anchorX - 4.5f, bounds.Y + 0.8f),
                new PointF(anchorX + 4.5f, bounds.Y + 0.8f)
            };
            using (Brush pointerBrush = new SolidBrush(fillEnd))
                graphics.FillPolygon(pointerBrush, pointer);

            using (GraphicsPath shadowPath = UiDrawing.RoundedRectangle(
                new RectangleF(bounds.X, bounds.Y + 1.8f,
                    bounds.Width, bounds.Height), 10f))
            using (Brush shadow = new SolidBrush(
                Color.FromArgb(darkTheme ? 72 : 42, 12, 28, 50)))
                graphics.FillPath(shadow, shadowPath);

            using (GraphicsPath path = UiDrawing.RoundedRectangle(bounds, 10f))
            using (LinearGradientBrush background = new LinearGradientBrush(
                bounds, fillStart, fillEnd, 0f))
            using (Pen border = new Pen(
                Color.FromArgb(150, 255, 255, 255), 0.8f))
            {
                graphics.FillPath(background, path);
                graphics.DrawPath(border, path);
            }

            RectangleF textBounds = new RectangleF(
                bounds.X + 6f, bounds.Y, bounds.Width - 12f, bounds.Height);
            using (Font font = UiDrawing.FittedPixelFont(
                graphics, text, textBounds, 11.5f, 9.5f, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
                UiDrawing.DrawText(graphics, text, font, textBrush, textBounds,
                    StringAlignment.Center, StringAlignment.Center);
        }

        internal static void DrawEmptyState(
            Graphics graphics, string lastError, bool darkTheme)
        {
            int designWidth = DashboardPresentation.DesignWidth;
            string message = String.IsNullOrWhiteSpace(lastError)
                ? "正在连接 CodexBar…" : lastError;
            RectangleF surface = new RectangleF(14, 66, designWidth - 28, 40);
            using (GraphicsPath path = UiDrawing.RoundedRectangle(surface, 12f))
            using (Brush fill = new SolidBrush(darkTheme
                ? Color.FromArgb(24, 255, 255, 255)
                : Color.FromArgb(132, 255, 255, 255)))
            using (Pen border = new Pen(darkTheme
                ? Color.FromArgb(34, 91, 178, 236)
                : Color.FromArgb(42, 61, 143, 199), 0.7f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            RectangleF messageBounds = new RectangleF(27, 66, designWidth - 54, 40);
            using (Font font = UiDrawing.FittedPixelFont(
                graphics, message, messageBounds, 13f, 10f, FontStyle.Regular))
            using (Brush brush = new SolidBrush(String.IsNullOrWhiteSpace(lastError)
                ? SecondaryText(darkTheme) : Color.FromArgb(218, 112, 0)))
            {
                UiDrawing.DrawText(graphics, message, font, brush, messageBounds,
                    StringAlignment.Near, StringAlignment.Center);
            }
        }

        private static void DrawAmbientGlow(
            Graphics graphics, RectangleF bounds, Color centerColor)
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

        private static void DrawTechGrid(Graphics graphics, int designHeight, bool darkTheme)
        {
            int designWidth = DashboardPresentation.DesignWidth;
            int headerHeight = DashboardPresentation.HeaderHeight;
            Color gridColor = darkTheme
                ? Color.FromArgb(12, 107, 204, 255)
                : Color.FromArgb(11, 19, 112, 170);
            using (Pen grid = new Pen(gridColor, 0.5f))
            {
                for (int x = 20; x < designWidth; x += 28)
                    graphics.DrawLine(grid, x, headerHeight, x, designHeight - 12);
                for (int y = headerHeight + 16; y < designHeight; y += 24)
                    graphics.DrawLine(grid, 12, y, designWidth - 12, y);
            }
        }

        private static Rectangle ScaleRectangle(RectangleF rectangle, float scale)
        {
            return new Rectangle(
                Scale(rectangle.X, scale), Scale(rectangle.Y, scale),
                Scale(rectangle.Width, scale), Scale(rectangle.Height, scale));
        }

        private static int Scale(float value, float scale)
        {
            return Math.Max(1, Convert.ToInt32(Math.Round(value * scale)));
        }

        private static Color PrimaryText(bool darkTheme)
        {
            return darkTheme
                ? Color.FromArgb(246, 248, 252)
                : Color.FromArgb(21, 36, 55);
        }

        private static Color SecondaryText(bool darkTheme)
        {
            return darkTheme
                ? Color.FromArgb(190, 199, 213)
                : Color.FromArgb(65, 84, 105);
        }

        private static Color TertiaryText(bool darkTheme)
        {
            return darkTheme
                ? Color.FromArgb(150, 161, 176)
                : Color.FromArgb(101, 119, 139);
        }

        private static List<DailyTokenUsage> DisplayDays(WeeklyTokenReport weeklyUsage)
        {
            if (weeklyUsage != null && weeklyUsage.Days != null &&
                weeklyUsage.Days.Count == 7)
            {
                return weeklyUsage.Days;
            }

            List<DailyTokenUsage> days = new List<DailyTokenUsage>();
            DateTime firstDay = DateTime.Now.Date.AddDays(-6);
            for (int offset = 0; offset < 7; offset++)
                days.Add(new DailyTokenUsage { Day = firstDay.AddDays(offset), Tokens = 0 });
            return days;
        }

        private static string Duration(double seconds)
        {
            int totalHours = Math.Max(0, Convert.ToInt32(Math.Floor(seconds / 3600)));
            int days = totalHours / 24;
            int hours = totalHours % 24;
            return days > 0 ? days + "d " + hours + "h" : hours + "h";
        }

        internal static void BarColors(double remaining, bool prominent,
            out Color start, out Color end)
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
    }
}
