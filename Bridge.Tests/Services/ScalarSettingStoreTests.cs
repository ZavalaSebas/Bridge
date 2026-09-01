using System.IO;
using Bridge.Services;

namespace Bridge.Tests.Services;

/// <summary>
/// Exercises the shared load/save core that every scalar <c>*SettingsStore</c>
/// now delegates to. This is the logic that used to be copy-pasted (and
/// untested) across ~two dozen stores; pinning it here covers the primary/legacy
/// fallback, trimming, and directory-creating save for all of them at once.
/// </summary>
public class ScalarSettingStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _primary;
    private readonly string _legacy;

    public ScalarSettingStoreTests()
    {
        _dir = Path.Combine(
            Path.GetTempPath(),
            "BridgeScalarSettingStoreTests",
            Guid.NewGuid().ToString("N"));
        _primary = Path.Combine(_dir, "setting.txt");
        _legacy = Path.Combine(_dir, "legacy.txt");
    }

    [Fact]
    public void Load_ReturnsFallback_WhenNeitherFileExists()
    {
        Assert.Equal("fallback", ScalarSettingStore.Load(_primary, _legacy, "fallback", TryIdentity));
    }

    [Fact]
    public void Load_ReadsPrimary_WhenPresent()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_primary, "true");

        Assert.True(ScalarSettingStore.Load(_primary, _legacy, false, bool.TryParse));
    }

    [Fact]
    public void Load_FallsBackToLegacy_WhenPrimaryMissing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_legacy, "true");

        Assert.True(ScalarSettingStore.Load(_primary, _legacy, false, bool.TryParse));
    }

    [Fact]
    public void Load_PrefersPrimary_OverLegacy()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_primary, "false");
        File.WriteAllText(_legacy, "true");

        Assert.False(ScalarSettingStore.Load(_primary, _legacy, true, bool.TryParse));
    }

    [Fact]
    public void Load_TrimsContent_BeforeParsing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_primary, "  true \r\n");

        Assert.True(ScalarSettingStore.Load(_primary, _legacy, false, bool.TryParse));
    }

    [Fact]
    public void Load_ReturnsFallback_WhenPrimaryInvalidAndNoLegacy()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_primary, "not-a-bool");

        Assert.True(ScalarSettingStore.Load(_primary, legacyPath: null, true, bool.TryParse));
    }

    [Fact]
    public void Load_HandlesNullLegacyPath()
    {
        Assert.Equal("fallback", ScalarSettingStore.Load(_primary, legacyPath: null, "fallback", TryIdentity));
    }

    [Fact]
    public void Save_WritesContent_AndCreatesMissingDirectory()
    {
        ScalarSettingStore.Save(_primary, "hello");

        Assert.True(File.Exists(_primary));
        Assert.Equal("hello", File.ReadAllText(_primary));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        ScalarSettingStore.Save(_primary, "Beta");

        Assert.Equal("Beta", ScalarSettingStore.Load(_primary, legacyPath: null, "Stable", TryIdentity));
    }

    private static bool TryIdentity(string raw, out string value)
    {
        value = raw;
        return raw.Length > 0;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
