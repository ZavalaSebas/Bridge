using Bridge.Services;

namespace Bridge.Tests.Services;

public class WhatsNewSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.WhatsNewSeenFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public WhatsNewSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void Load_WhenMissing_ReturnsNull()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Assert.Null(WhatsNewSettingsStore.Load());
    }

    [Fact]
    public void Save_AndLoad_RoundTripsVersion()
    {
        WhatsNewSettingsStore.Save(new Version(0, 4, 0));

        var loaded = WhatsNewSettingsStore.Load();
        Assert.NotNull(loaded);
        Assert.Equal(new Version(0, 4, 0), loaded);
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
