$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$drawingDir = Split-Path ([System.Drawing.Bitmap].Assembly.Location)
$references = @(
    Get-ChildItem $drawingDir -Filter 'System.Drawing*.dll' | ForEach-Object FullName
    Get-ChildItem $drawingDir -Filter 'System.Private.Windows*.dll' | ForEach-Object FullName
    Get-ChildItem $drawingDir -Filter 'System.Collections.dll' | ForEach-Object FullName
    [System.Collections.Generic.Queue[int]].Assembly.Location
)

$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class JungleSuppliedArtwork
{
    public static void Convert(string sourcePath, string destinationPath, int cropX, int cropY, int cropWidth, int cropHeight, int outputWidth, int outputHeight)
    {
        using (var source = new Bitmap(sourcePath))
        using (var rgba = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(rgba))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);
            RemoveCheckerboard(rgba);
            using (var result = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb))
            using (var output = Graphics.FromImage(result))
            {
                output.Clear(Color.Transparent);
                output.CompositingMode = CompositingMode.SourceCopy;
                output.InterpolationMode = InterpolationMode.HighQualityBicubic;
                output.PixelOffsetMode = PixelOffsetMode.HighQuality;
                output.DrawImage(rgba, new Rectangle(0, 0, outputWidth, outputHeight),
                    new Rectangle(cropX, cropY, cropWidth, cropHeight), GraphicsUnit.Pixel);
                result.Save(destinationPath, ImageFormat.Png);
            }
        }
    }

    private static void RemoveCheckerboard(Bitmap image)
    {
        int width = image.Width, height = image.Height;
        var candidates = new bool[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            Color c = image.GetPixel(x, y);
            int max = Math.Max(c.R, Math.Max(c.G, c.B));
            int min = Math.Min(c.R, Math.Min(c.G, c.B));
            // The supplied JPGs have the usual neutral gray/white transparency grid baked in.
            // The JPG's transparency grid is near-neutral gray/white. Keep warm pale wood by
            // requiring a much tighter neutral range than a generic bright-pixel key.
            candidates[y * width + x] = c.GetBrightness() > .78f && max - min < 16;
        }

        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            if (candidates[index]) image.SetPixel(x, y, Color.FromArgb(0, image.GetPixel(x, y)));
        }
        PadTransparentEdges(image, 4);
    }

    private static void PadTransparentEdges(Bitmap image, int iterations)
    {
        int width = image.Width, height = image.Height;
        var colors = new Color[width * height];
        var filled = new bool[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            colors[index] = image.GetPixel(x, y);
            filled[index] = colors[index].A > 0;
        }
        for (int step = 0; step < iterations; step++)
        {
            var nextFilled = (bool[])filled.Clone();
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (filled[index]) continue;
                int r = 0, g = 0, b = 0, count = 0;
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                {
                    int nx = x + ox, ny = y + oy;
                    if ((ox == 0 && oy == 0) || nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int neighbor = ny * width + nx;
                    if (!filled[neighbor]) continue;
                    r += colors[neighbor].R; g += colors[neighbor].G; b += colors[neighbor].B; count++;
                }
                if (count == 0) continue;
                colors[index] = Color.FromArgb(0, r / count, g / count, b / count);
                nextFilled[index] = true;
            }
            filled = nextFilled;
        }
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            if (colors[index].A == 0 && filled[index]) image.SetPixel(x, y, colors[index]);
        }
    }

    public static void MaskCoinCircle(string path)
    {
        string temporaryPath = path + ".masked.png";
        using (var image = new Bitmap(path))
        {
            float centerX = (image.Width - 1) * .5f, centerY = (image.Height - 1) * .5f;
            float radius = Math.Min(image.Width, image.Height) * .465f;
            for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
            {
                float dx = x - centerX, dy = y - centerY;
                if (Math.Sqrt(dx * dx + dy * dy) > radius) image.SetPixel(x, y, Color.FromArgb(0, image.GetPixel(x, y)));
            }
            image.Save(temporaryPath, ImageFormat.Png);
        }
        System.IO.File.Copy(temporaryPath, path, true);
        System.IO.File.Delete(temporaryPath);
    }

    public static void Defringe(string path)
    {
        string temporaryPath = path + ".clean.png";
        using (var image = new Bitmap(path))
        {
            int width = image.Width, height = image.Height;
            for (int pass = 0; pass < 2; pass++)
            {
                var remove = new bool[width * height];
                for (int y = 1; y < height - 1; y++) for (int x = 1; x < width - 1; x++)
                {
                    Color c = image.GetPixel(x, y);
                    int max = Math.Max(c.R, Math.Max(c.G, c.B));
                    int min = Math.Min(c.R, Math.Min(c.G, c.B));
                    if (c.A == 0 || c.GetBrightness() < .78f || max - min > 16) continue;
                    for (int oy = -1; oy <= 1 && !remove[y * width + x]; oy++) for (int ox = -1; ox <= 1; ox++)
                    {
                        if (image.GetPixel(x + ox, y + oy).A == 0) { remove[y * width + x] = true; break; }
                    }
                }
                for (int i = 0; i < remove.Length; i++) if (remove[i])
                {
                    Color c = image.GetPixel(i % width, i / width);
                    image.SetPixel(i % width, i / width, Color.FromArgb(0, c));
                }
            }
            image.Save(temporaryPath, ImageFormat.Png);
        }
        System.IO.File.Copy(temporaryPath, path, true);
        System.IO.File.Delete(temporaryPath);
    }

    public static void CreateCoinGlow(string path, int size)
    {
        using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            float center = (size - 1) * .5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center, dy = (y - center) / center;
                float radius = (float)Math.Sqrt(dx * dx + dy * dy);
                float alpha = Math.Max(0, 1 - radius);
                alpha = alpha * alpha * .32f;
                bitmap.SetPixel(x, y, Color.FromArgb((int)(alpha * 255), 255, 189, 50));
            }
            bitmap.Save(path, ImageFormat.Png);
        }
    }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies $references
$resourceArt = Join-Path $PSScriptRoot '..\Assets\JungleQuest\Resources\Artwork'
New-Item -ItemType Directory -Force -Path $resourceArt | Out-Null

$downloads = Join-Path $env:USERPROFILE 'Downloads'
[JungleSuppliedArtwork]::Convert(
    (Join-Path $downloads 'download.jpg'),
    (Join-Path $resourceArt 'JungleStumpArt.png'), 23, 133, 580, 384, 768, 509)
[JungleSuppliedArtwork]::Defringe((Join-Path $resourceArt 'JungleStumpArt.png'))
[JungleSuppliedArtwork]::Convert(
    (Join-Path $downloads 'Coin Money Clipart Transparent Background, Golden Coin Money Gold, Coin, Money, Gold PNG Image For Free Download.jpg'),
    (Join-Path $resourceArt 'JungleCoinArt.png'), 38, 36, 284, 284, 512, 512)
[JungleSuppliedArtwork]::MaskCoinCircle((Join-Path $resourceArt 'JungleCoinArt.png'))
[JungleSuppliedArtwork]::Defringe((Join-Path $resourceArt 'JungleCoinArt.png'))
[JungleSuppliedArtwork]::Convert(
    (Join-Path $downloads 'Download premium png of PNG Wood png plant tree log about wood log, tree trunk, log, timber, and tree stumps 12871245.jpg'),
    (Join-Path $resourceArt 'FallenLogArt.png'), 14, 16, 707, 345, 1024, 500)
[JungleSuppliedArtwork]::Defringe((Join-Path $resourceArt 'FallenLogArt.png'))
[JungleSuppliedArtwork]::Convert(
    (Join-Path $downloads 'Thorn Crown Hd Transparent, Hand Drawn Cartoon Crown Of Thorns Element, Spikes, Crown, Green PNG Image For Free Download.jpg'),
    (Join-Path $resourceArt 'ThornCrownArt.png'), 24, 111, 320, 139, 768, 334)
[JungleSuppliedArtwork]::Defringe((Join-Path $resourceArt 'ThornCrownArt.png'))
[JungleSuppliedArtwork]::CreateCoinGlow((Join-Path $resourceArt 'JungleCoinGlow.png'), 256)
Write-Output "Processed coin, fallen log, stump, and thorn artwork into $resourceArt"
