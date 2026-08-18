using Bridge.Core.Entities;
using Bridge.Storage;
using Bridge.Storage.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bridge.Tests.Storage;

public class RepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly BridgeDbContext _context;
    private readonly Repository<Genre> _repository;

    public RepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"bridge-test-{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<BridgeDbContext>()
            // Pooling=False — see the comment on the same line in GameRepositoryTests.cs.
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;
        _context = new BridgeDbContext(options);
        _context.MigrateToLatest();
        _repository = new Repository<Genre>(_context);
    }

    [Fact]
    public void GetOrCreateByName_CreatesOnce_ThenReturnsSameEntity()
    {
        var first = _repository.GetOrCreateByName("Action");
        var second = _repository.GetOrCreateByName("Action");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(_repository.GetAll());
    }

    [Fact]
    public void GetOrCreateByName_IsCaseInsensitive()
    {
        var first = _repository.GetOrCreateByName("Action");
        var second = _repository.GetOrCreateByName("action");

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void GetOrCreateByName_DifferentNames_CreatesDistinctEntities()
    {
        var action = _repository.GetOrCreateByName("Action");
        var rpg = _repository.GetOrCreateByName("RPG");

        Assert.NotEqual(action.Id, rpg.Id);
        Assert.Equal(2, _repository.GetAll().Count);
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
