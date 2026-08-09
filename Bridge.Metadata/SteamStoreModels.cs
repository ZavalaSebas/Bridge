using System.Text.Json.Serialization;

namespace Bridge.Metadata;

internal sealed class SteamAppDetailsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public SteamAppDetailsData? Data { get; set; }
}

internal sealed class SteamAppDetailsData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("about_the_game")]
    public string AboutTheGame { get; set; } = string.Empty;

    [JsonPropertyName("short_description")]
    public string ShortDescription { get; set; } = string.Empty;

    [JsonPropertyName("header_image")]
    public string HeaderImage { get; set; } = string.Empty;

    [JsonPropertyName("background")]
    public string? Background { get; set; }

    [JsonPropertyName("requirements")]
    public SteamRequirements? Requirements { get; set; }

    [JsonPropertyName("developers")]
    public List<string> Developers { get; set; } = [];

    [JsonPropertyName("publishers")]
    public List<string> Publishers { get; set; } = [];

    [JsonPropertyName("genres")]
    public List<SteamGenre> Genres { get; set; } = [];

    [JsonPropertyName("categories")]
    public List<SteamCategory> Categories { get; set; } = [];

    [JsonPropertyName("screenshots")]
    public List<SteamScreenshot> Screenshots { get; set; } = [];

    [JsonPropertyName("metacritic")]
    public SteamMetacritic? Metacritic { get; set; }

    [JsonPropertyName("release_date")]
    public SteamReleaseDate? ReleaseDate { get; set; }

    [JsonPropertyName("platforms")]
    public SteamPlatforms Platforms { get; set; } = new();
}

internal sealed class SteamRequirements
{
    [JsonPropertyName("minimum")]
    public string? Minimum { get; set; }

    [JsonPropertyName("recommended")]
    public string? Recommended { get; set; }
}

internal sealed class SteamGenre
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

internal sealed class SteamCategory
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

internal sealed class SteamScreenshot
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("path_thumbnail")]
    public string PathThumbnail { get; set; } = string.Empty;

    [JsonPropertyName("path_full")]
    public string PathFull { get; set; } = string.Empty;
}

internal sealed class SteamMetacritic
{
    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

internal sealed class SteamReleaseDate
{
    [JsonPropertyName("coming_soon")]
    public bool ComingSoon { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
}

internal sealed class SteamPlatforms
{
    [JsonPropertyName("windows")]
    public bool Windows { get; set; }

    [JsonPropertyName("mac")]
    public bool Mac { get; set; }

    [JsonPropertyName("linux")]
    public bool Linux { get; set; }
}

internal sealed class SteamAppReviewsResponse
{
    [JsonPropertyName("success")]
    public int Success { get; set; }

    [JsonPropertyName("query_summary")]
    public SteamQuerySummary? QuerySummary { get; set; }
}

internal sealed class SteamQuerySummary
{
    [JsonPropertyName("num_reviews")]
    public int NumReviews { get; set; }

    [JsonPropertyName("review_score")]
    public int ReviewScore { get; set; }

    [JsonPropertyName("review_score_desc")]
    public string ReviewScoreDesc { get; set; } = string.Empty;

    [JsonPropertyName("total_positive")]
    public int TotalPositive { get; set; }

    [JsonPropertyName("total_negative")]
    public int TotalNegative { get; set; }

    [JsonPropertyName("total_reviews")]
    public int TotalReviews { get; set; }
}
