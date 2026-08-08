using System.ComponentModel;

namespace Bridge.Core.Enums;

/// <summary>
/// Sortable fields for the library list. Mirrors Playnite's sort options
/// (PROJECT_FOUNDATION.md §12) for the fields Bridge can actually resolve:
/// direct Game properties plus reference entities Bridge has repositories for
/// (Genre/Platform/Company/GameSource). Fields needing repositories that don't
/// exist yet (AgeRating, Category, Feature, Series, Region, Tag) are left out
/// for now.
/// </summary>
public enum GameSortField
{
    [Description("Name")]
    Name,

    [Description("Time Played")]
    PlaytimeSeconds,

    [Description("Play Count")]
    PlayCount,

    [Description("Last Played")]
    LastPlayed,

    [Description("Recent Activity")]
    RecentActivity,

    [Description("Favorite")]
    Favorite,

    [Description("Hidden")]
    Hidden,

    [Description("Install Size")]
    InstallSizeBytes,

    [Description("Installation Folder")]
    InstallDirectory,

    [Description("Installation Status")]
    IsInstalled,

    [Description("Release Date")]
    ReleaseDate,

    [Description("Date Added")]
    Added,

    [Description("Date Modified")]
    Modified,

    [Description("Version")]
    Version,

    [Description("Community Score")]
    CommunityScore,

    [Description("Critic Score")]
    CriticScore,

    [Description("User Score")]
    UserScore,

    [Description("Developer")]
    Developer,

    [Description("Publisher")]
    Publisher,

    [Description("Platform")]
    Platform,

    [Description("Genre")]
    Genre,

    [Description("Library")]
    Source
}
