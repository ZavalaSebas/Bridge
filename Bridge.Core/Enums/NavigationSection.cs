using System.ComponentModel;

namespace Bridge.Core.Enums;

/// <summary>
/// Sidebar navigation sections. Library / ROMs / Favorites / Sources are UI shortcuts
/// over state that already exists in the main view model (they set the existing
/// FilterPreset / GroupField values); Statistics switches the content area;
/// Settings shows the in-app settings hub (shortcuts to existing windows). This enum is a UI
/// concept, so it lives here (like <see cref="ViewMode"/>) purely so XAML can
/// bind to it without referencing the app assembly.
/// </summary>
public enum NavigationSection
{
    [Description("Home")]
    Home,

    [Description("Library")]
    Library,

    [Description("ROMs")]
    Roms,

    [Description("Favorites")]
    Favorites,

    [Description("Sources")]
    Sources,

    [Description("Statistics")]
    Statistics,

    [Description("Settings")]
    Settings
}
