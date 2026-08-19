using Bridge.Services;

namespace Bridge.Tests.Services;

public class DetailPanelPositionSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.DetailPanelPositionFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public DetailPanelPositionSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void Load_DefaultsToRightWhenMissing()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Assert.Equal(DetailPanelPositionSettingsStore.Right, DetailPanelPositionSettingsStore.Load());
    }

    [Theory]
    [InlineData("left", DetailPanelPositionSettingsStore.Left)]
    [InlineData(" Right ", DetailPanelPositionSettingsStore.Right)]
    public void SaveAndLoad_RoundTripsKnownValues(string input, string expected)
    {
        DetailPanelPositionSettingsStore.Save(input);

        Assert.Equal(expected, DetailPanelPositionSettingsStore.Load());
    }

    [Theory]
    [InlineData("Top")]
    [InlineData("Bottom")]
    [InlineData("Center")]
    public void Normalize_LegacyOrInvalidValuesFallBackToRight(string input)
    {
        Assert.Equal(DetailPanelPositionSettingsStore.Right, DetailPanelPositionSettingsStore.Normalize(input));
    }

    [Fact]
    public void Save_IgnoresInvalidValues()
    {
        DetailPanelPositionSettingsStore.Save(DetailPanelPositionSettingsStore.Left);
        DetailPanelPositionSettingsStore.Save("Center");

        Assert.Equal(DetailPanelPositionSettingsStore.Left, DetailPanelPositionSettingsStore.Load());
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
