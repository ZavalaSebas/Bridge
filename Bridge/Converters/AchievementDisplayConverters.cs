using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Bridge.Core.Entities;
using Bridge.Resources;

namespace Bridge.Converters;

public sealed class AchievementNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GameAchievement achievement && achievement.IsHidden && !achievement.IsUnlocked
            ? Strings.HiddenAchievement
            : value is GameAchievement unlocked ? unlocked.Name : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AchievementDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GameAchievement achievement && achievement.IsHidden && !achievement.IsUnlocked
            ? string.Empty
            : value is GameAchievement item ? item.Description : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AchievementIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not GameAchievement achievement)
            return string.Empty;

        return achievement.IsUnlocked
            ? achievement.IconUrl ?? achievement.IconLockedUrl ?? string.Empty
            : achievement.IconLockedUrl ?? achievement.IconUrl ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AchievementUnlockedAtConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GameAchievement { IsUnlocked: true, UnlockedAt: { } unlockedAt }
            ? unlockedAt.ToLocalTime().ToString("g", culture)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AchievementLockedOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GameAchievement { IsUnlocked: true } ? 1.0 : 0.45;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AchievementRarityLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GameAchievement achievement ? GetLabel(achievement.Rarity) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static string GetLabel(AchievementRarity rarity) =>
        rarity switch
        {
            AchievementRarity.Common => Strings.AchievementRarityCommon,
            AchievementRarity.Uncommon => Strings.AchievementRarityUncommon,
            AchievementRarity.Rare => Strings.AchievementRarityRare,
            AchievementRarity.VeryRare => Strings.AchievementRarityVeryRare,
            AchievementRarity.Legendary => Strings.AchievementRarityLegendary,
            _ => string.Empty,
        };
}

public sealed class AchievementRarityBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not GameAchievement achievement)
            return Brushes.Transparent;

        return achievement.Rarity switch
        {
            AchievementRarity.Common => new SolidColorBrush(Color.FromRgb(0x7A, 0x82, 0x92)),
            AchievementRarity.Uncommon => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0xDD)),
            AchievementRarity.Rare => new SolidColorBrush(Color.FromRgb(0x8B, 0x6F, 0xFF)),
            AchievementRarity.VeryRare => new SolidColorBrush(Color.FromRgb(0xE0, 0x56, 0xFD)),
            AchievementRarity.Legendary => new SolidColorBrush(Color.FromRgb(0xF5, 0xA5, 0x23)),
            _ => Brushes.Transparent,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AchievementGlobalPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GameAchievement { GlobalUnlockPercent: { } percent }
            ? Strings.Format(nameof(Strings.AchievementGlobalPercentFormat), percent)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AchievementRarityVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GameAchievement { Rarity: not AchievementRarity.Unknown }
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AchievementUnlockedAccentVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GameAchievement { IsUnlocked: true } ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
