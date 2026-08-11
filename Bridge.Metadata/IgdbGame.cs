using System.Text.Json.Serialization;

namespace Bridge.Metadata;

/// <summary>Raw shape of one IGDB /v4/games result — only the fields this MVP requests and maps. IGDB's real schema has far more (see https://api-docs.igdb.com/#game).</summary>
public class IgdbGame
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("first_release_date")]
    public long? FirstReleaseDate { get; set; }

    [JsonPropertyName("cover")]
    public IgdbCover? Cover { get; set; }

    [JsonPropertyName("genres")]
    public List<IgdbGenre>? Genres { get; set; }

    [JsonPropertyName("websites")]
    public List<IgdbWebsite>? Websites { get; set; }
}

public class IgdbCover
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class IgdbGenre
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class IgdbWebsite
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("category")]
    public int Category { get; set; }
}
