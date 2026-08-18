using Bridge.Core.Contracts;
using Bridge.Core.Import;
using Bridge.Metadata;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class MetadataSyncServiceTests
{
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
        var service = new MetadataSyncService([steam, igdb], steam, new BridgeIgdbProvider(new HttpClient()));

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
        var service = new MetadataSyncService([first, second], steam, new BridgeIgdbProvider(new HttpClient()));

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
        var service = new MetadataSyncService([igdb], steam, new BridgeIgdbProvider(new HttpClient()));

        var found = await service.SearchForManualDownloadAsync("Pokemon Emerald", romImport: true, steamAppId: null);

        Assert.NotNull(found);
        Assert.Equal("Steam Store", found.Value.ProviderName);
        Assert.Equal(1, igdb.SearchCalls);
        Assert.Equal(1, steam.SearchCalls);
    }
}
