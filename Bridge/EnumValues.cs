using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using Bridge.Core.Enums;

namespace Bridge;

/// <summary>
/// Enum value lists exposed to XAML (x:Static / ObjectDataProvider) so
/// ComboBoxes can bind their ItemsSource to enum members.
/// </summary>
public static class EnumValues
{
    public static ReadOnlyCollection<LibraryFilterPreset> FilterPresets { get; } =
        new(Enum.GetValues<LibraryFilterPreset>());

    public static ReadOnlyCollection<GameSortField> SortFields { get; } =
        new(Enum.GetValues<GameSortField>());

    /// <summary>Display name for an enum member: Description attribute if present, else ToString().</summary>
    public static string GetDisplayName(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attr = field?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? value.ToString();
    }
}
