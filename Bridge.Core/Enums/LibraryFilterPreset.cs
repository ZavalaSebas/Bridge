namespace Bridge.Core.Enums;

/// <summary>
/// Library filter presets (All / Favorite / Most Played / Recently Played),
/// combinable with the name search box. Sort-based presets drive
/// SortDescriptions on the list view; Favorite adds a filter predicate.
/// </summary>
public enum LibraryFilterPreset
{
    All,
    Favorite,
    MostPlayed,
    RecentlyPlayed
}
