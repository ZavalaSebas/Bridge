using System.Globalization;
using System.Windows.Data;
using Bridge.Core.Entities;
using Bridge.Resources;

namespace Bridge.Converters;

/// <summary>
/// Resolves play-button label/symbol for a library row: selected games mirror
/// <see cref="ViewModels.MainViewModel.PlayButtonText"/> (Download/Downloading/Play/Stop);
/// other rows show Play/Stop from <see cref="Game.IsRunning"/> only.
/// </summary>
public class PlayButtonDisplayConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4 || values[0] is not Game game)
            return null;

        var selected = values[1] as Game;
        var playText = values[2] as string ?? Strings.Play;
        var playSymbol = values[3] as string ?? "Play24";
        var useSymbol = string.Equals(parameter as string, "Symbol", StringComparison.OrdinalIgnoreCase);

        if (selected is not null && selected.Id == game.Id)
            return useSymbol ? playSymbol : playText;

        if (game.IsRunning)
            return useSymbol ? "Stop24" : Strings.Stop;

        if (game.NeedsEmulatorDownload)
            return useSymbol ? "ArrowDownload24" : Strings.Download;

        return useSymbol ? "Play24" : Strings.Play;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
