$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$drawingDir = Split-Path ([System.Drawing.Bitmap].Assembly.Location)
$references = @(
    Get-ChildItem $drawingDir -Filter 'System.Drawing*.dll' | ForEach-Object FullName
    Get-ChildItem $drawingDir -Filter 'System.Private.Windows*.dll' | ForEach-Object FullName
)

$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class JungleCharacterArt
{
    static Color[] skin = { Hex("D99B68"), Hex("B9704F"), Hex("80523D"), Hex("CF8E61") };
    static Color[] hair = { Hex("3C2920"), Hex("251E25"), Hex("201F27"), Hex("713D2D") };
    static Color[] shirt = { Hex("4A8C4C"), Hex("A94D38"), Hex("398C8B"), Hex("D86B31") };
    static Color[] shade = { Hex("285438"), Hex("622F35"), Hex("245D66"), Hex("804326") };
    static Color[] trim = { Hex("D5B064"), Hex("E5B64F"), Hex("E5D28C"), Hex("65B3AF") };
    static Color[] trouser = { Hex("776443"), Hex("42504A"), Hex("C3AE6B"), Hex("536A68") };
    static Color[] dark = { Hex("2B2925"), Hex("30262B"), Hex("25272A"), Hex("372722") };

    public static void Generate(string outputDirectory)
    {
        string[] names = { "Maya", "Koa", "Tavi", "Nia" };
        System.IO.Directory.CreateDirectory(outputDirectory);
        for (int character = 0; character < names.Length; character++)
        {
            using (var sheet = new Bitmap(768, 768, PixelFormat.Format32bppArgb))
            using (var target = Graphics.FromImage(sheet))
            {
                target.Clear(Color.Transparent);
                target.CompositingMode = CompositingMode.SourceOver;
                target.InterpolationMode = InterpolationMode.HighQualityBicubic;
                target.PixelOffsetMode = PixelOffsetMode.HighQuality;
                for (int frame = 0; frame < 9; frame++)
                {
                    using (var large = new Bitmap(768, 768, PixelFormat.Format32bppArgb))
                    using (var paint = Graphics.FromImage(large))
                    {
                        paint.Clear(Color.Transparent);
                        paint.SmoothingMode = SmoothingMode.AntiAlias;
                        paint.CompositingQuality = CompositingQuality.HighQuality;
                        paint.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        paint.ScaleTransform(3f, 3f);
                        DrawPose(paint, character, frame);
                        using (var small = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
                        using (var downsample = Graphics.FromImage(small))
                        {
                            downsample.Clear(Color.Transparent);
                            downsample.CompositingMode = CompositingMode.SourceCopy;
                            downsample.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            downsample.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            downsample.DrawImage(large, new Rectangle(0, 0, 256, 256), new Rectangle(0, 0, 768, 768), GraphicsUnit.Pixel);
                            int column = frame % 3, row = frame / 3;
                            target.DrawImage(small, column * 256, row * 256, 256, 256);
                        }
                    }
                }
                sheet.Save(System.IO.Path.Combine(outputDirectory, names[character] + "Animations.png"), ImageFormat.Png);
            }
        }
    }

    public static void ExportFrames(string atlasPath, string outputDirectory, string characterName)
    {
        System.IO.Directory.CreateDirectory(outputDirectory);
        using (var atlas = new Bitmap(atlasPath))
        {
            for (int frame = 0; frame < 9; frame++)
            {
                int column = frame % 3, row = frame / 3;
                using (var image = atlas.Clone(new Rectangle(column * 256, row * 256, 256, 256), PixelFormat.Format32bppArgb))
                    image.Save(System.IO.Path.Combine(outputDirectory, characterName + "Frame" + frame + ".png"), ImageFormat.Png);
            }
        }
    }

    static void DrawPose(Graphics g, int c, int frame)
    {
        if (frame == 8)
        {
            g.TranslateTransform(128, 132);
            g.RotateTransform(90);
            g.TranslateTransform(-128, -132);
        }
        bool run = frame < 4;
        float swing = run ? new[] { 16f, -19f, -11f, 22f }[frame] : 0;
        float bodyDrop = frame == 7 ? 8 : (run && frame % 2 == 0 ? -4 : 0);
        if (frame == 5) bodyDrop = -8;
        if (frame == 6) bodyDrop = 2;
        g.TranslateTransform(0, bodyDrop);

        float leftKneeX = frame == 5 ? 99 : (frame == 6 ? 94 : 105 - swing);
        float rightKneeX = frame == 5 ? 143 : (frame == 6 ? 152 : 138 + swing);
        float leftFootY = frame == 5 ? 188 : (frame == 6 ? 186 : 222 - Math.Max(0, swing * .48f));
        float rightFootY = frame == 5 ? 194 : (frame == 6 ? 197 : 222 + Math.Min(0, swing * .48f));
        if (frame == 7) { leftKneeX = 106; rightKneeX = 139; leftFootY = 220; rightFootY = 220; }

        // Back leg, then foreground leg, with restrained outlines and softly shaded forms.
        Bone(g, new PointF(111, 151), new PointF(leftKneeX, 178), 12, trouser[c], dark[c]);
        Bone(g, new PointF(leftKneeX, 178), new PointF(leftKneeX - 5, leftFootY), 12, trouser[c], dark[c]);
        Boot(g, leftKneeX - 10, leftFootY - 4, c, false);
        Bone(g, new PointF(133, 151), new PointF(rightKneeX, 176), 14, Lighten(trouser[c], .09f), dark[c]);
        Bone(g, new PointF(rightKneeX, 176), new PointF(rightKneeX + 5, rightFootY), 14, Lighten(trouser[c], .09f), dark[c]);
        Boot(g, rightKneeX, rightFootY - 5, c, true);

        // Backpack silhouette and strap.
        RoundFill(g, 62, 93, 35, 53, 12, Lighten(shade[c], .15f), shade[c], dark[c], 4);
        RoundFill(g, 66, 95, 28, 13, 7, trim[c], Darken(trim[c], .13f), dark[c], 2);
        Line(g, new PointF(67, 108), new PointF(68, 137), dark[c], 2);
        Circle(g, 82, 117, 5, trim[c], Darken(trim[c], .2f), 1.5f);

        // Arms behind the body. Jump/fall poses lift them; running swings them opposite the legs.
        float armY = frame == 5 ? -28 : (frame == 6 ? -5 : 0);
        float armSwing = run ? -swing * .68f : (frame == 7 ? 20 : 0);
        Bone(g, new PointF(95, 102), new PointF(78 - armSwing * .35f, 124 + armY), 11, shade[c], dark[c]);
        Bone(g, new PointF(78 - armSwing * .35f, 124 + armY), new PointF(87 - armSwing, 139 + armY), 9, skin[c], dark[c]);

        // Torso: a separate silhouette, fabric shading, and the character-specific adventure gear.
        DrawTorso(g, c);
        if (c == 1)
        {
            Poly(g, new[] { new PointF(92, 101), new PointF(81, 115), new PointF(96, 145), new PointF(108, 136) },
                Hex("42645C"), dark[c], 3);
            Poly(g, new[] { new PointF(112, 102), new PointF(146, 103), new PointF(137, 135), new PointF(114, 132) },
                Hex("BC5B3C"), Darken(shade[c], .05f), 2);
            Line(g, new PointF(113, 105), new PointF(139, 129), trim[c], 5);
            Line(g, new PointF(102, 117), new PointF(138, 117), Hex("EAC46A"), 3);
        }
        else if (c == 2)
        {
            RoundFill(g, 101, 100, 42, 19, 7, Lighten(trim[c], .08f), trim[c], shade[c], 2);
            Line(g, new PointF(108, 107), new PointF(136, 107), Color.White, 2);
        }
        else if (c == 3)
        {
            Poly(g, new[] { new PointF(97, 95), new PointF(147, 100), new PointF(139, 116), new PointF(107, 110) },
                Hex("2D8F91"), Darken(shade[c], .1f), 2);
            RoundFill(g, 120, 116, 23, 22, 5, Lighten(shade[c], .2f), shade[c], dark[c], 2);
            Circle(g, 132, 128, 3, trim[c], Darken(trim[c], .2f), 1);
        }
        else
        {
            RoundFill(g, 101, 113, 20, 22, 4, Lighten(shirt[c], .2f), shirt[c], dark[c], 2);
            Line(g, new PointF(106, 118), new PointF(116, 118), trim[c], 2);
            Line(g, new PointF(113, 98), new PointF(131, 121), Hex("C08A4D"), 4);
        }
        RoundFill(g, 92, 153, 63, 8, 3, Lighten(trim[c], .08f), Darken(trim[c], .14f), dark[c], 1.5f);
        Circle(g, 121, 154, 4, Hex("E7C36B"), Hex("80521D"), 1.1f);
        Line(g, new PointF(99, 157), new PointF(110, 157), Hex("F1D18A"), .8f);

        // Far arm and the distinct wrist wrap.
        Bone(g, new PointF(145, 102), new PointF(159 + armSwing * .25f, 122 + armY), 11, Lighten(shirt[c], .05f), dark[c]);
        Bone(g, new PointF(159 + armSwing * .25f, 122 + armY), new PointF(149 + armSwing, 139 + armY), 9, skin[c], dark[c]);
        RoundFill(g, 146 + armSwing, 134 + armY, 13, 6, 2.5f, Lighten(trim[c], .08f), Darken(trim[c], .18f), dark[c], 1.2f);

        // Neck, face profile, eye and hairstyle.
        GraphicsState headState = g.Save();
        g.TranslateTransform(128, 61);
        g.ScaleTransform(.90f, .91f);
        g.TranslateTransform(-128, -61);
        RoundFill(g, 108, 78, 23, 24, 8, Lighten(skin[c], .04f), Darken(skin[c], .09f), dark[c], 2.2f);
        Circle(g, 101, 55, 10, Lighten(skin[c], .06f), Darken(skin[c], .17f), 2);
        Circle(g, 103, 55, 5.5f, Lighten(skin[c], .08f), Darken(skin[c], .1f), .8f);
        RoundFill(g, 101, 34, 54, 56, 20, Lighten(skin[c], .12f), skin[c], Darken(skin[c], .24f), 2.5f);
        Circle(g, 145, 61, 5, Lighten(skin[c], .05f), Darken(skin[c], .17f), 1.5f);
        Circle(g, 145.5f, 61.5f, 2, Lighten(skin[c], .28f), Color.Transparent, 0);
        // Side-lit cheek, defined nose bridge, eye, brow and a subtle smile.
        using (var cheek = new SolidBrush(Color.FromArgb(42, 177, 91, 67))) g.FillEllipse(cheek, 126, 64, 15, 8);
        Curve(g, new PointF(149, 50), new PointF(151, 57), new PointF(155, 62), new PointF(160, 65), Darken(skin[c], .16f), 1.3f);
        Poly(g, new[] { new PointF(153, 61), new PointF(162, 66), new PointF(153, 69) }, Lighten(skin[c], .02f), Darken(skin[c], .22f), 1.2f);
        Circle(g, 140, 51, 3.4f, Hex("F4E9D4"), Darken(skin[c], .2f), .8f);
        Circle(g, 141.4f, 51.7f, 1.55f, Darken(hair[c], .04f), Darken(hair[c], .04f), .3f);
        Line(g, new PointF(136.5f, 46), new PointF(145, 45), hair[c], 1.8f);
        Curve(g, new PointF(139, 76), new PointF(144, 78), new PointF(148, 77), new PointF(151, 74), Darken(skin[c], .38f), 1.15f);
        Line(g, new PointF(145, 79), new PointF(150, 78), Lighten(skin[c], .28f), .8f);
        if (c == 0) DrawExplorerHat(g);
        if (c == 1) DrawWarriorHair(g);
        if (c == 2) DrawScoutHeadband(g);
        if (c == 3) DrawAdventurerHair(g);
        // Hair catches thin warm highlights instead of reading as a flat cutout.
        if (c == 0) Curve(g, new PointF(108, 22), new PointF(117, 19), new PointF(129, 20), new PointF(136, 24), Hex("E0BD7C"), 1.1f);
        if (c == 1) Curve(g, new PointF(104, 36), new PointF(113, 29), new PointF(124, 30), new PointF(133, 33), Hex("55414A"), 1.2f);
        if (c == 2) Curve(g, new PointF(106, 34), new PointF(116, 30), new PointF(129, 34), new PointF(139, 39), Hex("565056"), 1.1f);
        if (c == 3) Curve(g, new PointF(108, 34), new PointF(118, 29), new PointF(129, 32), new PointF(138, 39), Hex("B86B4C"), 1.2f);
        g.Restore(headState);

    }

    static void DrawTorso(Graphics g, int c)
    {
        using (var path = new GraphicsPath())
        {
            path.StartFigure();
            path.AddBezier(101, 91, 106, 87, 137, 87, 145, 94);
            path.AddBezier(145, 94, 151, 102, 154, 119, 151, 137);
            path.AddBezier(151, 137, 150, 147, 146, 155, 141, 158);
            path.AddBezier(141, 158, 130, 161, 108, 160, 98, 157);
            path.AddBezier(98, 157, 91, 149, 89, 133, 91, 117);
            path.AddBezier(91, 117, 92, 105, 95, 96, 101, 91);
            path.CloseFigure();
            using (var brush = new LinearGradientBrush(new Rectangle(89, 88, 67, 72), Lighten(shirt[c], .20f), shade[c], LinearGradientMode.Horizontal)) g.FillPath(brush, path);
            using (var pen = new Pen(dark[c], 2.6f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
        }
        Curve(g, new PointF(103, 98), new PointF(111, 94), new PointF(126, 94), new PointF(136, 97), Lighten(shirt[c], .40f), 1.2f);
        Curve(g, new PointF(100, 111), new PointF(104, 120), new PointF(101, 131), new PointF(104, 140), Darken(shade[c], .08f), 1.2f);
        Curve(g, new PointF(142, 108), new PointF(145, 119), new PointF(143, 130), new PointF(140, 140), Darken(shade[c], .12f), 1.1f);
        Line(g, new PointF(107, 145), new PointF(116, 147), Lighten(shirt[c], .30f), 1.1f);
        Line(g, new PointF(128, 145), new PointF(136, 143), Darken(shade[c], .08f), 1.1f);
    }

    static void DrawExplorerHat(Graphics g)
    {
        Poly(g, new[] { new PointF(92, 41), new PointF(103, 16), new PointF(138, 16), new PointF(151, 43) },
            Hex("A77A43"), Hex("573C23"), 4);
        RoundFill(g, 84, 38, 86, 13, 6, Hex("D2AB69"), Hex("A77A43"), Hex("573C23"), 3);
        RoundFill(g, 101, 20, 39, 18, 7, Hex("C59654"), Hex("A77A43"), Hex("573C23"), 3);
        Line(g, new PointF(105, 35), new PointF(136, 35), Hex("5B4429"), 3);
        Line(g, new PointF(95, 44), new PointF(158, 44), Hex("E3C583"), 2);
    }

    static void DrawWarriorHair(Graphics g)
    {
        Poly(g, new[] { new PointF(99, 50), new PointF(95, 30), new PointF(104, 17), new PointF(110, 28), new PointF(118, 6), new PointF(124, 27), new PointF(138, 13), new PointF(136, 42) },
            Hex("30212B"), Hex("211A22"), 4);
        Line(g, new PointF(103, 36), new PointF(139, 35), Hex("D8A944"), 5);
        Poly(g, new[] { new PointF(124, 22), new PointF(139, 8), new PointF(135, 30) }, Hex("A7A33F"), Hex("66572A"), 2);
        Circle(g, 118, 34, 4, Hex("E2B44D"), Hex("57352A"), 1.2f);
    }

    static void DrawScoutHeadband(Graphics g)
    {
        Poly(g, new[] { new PointF(98, 48), new PointF(101, 31), new PointF(116, 25), new PointF(137, 35), new PointF(145, 53), new PointF(139, 57), new PointF(127, 42), new PointF(108, 40) },
            Hex("22232A"), Hex("1A1B20"), 3);
        RoundFill(g, 101, 39, 47, 9, 4, Hex("E3D18A"), Hex("AFA05F"), Hex("4B4936"), 2);
        Line(g, new PointF(106, 43), new PointF(141, 43), Hex("6CC0AE"), 2);
    }

    static void DrawAdventurerHair(Graphics g)
    {
        Circle(g, 91, 44, 17, Hex("7B412F"), Hex("4C2A24"), 4);
        Circle(g, 82, 37, 10, Hex("8B4A31"), Hex("4C2A24"), 3);
        Circle(g, 82, 57, 10, Hex("7B412F"), Hex("4C2A24"), 3);
        Poly(g, new[] { new PointF(98, 48), new PointF(103, 30), new PointF(120, 25), new PointF(138, 38), new PointF(145, 51), new PointF(134, 48), new PointF(119, 41), new PointF(106, 49) },
            Hex("84442F"), Hex("4B2A22"), 3);
        Line(g, new PointF(119, 35), new PointF(146, 41), Hex("4DA6A6"), 5);
        Circle(g, 88, 34, 5, Hex("D58B38"), Hex("653921"), 1.2f);
    }

    static void Boot(Graphics g, float x, float y, int c, bool front)
    {
        RoundFill(g, x, y + 1, front ? 25 : 22, 12, 5, Lighten(dark[c], .28f), dark[c], Hex("211D1A"), 2.2f);
        Line(g, new PointF(x + 5, y + 10), new PointF(x + 21, y + 10), Hex("B99051"), 1.1f);
        Line(g, new PointF(x + 9, y + 4), new PointF(x + 17, y + 4), Lighten(dark[c], .46f), .8f);
        Circle(g, x + (front ? 17 : 15), y + 5, 1.3f, Hex("E2C278"), Hex("4B3829"), .7f);
    }

    static void Bone(Graphics g, PointF a, PointF b, float width, Color fill, Color outline)
    {
        float finalWidth = width * .84f;
        using (var edge = new Pen(outline, finalWidth + 3) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        using (var center = new Pen(fill, finalWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            g.DrawLine(edge, a, b);
            g.DrawLine(center, a, b);
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length > .1f)
            {
                float nx = -dy / length * finalWidth * .18f;
                float ny = dx / length * finalWidth * .18f;
                using (var highlight = new Pen(Lighten(fill, .27f), Math.Max(.8f, finalWidth * .12f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(highlight, new PointF(a.X + nx, a.Y + ny), new PointF(b.X + nx, b.Y + ny));
            }
        }
        Circle(g, b.X - finalWidth * .18f, b.Y - finalWidth * .18f, finalWidth * .40f, fill, outline, 1.7f);
    }

    static void RoundFill(Graphics g, float x, float y, float w, float h, float r, Color top, Color bottom, Color outline, float outlineWidth)
    {
        using (var path = new GraphicsPath())
        {
            float d = r * 2;
            path.AddArc(x, y, d, d, 180, 90); path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90); path.AddArc(x, y + h - d, d, d, 90, 90); path.CloseFigure();
            using (var brush = new LinearGradientBrush(new RectangleF(x, y, w, h), top, bottom, LinearGradientMode.Vertical)) g.FillPath(brush, path);
            using (var pen = new Pen(outline, outlineWidth) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
        }
    }

    static void Poly(Graphics g, PointF[] points, Color fill, Color outline, float outlineWidth)
    {
        using (var path = new GraphicsPath())
        {
            path.AddPolygon(points);
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            using (var pen = new Pen(outline, outlineWidth) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
        }
    }

    static void Circle(Graphics g, float x, float y, float diameter, Color fill, Color outline, float outlineWidth)
    {
        using (var brush = new SolidBrush(fill)) g.FillEllipse(brush, x, y, diameter, diameter);
        if (outlineWidth > 0) using (var pen = new Pen(outline, outlineWidth)) g.DrawEllipse(pen, x, y, diameter, diameter);
    }

    static void Line(Graphics g, PointF a, PointF b, Color color, float width)
    {
        using (var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLine(pen, a, b);
    }

    static void Curve(Graphics g, PointF a, PointF b, PointF c, PointF d, Color color, float width)
    {
        using (var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawBezier(pen, a, b, c, d);
    }

    static Color Lighten(Color c, float amount) => Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * amount), (int)(c.G + (255 - c.G) * amount), (int)(c.B + (255 - c.B) * amount));
    static Color Darken(Color c, float amount) => Color.FromArgb(c.A, (int)(c.R * (1 - amount)), (int)(c.G * (1 - amount)), (int)(c.B * (1 - amount)));
    static Color Hex(string value) => ColorTranslator.FromHtml("#" + value);
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies $references
$output = Join-Path $PSScriptRoot '..\Assets\JungleQuest\Resources\CharacterArt'
[JungleCharacterArt]::Generate($output)
$frames = Join-Path $PSScriptRoot '..\Assets\JungleQuest\Resources\CharacterFrames'
foreach ($name in @('Maya', 'Koa', 'Tavi', 'Nia')) {
    [JungleCharacterArt]::ExportFrames((Join-Path $output ($name + 'Animations.png')), $frames, $name)
}
Write-Output "Generated four distinct 3x3 character animation sheets into $output"
