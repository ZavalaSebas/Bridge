using Bridge.Core.Entities;

namespace Bridge.Metadata;

/// <summary>Maps Steam global unlock percentages to rarity labels.</summary>
public static class SteamAchievementRarity
{
    public static AchievementRarity FromGlobalPercent(double? percent) =>
        percent switch
        {
            null => AchievementRarity.Unknown,
            >= 50 => AchievementRarity.Common,
            >= 20 => AchievementRarity.Uncommon,
            >= 5 => AchievementRarity.Rare,
            >= 1 => AchievementRarity.VeryRare,
            _ => AchievementRarity.Legendary,
        };
}
