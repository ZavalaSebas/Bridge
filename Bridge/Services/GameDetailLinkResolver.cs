using System.Text;
using System.Text.RegularExpressions;
using Bridge.Core.Entities;
using Bridge.Core.Utilities;

namespace Bridge.Services;

/// <summary>Builds external URLs for detail-panel and hero-bar navigation.</summary>
public static partial class GameDetailLinkResolver
{
    public static string GetGoogleSearchUrl(string query) =>
        $"https://www.google.com/search?q={Uri.EscapeDataString(query.Trim())}";

    public static string? GetSteamLibraryUrl(Game game) =>
        uint.TryParse(game.ExternalId, out _) ? $"steam://nav/games/details/{game.ExternalId}" : null;

    public static string GetEpicLibraryUrl(Game game) =>
        "com.epicgames.launcher://store/library";

    public static string? GetSteamStoreUrl(Game game) =>
        TryResolveSteamAppId(game, out var appId)
            ? $"https://store.steampowered.com/app/{appId}/"
            : null;

    public static string? GetSteamReviewsUrl(Game game) =>
        TryResolveSteamAppId(game, out var appId)
            ? $"https://steamcommunity.com/app/{appId}/reviews/"
            : null;

    public static string GetCommunityScoreUrl(Game game, string? sourceName)
    {
        if (TryResolveSteamAppId(game, out _))
            return GetSteamReviewsUrl(game)!;

        return GetMetacriticUrl(game);
    }

    public static bool TryResolveSteamAppId(Game game, out uint appId)
    {
        if (uint.TryParse(game.ExternalId, out appId))
            return true;

        var storeLink = FindHttpLink(game, static link =>
            link.Name.Equals("Steam Store", StringComparison.OrdinalIgnoreCase) ||
            link.Url.Contains("store.steampowered.com/app/", StringComparison.OrdinalIgnoreCase));

        if (storeLink is not null && TryParseSteamAppIdFromUrl(storeLink, out appId))
            return true;

        appId = 0;
        return false;
    }

    internal static bool TryParseSteamAppIdFromUrl(string url, out uint appId)
    {
        appId = 0;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var match = SteamAppPathRegex().Match(uri.AbsolutePath);
        return match.Success && uint.TryParse(match.Groups[1].Value, out appId);
    }

    public static string GetMetacriticUrl(Game game)
    {
        var stored = FindHttpLink(game, static link =>
            link.Url.Contains("metacritic.com", StringComparison.OrdinalIgnoreCase) ||
            link.Name.Contains("metacritic", StringComparison.OrdinalIgnoreCase));

        if (stored is not null)
        {
            var normalized = NormalizeMetacriticGameUrl(stored);
            if (TryGetMetacriticSlugFromGameUrl(normalized, out var slug) && !IsMetacriticPlatformSlug(slug))
                return normalized;
        }

        var fallbackSlug = BuildMetacriticSlug(game.Name);
        return string.IsNullOrWhiteSpace(fallbackSlug)
            ? "https://www.metacritic.com/browse/game/"
            : $"https://www.metacritic.com/game/{fallbackSlug}/";
    }

    private static readonly HashSet<string> MetacriticPlatformSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "pc", "switch", "nintendo-switch", "wii", "wii-u", "3ds", "ds", "gba", "game-boy-advance",
        "playstation-5", "playstation-4", "playstation-3", "playstation-2", "playstation",
        "ps5", "ps4", "ps3", "ps2", "psp", "vita",
        "xbox-series-x", "xbox-one", "xbox-360", "xbox",
        "ios", "iphone", "ipad", "android",
        "dreamcast", "gamecube", "nintendo-64", "n64",
        "meta-quest-2", "meta-quest-3",
    };

    internal static bool IsMetacriticPlatformSlug(string slug) =>
        MetacriticPlatformSlugs.Contains(slug);

    internal static string BuildMetacriticSlug(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return string.Empty;

        var builder = new StringBuilder(gameName.Length);
        foreach (var c in gameName.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(c);
            else if (c is ' ' or '-' or ':' or '\'' or '.')
                builder.Append('-');
        }

        return HyphenCollapse().Replace(builder.ToString(), "-").Trim('-');
    }

    internal static string NormalizeMetacriticGameUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return url.Trim();

        if (!uri.Host.Contains("metacritic.com", StringComparison.OrdinalIgnoreCase))
            return url.Trim();

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var slug = ExtractMetacriticGameSlug(segments);
        return string.IsNullOrWhiteSpace(slug)
            ? url.Trim()
            : $"https://www.metacritic.com/game/{slug}/";
    }

    internal static string? ExtractMetacriticGameSlug(IReadOnlyList<string> segments)
    {
        if (segments.Count < 2 ||
            !segments[0].Equals("game", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (segments.Count >= 3 && IsMetacriticPlatformSlug(segments[1]))
            return segments[2];

        return IsMetacriticPlatformSlug(segments[1]) ? null : segments[1];
    }

    private static bool TryGetMetacriticSlugFromGameUrl(string url, out string slug)
    {
        slug = string.Empty;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var extracted = ExtractMetacriticGameSlug(segments);
        if (string.IsNullOrWhiteSpace(extracted))
            return false;

        slug = extracted;
        return true;
    }

    public static string ResolveLinkUrl(Link link, Game? game = null)
    {
        if (link.Url.Contains("metacritic.com", StringComparison.OrdinalIgnoreCase) ||
            link.Name.Contains("metacritic", StringComparison.OrdinalIgnoreCase))
        {
            if (game is not null)
                return GetMetacriticUrl(game);

            var normalized = NormalizeMetacriticGameUrl(link.Url);
            if (TryGetMetacriticSlugFromGameUrl(normalized, out var slug) && !IsMetacriticPlatformSlug(slug))
                return normalized;
        }

        return link.Url.Trim();
    }

    public static string GetHowLongToBeatUrl(Game game)
    {
        var stored = FindHttpLink(game, static link =>
            link.Name.Equals("HowLongToBeat", StringComparison.OrdinalIgnoreCase) ||
            link.Url.Contains("howlongtobeat.com", StringComparison.OrdinalIgnoreCase));

        return stored ?? $"https://howlongtobeat.com/?q={Uri.EscapeDataString(game.Name)}";
    }

    public static bool IsRomLibrary(Game game, string? sourceName) =>
        game.Roms.Count > 0 ||
        string.Equals(sourceName, "ROM", StringComparison.OrdinalIgnoreCase);

    public static string ResolveLibraryFilterName(Game game, string? sourceName) =>
        IsRomLibrary(game, sourceName) ? "ROM" :
        !string.IsNullOrWhiteSpace(sourceName) ? sourceName : "Manual";

    private static string? FindHttpLink(Game game, Func<Link, bool> predicate)
    {
        foreach (var link in game.Links)
        {
            if (!predicate(link))
                continue;

            if (UrlValidator.IsSafeHttpUrl(link.Url))
                return link.Url.Trim();
        }

        return null;
    }

    [GeneratedRegex(@"/app/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SteamAppPathRegex();

    [GeneratedRegex("-+")]
    private static partial Regex HyphenCollapse();
}
