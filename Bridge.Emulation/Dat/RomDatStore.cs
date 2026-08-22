using System.Collections.Concurrent;

namespace Bridge.Emulation.Dat;

/// Downloads and caches No-Intro DAT files, exposing CRC lookup tables per platform.
public sealed class RomDatStore(HttpClient? httpClient = null)
{
    private const string AppFolderName = "Bridge";

    private readonly HttpClient _httpClient = httpClient ?? new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, RomDatGame>> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _loadLock = new();

    public static string DefaultCacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName,
        "rom-dat");

    public string CacheDirectory { get; init; } = DefaultCacheDirectory;

    public RomDatGame? Lookup(string datFileName, string crcHex, long? romSize = null)
    {
        if (string.IsNullOrWhiteSpace(crcHex))
            return null;

        var table = GetOrLoadTable(datFileName);
        if (!table.TryGetValue(crcHex.ToUpperInvariant(), out var game))
            return null;

        if (romSize is null or <= 0)
            return game;

        var size = (uint)Math.Min(romSize.Value, uint.MaxValue);
        return game.Roms.Any(rom => rom.Size == size) ? game : null;
    }

    public IReadOnlyDictionary<string, RomDatGame> GetOrLoadTable(string datFileName)
    {
        if (_tables.TryGetValue(datFileName, out var cached))
            return cached;

        lock (_loadLock)
        {
            if (_tables.TryGetValue(datFileName, out cached))
                return cached;

            EnsureDatFilePresent(datFileName);
            var path = Path.Combine(CacheDirectory, datFileName);
            cached = ClrmameDatParser.ParseFile(path);
            _tables[datFileName] = cached;
            return cached;
        }
    }

    public void RegisterTable(string datFileName, IReadOnlyDictionary<string, RomDatGame> table) =>
        _tables[datFileName] = table;

    private void EnsureDatFilePresent(string datFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        var path = Path.Combine(CacheDirectory, datFileName);
        if (File.Exists(path))
            return;

        var url = RomDatCatalog.GetDownloadUrl(datFileName);
        using var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        File.WriteAllBytes(path, response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
    }
}
