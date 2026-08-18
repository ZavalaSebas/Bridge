using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Bridge.Services;

/// <summary>A single image result from the image search.</summary>
public sealed record ImageSearchResult(string ThumbnailUrl, string ImageUrl, int Width, int Height);

/// Image search via DuckDuckGo — no API key. Returns thumbnail and full-size URLs.
public sealed class WebImageSearchService(HttpClient httpClient)
{
    private const string VqdEndpoint = "https://duckduckgo.com/";
    private const string ImageEndpoint = "https://duckduckgo.com/i.js";

    public async Task<List<ImageSearchResult>> SearchAsync(string query, int count = 48, CancellationToken cancellationToken = default)
    {
        var results = new List<ImageSearchResult>();
        if (string.IsNullOrWhiteSpace(query))
            return results;

        // DDG requires a vqd token from the HTML search page before i.js works.
        var vqd = await GetVqdAsync(query, cancellationToken);
        if (string.IsNullOrWhiteSpace(vqd))
            return results;

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{ImageEndpoint}?l=us-en&o=json&q={Uri.EscapeDataString(query)}&vqd={Uri.EscapeDataString(vqd)}&f=,,,,,&p=1");
        request.Headers.Referrer = new Uri($"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}&iax=images&ia=images");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return results;

        var payload = await response.Content.ReadFromJsonAsync<DdgImageResponse>(cancellationToken: cancellationToken);
        if (payload?.Results is not { Count: > 0 })
            return results;

        foreach (var item in payload.Results)
        {
            if (string.IsNullOrWhiteSpace(item.ImageUrl))
                continue;
            results.Add(new ImageSearchResult(item.ThumbnailUrl ?? item.ImageUrl, item.ImageUrl, item.Width, item.Height));
            if (results.Count >= count)
                break;
        }

        return results;
    }

    private async Task<string?> GetVqdAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{VqdEndpoint}?q={Uri.EscapeDataString(query)}&iax=images&ia=images");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            request.Headers.Accept.ParseAdd("text/html");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var match = System.Text.RegularExpressions.Regex.Match(html, "vqd=['\"]?([^'\"&;]+)['\"]?");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class DdgImageResponse
    {
        [JsonPropertyName("results")]
        public List<DdgImageItem>? Results { get; set; }
    }

    private sealed class DdgImageItem
    {
        [JsonPropertyName("thumbnail")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("image")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }
}
