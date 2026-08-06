namespace Bridge.Core.Entities;

/// <summary>Matches Playnite's Region — adds a SpecificationId used to match against emulation-DB region codes (PROJECT_FOUNDATION.md §28.1, §28.4).</summary>
public class Region : DatabaseObject
{
    public string SpecificationId { get; set; } = string.Empty;
}
