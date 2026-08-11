namespace Bridge.Core.Entities;

/// <summary>What kind of content a description block holds — drives which
/// template renders it in the details view.</summary>
public enum DescriptionBlockKind
{
    Paragraph,
    Heading,
    Subheading,
    List
}

/// <summary>
/// One ordered chunk of a game's description: either a run of text or a single
/// image, in the order they appeared in the source HTML. Text blocks carry a
/// Kind (paragraph/heading/subheading/list) so the details view can reproduce
/// the source's formatting — titles, sizes, bullet lists — instead of a flat
/// plain-text dump. Stored as a JSON list on Game (like GameActions/Links).
/// </summary>
public class DescriptionBlock
{
    public DescriptionBlockKind Kind { get; set; } = DescriptionBlockKind.Paragraph;
    public bool IsImage { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
