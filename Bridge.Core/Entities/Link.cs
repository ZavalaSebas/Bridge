namespace Bridge.Core.Entities;

/// <summary>Matches Playnite's Link (PROJECT_FOUNDATION.md §28.1 — the class is named Link, not "LinkItem").</summary>
public class Link
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
