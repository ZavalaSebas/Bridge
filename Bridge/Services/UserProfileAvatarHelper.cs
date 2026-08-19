using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bridge.Services;

public static class UserProfileAvatarHelper
{
    public static readonly string[] DefaultAvatarIds = ["blue", "cyan", "teal", "green", "purple", "pink"];

    private static readonly Dictionary<string, Color> DefaultColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blue"] = Color.FromRgb(0x00, 0x7A, 0xCC),
        ["cyan"] = Color.FromRgb(0x00, 0xB4, 0xD8),
        ["teal"] = Color.FromRgb(0x14, 0xB8, 0xA6),
        ["green"] = Color.FromRgb(0x10, 0xB9, 0x81),
        ["purple"] = Color.FromRgb(0x8B, 0x5C, 0xF6),
        ["pink"] = Color.FromRgb(0xEC, 0x48, 0x99)
    };

    public static Color GetDefaultColor(string avatarId) =>
        DefaultColors.TryGetValue(avatarId, out var color)
            ? color
            : DefaultColors["blue"];

    public static ImageSource GetAvatarImage(UserProfile profile, int size = 128)
    {
        if (profile.UseCustomAvatar && !string.IsNullOrWhiteSpace(profile.CustomAvatarPath) &&
            File.Exists(profile.CustomAvatarPath))
        {
            return LoadImage(profile.CustomAvatarPath, size);
        }

        return CreateDefaultAvatar(profile.DefaultAvatarId, profile.DisplayName, size);
    }

    public static string SaveCustomAvatar(string sourcePath)
    {
        Directory.CreateDirectory(Config.UserProfileDirectoryPath);
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension) ||
            extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".png";
        }

        var destination = Path.Combine(Config.UserProfileDirectoryPath, "avatar" + extension.ToLowerInvariant());
        File.Copy(sourcePath, destination, overwrite: true);
        return destination;
    }

    private static ImageSource LoadImage(string path, int size)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.DecodePixelWidth = size;
        bitmap.DecodePixelHeight = size;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static ImageSource CreateDefaultAvatar(string avatarId, string displayName, int size)
    {
        var color = GetDefaultColor(avatarId);
        var initial = GetInitial(displayName);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawEllipse(new SolidColorBrush(color), null, new Point(size / 2.0, size / 2.0), size / 2.0, size / 2.0);

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var formattedText = new FormattedText(
                initial,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                size * 0.42,
                Brushes.White,
                VisualTreeHelper.GetDpi(visual).PixelsPerDip);
            context.DrawText(
                formattedText,
                new Point((size - formattedText.Width) / 2, (size - formattedText.Height) / 2));
        }

        var render = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        render.Freeze();
        return render;
    }

    private static string GetInitial(string displayName)
    {
        var trimmed = displayName.Trim();
        if (trimmed.Length == 0)
            return "?";

        return char.ToUpperInvariant(trimmed[0]).ToString();
    }
}
