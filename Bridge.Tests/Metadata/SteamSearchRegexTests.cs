using System.Text.RegularExpressions;

namespace Bridge.Tests.Metadata;

public class SteamSearchRegexTests
{
    // Real shape of a Steam search result: an <a> with BOTH data-ds-packageid
    // (comes first) and data-ds-appid, wrapping the game title in a span.
    private const string ResultHtml = """
        <a href="https://store.steampowered.com/app/620/Gate/" class="search_result_row" data-ds-packageid="1" data-ds-appid="620" data-ds-bundleid="" data-ds-bundlediscount="0" data-ds-hasprice="1">
          <div class="col search_capsule"><img src="https://cdn.akamai.steamstatic.com/steam/apps/620/header.jpg" /></div>
          <div class="col search_name ellipsis"><span class="title">Gate</span></div>
        </a>
        """;

    private static readonly Regex SearchEntryRegex = new(
        @"<a[^>]*?data-ds-appid=""(\d+)""[^>]*?>(?:.*?<span class=""title"">([^<]+)</span>)?",
        RegexOptions.Singleline);

    [Fact]
    public void CapturesAppId_FromResultThatHasPackageIdFirst()
    {
        var match = SearchEntryRegex.Match(ResultHtml);

        Assert.True(match.Success);
        Assert.Equal("620", match.Groups[1].Value);
        Assert.Equal("Gate", match.Groups[2].Value);
    }

    [Fact]
    public void MatchesAllResults_AndPicksTheFirstAppId()
    {
        var html = ResultHtml
            + ResultHtml.Replace("app/620", "app/621").Replace("data-ds-appid=\"620\"", "data-ds-appid=\"621\"")
            + ResultHtml.Replace("app/620", "app/622").Replace("data-ds-appid=\"620\"", "data-ds-appid=\"622\"");

        var matches = SearchEntryRegex.Matches(html);

        Assert.True(matches.Count >= 3);
        Assert.Equal("620", matches[0].Groups[1].Value);
    }

    [Fact]
    public void DoesNotMatchBundleLinksWithoutAppId()
    {
        var html = """<a href="https://store.steampowered.com/bundle/1234/" data-ds-packageid="5678"><span class="title">Bundle</span></a>""";

        Assert.DoesNotMatch(SearchEntryRegex, html);
    }
}
