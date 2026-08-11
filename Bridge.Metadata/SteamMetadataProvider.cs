using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Import;

namespace Bridge.Metadata;

public partial class SteamMetadataProvider(HttpClient httpClient) : IGameMetadataProvider
{
    private const string AppDetailsUrl = "https://store.steampowered.com/api/appdetails?appids={0}&l=en";
    private const string AppReviewsUrl = "https://store.steampowered.com/appreviews/{0}?json=1&purchase_type=all";
    private const string SearchUrl = "https://store.steampowered.com/search/?term={0}&ignore_preferences=1&category1=998&ndl=1";
    private const string CoverVerticalUrl = "https://steamcdn-a.akamaihd.net/steam/apps/{0}/library_600x900_2x.jpg";

    // El "hero" de Steam (1920x620) es el fondo estándar de la librería — usarlo
    // siempre mantiene la proporción consistente (3.1:1) entre juegos, en vez de
    // mezclar heroes con screenshots (1920x1080) que alteran el alto del hero.
    private const string HeroUrl = "https://steamcdn-a.akamaihd.net/steam/apps/{0}/library_hero.jpg";

    public string Name => "Steam Store";

    public async Task<GameMetadata?> SearchAsync(string gameName, CancellationToken cancellationToken = default)
    {
        var appId = await SearchAppIdAsync(gameName, cancellationToken);
        if (appId is null)
            return null;

        return await GetByAppIdAsync(appId.Value.ToString(), cancellationToken);
    }

    public async Task<GameMetadata?> GetByAppIdAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (!uint.TryParse(appId, out var steamAppId))
            return null;

        var appDetails = await GetAppDetailsAsync(steamAppId, cancellationToken);
        if (appDetails is null)
            return null;

        int? communityScore = null;
        try
        {
            var reviews = await GetAppReviewsAsync(steamAppId, cancellationToken);
            if (reviews is { TotalReviews: > 0 } &&
                reviews.TotalPositive + reviews.TotalNegative > 0)
            {
                communityScore = CalculateCommunityScore(reviews.TotalPositive, reviews.TotalNegative);
            }
        }
        catch
        {
            // reviews are optional, not a hard failure
        }

        return MapToGameMetadata(appDetails, steamAppId, communityScore);
    }

    private async Task<uint?> SearchAppIdAsync(string gameName, CancellationToken cancellationToken)
    {
        try
        {
            var url = string.Format(SearchUrl, Uri.EscapeDataString(gameName));
            var html = await httpClient.GetStringAsync(url, cancellationToken);

            var matches = SearchEntryRegex().Matches(html);
            foreach (Match match in matches)
            {
                if (match.Groups[1].Value.Contains("data-ds-packageid"))
                    continue;

                var appId = match.Groups[2].Value;
                if (uint.TryParse(appId, out var id))
                    return id;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<SteamAppDetailsData?> GetAppDetailsAsync(uint appId, CancellationToken cancellationToken)
    {
        try
        {
            var url = string.Format(AppDetailsUrl, appId);
            var response = await httpClient.GetFromJsonAsync<Dictionary<string, SteamAppDetailsResponse>>(url, cancellationToken: cancellationToken);

            if (response is null || !response.TryGetValue(appId.ToString(), out var entry))
                return null;

            return entry is { Success: true, Data: not null } ? entry.Data : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<SteamQuerySummary?> GetAppReviewsAsync(uint appId, CancellationToken cancellationToken)
    {
        try
        {
            var url = string.Format(AppReviewsUrl, appId);
            var response = await httpClient.GetFromJsonAsync<SteamAppReviewsResponse>(url, cancellationToken: cancellationToken);
            return response is { Success: 1 } ? response.QuerySummary : null;
        }
        catch
        {
            return null;
        }
    }

    private static GameMetadata MapToGameMetadata(SteamAppDetailsData data, uint appId, int? communityScore)
    {
        var metadata = new GameMetadata
        {
            Name = data.Name,
            Description = StripHtml(data.AboutTheGame),
            DescriptionImages = ExtractImageUrls(data.AboutTheGame),
            DescriptionBlocks = ParseDescriptionBlocks(data.AboutTheGame),
            CoverImage = string.Format(CoverVerticalUrl, appId)
        };

        // Steam's per-game header image is the only artwork reliably present on
        // every appdetails response; the old `clienticon` field (which provided
        // a square .ico) is no longer returned by the API, so the header serves
        // as the library icon.
        if (!string.IsNullOrWhiteSpace(data.HeaderImage))
            metadata.Icon = data.HeaderImage;

        if (!string.IsNullOrWhiteSpace(data.ShortDescription) && string.IsNullOrWhiteSpace(metadata.Description))
            metadata.Description = StripHtml(data.ShortDescription);

        if (data.Metacritic is { } metacritic)
            metadata.CriticScore = metacritic.Score;

        if (communityScore.HasValue)
            metadata.CommunityScore = communityScore.Value;

        if (data.ReleaseDate is { ComingSoon: false } release)
            metadata.ReleaseDate = ParseSteamReleaseDate(release.Date);

        if (data.Developers is { Count: > 0 })
            metadata.Developers = data.Developers.Where(d => !IsPlaceholder(d)).ToList();

        if (data.Publishers is { Count: > 0 })
            metadata.Publishers = data.Publishers.Where(p => !IsPlaceholder(p)).ToList();

        if (data.Genres is { Count: > 0 })
            metadata.Genres = data.Genres.Select(g => g.Description).Where(g => !string.IsNullOrWhiteSpace(g)).ToList();

        if (data.Platforms is { } platforms)
        {
            metadata.Platforms = [];
            if (platforms.Windows) metadata.Platforms.Add("Windows");
            if (platforms.Mac) metadata.Platforms.Add("macOS");
            if (platforms.Linux) metadata.Platforms.Add("Linux");
        }

        if (data.Categories is { Count: > 0 })
            metadata.Features = data.Categories.Select(c => c.Description).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

        // Fondo estándar de Steam: el library_hero (1920x620), proporción
        // consistente para todos los juegos.
        metadata.BackgroundImage = string.Format(HeroUrl, appId);

        // Same link set as Playnite's Steam library plugin
        // (PlayniteExtensions SteamLibrary/SteamShared/MetadataProvider.cs):
        // Community Hub, Discussions, Guides, News, Store Page and PCGamingWiki,
        // plus Achievements/Workshop only when the game has them (category id 22
        // and 30 respectively).
        metadata.Links.AddRange(new[]
        {
            new Link { Name = "Community Hub", Url = $"https://steamcommunity.com/app/{appId}" },
            new Link { Name = "Discussions", Url = $"https://steamcommunity.com/app/{appId}/discussions/" },
            new Link { Name = "Guides", Url = $"https://steamcommunity.com/app/{appId}/guides/" },
            new Link { Name = "News", Url = $"https://store.steampowered.com/news/?appids={appId}" },
            new Link { Name = "Steam Store", Url = $"https://store.steampowered.com/app/{appId}" },
            new Link { Name = "PCGamingWiki", Url = $"https://pcgamingwiki.com/api/appid.php?appid={appId}" }
        });

        if (data.Categories is { Count: > 0 } categories)
        {
            if (categories.Any(c => c.Id == 22))
                metadata.Links.Add(new Link { Name = "Achievements", Url = $"https://steamcommunity.com/stats/{appId}/achievements" });

            if (categories.Any(c => c.Id == 30))
                metadata.Links.Add(new Link { Name = "Workshop", Url = $"https://steamcommunity.com/app/{appId}/workshop/" });
        }

        return metadata;
    }

    private static int CalculateCommunityScore(int totalPositive, int totalNegative)
    {
        // Reviews can be "mixed" with zero explicit positive/negative counts —
        // guard against the NaN/int.MinValue that the raw division would produce.
        var totalVotes = totalPositive + totalNegative;
        if (totalVotes <= 0)
        {
            return 0;
        }

        double average = (double)totalPositive / totalVotes;
        double score = average - (average - 0.5) * Math.Pow(2, -Math.Log10(totalVotes + 1));
        return (int)(score * 100);
    }

    private static ReleaseDate? ParseSteamReleaseDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // "21 Aug, 2016" or "Aug 2016" or "2016"
        if (DateTime.TryParseExact(dateStr, ["d MMM, yyyy", "MMM yyyy", "yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new ReleaseDate(date.Year, date.Month, date.Day);
        }

        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var fallback))
            return new ReleaseDate(fallback.Year, fallback.Month, fallback.Day);

        return null;
    }

    private static bool IsPlaceholder(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("N/A", StringComparison.OrdinalIgnoreCase);

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Replaces <br>, <br/>, <br /> with newlines before stripping tags
        var withLineBreaks = BrRegex().Replace(html, "\n");
        var noTags = HtmlTagRegex().Replace(withLineBreaks, string.Empty);
        return noTags.Trim();
    }

    // Steam's store description embeds <img src="..."> (screenshots). Keep those
    // URLs so the UI can show them under the description text.
    private static List<string> ExtractImageUrls(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        return ImgSrcRegex().Matches(html)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }

    // Splits the description HTML into ordered text/image blocks so the details
    // view can render screenshots interleaved with the text — in the same place
    // the source put them — instead of a plain-text paragraph plus a strip of
    // images at the bottom. Text chunks are further split by block tags into
    // paragraphs, headings, subheadings and bullet lists so the source's
    // formatting (titles, sizes, list order) survives into the UI.
    private static List<DescriptionBlock> ParseDescriptionBlocks(string html)
    {
        var blocks = new List<DescriptionBlock>();
        if (string.IsNullOrWhiteSpace(html))
            return blocks;

        var matches = ImgSrcRegex().Matches(html);
        var lastIndex = 0;

        foreach (Match match in matches)
        {
            blocks.AddRange(ParseTextBlocks(html[lastIndex..match.Index]));
            blocks.Add(new DescriptionBlock { IsImage = true, Url = match.Groups[1].Value });
            lastIndex = match.Index + match.Length;
        }

        blocks.AddRange(ParseTextBlocks(html[lastIndex..]));
        return blocks;
    }

    // Turns a text-only HTML fragment into typed DescriptionBlocks. Splits on
    // headings (h2/h3) and lists (ul/li/ol) first so the source order and
    // structure are preserved; leftover runs become paragraphs.
    private static List<DescriptionBlock> ParseTextBlocks(string html)
    {
        var blocks = new List<DescriptionBlock>();
        if (string.IsNullOrWhiteSpace(html))
            return blocks;

        var pending = html;
        var position = 0;

        foreach (Match match in BlockStartRegex().Matches(html))
        {
            var preceding = StripHtml(pending[position..match.Index]);
            if (!string.IsNullOrWhiteSpace(preceding))
                blocks.Add(new DescriptionBlock { Text = preceding });

            position = match.Index + match.Length;

            var inner = match.Groups[2].Value;
            var innerText = StripHtml(inner);
            if (string.IsNullOrWhiteSpace(innerText))
                continue;

            var tag = match.Groups[1].Value.ToLowerInvariant();
            switch (tag)
            {
                case "h2":
                    blocks.Add(new DescriptionBlock { Kind = DescriptionBlockKind.Heading, Text = innerText });
                    break;
                case "h3":
                    blocks.Add(new DescriptionBlock { Kind = DescriptionBlockKind.Subheading, Text = innerText });
                    break;
                case "ul":
                case "ol": // each li becomes a bullet item
                    foreach (Match item in ListItemRegex().Matches(inner))
                    {
                        var itemText = StripHtml(item.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(itemText))
                            blocks.Add(new DescriptionBlock
                            {
                                Kind = DescriptionBlockKind.List,
                                Text = itemText
                            });
                    }
                    break;
                default: // p / div — a plain paragraph
                    blocks.Add(new DescriptionBlock { Text = innerText });
                    break;
            }
        }

        var trailing = StripHtml(pending[position..]);
        if (!string.IsNullOrWhiteSpace(trailing))
            blocks.Add(new DescriptionBlock { Text = trailing });

        return blocks;
    }

    // Matches a block-level element (h2/h3/ul/ol) and captures its inner HTML.
    // H2/H3 are grouped so "h3" text stays with the tag we switch on.
    [GeneratedRegex(@"<(h2|h3|ul|ol|p|div)[^>]*>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BlockStartRegex();

    [GeneratedRegex(@"<li[^>]*>(.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"<img[^>]*src=""([^""]+)""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcRegex();

    [GeneratedRegex(@"<a[^>]*?\s*(data-ds-packageid|data-ds-appid)=""(\d+)""[^>]*?>.*?<span class=""title"">([^<]+)</span>", RegexOptions.Singleline)]
    private static partial Regex SearchEntryRegex();

    [GeneratedRegex(@"<br\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BrRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
