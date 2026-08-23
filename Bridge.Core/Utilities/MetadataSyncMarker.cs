namespace Bridge.Core.Utilities;

/// <summary>Metadata synchronization aspect — used to seal markers independently.</summary>
public enum MetadataSyncMarker
{
    /// <summary>Text metadata (Description, images, references).</summary>
    Metadata,
    
    /// <summary>Social links (Steam, IGDB, Reddit, YouTube, etc.).</summary>
    Links,
    
    /// <summary>HowLongToBeat playtime data.</summary>
    TimeToBeat,
}
