using Bridge.Services;

namespace Bridge.Tests.Services;

public class SetupCompleteSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.SetupCompleteFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public SetupCompleteSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void IsComplete_ReturnsFalseWhenMissing()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Assert.False(SetupCompleteSettingsStore.IsComplete());
    }

    [Fact]
    public void MarkComplete_PersistsTrue()
    {
        SetupCompleteSettingsStore.MarkComplete();

        Assert.True(SetupCompleteSettingsStore.IsComplete());
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
