using System.ComponentModel;

namespace Bridge.Core.Enums;

/// <summary>
/// Main content view modes, mirroring Playnite's view switcher. "List" is the
/// list + detail panel; "Grid" is a cover wall with hover actions (Play / Info)
/// over each cover; "Table" is a detailed list where every row shows the same
/// fields as the info window (no images).
/// </summary>
public enum ViewMode
{
    [Description("List")]
    List,

    [Description("Covers")]
    Grid,

    [Description("Details")]
    Table
}
