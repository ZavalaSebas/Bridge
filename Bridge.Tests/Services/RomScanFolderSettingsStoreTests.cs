using Bridge.Services;

namespace Bridge.Tests.Services;

public class RomScanFolderSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.RomScanFolderFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public RomScanFolderSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void Load_ReturnsNullWhenMissing()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Assert.Null(RomScanFolderSettingsStore.Load());
    }

    [Fact]
    public void SaveAndLoad_RoundTripsFolderPath()
    {
        RomScanFolderSettingsStore.Save(@"D:\Games\ROMs");

        Assert.Equal(@"D:\Games\ROMs", RomScanFolderSettingsStore.Load());
    }

    [Fact]
    public void Save_NullOrWhitespaceClearsFile()
    {
        RomScanFolderSettingsStore.Save(@"D:\Games\ROMs");
        RomScanFolderSettingsStore.Save("  ");

        Assert.Null(RomScanFolderSettingsStore.Load());
        Assert.False(File.Exists(_path));
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
