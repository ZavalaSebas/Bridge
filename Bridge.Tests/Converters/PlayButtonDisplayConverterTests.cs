using System.Globalization;
using Bridge.Converters;
using Bridge.Core.Entities;
using Bridge.Resources;

namespace Bridge.Tests.Converters;

public class PlayButtonDisplayConverterTests
{
    private readonly PlayButtonDisplayConverter _converter = new();

    [Fact]
    public void Selected_game_mirrors_play_button_text_and_symbol()
    {
        var game = new Game { Id = Guid.NewGuid(), Name = "Selected" };
        var text = _converter.Convert(
            [game, game, Strings.Download, "ArrowDownload24"],
            typeof(string),
            null!,
            CultureInfo.InvariantCulture);
        var symbol = _converter.Convert(
            [game, game, Strings.Download, "ArrowDownload24"],
            typeof(string),
            "Symbol",
            CultureInfo.InvariantCulture);

        Assert.Equal(Strings.Download, text);
        Assert.Equal("ArrowDownload24", symbol);
    }

    [Fact]
    public void Non_selected_running_game_shows_stop()
    {
        var game = new Game { Id = Guid.NewGuid(), Name = "Running", IsRunning = true };
        var selected = new Game { Id = Guid.NewGuid(), Name = "Other" };

        var text = _converter.Convert(
            [game, selected, Strings.Play, "Play24"],
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal(Strings.Stop, text);
    }

    [Fact]
    public void Non_selected_managed_rom_needing_install_shows_download()
    {
        var game = new Game { Id = Guid.NewGuid(), Name = "ROM", NeedsEmulatorDownload = true };
        var selected = new Game { Id = Guid.NewGuid(), Name = "Other" };

        var text = _converter.Convert(
            [game, selected, Strings.Play, "Play24"],
            typeof(string),
            null!,
            CultureInfo.InvariantCulture);
        var symbol = _converter.Convert(
            [game, selected, Strings.Play, "Play24"],
            typeof(string),
            "Symbol",
            CultureInfo.InvariantCulture);

        Assert.Equal(Strings.Download, text);
        Assert.Equal("ArrowDownload24", symbol);
    }
}
