using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Import;
using ReleaseDate = Bridge.Core.Entities.ReleaseDate;

namespace Bridge.Metadata;

/// <summary>
/// Metadata provider backed by Bridge's own Cloudflare Worker
/// (https://bridge-igdb.sebaszavala120.workers.dev/metadata). The Worker holds
/// the IGDB/Twitch credentials as Worker Secrets server-side, so Bridge gets
/// IGDB metadata with zero user configuration — the same architecture Playnite
/// uses (its own api2.playnite.link backend), but with our own infra instead of
/// depending on Playnite's. The Worker returns the raw IGDB shape (cover.url,
/// artworks[].url, websites[].type, ...), mapped here to GameMetadata.
/// </summary>
public sealed class BridgeIgdbProvider(HttpClient httpClient) : IGameMetadataProvider
{
    private const string MetadataEndpoint = MetadataEndpoints.BridgeIgdbWorker;

    public string Name => "IGDB";

    public async Task<GameMetadata?> SearchAsync(string gameName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                MetadataEndpoint,
                new WorkerMetadataRequest(gameName.Trim()),
                cts.Token);

            if (!response.IsSuccessStatusCode)
                return null;

            var game = await response.Content.ReadFromJsonAsync<WorkerGame>(cancellationToken: cts.Token);
            return game is null ? null : Map(game);
        }
        catch (OperationCanceledException)
        {
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

    private static GameMetadata Map(WorkerGame game)
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

        if (game.Cover?.Url is { } coverUrl)
        {
            metadata.CoverImage = IgdbMetadataProvider.UpgradeImageUrl(coverUrl, "t_cover_big");
            metadata.Icon = IgdbMetadataProvider.UpgradeImageUrl(coverUrl, "t_thumb");
        }

        // Background: wide IGDB artwork, falling back to a screenshot-like
        // artwork if none.
        var artworkUrl = game.Artworks?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Url))?.Url;
        if (!string.IsNullOrWhiteSpace(artworkUrl))
        {
            metadata.BackgroundImage = IgdbMetadataProvider.UpgradeImageUrl(artworkUrl, "t_1080p");
        }

        // Galería de screenshots: IGDB guarda las capturas reales de cada juego
        // (16:9), igual que Steam. Igual que con Steam, se muestran como galería
        // en el detalle — solo los juegos con al menos 2 screenshots muestran la
        // galería (ScreenshotGallery se auto-colapsa si no hay).
        if (game.Screenshots is { Count: > 0 } screenshots)
        {
            metadata.Screenshots = screenshots
                .Where(s => !string.IsNullOrWhiteSpace(s.Url))
                .Select(s => IgdbMetadataProvider.UpgradeImageUrl(s.Url!, "t_1080p"))
                .ToList();
        }

        if (game.InvolvedCompanies is { Count: > 0 } companies)
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

        if (game.Genres is { Count: > 0 } genres)
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

        if (game.Websites is { Count: > 0 } websites)
        {
            foreach (var website in websites)
            {
                if (string.IsNullOrWhiteSpace(website.Url))
                    continue;
                metadata.Links.Add(new Link
                {
                    Name = IgdbMetadataProvider.WebsiteCategoryName(website.Type),
                    Url = website.Url.StartsWith("//") ? "https:" + website.Url : website.Url
                });
            }
        }

        return metadata;
    }
}

internal sealed class WorkerMetadataRequest
{
    public WorkerMetadataRequest(string name)
    {
        Name = name;
    }

    [JsonPropertyName("name")]
    public string Name { get; }
}

internal sealed class WorkerGame
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("first_release_date")]
    public long? FirstReleaseDate { get; set; }

    [JsonPropertyName("cover")]
    public WorkerCover? Cover { get; set; }

    [JsonPropertyName("artworks")]
    public List<WorkerArtwork>? Artworks { get; set; }

    [JsonPropertyName("screenshots")]
    public List<WorkerArtwork>? Screenshots { get; set; }

    [JsonPropertyName("involved_companies")]
    public List<WorkerCompany>? InvolvedCompanies { get; set; }

    [JsonPropertyName("genres")]
    public List<WorkerGenre>? Genres { get; set; }

    [JsonPropertyName("rating")]
    public double? Rating { get; set; }

    [JsonPropertyName("aggregated_rating")]
    public double? AggregatedRating { get; set; }

    [JsonPropertyName("websites")]
    public List<WorkerWebsite>? Websites { get; set; }
}

internal sealed class WorkerCover
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class WorkerArtwork
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class WorkerCompany
{
    [JsonPropertyName("company")]
    public WorkerCompanyName? Company { get; set; }

    [JsonPropertyName("developer")]
    public bool Developer { get; set; }

    [JsonPropertyName("publisher")]
    public bool Publisher { get; set; }
}

internal sealed class WorkerCompanyName
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class WorkerGenre
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class WorkerWebsite
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }
}
