using Bridge.Services;

namespace Bridge.Tests.Services;

public class UserProfileSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.UserProfileFilePath;
    private readonly bool _hadFile;
    private readonly string? _previousContents;

    public UserProfileSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
    }

    [Fact]
    public void SaveAndLoad_RoundTripsProfile()
    {
        var profile = new UserProfile
        {
            DisplayName = "Alex",
            DefaultAvatarId = "purple",
            UseCustomAvatar = false
        };

        UserProfileSettingsStore.Save(profile);
        var loaded = UserProfileSettingsStore.Load();

        Assert.Equal("Alex", loaded.DisplayName);
        Assert.Equal("purple", loaded.DefaultAvatarId);
        Assert.False(loaded.UseCustomAvatar);
    }

    public void Dispose()
    {
        if (_hadFile && _previousContents is not null)
            File.WriteAllText(_path, _previousContents);
        else if (File.Exists(_path))
            File.Delete(_path);
    }
}
