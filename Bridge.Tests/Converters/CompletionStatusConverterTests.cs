using System.Windows;
using Bridge.Converters;
using Bridge.Core.Entities;

namespace Bridge.Tests.Converters;

public class CompletionStatusConverterTests
{
    [Fact]
    public void EmptyGuidToVis_Empty_IsCollapsed()
    {
        var converter = new EmptyGuidToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, converter.Convert(Guid.Empty, typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void EmptyGuidToVis_Set_IsVisible()
    {
        var converter = new EmptyGuidToVisibilityConverter();
        Assert.Equal(Visibility.Visible, converter.Convert(Guid.NewGuid(), typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CompletionStatusIdToText_EmptyGuid_ReturnsEmpty()
    {
        var converter = new CompletionStatusIdToTextConverter();
        Assert.Equal(string.Empty, converter.Convert(Guid.Empty, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CompletionStatusToColor_StringStillWorks()
    {
        var converter = new CompletionStatusToColorConverter();
        var brush = converter.Convert(Bridge.Resources.Strings.CompletionStatusCompleted, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(brush);
    }

    [Fact]
    public void CompletionStatusId_RaisesPropertyChanged()
    {
        var game = new Game();
        string? changed = null;
        game.PropertyChanged += (_, e) => changed = e.PropertyName;

        game.CompletionStatusId = Guid.NewGuid();

        Assert.Equal(nameof(Game.CompletionStatusId), changed);
    }
}
