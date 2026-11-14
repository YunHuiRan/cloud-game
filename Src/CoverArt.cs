using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ludens
{
    /// <summary>Generates deterministic poster and wallpaper art for games without artwork.</summary>
    public static class CoverArt
    {
        private sealed class Palette
        {
            public Color Accent;
            public Color Deep;
            public Color Mid;
            public Color Light;
        }

        private static readonly Palette[] Palettes = new Palette[]
        {
            P("#FF6B45", "#120E0C", "#2A1913", "#3B241B"),
            P("#5CB3FF", "#08131F", "#132A3D", "#1B3A52"),
            P("#A98BFF", "#100C1B", "#241C3B", "#33264F"),
            P("#43E0AB", "#061A16", "#0D3129", "#123F34"),
            P("#FFC24B", "#1A1408", "#33270F", "#443314"),
            P("#FF7FAE", "#1B0D14", "#351A27", "#482335"),
            P("#55E4EA", "#06191D", "#0C3037", "#114149"),
            P("#C9F56A", "#121805", "#27320D", "#354414"),
            P("#FF8A5B", "#160F0B", "#332017", "#452A1D"),
            P("#7C9CFF", "#0B1020", "#1B2540", "#263455")
        };

        private static Palette P(string accent, string deep, string mid, string light)
        {
            return new Palette
            {
                Accent = (Color)ColorConverter.ConvertFromString(accent),
                Deep = (Color)ColorConverter.ConvertFromString(deep),
                Mid = (Color)ColorConverter.ConvertFromString(mid),
                Light = (Color)ColorConverter.ConvertFromString(light)
            };
        }

        public static void GenerateCover(GameEntry game, string path)
        {
            const int width = 720;
            const int height = 960;
            Palette p = Pick(game);
            Random random = new Random(Hash(game.Id + "|" + game.Title));
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                Rect full = new Rect(0, 0, width, height);
                LinearGradientBrush bg = new LinearGradientBrush(p.Mid, p.Deep, new Point(0.1, 0), new Point(0.9, 1));
                dc.DrawRectangle(bg, null, full);

                RadialGradientBrush glow = new RadialGradientBrush();
                glow.GradientStops.Add(new GradientStop(WithAlpha(p.Accent, 0.52), 0));
                glow.GradientStops.Add(new GradientStop(WithAlpha(p.Accent, 0.18), 0.34));
                glow.GradientStops.Add(new GradientStop(WithAlpha(p.Accent, 0.0), 0.72));
                glow.GradientStops.Add(new GradientStop(WithAlpha(p.Accent, 0.0), 1));
                dc.DrawEllipse(glow, null, new Point(width * 0.78, height * 0.17), width * 0.72, width * 0.72);

                // Diagonal colour fields.
                for (int i = 0; i < 4; i++)
                {
                    double offset = -260 + i * 210 + random.Next(-30, 30);
                    PointCollection points = new PointCollection();
                    points.Add(new Point(offset, height));
                    points.Add(new Point(offset + 150, height));
                    points.Add(new Point(offset + 560, 0));
                    points.Add(new Point(offset + 410, 0));
                    Color band = i == 1 ? p.Accent : p.Light;
                    dc.DrawGeometry(new SolidColorBrush(WithAlpha(band, i == 1 ? 0.14 : 0.20)), null,
                        new PathGeometry(new PathFigure[] { new PathFigure(points[0], new PathSegment[] { new LineSegment(points[1], true), new LineSegment(points[2], true), new LineSegment(points[3], true) }, true) }));
                }

                // Fine technical grid.
                Pen grid = new Pen(new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)), 1);
                for (int x = 36; x < width; x += 48) dc.DrawLine(grid, new Point(x, 0), new Point(x, height));
                for (int y = 36; y < height; y += 48) dc.DrawLine(grid, new Point(0, y), new Point(width, y));

                // Concentric rings.
                Pen ring = new Pen(new SolidColorBrush(WithAlpha(Colors.White, 0.16)), 1.3);
                for (int i = 1; i <= 4; i++)
                    dc.DrawEllipse(null, ring, new Point(width * 0.84, height * 0.22), 54 + i * 46, 54 + i * 46);
                dc.DrawEllipse(new SolidColorBrush(WithAlpha(p.Accent, 0.85)), null,
                    new Point(width * 0.84 + 140, height * 0.22 - 120), 5, 5);

                // Huge ghost initial.
                string initial = Initial(game.Title);
                if (!string.IsNullOrEmpty(initial))
                {
                    FormattedText ghost = Text(initial, 430, WithAlpha(Colors.White, 0.065), "Segoe UI", FontWeights.Black);
                    dc.DrawText(ghost, new Point(width - ghost.Width - 22, height * 0.10));
                }

                // Top index / label.
                FormattedText index = Text((Math.Abs(Hash(game.Id)) % 97 + 1).ToString("00"), 17,
                    WithAlpha(Colors.White, 0.62), "Consolas", FontWeights.Bold);
                dc.DrawText(index, new Point(46, 42));
                dc.DrawRectangle(new SolidColorBrush(p.Accent), null, new Rect(46, 74, 34, 3));

                // Bottom scrim.
                LinearGradientBrush scrim = new LinearGradientBrush(
                    Color.FromArgb(0, 0, 0, 0), Color.FromArgb(214, 4, 5, 7),
                    new Point(0, 0.36), new Point(0, 1));
                dc.DrawRectangle(scrim, null, new Rect(0, height * 0.36, width, height * 0.64));

                // Title block.
                double titleTop = height - 306;
                string title = string.IsNullOrEmpty(game.Title) ? "未命名游戏" : game.Title;
                double titleSize = title.Length > 26 ? 46 : (title.Length > 15 ? 54 : 64);
                FormattedText titleText = Text(title, titleSize, Colors.White, "Microsoft YaHei UI", FontWeights.Bold);
                titleText.MaxTextWidth = width - 92;
                titleText.MaxTextHeight = 168;
                titleText.Trimming = TextTrimming.CharacterEllipsis;
                titleText.LineHeight = titleSize * 1.18;
                FormattedText shadow = Text(title, titleSize, Color.FromArgb(130, 0, 0, 0), "Microsoft YaHei UI", FontWeights.Bold);
                shadow.MaxTextWidth = width - 92;
                shadow.MaxTextHeight = 168;
                shadow.Trimming = TextTrimming.CharacterEllipsis;
                shadow.LineHeight = titleSize * 1.18;
                dc.DrawText(shadow, new Point(48, titleTop + 3));
                dc.DrawText(titleText, new Point(46, titleTop));

                string meta = BuildMeta(game);
                FormattedText metaText = Text(meta, 18, WithAlpha(Colors.White, 0.72), "Microsoft YaHei UI", FontWeights.Normal);
                metaText.MaxTextWidth = width - 92;
                metaText.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(metaText, new Point(46, height - 92));

                dc.DrawRectangle(new SolidColorBrush(WithAlpha(Colors.White, 0.22)), null, new Rect(46, height - 54, width - 92, 1));
                FormattedText year = Text(game.ReleaseYear ?? "", 12, WithAlpha(Colors.White, 0.52), "Consolas", FontWeights.Normal);
                dc.DrawText(year, new Point(width - 46 - year.Width, height - 42));
            }
            SavePng(visual, width, height, path);
        }

        public static void GenerateBackground(GameEntry game, string path)
        {
            const int width = 1600;
            const int height = 900;
            Palette p = Pick(game);
            Random random = new Random(Hash(game.Id + "|bg|" + game.Title));
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                Rect full = new Rect(0, 0, width, height);
                LinearGradientBrush bg = new LinearGradientBrush(p.Deep, p.Mid, new Point(0.15, 0.1), new Point(0.85, 1));
                dc.DrawRectangle(bg, null, full);

                for (int i = 0; i < 3; i++)
                {
                    double cx = width * (0.18 + i * 0.30);
                    double cy = height * (0.18 + random.NextDouble() * 0.55);
                    double radius = 260 + random.Next(200);
                    RadialGradientBrush glow = new RadialGradientBrush(
                        WithAlpha(i == 1 ? p.Accent : p.Light, 0.30 - i * 0.05),
                        WithAlpha(p.Accent, 0));
                    dc.DrawEllipse(glow, null, new Point(cx, cy), radius, radius * 0.78);
                }

                // Sweeping arcs.
                Pen arc = new Pen(new SolidColorBrush(WithAlpha(Colors.White, 0.07)), 1.4);
                for (int i = 0; i < 6; i++)
                {
                    double radius = 220 + i * 120;
                    dc.DrawEllipse(null, arc, new Point(width * 0.82, height * 0.52), radius, radius);
                }

                // Diagonal hairlines.
                Pen line = new Pen(new SolidColorBrush(WithAlpha(Colors.White, 0.035)), 1);
                for (int i = -10; i < 22; i++)
                {
                    double x = i * 110;
                    dc.DrawLine(line, new Point(x, height), new Point(x + 420, 0));
                }

                FormattedText word = Text("CLOUD", 260, WithAlpha(Colors.White, 0.028), "Segoe UI", FontWeights.Black);
                word.SetFontStretch(FontStretches.Expanded);
                dc.DrawText(word, new Point(width - word.Width + 60, height * 0.55));

                // Vignette.
                RadialGradientBrush vignette = new RadialGradientBrush(
                    Color.FromArgb(0, 0, 0, 0), Color.FromArgb(185, 0, 0, 0));
                vignette.GradientOrigin = new Point(0.5, 0.45);
                vignette.Center = new Point(0.5, 0.45);
                vignette.RadiusX = 0.78;
                vignette.RadiusY = 0.92;
                dc.DrawRectangle(vignette, null, full);
            }
            SavePng(visual, width, height, path);
        }

        public static void GenerateLogo(GameEntry game, string path)
        {
            const int size = 512;
            Palette p = Pick(game);
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));
                LinearGradientBrush bg = new LinearGradientBrush(p.Accent, Darken(p.Accent, 0.35), new Point(0, 0), new Point(1, 1));
                dc.DrawRoundedRectangle(bg, null, new Rect(44, 44, size - 88, size - 88), 58, 58);
                string initial = Initial(game.Title);
                FormattedText mark = Text(initial, 220, Colors.White, "Segoe UI", FontWeights.Black);
                dc.DrawText(mark, new Point((size - mark.Width) / 2, (size - mark.Height) / 2 - 18));
                FormattedText caption = Text("CLOUD GAME", 17, WithAlpha(Colors.White, 0.8), "Segoe UI", FontWeights.SemiBold);
                caption.SetFontStretch(FontStretches.Expanded);
                dc.DrawText(caption, new Point((size - caption.Width) / 2, size - 106));
            }
            SavePng(visual, size, size, path);
        }

        private static string BuildMeta(GameEntry game)
        {
            string meta = "";
            if (!string.IsNullOrEmpty(game.Genre)) meta = game.Genre;
            if (!string.IsNullOrEmpty(game.Developer))
                meta = string.IsNullOrEmpty(meta) ? game.Developer : meta + "  ·  " + game.Developer;
            if (string.IsNullOrEmpty(meta)) meta = "本地游戏";
            return meta;
        }

        private static string Initial(string title)
        {
            if (string.IsNullOrEmpty(title)) return "?";
            for (int i = 0; i < title.Length; i++)
            {
                char c = title[i];
                if (!char.IsWhiteSpace(c) && c != ':' && c != '-' && c != '《' && c != '》') return c.ToString().ToUpperInvariant();
            }
            return "?";
        }

        private static Palette Pick(GameEntry game)
        {
            int index = Math.Abs(Hash((game.Id ?? "") + "|" + (game.Title ?? ""))) % Palettes.Length;
            return Palettes[index];
        }

        public static Color AccentFor(GameEntry game)
        {
            return Pick(game).Accent;
        }

        private static FormattedText Text(string text, double size, Color color, string family, FontWeight weight)
        {
            return new FormattedText(
                text ?? "",
                CultureInfo.GetCultureInfo("zh-CN"),
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily(family + ", Microsoft YaHei UI, Segoe UI"),
                    FontStyles.Normal, weight, FontStretches.Normal),
                size,
                new SolidColorBrush(color),
                1.0);
        }

        private static FormattedText Text(string text, double size, Brush brush, string family, FontWeight weight)
        {
            return new FormattedText(
                text ?? "",
                CultureInfo.GetCultureInfo("zh-CN"),
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily(family + ", Microsoft YaHei UI, Segoe UI"),
                    FontStyles.Normal, weight, FontStretches.Normal),
                size,
                brush,
                1.0);
        }

        private static void SavePng(DrawingVisual visual, int width, int height, string path)
        {
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }
        }

        private static Color WithAlpha(Color color, double alpha)
        {
            return Color.FromArgb((byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255), color.R, color.G, color.B);
        }

        private static Color Darken(Color color, double amount)
        {
            return Color.FromRgb(
                (byte)(color.R * (1 - amount)),
                (byte)(color.G * (1 - amount)),
                (byte)(color.B * (1 - amount)));
        }

        private static int Hash(string value)
        {
            unchecked
            {
                int hash = (int)2166136261;
                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619;
                    }
                }
                return hash;
            }
        }
    }
}
