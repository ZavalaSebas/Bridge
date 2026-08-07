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
            if (reviews is { TotalReviews: > 0 })
                communityScore = CalculateCommunityScore(reviews.TotalPositive, reviews.TotalNegative);
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
            CoverImage = string.Format(CoverVerticalUrl, appId)
        };

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
            if (platforms.Windows) metadata.Platforms.Add("pc_windows");
            if (platforms.Mac) metadata.Platforms.Add("macintosh");
            if (platforms.Linux) metadata.Platforms.Add("pc_linux");
        }

        if (data.Categories is { Count: > 0 })
            metadata.Features = data.Categories.Select(c => c.Description).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

        // Background from last screenshot (path_full, removing Query String)
        if (data.Screenshots is { Count: > 0 })
        {
            var qsIndex = data.Screenshots[0].PathFull.IndexOf('?');
            metadata.BackgroundImage = qsIndex >= 0
                ? data.Screenshots[0].PathFull[..qsIndex]
                : data.Screenshots[0].PathFull;
        }

        // Header image as fallback background
        if (!string.IsNullOrWhiteSpace(data.HeaderImage) && string.IsNullOrWhiteSpace(metadata.BackgroundImage))
            metadata.BackgroundImage = data.HeaderImage;

        // Links: store page
        metadata.Links.Add(new Link
        {
            Name = "Steam Store",
            Url = $"https://store.steampowered.com/app/{appId}/"
        });

        return metadata;
    }

    private static int CalculateCommunityScore(int totalPositive, int totalNegative)
    {
        var totalVotes = totalPositive + totalNegative;
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

        // Fallback: try general parse
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

    [GeneratedRegex(@"<a[^>]*?\s*(data-ds-packageid|data-ds-appid)=""(\d+)""[^>]*?>.*?<span class=""title"">([^<]+)</span>", RegexOptions.Singleline)]
    private static partial Regex SearchEntryRegex();

    [GeneratedRegex(@"<br\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BrRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
