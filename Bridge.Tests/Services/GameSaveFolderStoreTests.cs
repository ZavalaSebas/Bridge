using Bridge.Services;

namespace Bridge.Tests.Services;

[Collection(nameof(AppDataSettingsTestCollection))]
public class GameSaveFolderStoreTests : IDisposable
{
    private readonly string _path = Config.GameSaveFoldersFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public GameSaveFolderStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Fact]
    public void Get_ReturnsNullWhenMissing()
    {
        Assert.Null(GameSaveFolderStore.Get(Guid.NewGuid()));
    }

    [Fact]
    public void SetAndGet_RoundTripsFolder()
    {
        var id = Guid.NewGuid();
        GameSaveFolderStore.Set(id, @"D:\Saves\Hades");

        Assert.Equal(@"D:\Saves\Hades", GameSaveFolderStore.Get(id));
        Assert.Equal(@"D:\Saves\Hades", Assert.Contains(id, GameSaveFolderStore.GetAll()));
    }

    [Fact]
    public void Set_WhitespaceRemovesEntry()
    {
        var id = Guid.NewGuid();
        GameSaveFolderStore.Set(id, @"D:\Saves\Hades");
        GameSaveFolderStore.Set(id, "  ");

        Assert.Null(GameSaveFolderStore.Get(id));
        Assert.DoesNotContain(id, GameSaveFolderStore.GetAll().Keys);
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
