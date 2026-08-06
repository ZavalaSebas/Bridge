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
        if (string.IsNullOrEmpty(json))
        {
            return default!;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }
}
