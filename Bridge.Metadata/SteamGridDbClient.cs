using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Bridge.Metadata;

public enum SteamGridDbAssetKind
{
    Icon,
    Cover,
    Hero,
    Logo
}

public sealed record SteamGridDbGame(int Id, string Name);

public sealed record SteamGridDbAsset(int Id, string Url, string ThumbUrl, int Width, int Height);

/// <summary>SteamGridDB API v2 client for community game artwork.</summary>
public sealed class SteamGridDbClient(HttpClient httpClient, SteamGridDbSettings settings)
{
    private const string BaseUrl = "https://www.steamgriddb.com/api/v2/";

    public bool IsConfigured => settings.IsConfigured;

    public async Task<IReadOnlyList<SteamGridDbGame>> SearchGamesAsync(string term, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(term))
            return [];

        using var request = CreateRequest(HttpMethod.Get, $"search/autocomplete/{Uri.EscapeDataString(term.Trim())}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        var payload = await response.Content.ReadFromJsonAsync<SgdbResponse<SgdbGameDto[]>>(cancellationToken: cancellationToken);
        if (payload?.Data is not { Length: > 0 } games)
            return [];

        return games
            .Where(g => g.Id > 0 && !string.IsNullOrWhiteSpace(g.Name))
            .Select(g => new SteamGridDbGame(g.Id, g.Name.Trim()))
            .ToList();
    }

    public async Task<IReadOnlyList<SteamGridDbAsset>> GetAssetsAsync(
        int gameId,
        SteamGridDbAssetKind kind,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured || gameId <= 0)
            return [];

        var segment = kind switch
        {
            SteamGridDbAssetKind.Icon => "icons",
            SteamGridDbAssetKind.Cover => "grids",
            SteamGridDbAssetKind.Hero => "heroes",
            SteamGridDbAssetKind.Logo => "logos",
            _ => "grids"
        };

        using var request = CreateRequest(HttpMethod.Get, $"{segment}/game/{gameId}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        var payload = await response.Content.ReadFromJsonAsync<SgdbResponse<SgdbAssetDto[]>>(cancellationToken: cancellationToken);
        if (payload?.Data is not { Length: > 0 } assets)
            return [];

        return assets
            .Where(a => a.Id > 0 && !string.IsNullOrWhiteSpace(a.Url))
            .Select(a => new SteamGridDbAsset(
                a.Id,
                a.Url.Trim(),
                string.IsNullOrWhiteSpace(a.Thumb) ? a.Url.Trim() : a.Thumb.Trim(),
                a.Width,
                a.Height))
            .ToList();
    }

    public static SteamGridDbAssetKind MediaFieldToKind(string mediaField) => mediaField switch
    {
        "Icon" => SteamGridDbAssetKind.Icon,
        "CoverImage" => SteamGridDbAssetKind.Cover,
        "BackgroundImage" => SteamGridDbAssetKind.Hero,
        "LogoImage" => SteamGridDbAssetKind.Logo,
        _ => SteamGridDbAssetKind.Cover
    };

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var request = new HttpRequestMessage(method, BaseUrl + relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private sealed class SgdbResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private sealed class SgdbGameDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SgdbAssetDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("thumb")]
        public string? Thumb { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }
}
