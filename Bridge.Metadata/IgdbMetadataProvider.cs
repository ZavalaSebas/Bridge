using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bridge.Core.Contracts;
using Bridge.Core.Import;
using ReleaseDate = Bridge.Core.Entities.ReleaseDate;

namespace Bridge.Metadata;

public class IgdbMetadataProvider(HttpClient httpClient, IgdbSettings settings, IgdbAuthClient authClient) : IGameMetadataProvider
{
    private const string GamesEndpoint = "https://api.igdb.com/v4/games";

    public string Name => "IGDB";

    public async Task<GameMetadata?> SearchAsync(string gameName, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException("IGDB Client ID/Secret are not configured — set them up before downloading metadata.");
        }

        var token = await authClient.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, GamesEndpoint)
        {
            Content = new StringContent(
                $"""search "{EscapeApicalypseString(gameName)}"; fields name,summary,first_release_date,cover.url,genres.name; limit 1;""",
                Encoding.UTF8,
                "text/plain")
        };
        request.Headers.Add("Client-ID", settings.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<IgdbGame>>(cancellationToken: cancellationToken);
        var match = results?.FirstOrDefault();
        return match is null ? null : MapToGameMetadata(match);
    }

    public static GameMetadata MapToGameMetadata(IgdbGame game)
    {
        var metadata = new GameMetadata
        {
            Name = game.Name ?? string.Empty,
            Description = game.Summary ?? string.Empty
        };

        if (game.FirstReleaseDate is { } unixSeconds)
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            metadata.ReleaseDate = new ReleaseDate(date.Year, date.Month, date.Day);
        }

        if (game.Cover?.Url is { } coverUrl)
        {
            metadata.CoverImage = UpgradeImageUrl(coverUrl);
        }

        if (game.Genres is { } genres)
        {
            metadata.Genres = genres
                .Select(g => g.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
        }

        return metadata;
    }

    // IGDB returns protocol-relative thumbnail URLs like
    // "//images.igdb.com/igdb/image/upload/t_thumb/abc123.jpg" — needs
    // "https:" prefixed and the size token swapped for a bigger one to be
    // useful as cover art. Real IGDB size tokens, not invented ones:
    // t_thumb, t_cover_small, t_cover_big, t_screenshot_big, t_1080p, etc.
    public static string UpgradeImageUrl(string url, string size = "t_cover_big")
    {
        var withScheme = url.StartsWith("//") ? "https:" + url : url;
        return withScheme.Replace("t_thumb", size);
    }

    private static string EscapeApicalypseString(string value) => value.Replace("\"", "\\\"");
}
