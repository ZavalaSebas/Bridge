using System.Net.Http;
using System.Text.Json;

namespace Bridge.Services;

public sealed class FreeGamesService
{
    private readonly HttpClient _httpClient;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static DateTime _lastFetch = DateTime.MinValue;
    private static List<FreeGameNotification>? _cache;

    public FreeGamesService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<IReadOnlyList<FreeGameNotification>> GetFreeGamesAsync(CancellationToken ct = default)
    {
        if (_cache != null && DateTime.UtcNow - _lastFetch < CacheDuration)
            return _cache;

        try
        {
            var results = new List<FreeGameNotification>();

            // Fetch Epic and Steam in parallel via GamerPower
            var epicTask = FetchFromGamerPowerAsync("epic-games-store", ct);
            var steamTask = FetchFromGamerPowerAsync("steam", ct);

            await Task.WhenAll(epicTask, steamTask);

            results.AddRange(await epicTask);
            results.AddRange(await steamTask);

            // Filter: only active worth >0 games/dlc, and status active
            var filtered = results
                .Where(g => g.Type.Equals("Game", StringComparison.OrdinalIgnoreCase))
                .Where(g => g.Worth != "N/A" && !string.IsNullOrWhiteSpace(g.Worth))
                .GroupBy(g => g.Id).Select(g => g.First())
                .OrderByDescending(g => g.PublishedDate)
                .ToList();

            _cache = filtered;
            _lastFetch = DateTime.UtcNow;
            return filtered;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Failed to fetch free games from GamerPower.", ex);
            return (IReadOnlyList<FreeGameNotification>?)_cache ?? Array.Empty<FreeGameNotification>();
        }
    }

    private async Task<List<FreeGameNotification>> FetchFromGamerPowerAsync(string platform, CancellationToken ct)
    {
        var url = $"https://www.gamerpower.com/api/giveaways?platform={platform}";
        try
        {
            using var resp = await _httpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return new List<FreeGameNotification>();
            var json = await resp.Content.ReadAsStringAsync(ct);
            var dtos = JsonSerializer.Deserialize<List<GamerPowerDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null) return new List<FreeGameNotification>();
            return dtos
                .Where(d => d.status == "Active")
                .Select(d => new FreeGameNotification
                {
                    Id = d.id,
                    Title = d.title ?? string.Empty,
                    Worth = d.worth ?? string.Empty,
                    Thumbnail = d.thumbnail ?? string.Empty,
                    Image = d.image ?? string.Empty,
                    Description = d.description ?? string.Empty,
                    OpenGiveawayUrl = d.open_giveaway_url ?? d.gamerpower_url ?? string.Empty,
                    GamerpowerUrl = d.gamerpower_url ?? string.Empty,
                    Platforms = d.platforms ?? string.Empty,
                    Type = d.type ?? string.Empty,
                    EndDate = d.end_date ?? string.Empty,
                    PublishedDate = TryParseDate(d.published_date)
                }).ToList();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Failed to fetch free games for a GamerPower platform.", ex);
            return new List<FreeGameNotification>();
        }
    }

    private static DateTime TryParseDate(string? s)
    {
        if (DateTime.TryParse(s, out var d)) return d;
        return DateTime.MinValue;
    }

    private sealed class GamerPowerDto
    {
        public int id { get; set; }
        public string? title { get; set; }
        public string? worth { get; set; }
        public string? thumbnail { get; set; }
        public string? image { get; set; }
        public string? description { get; set; }
        public string? open_giveaway_url { get; set; }
        public string? gamerpower_url { get; set; }
        public string? platforms { get; set; }
        public string? type { get; set; }
        public string? end_date { get; set; }
        public string? published_date { get; set; }
        public string? status { get; set; }
    }
}
