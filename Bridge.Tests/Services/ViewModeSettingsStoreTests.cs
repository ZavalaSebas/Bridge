using Bridge.Core.Enums;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class ViewModeLegacyMigrationTests
{
    [Theory]
    [InlineData("Grid", "Covers")]
    [InlineData(" covers ", "Covers")]
    [InlineData("List", "List")]
    [InlineData("Table", "Table")]
    public void ViewModeSettingsStore_NormalizeLegacyName_MapsKnownValues(string input, string expected)
    {
        Assert.Equal(expected, ViewModeSettingsStore.NormalizeLegacyName(input));
    }

    [Fact]
    public void ViewModeSettingsStore_NormalizeLegacyName_AllowsCoversEnumParse()
    {
        var normalized = ViewModeSettingsStore.NormalizeLegacyName("Grid");
        Assert.True(Enum.TryParse<ViewMode>(normalized, out var mode));
        Assert.Equal(ViewMode.Covers, mode);
    }

    [Theory]
    [InlineData("Grid", "Covers")]
    [InlineData("Covers", "Covers")]
    [InlineData("List", "List")]
    public void ScrollPositionSettingsStore_NormalizeLegacyViewKey_MapsKnownValues(string input, string expected)
    {
        Assert.Equal(expected, ScrollPositionSettingsStore.NormalizeLegacyViewKey(input));
    }
}
