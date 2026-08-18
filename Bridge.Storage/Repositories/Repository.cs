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
public class Repository<T>(IDbContextFactory<BridgeDbContext> factory) : IRepository<T> where T : DatabaseObject, new()
{
    public virtual T? Get(Guid id)
    {
        using var context = factory.CreateDbContext();
        return context.Set<T>().Find(id);
    }

    public virtual IReadOnlyList<T> GetAll()
    {
        using var context = factory.CreateDbContext();
        return context.Set<T>().AsNoTracking().ToList();
    }

    public void Add(T item)
    {
        using var context = factory.CreateDbContext();
        context.Set<T>().Add(item);
        context.SaveChanges();
    }

    public void Update(T item)
    {
        using var context = factory.CreateDbContext();
        context.Set<T>().Update(item);
        context.SaveChanges();
    }

    public bool Remove(Guid id)
    {
        using var context = factory.CreateDbContext();
        var set = context.Set<T>();
        var entity = set.Find(id);
        if (entity is null)
        {
            return false;
        }

        set.Remove(entity);
        context.SaveChanges();
        return true;
    }

    public T GetOrCreateByName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException("Reference entity name cannot be empty.", nameof(name));

        using var context = factory.CreateDbContext();
        var set = context.Set<T>();
        var existing = set.FirstOrDefault(x => x.Name.Trim().ToLower() == normalized.ToLower());
        if (existing is not null)
        {
            return existing;
        }

        var created = new T { Name = normalized };
        try
        {
            set.Add(created);
            context.SaveChanges();
            return created;
        }
        catch (DbUpdateException)
        {
            // Another import/metadata pass may have inserted the same name concurrently.
            context.Entry(created).State = EntityState.Detached;
            return set.First(x => x.Name.Trim().ToLower() == normalized.ToLower());
        }
    }
}
