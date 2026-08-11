using System.Globalization;
using System.Windows.Data;
using Bridge.Core.Entities;

namespace Bridge.Converters;

/// <summary>
/// Feeds the details description ItemsControl: returns the game's ordered
/// DescriptionBlocks when it has them, otherwise falls back to a single text
/// block with the plain Description (older games imported before the blocks
/// column existed), so the renderer always has a uniform block list.
/// </summary>
public class DescriptionBlocksConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Game game)
            return Array.Empty<DescriptionBlock>();

        if (game.DescriptionBlocks is { Count: > 0 } blocks)
            return blocks;

        if (!string.IsNullOrWhiteSpace(game.Description))
            return new[] { new DescriptionBlock { Text = game.Description } };

        return Array.Empty<DescriptionBlock>();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
