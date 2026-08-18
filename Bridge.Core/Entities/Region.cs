namespace Bridge.Core.Entities;

/// <summary>Release region with an emulation-db specification id.</summary>
public class Region : DatabaseObject
{
    public string SpecificationId { get; set; } = string.Empty;
}
