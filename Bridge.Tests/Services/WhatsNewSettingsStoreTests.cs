using Bridge.Services;

namespace Bridge.Tests.Services;

public class WhatsNewSettingsStoreTests : IDisposable
{
    private readonly string _path = Config.WhatsNewSeenFilePath;
    private readonly string _legacyPath = Path.Combine(Config.AppDataPath, "whats-new-seen.txt");
    private readonly bool _hadFile;
    private readonly string? _previousContents;
    private readonly bool _hadLegacyFile;
    private readonly string? _previousLegacyContents;

    public WhatsNewSettingsStoreTests()
    {
        _hadFile = File.Exists(_path);
        _previousContents = _hadFile ? File.ReadAllText(_path) : null;
        _hadLegacyFile = File.Exists(_legacyPath);
        _previousLegacyContents = _hadLegacyFile ? File.ReadAllText(_legacyPath) : null;
    }

    [Fact]
    public void Load_WhenMissing_ReturnsNull()
    {
        DeleteBoth();

        Assert.Null(WhatsNewSettingsStore.Load());
    }

    [Fact]
    public void Save_AndLoad_RoundTripsVersion()
    {
        DeleteBoth();
        WhatsNewSettingsStore.Save(new Version(0, 4, 0));

        var loaded = WhatsNewSettingsStore.Load();
        Assert.NotNull(loaded);
        Assert.Equal(new Version(0, 4, 0), loaded);
    }

    public void Dispose()
    {
        RestoreFile(_path, _hadFile, _previousContents);
        RestoreFile(_legacyPath, _hadLegacyFile, _previousLegacyContents);
    }

    private void DeleteBoth()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        if (File.Exists(_legacyPath))
            File.Delete(_legacyPath);
    }

    private static void RestoreFile(string path, bool hadFile, string? previousContents)
    {
        if (hadFile && previousContents is not null)
            File.WriteAllText(path, previousContents);
        else if (File.Exists(path))
            File.Delete(path);
    }
}
