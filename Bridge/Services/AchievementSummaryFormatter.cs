using Bridge.Core.Entities;
using Bridge.Resources;

namespace Bridge.Services;

public static class AchievementSummaryFormatter
{
    public static string FormatHeroText(GameAchievementsSnapshot snapshot) =>
        snapshot.TracksProgress
            ? snapshot.RemainingCount > 0
                ? Strings.Format(
                    nameof(Strings.AchievementsBarSummaryFormat),
                    snapshot.UnlockedCount,
                    snapshot.TotalCount,
                    snapshot.RemainingCount)
                : Strings.Format(
                    nameof(Strings.AchievementsBarCompleteFormat),
                    snapshot.UnlockedCount,
                    snapshot.TotalCount)
            : Strings.Format(nameof(Strings.AchievementsCatalogCountFormat), snapshot.TotalCount);

    public static string FormatHeroToolTip(GameAchievementsSnapshot snapshot) =>
        snapshot.TracksProgress
            ? $"{Strings.Format(nameof(Strings.AchievementsProgressFormat), snapshot.UnlockedCount, snapshot.TotalCount)} · {Strings.Format(nameof(Strings.AchievementsCompletionPercentFormat), snapshot.CompletionPercent)}"
            : Strings.AchievementsCatalogOnlyHint;
}
