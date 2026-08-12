using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Bridge.Services;

/// <summary>
/// Runtime theme: the user picks an accent color and the whole palette is
/// recomputed from it (accent + indigo-tinted backgrounds + borders). The theme
/// brushes are replaced in Application resources; consumers reference them with
/// DynamicResource so they re-resolve instantly. The choice is persisted to
/// AppData and restored on startup.
/// </summary>
public static class ThemeManager
{
    private static readonly string SettingsFile = Path.Combine(Config.AppDataPath, "theme.json");

    public static Color CurrentAccent { get; private set; } = Color.FromRgb(0x00, 0x7A, 0xCC);

    public static IReadOnlyList<(string Name, Color Color)> Presets { get; } = new (string, Color)[]
    {
        ("Blue",    Color.FromRgb(0x00, 0x7A, 0xCC)),
        ("Cyan",    Color.FromRgb(0x00, 0xB4, 0xD8)),
        ("Teal",    Color.FromRgb(0x14, 0xB8, 0xA6)),
        ("Green",   Color.FromRgb(0x10, 0xB9, 0x81)),
        ("Purple",  Color.FromRgb(0x8B, 0x5C, 0xF6)),
        ("Pink",    Color.FromRgb(0xEC, 0x48, 0x99)),
        ("Red",     Color.FromRgb(0xEF, 0x44, 0x44)),
        ("Orange",  Color.FromRgb(0xF9, 0x73, 0x16)),
        ("Amber",   Color.FromRgb(0xF5, 0x9E, 0x0B)),
    };

    public static void Apply(Color accent)
    {
        CurrentAccent = accent;
        var resources = Application.Current.Resources;

        Color accentSecondary, accentTertiary, accentHover;
        Color bg1, bg2, bg3, bg4, sep, stroke1, stroke2;

        // The default Blue keeps the original hand-tuned indigo palette (which
        // was a different hue than the accent); other colors are generated from
        // their own hue so the whole theme shifts cohesively.
        if (ToHex(accent) == "#007ACC")
        {
            accentSecondary = FromHex("#2B9ADE");
            accentTertiary = FromHex("#0063A8");
            accentHover = FromHex("#1F4E79");
            bg1 = FromHex("#151A28");
            bg2 = FromHex("#1B2132");
            bg3 = FromHex("#232B40");
            bg4 = FromHex("#2C3550");
            sep = FromHex("#34415F");
            stroke1 = FromHex("#3A4668");
            stroke2 = FromHex("#4A5878");
        }
        else
        {
            accentSecondary = Shift(accent, light: 0.20f);
            accentTertiary = Shift(accent, light: -0.20f);
            accentHover = Tint(accent, light: 0.30f, sat: 0.62f);
            bg1 = Tint(accent, light: 0.09f, sat: 0.28f);
            bg2 = Tint(accent, light: 0.12f, sat: 0.30f);
            bg3 = Tint(accent, light: 0.16f, sat: 0.32f);
            bg4 = Tint(accent, light: 0.20f, sat: 0.34f);
            sep = Tint(accent, light: 0.24f, sat: 0.30f);
            stroke1 = Tint(accent, light: 0.27f, sat: 0.26f);
            stroke2 = Tint(accent, light: 0.32f, sat: 0.26f);
        }

        // Accent
        Replace(resources, "SystemAccentColorPrimaryBrush", accent);
        Replace(resources, "SystemAccentColorSecondaryBrush", accentSecondary);
        Replace(resources, "SystemAccentColorTertiaryBrush", accentTertiary);
        Replace(resources, "Bridge.SystemAccentBrush", accent);
        Replace(resources, "Bridge.Accent.Hover", accentHover);

        // Wpf.Ui accent brushes (Slider/CheckBox/Button/selection)
        Replace(resources, "AccentFillColorDefaultBrush", accent);
        Replace(resources, "AccentFillColorSecondaryBrush", accent);
        Replace(resources, "AccentFillColorTertiaryBrush", accent);
        Replace(resources, "AccentFillColorSelectedTextBackgroundBrush", accent);
        Replace(resources, "AccentTextFillColorPrimaryBrush", accent);
        Replace(resources, "AccentTextFillColorSecondaryBrush", accentSecondary);
        Replace(resources, "AccentTextFillColorTertiaryBrush", accentTertiary);

        // Solid backgrounds
        Replace(resources, "ApplicationBackgroundBrush", bg1);
        Replace(resources, "SolidBackgroundFillColorBaseBrush", bg1);
        Replace(resources, "SolidBackgroundFillColorBaseAltBrush", bg1);
        Replace(resources, "SolidBackgroundFillColorSecondaryBrush", bg2);
        Replace(resources, "SolidBackgroundFillColorTertiaryBrush", bg3);
        Replace(resources, "SolidBackgroundFillColorQuarternaryBrush", bg4);

        // Control fills
        Replace(resources, "ControlFillColorDefaultBrush", bg3);
        Replace(resources, "ControlFillColorSecondaryBrush", bg4);
        Replace(resources, "ControlFillColorTertiaryBrush", stroke1);
        Replace(resources, "ControlFillColorInputActiveBrush", bg3);
        Replace(resources, "ControlSolidFillColorDefaultBrush", bg3);
        Replace(resources, "SubtleFillColorSecondaryBrush", bg2);
        Replace(resources, "SubtleFillColorTertiaryBrush", bg3);
        Replace(resources, "ControlAltFillColorSecondaryBrush", bg2);
        Replace(resources, "ControlAltFillColorTertiaryBrush", bg3);

        // Bridge surfaces
        Replace(resources, "Bridge.Sidebar.Background", bg2, opacity: 0.75);
        Replace(resources, "Bridge.Card.Background", bg2);
        Replace(resources, "Bridge.Card.Hover", bg4);
        Replace(resources, "Bridge.SeparatorBrush", sep);

        // Borders
        Replace(resources, "ControlStrokeColorDefaultBrush", stroke1);
        Replace(resources, "SurfaceStrokeColorDefaultBrush", stroke1);
        Replace(resources, "ControlStrokeColorSecondaryBrush", stroke2);
        Replace(resources, "ControlStrongFillColorDefaultBrush", stroke2);

        Save();

        // DynamicResource in Style triggers (hover/selected/sidebar border) isn't
        // re-evaluated on resource change; restyle the open window so the new
        // accent is visible everywhere immediately. No-op during startup Load.
        RefreshWindow();
    }

    /// <summary>Restores the saved accent (or the default) at startup.</summary>
    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFile) &&
                TryParseHex(File.ReadAllText(SettingsFile).Trim(), out var saved))
            {
                Apply(saved);
                return;
            }
        }
        catch
        {
            // Corrupt/missing settings — fall back to the default.
        }

        Apply(CurrentAccent);
    }

    public static string ToHex(Color c)
        => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>A grid of saturated colors for the custom picker.</summary>
    public static IReadOnlyList<Color> Palette
    {
        get
        {
            var list = new List<Color>();
            for (float h = 0; h < 360f; h += 30f)
            {
                for (float l = 0.30f; l <= 0.85f; l += 0.11f)
                {
                    list.Add(FromHsl(h / 360f, 0.75f, l));
                }
            }

            return list;
        }
    }

    public static bool TryParseHex(string hex, out Color color) => TryParseHexCore(hex, out color);

    /// <summary>
    /// DynamicResource inside Style triggers (hover/selected states, sidebar
    /// active border, view toggles, ...) is not re-evaluated when the resource
    /// changes. Re-applying every element's style forces the trigger setters to
    /// resolve against the new palette.
    /// </summary>
    public static void RefreshWindow()
    {
        if (Application.Current.MainWindow is { } window)
        {
            ForceRestyle(window);
        }
    }

    private static void ForceRestyle(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Style is not null)
            {
                var style = fe.Style;
                fe.Style = null;
                fe.Style = style;
            }

            ForceRestyle(child);
        }
    }

    private static void Replace(ResourceDictionary resources, string key, Color color, double opacity = 1.0)
    {
        var brush = new SolidColorBrush(color);
        if (opacity < 1.0)
        {
            brush.Opacity = opacity;
        }

        resources[key] = brush;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, ToHex(CurrentAccent));
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static bool TryParseHexCore(string hex, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(hex) || hex.Length != 7 || hex[0] != '#')
        {
            return false;
        }

        return byte.TryParse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)
            && (color = Color.FromRgb(r, g, b)) is var _;
    }

    private static Color FromHex(string hex)
    {
        if (!TryParseHexCore(hex, out var color))
        {
            throw new ArgumentException($"Invalid color hex: {hex}", nameof(hex));
        }

        return color;
    }

    // --- HSL helpers -------------------------------------------------------

    private static Color Shift(Color c, float light)
    {
        var (h, s, l) = ToHsl(c);
        return FromHsl(h, s, Math.Clamp(l + light, 0f, 1f));
    }

    private static Color Tint(Color c, float light, float sat)
    {
        var (h, _, _) = ToHsl(c);
        return FromHsl(h, sat, light);
    }

    private static (float h, float s, float l) ToHsl(Color c)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        float l = (max + min) / 2f;

        if (Math.Abs(max - min) < 0.0001f)
        {
            return (0f, 0f, l);
        }

        float d = max - min;
        float s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
        float h;
        if (max == r) h = (g - b) / d + (g < b ? 6f : 0f);
        else if (max == g) h = (b - r) / d + 2f;
        else h = (r - g) / d + 4f;
        h /= 6f;
        return (h, s, l);
    }

    private static Color FromHsl(float h, float s, float l)
    {
        if (s <= 0f)
        {
            var g0 = (byte)(l * 255f);
            return Color.FromRgb(g0, g0, g0);
        }

        float Hue2Rgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;
        return Color.FromRgb(
            (byte)(Hue2Rgb(p, q, h + 1f / 3f) * 255f),
            (byte)(Hue2Rgb(p, q, h) * 255f),
            (byte)(Hue2Rgb(p, q, h - 1f / 3f) * 255f));
    }
}
