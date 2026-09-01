using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Bridge.Core.Entities;

namespace Bridge.Metadata;

/// <summary>
/// Loads public Steam achievement definitions from the community stats page.
/// Used for external games linked to a Steam app ID when local schema files are missing.
/// </summary>
public sealed partial class SteamCommunityAchievementsClient(HttpClient httpClient)
{
    public async Task<GameAchievementsSnapshot?> GetCatalogAsync(
        string appId,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (!uint.TryParse(appId, out _))
            return null;

        var locale = MapCommunityLanguage(language);
        var url = $"https://steamcommunity.com/stats/{appId}/achievements/?l={Uri.EscapeDataString(locale)}";

        using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("achieveRow", StringComparison.Ordinal))
            return null;

        var achievements = new List<GameAchievement>();
        var index = 0;
        foreach (Match match in AchievementRowRegex().Matches(html))
        {
            var name = WebUtility.HtmlDecode(match.Groups["name"].Value.Trim());
            if (string.IsNullOrWhiteSpace(name))
                continue;

            double? globalPercent = null;
            var percentText = match.Groups["percent"].Value.Trim();
            if (double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                globalPercent = percent;

            var iconUrl = match.Groups["icon"].Value.Trim();
            achievements.Add(new GameAchievement
            {
                ApiName = $"community_{index++}",
                Name = name,
                Description = WebUtility.HtmlDecode(match.Groups["desc"].Value.Trim()),
                IsHidden = false,
                IsUnlocked = false,
                IconUrl = string.IsNullOrWhiteSpace(iconUrl) ? null : iconUrl,
                IconLockedUrl = string.IsNullOrWhiteSpace(iconUrl) ? null : iconUrl,
                GlobalUnlockPercent = globalPercent,
                Rarity = SteamAchievementRarity.FromGlobalPercent(globalPercent),
            });
        }

        if (achievements.Count == 0)
            return null;

        return new GameAchievementsSnapshot
        {
            Achievements = achievements,
            UnlockedCount = 0,
            TracksProgress = false,
        };
    }

    [GeneratedRegex(
        @"<div class=""achieveRow[^""]*"">.*?<img src=""(?<icon>[^""]+)"".*?<div class=""achievePercent"">(?<percent>[\d.]+)%</div>.*?<h3>(?<name>[^<]*)</h3>.*?<h5>(?<desc>[^<]*)</h5>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex AchievementRowRegex();

    private static string MapCommunityLanguage(string language) =>
        language switch
        {
            "spanish" => "spanish",
            "german" => "german",
            "french" => "french",
            "italian" => "italian",
            "portuguese" => "portuguese",
            "russian" => "russian",
            "japanese" => "japanese",
            "korean" => "korean",
            "schinese" => "schinese",
            "tchinese" => "tchinese",
            _ => "english",
        };
}
