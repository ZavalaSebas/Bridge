using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Import;
using Bridge.Core.Utilities;
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
                $"""search "{EscapeApicalypseString(gameName)}"; fields name,summary,first_release_date,cover.url,genres.name,websites.url,websites.category; limit 1;""",
                Encoding.UTF8,
                "text/plain")
        };
        request.Headers.Add("Client-ID", settings.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // IGDB rejects requests without a valid User-Agent (403).
        request.Headers.UserAgent.ParseAdd("Bridge/0.1");

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
            try
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                metadata.ReleaseDate = new ReleaseDate(date.Year, date.Month, date.Day);
            }
            catch (ArgumentOutOfRangeException)
            {
                // A corrupt/out-of-range timestamp shouldn't discard the whole
                // IGDB result — just leave the release date unset.
            }
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

        // IGDB website categories → link labels (YouTube, Reddit, Twitter, …).
        if (game.Websites is { Count: > 0 })
        {
            foreach (var website in game.Websites)
            {
                if (string.IsNullOrWhiteSpace(website.Url))
                    continue;

                var sanitized = SanitizeWebsiteUrl(website.Url.StartsWith("//") ? "https:" + website.Url : website.Url);
                if (string.IsNullOrWhiteSpace(sanitized))
                    continue;

                metadata.Links.Add(new Link
                {
                    Name = WebsiteCategoryName(website.Category),
                    Url = sanitized
                });
            }
        }

        return metadata;
    }

    // IGDB website_category enum → readable label. Unknown ids → "Website".
    internal static string WebsiteCategoryName(int category) => category switch
    {
        1 => "Official",
        2 => "Wikia",
        3 => "Wikipedia",
        4 => "Facebook",
        5 => "Twitter",
        6 => "Twitch",
        8 => "Instagram",
        9 => "YouTube",
        10 => "iPhone",
        11 => "iPad",
        12 => "Android",
        13 => "Steam",
        14 => "Reddit",
        15 => "Itch.io",
        16 => "Epic Games",
        17 => "GOG",
        18 => "Discord",
        20 => "Google Play",
        21 => "App Store",
        22 => "Direct Download",
        23 => "Google Plus",
        _ => "Website"
    };

    // IGDB returns protocol-relative thumbnail URLs like
    // "//images.igdb.com/igdb/image/upload/t_thumb/abc123.jpg" — needs
    // "https:" prefixed and the size token swapped for a bigger one to be
    // useful as cover art. Real IGDB size tokens, not invented ones:
    // t_thumb, t_cover_small, t_cover_big, t_screenshot_big, t_1080p, etc.
    public static string UpgradeImageUrl(string url, string size = "t_cover_big")
    {
        var withScheme = url.StartsWith("//") ? "https:" + url : url;
        // Real IGDB URLs contain exactly one t_thumb token, so Replace-all is
        // equivalent to replace-first here; kept simple.
        return withScheme.Replace("t_thumb", size);
    }

    // Apicalypse search strings reject control characters and unbalanced quotes.
    private static string EscapeApicalypseString(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 200)
            trimmed = trimmed[..200];

        return trimmed
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string SanitizeWebsiteUrl(string url) =>
        UrlValidator.SanitizePersistedUrl(url) ?? string.Empty;
}
