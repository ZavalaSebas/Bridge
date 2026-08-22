namespace Bridge.Core.Entities;

/// <summary>Steam-style rarity bucket derived from global unlock percentage.</summary>
public enum AchievementRarity
{
    Unknown = 0,
    Common,
    Uncommon,
    Rare,
    VeryRare,
    Legendary,
}

/// <summary>One achievement row for the library detail panel.</summary>
public sealed class GameAchievement
{
    public required string ApiName { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public bool IsHidden { get; init; }
    public bool IsUnlocked { get; init; }
    public DateTime? UnlockedAt { get; init; }
    public string? IconUrl { get; init; }
    public string? IconLockedUrl { get; init; }
    /// <summary>Share of players who unlocked this achievement (0–100).</summary>
    public double? GlobalUnlockPercent { get; init; }
    public AchievementRarity Rarity { get; init; }
}

/// <summary>Achievement progress snapshot for one game (Steam or Epic).</summary>
public sealed class GameAchievementsSnapshot
{
    public required IReadOnlyList<GameAchievement> Achievements { get; init; }

    public int UnlockedCount { get; init; }

    /// <summary>When false, only achievement definitions are shown (no personal progress).</summary>
    public bool TracksProgress { get; init; } = true;

    public int TotalCount => Achievements.Count;

    public int RemainingCount => TracksProgress ? Math.Max(0, TotalCount - UnlockedCount) : TotalCount;

    public double CompletionPercent =>
        TracksProgress && TotalCount > 0 ? UnlockedCount * 100.0 / TotalCount : 0;
}
