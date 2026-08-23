using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Core.Utilities;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class InstalledGameImportServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "BridgeImportTest_" + Guid.NewGuid());

    public InstalledGameImportServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ImportNewFromFolder_AddsOnlyNewExecutables()
    {
        var gameExe = Path.Combine(_tempDir, "Cool Game.exe");
        File.WriteAllBytes(gameExe, [0x4D, 0x5A]);

        var repo = new InMemoryGameRepository();
        var sources = new InMemorySourceRepository();
        var service = new InstalledGameImportService(new InstalledGameDetector(), repo, sources);
        var result = service.ImportNewFromFolder(_tempDir);

        Assert.Single(result.Added);
        Assert.Equal("Cool Game", result.Added[0].Name);
        Assert.Equal(GameActionType.File, result.Added[0].GameActions[0].Type);
        Assert.Equal(GameSource.BridgeId, result.Added[0].SourceId);
        Assert.Single(repo.Games);
    }

    [Fact]
    public void ImportNewFromFolder_SkipsAlreadyImportedPath()
    {
        var gameExe = Path.Combine(_tempDir, "Existing Game.exe");
        File.WriteAllBytes(gameExe, [0x4D, 0x5A]);

        var repo = new InMemoryGameRepository();
        var sources = new InMemorySourceRepository();
        repo.Games.Add(new Game
        {
            Name = "Existing Game",
            GameActions =
            {
                new GameAction
                {
                    Type = GameActionType.File,
                    Path = gameExe
                }
            }
        });

        var service = new InstalledGameImportService(new InstalledGameDetector(), repo, sources);
        var result = service.ImportNewFromFolder(_tempDir);

        Assert.Empty(result.Added);
        Assert.Single(repo.Games);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class InMemoryGameRepository : IGameRepository
    {
        public List<Game> Games { get; } = [];

        public IReadOnlyList<Game> GetAll() => Games;

        public Game? Get(Guid id) => Games.FirstOrDefault(g => g.Id == id);

        public void Add(Game game) => Games.Add(game);

        public void AddMany(IReadOnlyList<Game> games) => Games.AddRange(games);

        public void UpdateManyMetadataSyncMarkers(IReadOnlyList<Game> games, MetadataSyncMarker marker) { }

        public void Update(Game game) { }

        public bool Remove(Guid id) => Games.RemoveAll(g => g.Id == id) > 0;

        public Game GetOrCreateByName(string name) => throw new NotSupportedException();

        public Game? FindByExternalId(string externalId, Guid sourceId) => null;
    }

    private sealed class InMemorySourceRepository : IRepository<GameSource>
    {
        public List<GameSource> Sources { get; } = [];

        public GameSource? Get(Guid id) => Sources.FirstOrDefault(s => s.Id == id);

        public IReadOnlyList<GameSource> GetAll() => Sources;

        public void Add(GameSource item) => Sources.Add(item);

        public void Update(GameSource item) { }

        public bool Remove(Guid id) => Sources.RemoveAll(s => s.Id == id) > 0;

        public GameSource GetOrCreateByName(string name) => throw new NotSupportedException();
    }
}
