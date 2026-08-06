using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bridge.Core.Import;
using ReleaseDate = Bridge.Core.Entities.ReleaseDate;

namespace Bridge.Metadata;

/// <summary>
/// Bridge's one and only text-metadata source, per the user's explicit choice
/// (2026-08-05): IGDB, because it's the de facto standard metadata addon in
/// Playnite's real ecosystem (not bundled in Playnite's own core repo — see
/// PROJECT_FOUNDATION.md §28.20 — but the closest thing to "what Playnite
/// actually uses" in practice). Matches ADR-8's reasoning shape even though
/// this supersedes the SteamGridDB-images-only choice that ADR briefly
/// explored and the user rejected — see ARCHITECTURE.md for the current record.
///
/// MVP scope: search by exact name, take the first IGDB result, map Name/
/// Description/ReleaseDate/CoverImage/Genres only. No developer/publisher
/// mapping yet (IGDB's involved_companies needs role-filtering — see
/// PROJECT_FOUNDATION.md §28.3 for the full field list Playnite's real
/// MetadataDownloader resolves, most of which isn't wired up here yet). No
/// SkipExistingValues semantics yet — every call overwrites the target fields
/// unconditionally; the caller decides whether to call this at all.
/// </summary>
public class IgdbMetadataProvider(HttpClient httpClient, IgdbSettings settings, IgdbAuthClient authClient)
{
    private const string GamesEndpoint = "https://api.igdb.com/v4/games";

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
