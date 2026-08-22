using Bridge.Services;

namespace Bridge.Tests.Services;

public class SidebarTranslucentSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.SidebarTranslucentFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public SidebarTranslucentSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void Load_DefaultsToFalseWhenMissing()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Assert.False(SidebarTranslucentSettingsStore.Load());
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTrue()
    {
        SidebarTranslucentSettingsStore.Save(true);

        Assert.True(SidebarTranslucentSettingsStore.Load());
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
