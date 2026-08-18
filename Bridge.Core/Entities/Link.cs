namespace Bridge.Core.Entities;

/// <summary>Named URL attached to a game (store, wiki, social, etc.).</summary>
public class Link
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
