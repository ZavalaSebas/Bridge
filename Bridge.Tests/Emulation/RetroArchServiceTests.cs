using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Emulation;

namespace Bridge.Tests.Emulation;

public class RetroArchServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly EmulationPaths _paths;
    private readonly RetroArchService _service;

    public RetroArchServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"bridge-retroarch-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);
        _paths = new EmulationPaths(
            Path.Combine(_tempRoot, "install"),
            Path.Combine(_tempRoot, "download"),
            Path.Combine(_tempRoot, "version.txt"));
        _service = new RetroArchService(
            new EmptyRepository<Emulator>(),
            new EmptyRepository<Platform>(),
            new HttpClient(),
            _paths);
    }

    [Fact]
    public void IsManagedRom_TrueWhenBridgeRetroArchActionPresent()
    {
        var game = new Game();
        game.GameActions.Add(new GameAction
        {
            Type = GameActionType.Emulator,
            Name = "Bridge RetroArch",
            IsPlayAction = true
        });

        Assert.True(_service.IsManagedRom(game));
    }

    [Fact]
    public void IsManagedRom_FalseForManualGames() =>
        Assert.False(_service.IsManagedRom(new Game()));

    [Fact]
    public void NeedsInstall_TrueWhenFrontendMissing()
    {
        var platformId = Guid.NewGuid();
        var game = ManagedGame(platformId);
        var service = new RetroArchService(
            new EmptyRepository<Emulator>(),
            new StubPlatformRepository(platformId, "Nintendo Entertainment System"),
            new HttpClient(),
            _paths);

        Assert.True(service.NeedsInstall(game));
    }

    private static Game ManagedGame(Guid platformId)
    {
        var game = new Game { PlatformIds = [platformId] };
        game.GameActions.Add(new GameAction
        {
            Type = GameActionType.Emulator,
            Name = "Bridge RetroArch",
            IsPlayAction = true
        });
        return game;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Best effort cleanup for temp test dirs.
        }
    }

    private sealed class EmptyRepository<T> : IRepository<T> where T : DatabaseObject
    {
        public T? Get(Guid id) => null;
        public IReadOnlyList<T> GetAll() => [];
        public void Add(T entity) { }
        public void Update(T entity) { }
        public bool Remove(Guid id) => false;
        public T GetOrCreateByName(string name) => throw new NotSupportedException();
    }

    private sealed class StubPlatformRepository(Guid id, string name) : IRepository<Platform>
    {
        public Platform? Get(Guid platformId) =>
            platformId == id ? new Platform { Id = id, Name = name } : null;

        public IReadOnlyList<Platform> GetAll() => [Get(id)!];
        public void Add(Platform entity) { }
        public void Update(Platform entity) { }
        public bool Remove(Guid platformId) => false;
        public Platform GetOrCreateByName(string name) => throw new NotSupportedException();
    }
}
