namespace Bridge.Converters;

/// <summary>
/// Target decode width (long edge, in pixels) for cached artwork. The decoder
/// clamps to the source width so a bucket never upscales a smaller original, and
/// aspect is preserved because only the width is set. <see cref="Native"/> keeps
/// the full source resolution.
/// </summary>
public enum ArtworkDecodeSize
{
    Native = 0,
    Icon = 64,
    Thumb = 320,
    Cover = 512,
    Large = 1024,
    Hero = 1600,
}
