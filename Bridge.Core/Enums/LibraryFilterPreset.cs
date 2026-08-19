namespace Bridge.Core.Enums;

/// <summary>
/// Library filter presets — pure predicates that decide WHICH games show,
/// combinable with the name search box. Ordering is a separate concern
/// (SortField/SortDescending); a preset never touches the sort.
/// </summary>
public enum LibraryFilterPreset
{
    All,
    Favorite,
    Roms,
    Installed,
    NotPlayed,
    RecentlyPlayed
}
