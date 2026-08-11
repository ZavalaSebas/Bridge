namespace Bridge.Core.Entities;

/// <summary>
/// One ordered chunk of a game's description: either a run of text or a single
/// image, in the order they appeared in the source HTML. Stored as a JSON list
/// on Game (like GameActions/Links) so the details view can render text and
/// screenshots interleaved, exactly where the source had them — instead of a
/// plain-text description plus an image strip appended at the end.
/// </summary>
public class DescriptionBlock
{
    public bool IsImage { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
