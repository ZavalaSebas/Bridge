namespace Bridge.Core.Entities;

/// <summary>Platform with optional fallback artwork and an emulation-db specification id.</summary>
public class Platform : DatabaseObject
{
    public string SpecificationId { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
}
