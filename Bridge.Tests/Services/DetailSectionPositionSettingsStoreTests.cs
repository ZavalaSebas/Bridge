using Bridge.Services;

namespace Bridge.Tests.Services;

public class DetailSectionPositionSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.DetailSectionPositionFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public DetailSectionPositionSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void Load_DefaultsToRightWhenMissing()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Assert.Equal(DetailSectionPositionSettingsStore.Right, DetailSectionPositionSettingsStore.Load());
    }

    [Theory]
    [InlineData("left", DetailSectionPositionSettingsStore.Left)]
    [InlineData(" Right ", DetailSectionPositionSettingsStore.Right)]
    public void SaveAndLoad_RoundTripsKnownValues(string input, string expected)
    {
        DetailSectionPositionSettingsStore.Save(input);

        Assert.Equal(expected, DetailSectionPositionSettingsStore.Load());
    }

    [Theory]
    [InlineData("Top")]
    [InlineData("Center")]
    public void Normalize_InvalidValuesFallBackToRight(string input)
    {
        Assert.Equal(DetailSectionPositionSettingsStore.Right, DetailSectionPositionSettingsStore.Normalize(input));
    }

    [Fact]
    public void Save_IgnoresInvalidValues()
    {
        DetailSectionPositionSettingsStore.Save(DetailSectionPositionSettingsStore.Right);
        DetailSectionPositionSettingsStore.Save("Center");

        Assert.Equal(DetailSectionPositionSettingsStore.Right, DetailSectionPositionSettingsStore.Load());
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
