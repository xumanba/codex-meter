using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CodexMeter
{
    internal static class UiDrawing
    {
        internal static Font PixelFont(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Pixel);
        }

        internal static Font FittedPixelFont(Graphics graphics, string text,
            RectangleF bounds, float preferredSize, float minimumSize, FontStyle style)
        {
            return PixelFont(FittedPixelFontSize(
                graphics, text, bounds, preferredSize, minimumSize, style), style);
        }

        internal static float FittedPixelFontSize(Graphics graphics, string text,
            RectangleF bounds, float preferredSize, float minimumSize, FontStyle style)
        {
            float size = Math.Max(minimumSize, preferredSize);
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.FormatFlags |= StringFormatFlags.NoWrap |
                    StringFormatFlags.MeasureTrailingSpaces;
                while (size > minimumSize)
                {
                    using (Font font = PixelFont(size, style))
                    {
                        SizeF measured = graphics.MeasureString(text ?? String.Empty, font,
                            new SizeF(10000f, bounds.Height), format);
                        if (measured.Width <= bounds.Width - 1f &&
                            measured.Height <= bounds.Height + 1f)
                        {
                            return size;
                        }
                    }
                    size = Math.Max(minimumSize, size - 0.25f);
                }
            }
            return minimumSize;
        }

        internal static void DrawText(Graphics graphics, string text, Font font, Brush brush,
            RectangleF bounds, StringAlignment horizontal, StringAlignment vertical)
        {
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment = horizontal;
                format.LineAlignment = vertical;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags |= StringFormatFlags.NoWrap;
                graphics.DrawString(text ?? String.Empty, font, brush, bounds, format);
            }
        }

        internal static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
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
    }
}
