using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bridge.Storage.Converters;

/// <summary>
/// EF Core can't map List&lt;Guid&gt;, List&lt;GameAction&gt;, etc. to plain SQLite
/// columns on its own. Rather than modeling every one of those as its own EF
/// owned-entity table (a lot of ceremony for what's really just "a list of small
/// values attached to a Game"), each such property is stored as one JSON text
/// column, serialized/deserialized on the way in and out. This is a deliberate
/// simplicity-over-normalization tradeoff for the MVP — if a property ever needs
/// to be queried/filtered on directly in SQL, split it out into a real table then,
/// not before.
/// </summary>
public class JsonValueConverter<T> : ValueConverter<T, string>
{
    public JsonValueConverter() : base(
        value => JsonSerializer.Serialize(value, JsonOptions),
        json => Deserialize(json))
    {
    }

    private static readonly JsonSerializerOptions JsonOptions = new();

    private static T Deserialize(string json)
    {
        // An empty JSON column — or a literal "null" written by some other tool —
        // means "no data". For collection properties (the common case —
        // List<GameAction>, List<Guid>, ...) that must round-trip as an empty
        // list, not null: a null list violates the entities' "always non-null
        // collections" contract and NREs on callers like MainViewModel's
        // GameActions.FirstOrDefault. For non-collection values (ReleaseDate?)
        // null is the correct result. Deserialize<T> can also legitimately return
        // null for a non-nullable T ("null" JSON or JsonIgnoreCondition), which is
        // why the ?? is applied on the parse result too.
        if (string.IsNullOrEmpty(json) || json == "null")
        {
            return CreateEmptyValue();
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? CreateEmptyValue();
    }

    private static T CreateEmptyValue() =>
        typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>)
            ? (T)Activator.CreateInstance(typeof(T))!
            : default!;
}
