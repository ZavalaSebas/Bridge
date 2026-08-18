using Bridge.Core.Entities;

namespace Bridge.Core.Contracts;

/// <summary>CRUD for reference entities (Genre, Tag, Platform, etc.).</summary>
public interface IRepository<T> where T : DatabaseObject
{
    T? Get(Guid id);
    IReadOnlyList<T> GetAll();
    void Add(T item);
    void Update(T item);
    bool Remove(Guid id);

    /// <summary>Find by name (case-insensitive) or create — used when importing metadata by name.</summary>
    T GetOrCreateByName(string name);
}
