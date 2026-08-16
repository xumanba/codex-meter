[CmdletBinding()]
param(
    [string]$OutputPath,
    [string]$SourcePath
)

$ErrorActionPreference = "Stop"

if ([String]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path -Parent $PSScriptRoot) "assets\CodexMeter.ico"
}
if ([String]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Join-Path (Split-Path -Parent $PSScriptRoot) "assets\CodexMeter-source-balanced.png"
}
if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "The source PNG was not found: $SourcePath"
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

    public static void Generate(string sourcePath, string outputPath)
    {
        List<byte[]> frames = new List<byte[]>();
        using (Image source = Image.FromFile(sourcePath))
        {
            foreach (int size in Sizes)
                frames.Add(RenderDib(source, size));
        }

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

    private static byte[] RenderDib(Image source, int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, new Rectangle(0, 0, size, size));

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
$resolvedSourcePath = [IO.Path]::GetFullPath($SourcePath)
[CodexMeterIconGenerator]::Generate($resolvedSourcePath, $resolvedOutputPath)

$icon = New-Object Drawing.Icon($resolvedOutputPath, 32, 32)
try {
    $preview = $icon.ToBitmap()
    $preview.Dispose()
    Write-Host "ICON_OK"
    Write-Host "Source: $resolvedSourcePath"
    Write-Host "Output: $resolvedOutputPath"
    Write-Host "Embedded sizes: 16, 20, 24, 32, 40, 48, 64, 128, 256"
    Write-Host "Preview size: $($icon.Width)x$($icon.Height)"
} finally {
    $icon.Dispose()
}
