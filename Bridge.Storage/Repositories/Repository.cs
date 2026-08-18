using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bridge.Storage.Repositories;

/// <summary>
/// Generic implementation of IRepository&lt;T&gt; — covers every reference
/// entity (Genre, Category, Tag, Series, AgeRating, GameFeature, Company,
/// Region, Platform, GameSource, CompletionStatus). Each write commits
/// immediately (no unit-of-work/batching) — simplest thing that works for the
/// MVP; see the note on IRepository.cs about adding batching later if profiling
/// ever shows it's needed, not before.
/// </summary>
public class Repository<T>(BridgeDbContext context) : IRepository<T> where T : DatabaseObject, new()
{
    protected readonly BridgeDbContext Context = context;
    protected readonly DbSet<T> Set = context.Set<T>();

    public virtual T? Get(Guid id) => Set.Find(id);

    public virtual IReadOnlyList<T> GetAll() => Set.AsNoTracking().ToList();

    public void Add(T item)
    {
        Set.Add(item);
        Context.SaveChanges();
    }

    public void Update(T item)
    {
        Set.Update(item);
        Context.SaveChanges();
    }

    public bool Remove(Guid id)
    {
        var entity = Set.Find(id);
        if (entity is null)
        {
            return false;
        }

        Set.Remove(entity);
        Context.SaveChanges();
        return true;
    }

    public T GetOrCreateByName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException("Reference entity name cannot be empty.", nameof(name));

        var existing = Set.FirstOrDefault(x => x.Name.Trim().ToLower() == normalized.ToLower());
        if (existing is not null)
        {
            return existing;
        }

        var created = new T { Name = normalized };
        try
        {
            Set.Add(created);
            Context.SaveChanges();
            return created;
        }
        catch (DbUpdateException)
        {
            // Another import/metadata pass may have inserted the same name concurrently.
            Context.Entry(created).State = EntityState.Detached;
            return Set.First(x => x.Name.Trim().ToLower() == normalized.ToLower());
        }
    }
}
