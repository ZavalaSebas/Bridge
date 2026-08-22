using System.IO;
using System.Text.Json;
using Bridge.Metadata;

namespace Bridge.Services;

/// Caches RetroAchievements ROM MD5 → game ID mappings per console.
public sealed class RetroAchievementsHashIndex(RetroAchievementsClient client)
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);
    private static readonly string IndexDirectory = Path.Combine(Config.AppDataPath, "ra-hash-index");

    private readonly Lock _lock = new();
    private IReadOnlyDictionary<string, int>? _consoleIdsByName;
    private readonly Dictionary<int, CachedHashIndex> _hashIndexes = [];

    public async Task<int?> TryResolveGameIdAsync(
        string md5,
        int consoleId,
        string webApiKey,
        CancellationToken cancellationToken = default)
    {
        var index = await GetOrBuildIndexAsync(consoleId, webApiKey, cancellationToken);
        return index.TryGetValue(md5.Trim().ToLowerInvariant(), out var gameId)
            ? gameId
            : null;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetConsoleIdsAsync(
        string webApiKey,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_consoleIdsByName is not null)
                return _consoleIdsByName;
        }

        var fetched = await client.GetConsoleIdsAsync(webApiKey, cancellationToken);
        lock (_lock)
        {
            _consoleIdsByName = fetched;
            return _consoleIdsByName;
        }
    }

    private async Task<IReadOnlyDictionary<string, int>> GetOrBuildIndexAsync(
        int consoleId,
        string webApiKey,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_hashIndexes.TryGetValue(consoleId, out var cached) &&
                DateTime.UtcNow - cached.CachedAt < CacheLifetime)
            {
                return cached.HashToGameId;
            }
        }

        var loaded = TryLoadFromDisk(consoleId);
        if (loaded is not null && DateTime.UtcNow - loaded.CachedAt < CacheLifetime)
        {
            lock (_lock)
            {
                _hashIndexes[consoleId] = loaded;
                return loaded.HashToGameId;
            }
        }

        var fetched = await client.GetHashIndexAsync(webApiKey, consoleId, cancellationToken);
        var entry = new CachedHashIndex
        {
            CachedAt = DateTime.UtcNow,
            HashToGameId = fetched.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase),
        };

        TrySaveToDisk(consoleId, entry);

        lock (_lock)
        {
            _hashIndexes[consoleId] = entry;
            return entry.HashToGameId;
        }
    }

    private static CachedHashIndex? TryLoadFromDisk(int consoleId)
    {
        var path = GetIndexPath(consoleId);
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CachedHashIndex>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void TrySaveToDisk(int consoleId, CachedHashIndex entry)
    {
        try
        {
            Directory.CreateDirectory(IndexDirectory);
            File.WriteAllText(GetIndexPath(consoleId), JsonSerializer.Serialize(entry));
        }
        catch (IOException)
        {
            // Best-effort cache.
        }
    }

    public void ClearMemoryCache()
    {
        lock (_lock)
        {
            _consoleIdsByName = null;
            _hashIndexes.Clear();
        }
    }

    private static string GetIndexPath(int consoleId) =>
        Path.Combine(IndexDirectory, $"{consoleId}.json");

    private sealed class CachedHashIndex
    {
        public DateTime CachedAt { get; set; }
        public Dictionary<string, int> HashToGameId { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
