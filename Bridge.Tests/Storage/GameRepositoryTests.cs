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
        _context.MigrateToLatest();
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
    public void GetAll_SurvivesCorruptJsonColumns()
    {
        // Hand-edited DB / interrupted write: a JSON column holds garbage. The
        // load path must treat it as "no data", not crash the whole library.
        var game1 = new Game { Name = "Corrupt", ExternalId = "c1", SourceId = Guid.NewGuid() };
        game1.GameActions.Add(new GameAction { Name = "Play", Path = "a.exe" });
        var game2 = new Game { Name = "Corrupt2", ExternalId = "c2", SourceId = Guid.NewGuid() };
        game2.GenreIds.Add(Guid.NewGuid());
        _repository.Add(game1);
        _repository.Add(game2);

        // Rewrite the JSON columns to garbage behind EF's back (no braces — EF's
        // ExecuteSqlRaw treats {0} as a format placeholder).
        _context.Database.ExecuteSqlRaw(
            "UPDATE Games SET GameActions = '] broken json' WHERE Id = @p0",
            new Microsoft.Data.Sqlite.SqliteParameter("@p0", game1.Id));
        _context.Database.ExecuteSqlRaw(
            "UPDATE Games SET GenreIds = ']]] broken' WHERE Id = @p0",
            new Microsoft.Data.Sqlite.SqliteParameter("@p0", game2.Id));

        using var freshContext = new BridgeDbContext(_options);
        var games = new GameRepository(freshContext).GetAll();

        Assert.Contains(games, g => g.Name == "Corrupt");
        Assert.Contains(games, g => g.Name == "Corrupt2");
        Assert.Empty(games.First(g => g.Name == "Corrupt").GameActions);
        Assert.Empty(games.First(g => g.Name == "Corrupt2").GenreIds);
    }

    [Fact]
    public void GetAll_ResetsTransientRuntimeFlags_ButPreservesIsRunning()
    {
        // A crash or forced close mid-game leaves IsRunning=true persisted. The
        // stale-flag reset for IsRunning now happens once, in
        // MainViewModel.LoadGames on startup — ResetTransientFlags must NOT reset
        // IsRunning here, because the background metadata sync calls GetAll while
        // a game the user just launched has IsRunning=true (resetting it would
        // flip the hero button back to Play mid-game). The other transient flags
        // are still reset on every read.
        var game = new Game { Name = "Running right now", IsRunning = true, IsLaunching = true };
        _repository.Add(game);

        using var freshContext = new BridgeDbContext(_options);
        var reloaded = new GameRepository(freshContext).GetAll();

        var loaded = Assert.Single(reloaded);
        Assert.True(loaded.IsRunning); // preserved — it's a live launcher flag
        Assert.False(loaded.IsLaunching);
        Assert.False(loaded.IsInstalling);
        Assert.False(loaded.IsUninstalling);
    }

    [Fact]
    public void Update_PersistsFavoriteAndHidden()
    {
        var game = new Game { Name = "Flag Test" };
        _repository.Add(game);

        var loaded = _repository.Get(game.Id)!;
        loaded.Favorite = true;
        loaded.Hidden = true;
        _repository.Update(loaded);

        using var freshContext = new BridgeDbContext(_options);
        var reloaded = new GameRepository(freshContext).Get(game.Id)!;

        Assert.True(reloaded.Favorite);
        Assert.True(reloaded.Hidden);
    }

    [Fact]
    public void Update_PersistsUnfavoriting()
    {
        var game = new Game { Name = "Unfav Test", Favorite = true, Hidden = true };
        _repository.Add(game);

        var loaded = _repository.Get(game.Id)!;
        loaded.Favorite = false;
        loaded.Hidden = false;
        _repository.Update(loaded);

        using var freshContext = new BridgeDbContext(_options);
        var reloaded = new GameRepository(freshContext).Get(game.Id)!;

        Assert.False(reloaded.Favorite);
        Assert.False(reloaded.Hidden);
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
