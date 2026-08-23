using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
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

    // Steam's library_hero (1920x620, 3.1:1) is the official library image and
    // is barely cropped in Bridge's hero, so it stays consistent across games.
    // 16:9 screenshots lose roughly 25% height on wide viewports, so they are
    // kept for a separate gallery section.
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceWarning("Steam reviews lookup failed for app {0}: {1}", steamAppId, ex.Message);
        }

        return MapToGameMetadata(appDetails, steamAppId, communityScore);
    }

    private async Task<uint?> SearchAppIdAsync(string gameName, CancellationToken cancellationToken)
    {
        try
        {
            var url = string.Format(SearchUrl, Uri.EscapeDataString(gameName));
            var html = await httpClient.GetStringAsync(url, cancellationToken);

            var queryWords = Tokenize(gameName);
            var matches = SearchEntryRegex().Matches(html);
            foreach (Match match in matches)
            {
                var appId = match.Groups[1].Value;
                if (!uint.TryParse(appId, out var id))
                    continue;

                // The store's search can rank a totally unrelated game first
                // ("Genshin Impact" → "Dream of Corpse Lady", appid 2842800 —
                // Genshin isn't on Steam at all). Only accept a result whose
                // title actually contains the searched words, so a mismatch
                // falls through to the next provider instead of grabbing the
                // wrong game's metadata.
                var title = match.Groups[2].Value;
                if (queryWords.Count > 0 && !TitleContains(queryWords, title))
                    continue;

                return id;
            }

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceWarning("Steam search failed for \"{0}\": {1}", gameName, ex.Message);
            return null;
        }
    }
    // filler ("of the", "and", ...) so "Fallout 3 - Game of the Year" still
    // matches "Fallout 3". Short tokens are kept on purpose: "2" in "Risk of
    // Rain 2" and "V" in "Grand Theft Auto V" are what tell it apart from the
    // original release. The title on the store page must contain every one of
    // these words for the result to be accepted.
    private static List<string> Tokenize(string name)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "of", "and", "or", "for", "with", "in", "on",
            "at", "to", "by", "vs", "edition", "game"
        };

        return [.. name
            .Split([' ', '-', '_', ':', '.', '(', ')', '\'', '"', '™', '®'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !stopWords.Contains(w))
            .Select(w => w.ToLowerInvariant())
            .Distinct()];
    }

    private static bool TitleContains(IReadOnlyCollection<string> queryWords, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var titleWords = title
            .Split([' ', '-', '_', ':', '.', '(', ')', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        return queryWords.All(titleWords.Contains);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceWarning("Steam appdetails failed for app {0}: {1}", appId, ex.Message);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceWarning("Steam appreviews failed for app {0}: {1}", appId, ex.Message);
            return null;
        }
    }

    private static GameMetadata MapToGameMetadata(SteamAppDetailsData data, uint appId, int? communityScore)
    {
        var metadata = new GameMetadata
        {
            Name = data.Name,
            ExternalId = appId.ToString(),
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
        {
            metadata.CriticScore = metacritic.Score;
            if (!string.IsNullOrWhiteSpace(metacritic.Url))
            {
                var sanitized = Bridge.Core.Utilities.UrlValidator.SanitizePersistedUrl(metacritic.Url);
                if (sanitized is not null)
                    metadata.Links.Add(new Link { Name = "Metacritic", Url = sanitized });
            }
        }

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

        // Steam's standard hero background: library_hero (1920x620).
        metadata.BackgroundImage = string.Format(HeroUrl, appId);

        // Screenshot gallery: use full-resolution paths (1920x1080) without
        // Steam's resize query string. These are the game's real screenshots and
        // are shown in the details gallery.
        if (data.Screenshots is { Count: > 0 })
        {
            metadata.Screenshots = data.Screenshots
                .Select(s => s.PathFull)
                .Select(StripQueryString)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();
        }

        // Store/community links plus Achievements/Workshop when present (category 22/30).
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

        // "21 Aug, 2016" / "Aug 2016" / "2016". Exact-match each format and
        // preserve the granularity the source gave us: a bare "2016" must stay
        // a year-only ReleaseDate, not be padded to 2016-01-01.
        var trimmed = dateStr.Trim();
        if (DateTime.TryParseExact(trimmed, "d MMM, yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var fullDate))
        {
            return new ReleaseDate(fullDate.Year, fullDate.Month, fullDate.Day);
        }

        if (DateTime.TryParseExact(trimmed, "MMM yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var yearMonth))
        {
            return new ReleaseDate(yearMonth.Year, yearMonth.Month);
        }

        if (int.TryParse(trimmed, out var year))
        {
            return new ReleaseDate(year);
        }

        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var fallback))
            return new ReleaseDate(fallback.Year, fallback.Month, fallback.Day);

        return null;
    }

    private static bool IsPlaceholder(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("N/A", StringComparison.OrdinalIgnoreCase);

    private static string StripQueryString(string url)
    {
        var qsIndex = url.IndexOf('?');
        return qsIndex >= 0 ? url[..qsIndex] : url;
    }

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

    // Each search result is an <a> carrying both data-ds-packageid and
    // data-ds-appid (packageid comes first). Greedily match the whole tag so we
    // land on data-ds-appid — the appid we actually need — and capture the title.
    [GeneratedRegex(@"<a[^>]*?data-ds-appid=""(\d+)""[^>]*?>(?:.*?<span class=""title"">([^<]+)</span>)?", RegexOptions.Singleline)]
    private static partial Regex SearchEntryRegex();

    [GeneratedRegex(@"<br\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BrRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
