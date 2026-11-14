using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Ludens
{
    public sealed class AccentOption
    {
        public string Name { get; set; }
        public string Hex { get; set; }
        public SolidColorBrush Brush { get; set; }
    }

    public static class ThemeManager
    {
        public static readonly AccentOption[] Accents = new AccentOption[]
        {
            Option("燃橙", "#FF5B3A"),
            Option("极光蓝", "#5B9DFF"),
            Option("薄荷", "#35D6A4"),
            Option("琥珀", "#FFC24B"),
            Option("紫罗兰", "#A78BFA"),
            Option("樱粉", "#FF7FAE")
        };

        public static bool IsDark { get; private set; }
        public static Color AccentColor { get; private set; }
        public static string CurrentTheme { get; private set; }

        private static AccentOption Option(string name, string hex)
        {
            Color color = (Color)ColorConverter.ConvertFromString(hex);
            return new AccentOption { Name = name, Hex = hex, Brush = new SolidColorBrush(color) };
        }

        public static void Apply(AppSettings settings)
        {
            if (settings == null) settings = new AppSettings();
            IsDark = !string.Equals(settings.Theme, "light", StringComparison.OrdinalIgnoreCase);
            CurrentTheme = IsDark ? "dark" : "light";
            AccentColor = ParseColor(settings.AccentHex, Color.FromRgb(0xFF, 0x5B, 0x3A));
            ApplyTheme(IsDark, AccentColor);
        }

        public static void ApplyTheme(bool dark, Color accent)
        {
            IsDark = dark;
            AccentColor = accent;
            CurrentTheme = dark ? "dark" : "light";
            Application app = Application.Current;
            if (app == null) return;

            if (dark)
            {
                Set(app, "Bg0", "#08090C");
                Set(app, "Bg1", "#0E1014");
                Set(app, "Bg2", "#14171C");
                Set(app, "Bg3", "#1B1F26");
                Set(app, "Card", "#12151A");
                Set(app, "CardHover", "#191D24");
                Set(app, "Elevated", "#1D2129");
                Set(app, "Nav", "#0B0D11");
                Set(app, "Input", "#14171D");
                Set(app, "Hover", "#1B1F27");
                Set(app, "TextPrimary", "#F3F4F6");
                Set(app, "TextSecondary", "#9DA4B1");
                Set(app, "TextMuted", "#6C7381");
                Set(app, "Border", "#242831");
                Set(app, "BorderStrong", "#343A47");
                Set(app, "Overlay", "#090A0D");
                Set(app, "Shadow", "#000000");
                Set(app, "Scrim", "#B308090C");
                Set(app, "Glass", "#E6111418");
                Set(app, "White", "#FFFFFF");
                Set(app, "HoverOverlay", "#14FFFFFF");
                Set(app, "ScrollThumb", "#3A404D");
            }
            else
            {
                Set(app, "Bg0", "#F1F0ED");
                Set(app, "Bg1", "#F7F6F3");
                Set(app, "Bg2", "#FCFCFA");
                Set(app, "Bg3", "#FFFFFF");
                Set(app, "Card", "#FFFFFF");
                Set(app, "CardHover", "#F4F2EE");
                Set(app, "Elevated", "#FFFFFF");
                Set(app, "Nav", "#F7F6F3");
                Set(app, "Input", "#FFFFFF");
                Set(app, "Hover", "#EDEAE4");
                Set(app, "TextPrimary", "#181A1F");
                Set(app, "TextSecondary", "#4F5663");
                Set(app, "TextMuted", "#6F7683");
                Set(app, "Border", "#E4E1DB");
                Set(app, "BorderStrong", "#D1CDC5");
                Set(app, "Overlay", "#F7F6F3");
                Set(app, "Shadow", "#3A3228");
                Set(app, "Scrim", "#CCF1F0ED");
                Set(app, "Glass", "#F2FFFFFF");
                Set(app, "White", "#FFFFFF");
                Set(app, "HoverOverlay", "#10000000");
                Set(app, "ScrollThumb", "#C9C4BB");
            }

            Set(app, "Accent", accent);
            Set(app, "AccentHover", dark ? Lighten(accent, 0.12) : Darken(accent, 0.08));
            Set(app, "AccentPressed", Darken(accent, dark ? 0.14 : 0.18));
            Set(app, "AccentSoft", WithAlpha(accent, dark ? 0.15 : 0.12));
            Set(app, "AccentSoftStrong", WithAlpha(accent, dark ? 0.26 : 0.20));
            Set(app, "AccentGlow", WithAlpha(accent, dark ? 0.34 : 0.22));
            Set(app, "AccentForeground", ContrastText(accent));
            Set(app, "Success", dark ? "#4ED29B" : "#159A6D");
            Set(app, "Danger", dark ? "#FF6577" : "#D93A4B");
            Set(app, "Warning", dark ? "#F7C65A" : "#B98514");
            Set(app, "Focus", WithAlpha(accent, 0.55));
        }

        public static void ApplyTheme(string theme)
        {
            AppSettings settings = AppStore.Instance.Settings;
            settings.Theme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
            Apply(settings);
        }

        public static void ApplyAccent(string hex)
        {
            AppSettings settings = AppStore.Instance.Settings;
            settings.AccentHex = hex;
            Apply(settings);
        }

        public static SolidColorBrush Brush(string key)
        {
            object value = Application.Current == null ? null : Application.Current.TryFindResource(key);
            SolidColorBrush brush = value as SolidColorBrush;
            if (brush != null) return brush;
            return Brushes.Transparent;
        }

        public static Color ResourceColor(string key)
        {
            return Brush(key).Color;
        }

        public static Color Lighten(Color color, double amount)
        {
            return Color.FromArgb(color.A,
                (byte)Math.Min(255, color.R + (255 - color.R) * amount),
                (byte)Math.Min(255, color.G + (255 - color.G) * amount),
                (byte)Math.Min(255, color.B + (255 - color.B) * amount));
        }

        public static Color Darken(Color color, double amount)
        {
            return Color.FromArgb(color.A,
                (byte)(color.R * (1 - amount)),
                (byte)(color.G * (1 - amount)),
                (byte)(color.B * (1 - amount)));
        }

        public static Color WithAlpha(Color color, double alpha)
        {
            return Color.FromArgb((byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255), color.R, color.G, color.B);
        }

        private static Color ContrastText(Color accent)
        {
            double luminance = (0.2126 * accent.R + 0.7152 * accent.G + 0.0722 * accent.B) / 255.0;
            return luminance > 0.68 ? Color.FromRgb(0x1A, 0x18, 0x14) : Colors.White;
        }

        private static Color ParseColor(string value, Color fallback)
        {
            try
            {
                if (!string.IsNullOrEmpty(value) && value.StartsWith("#"))
                    return (Color)ColorConverter.ConvertFromString(value);
            }
            catch { }
            return fallback;
        }

        private static void Set(Application app, string key, string hex)
        {
            Set(app, key, (Color)ColorConverter.ConvertFromString(hex));
        }

        private static void Set(Application app, string key, Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            app.Resources[key] = brush;
            app.Resources[key + "Color"] = color;
        }
    }
}
