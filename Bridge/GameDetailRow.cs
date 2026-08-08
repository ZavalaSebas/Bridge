using Bridge.Core.Entities;

namespace Bridge;

/// <summary>
/// One row of the detailed list view: the Game plus the reference-field
/// display strings (Developers/Publishers/Platforms) already resolved to
/// names, so a data template can bind them without touching repositories.
/// </summary>
public class GameDetailRow
{
    public required Game Game { get; init; }
    public required string DevelopersText { get; init; }
    public required string PublishersText { get; init; }
    public required string PlatformsText { get; init; }
    public required string GenresText { get; init; }
    public required string LibraryText { get; init; }
}
