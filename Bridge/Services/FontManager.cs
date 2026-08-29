using System.Windows;
using System.Windows.Media;

namespace Bridge.Services;

/// <summary>
/// Runtime font family switcher — espejo de ThemeManager pero para Bridge.FontFamily.
/// Reemplaza el recurso Application["Bridge.FontFamily"] y persiste la elección.
/// </summary>
public static class FontManager
{
    public static AppFont CurrentFont { get; private set; } = AppFont.Inter;

    public static IReadOnlyList<(AppFont Id, string Label, string FamilySource)> Fonts { get; } =
    [
        (AppFont.Inter, "Inter", "pack://application:,,,/Fonts/#Inter, Segoe UI"),
        (AppFont.SegoeUi, "Segoe UI", "Segoe UI"),
        (AppFont.SegoeUiVariable, "Outfit", "pack://application:,,,/Fonts/#Outfit, Segoe UI"),
        (AppFont.Consolas, "Consolas", "Consolas, Segoe UI"),
        (AppFont.Georgia, "Georgia", "Georgia, Segoe UI"),
    ];

    public static void Apply(AppFont font)
    {
        CurrentFont = font;
        var source = GetFamilySource(font);
        var family = new FontFamily(source);
        Application.Current.Resources["Bridge.FontFamily"] = family;
        FontSettingsStore.Save(font);
        ThemeManager.RefreshWindow();
    }

    public static void Load()
    {
        try
        {
            var saved = FontSettingsStore.Load();
            ApplyWithoutSave(saved);
            return;
        }
        catch { }
        ApplyWithoutSave(AppFont.Inter);
    }

    private static void ApplyWithoutSave(AppFont font)
    {
        CurrentFont = font;
        var source = GetFamilySource(font);
        Application.Current.Resources["Bridge.FontFamily"] = new FontFamily(source);
    }

    public static string GetFamilySource(AppFont font) =>
        Fonts.FirstOrDefault(f => f.Id == font).FamilySource ?? "pack://application:,,,/Fonts/#Inter";

    public static string GetLabel(AppFont font) =>
        Fonts.FirstOrDefault(f => f.Id == font).Label ?? "Inter";
}
