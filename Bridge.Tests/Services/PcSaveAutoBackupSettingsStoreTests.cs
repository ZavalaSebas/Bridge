using Bridge.Services;

namespace Bridge.Tests.Services;

[Collection(nameof(AppDataSettingsTestCollection))]
public class PcSaveAutoBackupSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.PcSaveAutoBackupFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public PcSaveAutoBackupSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void Load_DefaultsToTrueWhenMissing()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Assert.True(PcSaveAutoBackupSettingsStore.Load());
    }

    [Fact]
    public void SaveAndLoad_RoundTripsFalse()
    {
        PcSaveAutoBackupSettingsStore.Save(false);

        Assert.False(PcSaveAutoBackupSettingsStore.Load());
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
