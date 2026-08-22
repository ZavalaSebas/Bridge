using Bridge.Core.Entities;
using Bridge.Services;

using Bridge.Metadata;

namespace Bridge.Tests.Services;

public class SteamAchievementRarityTests
{
    [Theory]
    [InlineData(85, AchievementRarity.Common)]
    [InlineData(50, AchievementRarity.Common)]
    [InlineData(49.9, AchievementRarity.Uncommon)]
    [InlineData(20, AchievementRarity.Uncommon)]
    [InlineData(19.9, AchievementRarity.Rare)]
    [InlineData(5, AchievementRarity.Rare)]
    [InlineData(4.9, AchievementRarity.VeryRare)]
    [InlineData(1, AchievementRarity.VeryRare)]
    [InlineData(0.9, AchievementRarity.Legendary)]
    public void FromGlobalPercent_UsesSteamLikeBuckets(double percent, AchievementRarity expected) =>
        Assert.Equal(expected, SteamAchievementRarity.FromGlobalPercent(percent));

    [Fact]
    public void FromGlobalPercent_ReturnsUnknownForNull() =>
        Assert.Equal(AchievementRarity.Unknown, SteamAchievementRarity.FromGlobalPercent(null));
}
