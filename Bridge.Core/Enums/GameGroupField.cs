using System.ComponentModel;

namespace Bridge.Core.Enums;

/// <summary>
/// Grouping modes for the library list, mirroring Playnite's group options
/// (PROJECT_FOUNDATION.md §12) for the fields Bridge can actually resolve.
/// "None" disables grouping. Fields needing repositories that don't exist yet
/// (AgeRating, Category, Feature, Series, Region, Tag) are left out for now —
/// see GameSortField for the same constraint. "Library" groups by the game's
/// source (SourceId resolved to its display name).
/// </summary>
public enum GameGroupField
{
    [Description("Don't group")]
    None,

    [Description("Name")]
    Name,

    [Description("Library")]
    Library,

    [Description("Developer")]
    Developer,

    [Description("Publisher")]
    Publisher,

    [Description("Platform")]
    Platform,

    [Description("Genre")]
    Genre,

    [Description("Installation Status")]
    IsInstalled,

    [Description("Completion Status")]
    CompletionStatus,

    [Description("Time Played")]
    PlaytimeSeconds,

    [Description("Play Count")]
    PlayCount,

    [Description("Install Size")]
    InstallSizeBytes,

    [Description("Install Drive")]
    InstallDrive,

    [Description("Last Played")]
    LastPlayed,

    [Description("Recent Activity")]
    RecentActivity,

    [Description("Release Year")]
    ReleaseYear,

    [Description("Date Added")]
    Added,

    [Description("Date Modified")]
    Modified,

    [Description("Community Score")]
    CommunityScore,

    [Description("Critic Score")]
    CriticScore,

    [Description("User Score")]
    UserScore
}
