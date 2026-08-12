using Bridge.Core.Entities;
using Bridge.Storage;
using Bridge.Storage.Repositories;
using Bridge.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Bridge.Tests.ViewModels;

public class GameEditViewModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly BridgeDbContext _context;
    private readonly GameRepository _gameRepository;
    private readonly Repository<Genre> _genreRepository;
    private readonly Repository<Company> _companyRepository;
    private readonly Repository<Platform> _platformRepository;

    public GameEditViewModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"bridge-gameedit-{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<BridgeDbContext>()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;
        _context = new BridgeDbContext(options);
        _context.Database.EnsureCreated();
        _gameRepository = new GameRepository(_context);
        _genreRepository = new Repository<Genre>(_context);
        _companyRepository = new Repository<Company>(_context);
        _platformRepository = new Repository<Platform>(_context);
    }

    private GameEditViewModel Build(Game game, bool isNew = false)
        => new(game, _gameRepository, _genreRepository, _companyRepository, _platformRepository, isNew);

    [Fact]
    public void Save_ReturnsFalseForEmptyName()
    {
        var game = new Game { Name = "Original" };
        var vm = Build(game);
        vm.Name = "   ";

        Assert.False(vm.Save());
    }

    [Fact]
    public void Save_TrimsNameAndPersists()
    {
        var game = new Game { Name = "Original" };
        _gameRepository.Add(game);
        var vm = Build(game);
        vm.Name = "  Half-Life 2  ";

        Assert.True(vm.Save());
        var reloaded = _gameRepository.Get(game.Id);
        Assert.Equal("Half-Life 2", reloaded!.Name);
    }

    [Fact]
    public void Save_OnNewGame_AddsAndSetsAdded()
    {
        var game = new Game();
        var vm = Build(game, isNew: true);
        vm.Name = "Manual Game";

        Assert.True(vm.Save());

        var reloaded = _gameRepository.Get(game.Id);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded.Added);
    }

    [Fact]
    public void Save_OnExistingGame_UpdatesWithoutChangingAdded()
    {
        var added = new DateTime(2020, 1, 1);
        var game = new Game { Name = "Old", Added = added };
        _gameRepository.Add(game);

        var vm = Build(game);
        vm.Name = "Renamed";

        Assert.True(vm.Save());
        var reloaded = _gameRepository.Get(game.Id);
        Assert.Equal("Renamed", reloaded!.Name);
        Assert.Equal(added, reloaded.Added);
    }

    [Fact]
    public void Save_PersistsSortingName()
    {
        var game = new Game { Name = "The Witcher 3" };
        _gameRepository.Add(game);
        var vm = Build(game);
        vm.SortingName = "Witcher 3";

        Assert.True(vm.Save());
        Assert.Equal("Witcher 3", _gameRepository.Get(game.Id)!.SortingName);
    }

    public void Dispose()
    {
        _context.Dispose();
        try { File.Delete(_dbPath); }
        catch { /* best-effort cleanup */ }
    }
}
