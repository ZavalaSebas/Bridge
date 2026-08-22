using Bridge.Services;

namespace Bridge.Tests.Services;

public class TranslucentBackgroundSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.TranslucentBackgroundFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public TranslucentBackgroundSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void Load_DefaultsToTrueWhenMissing()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Assert.True(TranslucentBackgroundSettingsStore.Load());
    }

    [Fact]
    public void SaveAndLoad_RoundTripsFalse()
    {
        TranslucentBackgroundSettingsStore.Save(false);

        Assert.False(TranslucentBackgroundSettingsStore.Load());
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
