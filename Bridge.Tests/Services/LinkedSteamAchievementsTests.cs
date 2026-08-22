using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Metadata;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class LinkedSteamAchievementsTests
{
    [Fact]
    public void SupportsAchievements_ReturnsTrueForBridgeGameWithSteamLink()
    {
        var game = new Game
        {
            SourceId = GameSource.BridgeId,
            Name = "Risk of Rain 2",
            Links =
            [
                new Link
                {
                    Name = "Steam Store",
                    Url = "https://store.steampowered.com/app/632360/",
                },
            ],
        };

        var service = CreateService();

        Assert.True(service.SupportsAchievements(game));
        Assert.True(service.IsDefinitionsOnly(game));
    }

    [Fact]
    public void SupportsAchievements_ReturnsFalseForBridgeGameWithoutSteamLink()
    {
        var game = new Game
        {
            SourceId = GameSource.BridgeId,
            Name = "Some External Game",
        };

        var service = CreateService();

        Assert.False(service.SupportsAchievements(game));
    }

    private static GameAchievementsService CreateService()
    {
        var sourceRepository = new StubSourceRepository();
        var steamService = new SteamAchievementsService(
            sourceRepository,
            new SteamGlobalAchievementStatsClient(new HttpClient()),
            new SteamCommunityAchievementsClient(new HttpClient()));
        var epicService = new EpicAchievementsService(
            sourceRepository,
            new EpicAuthClient(new HttpClient()),
            new EpicAchievementsClient(new HttpClient()));
        var retroService = new RetroAchievementsAchievementsService(
            sourceRepository,
            new RetroAchievementsSettings(),
            new RetroAchievementsClient(new HttpClient()),
            new RetroAchievementsHashIndex(new RetroAchievementsClient(new HttpClient())));

        return new GameAchievementsService(steamService, epicService, retroService);
    }

    private sealed class StubSourceRepository : IRepository<GameSource>
    {
        private readonly Dictionary<string, GameSource> _byName = new(StringComparer.OrdinalIgnoreCase);

        public GameSource? Get(Guid id) =>
            _byName.Values.FirstOrDefault(source => source.Id == id);

        public IReadOnlyList<GameSource> GetAll() => _byName.Values.ToList();

        public void Add(GameSource item) => _byName[item.Name] = item;

        public void Update(GameSource item) => _byName[item.Name] = item;

        public bool Remove(Guid id)
        {
            var existing = Get(id);
            return existing is not null && _byName.Remove(existing.Name);
        }

        public GameSource GetOrCreateByName(string name)
        {
            if (_byName.TryGetValue(name, out var existing))
                return existing;

            existing = new GameSource { Name = name };
            _byName[name] = existing;
            return existing;
        }
    }
}
