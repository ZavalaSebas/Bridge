using Bridge.Services;

namespace Bridge.Tests.Services;

public class InstalledScanFolderSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.InstalledScanFolderFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public InstalledScanFolderSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void Load_ReturnsNullWhenMissing()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Assert.Null(InstalledScanFolderSettingsStore.Load());
    }

    [Fact]
    public void SaveAndLoad_RoundTripsFolderPath()
    {
        InstalledScanFolderSettingsStore.Save(@"D:\Games\Installed");

        Assert.Equal(@"D:\Games\Installed", InstalledScanFolderSettingsStore.Load());
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
