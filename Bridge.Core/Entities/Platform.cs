namespace Bridge.Core.Entities;

/// <summary>Matches Playnite's Platform — SpecificationId matches emulation-DB entries; Icon/Cover/Background are per-platform fallback art (PROJECT_FOUNDATION.md §28.1).</summary>
public class Platform : DatabaseObject
{
    public string SpecificationId { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
}
