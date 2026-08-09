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

    public virtual IReadOnlyList<T> GetAll() => Set.ToList();

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
        // Normalize the search term (stored rows keep the name the caller gave
        // them). Lower() here translates to SQLite's ASCII-only lower(), i.e.
        // effectively ordinal — matching the rest of the app's OrdinalIgnoreCase
        // comparisons. No Trim meant "Action" vs "Action " created duplicates.
        var normalized = name.Trim();
        var existing = Set.FirstOrDefault(x => x.Name.Trim().ToLower() == normalized.ToLower());
        if (existing is not null)
        {
            return existing;
        }

        var created = new T { Name = normalized };
        Set.Add(created);
        Context.SaveChanges();
        return created;
    }
}
