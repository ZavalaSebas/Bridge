using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Import;
using ReleaseDate = Bridge.Core.Entities.ReleaseDate;

namespace Bridge.Metadata;

/// <summary>
/// Zero-config IGDB metadata via a public hosted proxy. Users can still use
/// IgdbMetadataProvider with their own Twitch/IGDB credentials.
/// </summary>
public sealed class PlayniteIgdbProvider(HttpClient httpClient) : IGameMetadataProvider
{
    private const string BackendBase = "https://api2.playnite.link/api/igdb/";

    public string Name => "IGDB (Playnite)";

    public async Task<GameMetadata?> SearchAsync(string gameName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                BackendBase + "metadata",
                new PlayniteMetadataRequest(gameName.Trim()),
                cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var envelope = await response.Content.ReadFromJsonAsync<PlayniteEnvelope<PlayniteGame>>(cancellationToken: cts.Token).ConfigureAwait(false);
            return envelope?.Data is { } game ? Map(game) : null;
        }
        catch (OperationCanceledException)
        {
            // Timeout — treat as "no result" so the chain moves on instead of
            // blocking the UI on a slow/unreachable proxy.
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static GameMetadata Map(PlayniteGame game)
    {
        var metadata = new GameMetadata
        {
            Name = game.Name ?? string.Empty,
            Description = game.Summary ?? string.Empty
        };

        if (game.FirstReleaseDate is { } unixSeconds)
        {
            try
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                metadata.ReleaseDate = new ReleaseDate(date.Year, date.Month, date.Day);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Corrupt timestamp — leave the date unset.
            }
        }

        var coverUrl = game.CoverExpanded?.Url;
        if (!string.IsNullOrWhiteSpace(coverUrl))
        {
            metadata.CoverImage = IgdbMetadataProvider.UpgradeImageUrl(coverUrl, "t_cover_big");
            metadata.Icon = IgdbMetadataProvider.UpgradeImageUrl(coverUrl, "t_thumb");
        }

        // Wide IGDB artwork for the details background; fall back to a screenshot.
        var backgroundUrl = game.ArtworksExpanded?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Url))?.Url;
        if (string.IsNullOrWhiteSpace(backgroundUrl))
        {
            backgroundUrl = game.ScreenshotsExpanded?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Url))?.Url;
        }

        if (!string.IsNullOrWhiteSpace(backgroundUrl))
        {
            metadata.BackgroundImage = IgdbMetadataProvider.UpgradeImageUrl(backgroundUrl, "t_1080p");
        }

        if (game.InvolvedCompaniesExpanded is { Count: > 0 } companies)
        {
            metadata.Developers = companies
                .Where(c => c.Developer)
                .Select(c => c.Company?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList()!;
            metadata.Publishers = companies
                .Where(c => c.Publisher)
                .Select(c => c.Company?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList()!;
        }

        if (game.GenresExpanded is { Count: > 0 } genres)
        {
            metadata.Genres = genres
                .Select(g => g.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList()!;
        }

        if (game.AggregatedRating is { } critic)
            metadata.CriticScore = (int)Math.Round(critic);
        if (game.Rating is { } community)
            metadata.CommunityScore = (int)Math.Round(community);

        if (game.WebsitesExpanded is { Count: > 0 } websites)
        {
            foreach (var website in websites)
            {
                if (string.IsNullOrWhiteSpace(website.Url))
                    continue;
                metadata.Links.Add(new Link
                {
                    Name = website.TypeExpanded?.Type ?? IgdbMetadataProvider.WebsiteCategoryName(website.Category),
                    Url = website.Url.StartsWith("//") ? "https:" + website.Url : website.Url
                });
            }
        }

        return metadata;
    }
}

internal sealed class PlayniteMetadataRequest
{
    public PlayniteMetadataRequest(string name)
    {
        Name = name;
    }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("releaseYear")]
    public int ReleaseYear { get; set; }

    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = string.Empty;
}

internal sealed class PlayniteEnvelope<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

internal sealed class PlayniteGame
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("first_release_date")]
    public long? FirstReleaseDate { get; set; }

    [JsonPropertyName("aggregated_rating")]
    public double? AggregatedRating { get; set; }

    [JsonPropertyName("rating")]
    public double? Rating { get; set; }

    [JsonPropertyName("cover_expanded")]
    public PlayniteCover? CoverExpanded { get; set; }

    [JsonPropertyName("artworks_expanded")]
    public List<PlayniteArtwork>? ArtworksExpanded { get; set; }

    [JsonPropertyName("screenshots_expanded")]
    public List<PlayniteArtwork>? ScreenshotsExpanded { get; set; }

    [JsonPropertyName("involved_companies_expanded")]
    public List<PlayniteCompany>? InvolvedCompaniesExpanded { get; set; }

    [JsonPropertyName("genres_expanded")]
    public List<PlayniteGenre>? GenresExpanded { get; set; }

    [JsonPropertyName("websites_expanded")]
    public List<PlayniteWebsite>? WebsitesExpanded { get; set; }
}

internal sealed class PlayniteCover
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class PlayniteArtwork
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class PlayniteCompany
{
    [JsonPropertyName("developer")]
    public bool Developer { get; set; }

    [JsonPropertyName("publisher")]
    public bool Publisher { get; set; }

    [JsonPropertyName("company_expanded")]
    public PlayniteCompanyName? Company { get; set; }
}

internal sealed class PlayniteCompanyName
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class PlayniteGenre
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class PlayniteWebsite
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("category")]
    public int Category { get; set; }

    [JsonPropertyName("type_expanded")]
    public PlayniteWebsiteType? TypeExpanded { get; set; }
}

internal sealed class PlayniteWebsiteType
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
