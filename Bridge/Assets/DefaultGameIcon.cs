using System.Windows.Media;

namespace Bridge.Assets;

/// <summary>Fallback artwork for games without a custom icon.</summary>
public static class DefaultGameIcon
{
    public static ImageSource Source => DefaultGameArtwork.Icon;
}
