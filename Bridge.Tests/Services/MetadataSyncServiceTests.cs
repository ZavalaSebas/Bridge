using Bridge.Core.Contracts;
using Bridge.Core.Import;
using Bridge.Emulation.Dat;
using Bridge.Metadata;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class MetadataSyncServiceTests
{
    private static MetadataSyncService CreateService(
        IGameMetadataProvider[] chain,
        IGameMetadataProvider steam,
        RomDatMatcher? datMatcher = null) =>
        new(chain, steam, new BridgeIgdbProvider(new HttpClient()), datMatcher ?? RomDatMatcher.Disabled);
    private sealed class StubProvider(string name, GameMetadata? result) : IGameMetadataProvider
    {
        public string Name { get; } = name;
        public int SearchCalls { get; private set; }

        public Task<GameMetadata?> SearchAsync(string gameName, CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task SearchForAddedGameAsync_SteamFirst_skips_chain_when_steam_finds()
    {
        var steam = new StubProvider("Steam Store", new GameMetadata { Name = "Portal" });
        var igdb = new StubProvider("IGDB", new GameMetadata { Name = "Wrong" });
        var service = CreateService([steam, igdb], steam);

        var found = await service.SearchForAddedGameAsync("Portal", romImport: false);

        Assert.NotNull(found);
        Assert.Equal("Steam Store", found.Value.ProviderName);
        Assert.Equal(1, steam.SearchCalls);
        Assert.Equal(0, igdb.SearchCalls);
    }

    [Fact]
    public async Task SearchByNameChainAsync_falls_through_to_next_provider()
    {
        var first = new StubProvider("First", null);
        var second = new StubProvider("Second", new GameMetadata { Name = "Hollow Knight" });
        var steam = new StubProvider("Steam Store", null);
        var service = CreateService([first, second], steam);

        var found = await service.SearchByNameChainAsync("Hollow Knight");

        Assert.NotNull(found);
        Assert.Equal("Second", found.Value.ProviderName);
        Assert.Equal(1, first.SearchCalls);
        Assert.Equal(1, second.SearchCalls);
    }

    [Fact]
    public async Task SearchForManualDownloadAsync_romImport_tries_steam_last()
    {
        var igdb = new StubProvider("IGDB", null);
        var steam = new StubProvider("Steam Store", new GameMetadata { Name = "Pokemon Emerald" });
        var service = CreateService([igdb], steam);

        var found = await service.SearchForManualDownloadAsync("Pokemon Emerald", romImport: true, steamAppId: null);

        Assert.Null(found);
        Assert.Equal(1, igdb.SearchCalls);
        Assert.Equal(0, steam.SearchCalls);
    }

    [Fact]
    public async Task SearchRomMetadataAsync_tries_spanish_title_variants()
    {
        var igdb = new CountingProvider("IGDB", query =>
            query.Equals("Pokemon Yellow Version", StringComparison.OrdinalIgnoreCase)
                ? new GameMetadata { Name = "Pokémon Yellow Version" }
                : null);
        var steam = new StubProvider("Steam Store", null);
        var service = CreateService([igdb], steam);

        var found = await service.SearchRomMetadataAsync("Pokemon Amarillo");

        Assert.NotNull(found);
        Assert.Equal("Pokémon Yellow Version", found!.Value.Metadata.Name);
        Assert.Equal(2, igdb.SearchCalls);
        Assert.Equal(0, steam.SearchCalls);
    }

    private sealed class CountingProvider(string name, Func<string, GameMetadata?> resolve) : IGameMetadataProvider
    {
        public string Name { get; } = name;
        public int SearchCalls { get; private set; }

        public Task<GameMetadata?> SearchAsync(string gameName, CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(resolve(gameName));
        }
    }
}
