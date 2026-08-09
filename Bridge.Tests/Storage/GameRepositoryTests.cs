using Bridge.Core.Entities;
using Bridge.Storage;
using Bridge.Storage.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bridge.Tests.Storage;

// Each test gets its own SQLite file (not :memory: — the JSON-converter/EF
// mapping is exactly what needs verifying, and in-memory providers don't
// exercise the same code path as the real Sqlite provider). Disposed after
// every test so they don't leak files or interfere with each other.
public class GameRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContextOptions<BridgeDbContext> _options;
    private readonly BridgeDbContext _context;
    private readonly GameRepository _repository;

    public GameRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"bridge-test-{Guid.NewGuid()}.db");
        _options = new DbContextOptionsBuilder<BridgeDbContext>()
            // Pooling=False: without it, Microsoft.Data.Sqlite keeps the file
            // handle alive after Dispose() (connection pooling), and the
            // File.Delete in this class's own Dispose() intermittently throws
            // IOException — this bit real test runs, not just scratch scripts.
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;
        _context = new BridgeDbContext(_options);
        _context.Database.EnsureCreated();
        _repository = new GameRepository(_context);
    }

    [Fact]
    public void Add_ThenGet_RoundTripsAllFieldTypes()
    {
        var game = new Game
        {
            Name = "Test Game",
            ExternalId = "12345",
            SourceId = Guid.NewGuid(),
            GenreIds = [Guid.NewGuid()],
            ReleaseDate = new ReleaseDate(2024, 6, 15),
            GameActions = [new GameAction { Name = "Play", Path = "game.exe" }],
            Roms = [new GameRom { Name = "disc1", Path = @"C:\roms\game.iso" }],
            Links = [new Link { Name = "Store", Url = "https://example.com" }]
        };

        _repository.Add(game);

        // Fresh context, proves it round-trips from disk, not just the change tracker.
        using var freshContext = new BridgeDbContext(_options);
        var loaded = new GameRepository(freshContext).Get(game.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Test Game", loaded.Name);
        Assert.Single(loaded.GenreIds);
        Assert.Equal(new ReleaseDate(2024, 6, 15), loaded.ReleaseDate);
        Assert.Single(loaded.GameActions);
        Assert.Equal("game.exe", loaded.GameActions[0].Path);
        Assert.Single(loaded.Roms);
        Assert.Equal(@"C:\roms\game.iso", loaded.Roms[0].Path);
        Assert.Single(loaded.Links);
        Assert.Equal("https://example.com", loaded.Links[0].Url);
    }

    [Fact]
    public void IsCustomGame_IsFalse_WhenSourceIdIsSet()
    {
        var game = new Game { Name = "Sourced", SourceId = Guid.NewGuid() };
        Assert.False(game.IsCustomGame);
    }

    [Fact]
    public void IsCustomGame_IsTrue_WhenSourceIdIsManualDefault()
    {
        var game = new Game { Name = "Manual" };
        Assert.True(game.IsCustomGame);
    }

    [Fact]
    public void FindByExternalId_ResolvesTheDedupKey()
    {
        var sourceId = Guid.NewGuid();
        var game = new Game { Name = "Dedup Test", ExternalId = "abc", SourceId = sourceId };
        _repository.Add(game);

        var found = _repository.FindByExternalId("abc", sourceId);

        Assert.NotNull(found);
        Assert.Equal(game.Id, found.Id);
    }

    [Fact]
    public void FindByExternalId_ReturnsNull_WhenSourceIdDoesNotMatch()
    {
        var game = new Game { Name = "Dedup Test", ExternalId = "abc", SourceId = Guid.NewGuid() };
        _repository.Add(game);

        var found = _repository.FindByExternalId("abc", Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public void MutatingGameActionsInPlace_ThenUpdate_Persists()
    {
        var game = new Game { Name = "Action Test" };
        _repository.Add(game);

        var loaded = _repository.Get(game.Id)!;
        loaded.GameActions.Add(new GameAction { Name = "Play", Path = "notepad.exe" });
        _repository.Update(loaded);

        using var freshContext = new BridgeDbContext(_options);
        var reloaded = new GameRepository(freshContext).Get(game.Id)!;

        Assert.Single(reloaded.GameActions);
        Assert.Equal("notepad.exe", reloaded.GameActions[0].Path);
    }

    [Fact]
    public void Remove_DeletesTheGame()
    {
        var game = new Game { Name = "To Delete" };
        _repository.Add(game);

        var removed = _repository.Remove(game.Id);

        Assert.True(removed);
        Assert.Null(_repository.Get(game.Id));
    }

    [Fact]
    public void GetAll_ResetsTransientRuntimeFlags()
    {
        // A crash or forced close mid-game leaves IsRunning=true persisted (the
        // Game doc comment assigns Bridge.Storage's load path the reset). The
        // next session must not show the game as running forever.
        var game = new Game { Name = "Crashed mid-game", IsRunning = true, IsLaunching = true };
        _repository.Add(game);

        using var freshContext = new BridgeDbContext(_options);
        var reloaded = new GameRepository(freshContext).GetAll();

        var loaded = Assert.Single(reloaded);
        Assert.False(loaded.IsRunning);
        Assert.False(loaded.IsLaunching);
        Assert.False(loaded.IsInstalling);
        Assert.False(loaded.IsUninstalling);
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
