[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

if ([String]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path -Parent $PSScriptRoot) "assets\CodexMeter.ico"
}

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies "System.Drawing.dll" -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

public static class CodexMeterIconGenerator
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    public static void Generate(string outputPath)
    {
        List<byte[]> frames = new List<byte[]>();
        foreach (int size in Sizes)
            frames.Add(RenderDib(size));

        string directory = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)frames.Count);

            int offset = 6 + (16 * frames.Count);
            for (int index = 0; index < frames.Count; index++)
            {
                int size = Sizes[index];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)frames[index].Length);
                writer.Write((uint)offset);
                offset += frames[index].Length;
            }

            foreach (byte[] frame in frames)
                writer.Write(frame);
        }
    }

    private static byte[] RenderDib(int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            float margin = Math.Max(1f, size * 0.055f);
            RectangleF circle = new RectangleF(margin, margin, size - (2f * margin), size - (2f * margin));
            using (LinearGradientBrush blue = new LinearGradientBrush(
                circle, Color.FromArgb(50, 203, 255), Color.FromArgb(0, 104, 255), 52f))
            {
                graphics.FillEllipse(blue, circle);
            }

            if (size >= 32)
            {
                using (Pen highlight = new Pen(Color.FromArgb(95, 255, 255, 255), Math.Max(1f, size * 0.018f)))
                    graphics.DrawArc(highlight, circle.X + (size * 0.055f), circle.Y + (size * 0.055f),
                        circle.Width - (size * 0.11f), circle.Height - (size * 0.11f), 205f, 115f);
            }

            using (SolidBrush white = new SolidBrush(Color.White))
            {
                FillSparkle(graphics, white, size * 0.53f, size * 0.52f,
                    size * 0.235f, size * 0.155f, size * 0.045f);

                if (size >= 20)
                    FillSparkle(graphics, white, size * 0.31f, size * 0.31f,
                        size * 0.070f, size * 0.050f, size * 0.018f);

                if (size >= 40)
                    FillSparkle(graphics, white, size * 0.70f, size * 0.27f,
                        size * 0.050f, size * 0.036f, size * 0.014f);
            }

            int maskStride = ((size + 31) / 32) * 4;
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((uint)40);
                writer.Write(size);
                writer.Write(size * 2);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)0);
                writer.Write((uint)(size * size * 4));
                writer.Write(0);
                writer.Write(0);
                writer.Write((uint)0);
                writer.Write((uint)0);

                for (int y = size - 1; y >= 0; y--)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        writer.Write(pixel.B);
                        writer.Write(pixel.G);
                        writer.Write(pixel.R);
                        writer.Write(pixel.A);
                    }
                }

                for (int y = size - 1; y >= 0; y--)
                {
                    byte[] maskRow = new byte[maskStride];
                    for (int x = 0; x < size; x++)
                    {
                        if (bitmap.GetPixel(x, y).A < 128)
                            maskRow[x / 8] |= (byte)(0x80 >> (x % 8));
                    }
                    writer.Write(maskRow);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }
    }

    private static void FillSparkle(Graphics graphics, Brush brush, float x, float y,
        float verticalRadius, float horizontalRadius, float innerRadius)
    {
        PointF[] points =
        {
            new PointF(x, y - verticalRadius),
            new PointF(x + innerRadius, y - innerRadius),
            new PointF(x + horizontalRadius, y),
            new PointF(x + innerRadius, y + innerRadius),
            new PointF(x, y + verticalRadius),
            new PointF(x - innerRadius, y + innerRadius),
            new PointF(x - horizontalRadius, y),
            new PointF(x - innerRadius, y - innerRadius)
        };

        graphics.FillPolygon(brush, points);
    }
}
'@

$resolvedOutputPath = [IO.Path]::GetFullPath($OutputPath)
[CodexMeterIconGenerator]::Generate($resolvedOutputPath)

$icon = New-Object Drawing.Icon($resolvedOutputPath, 32, 32)
try {
    $preview = $icon.ToBitmap()
    $preview.Dispose()
    Write-Host "ICON_OK"
    Write-Host "Output: $resolvedOutputPath"
    Write-Host "Embedded sizes: 16, 20, 24, 32, 40, 48, 64, 128, 256"
    Write-Host "Preview size: $($icon.Width)x$($icon.Height)"
} finally {
    $icon.Dispose()
}
