using System.ComponentModel;

namespace Bridge.Core.Enums;

/// <summary>Library layout: list with detail panel, cover grid, or field table.</summary>
public enum ViewMode
{
    [Description("List")]
    List,

    [Description("Covers")]
    Covers,

    [Description("Table")]
    Table
}
