using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
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
        var service = new InstalledGameImportService(new InstalledGameDetector(), repo);
        var result = service.ImportNewFromFolder(_tempDir);

        Assert.Single(result.Added);
        Assert.Equal("Cool Game", result.Added[0].Name);
        Assert.Equal(GameActionType.File, result.Added[0].GameActions[0].Type);
        Assert.Single(repo.Games);
    }

    [Fact]
    public void ImportNewFromFolder_SkipsAlreadyImportedPath()
    {
        var gameExe = Path.Combine(_tempDir, "Existing Game.exe");
        File.WriteAllBytes(gameExe, [0x4D, 0x5A]);

        var repo = new InMemoryGameRepository();
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

        var service = new InstalledGameImportService(new InstalledGameDetector(), repo);
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

        public void Update(Game game) { }

        public bool Remove(Guid id) => Games.RemoveAll(g => g.Id == id) > 0;

        public Game GetOrCreateByName(string name) => throw new NotSupportedException();

        public Game? FindByExternalId(string externalId, Guid sourceId) => null;
    }
}
