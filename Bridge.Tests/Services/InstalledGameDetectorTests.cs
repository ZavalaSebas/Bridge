using Bridge.Services;

namespace Bridge.Tests.Services;

public class InstalledGameDetectorTests : IDisposable
{
    private readonly string _tempDir;

    public InstalledGameDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bridge-installed-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ScanFolder_FindsExeAndBatFiles()
    {
        var exePath = Path.Combine(_tempDir, "My Game.exe");
        var batPath = Path.Combine(_tempDir, "launcher.bat");
        File.WriteAllText(exePath, "exe");
        File.WriteAllText(batPath, "bat");

        var found = new InstalledGameDetector().ScanFolder(_tempDir);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, c => c.ExecutablePath.Equals(exePath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(found, c => c.ExecutablePath.Equals(batPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ScanFolder_IgnoresUnrelatedExtensions()
    {
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "text");
        File.WriteAllText(Path.Combine(_tempDir, "readme.md"), "md");

        var found = new InstalledGameDetector().ScanFolder(_tempDir);

        Assert.Empty(found);
    }

    [Theory]
    [InlineData("unins000.exe")]
    [InlineData("setup.exe")]
    [InlineData("vc_redist.x64.exe")]
    [InlineData("UnityCrashHandler64.exe")]
    [InlineData("python.exe")]
    public void ScanFolder_FiltersOutInstallersAndHelpers(string filename)
    {
        File.WriteAllText(Path.Combine(_tempDir, filename), "x");

        var found = new InstalledGameDetector().ScanFolder(_tempDir);

        Assert.Empty(found);
    }

    [Fact]
    public void ScanFolder_ScansSubfoldersRecursively()
    {
        var sub = Path.Combine(_tempDir, "sub", "deep");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "Deep Game.exe"), "exe");

        var found = new InstalledGameDetector().ScanFolder(_tempDir);

        Assert.Contains(found, c => c.Name == "Deep Game");
    }

    [Fact]
    public void ScanFolder_ThrowsForMissingFolder()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            new InstalledGameDetector().ScanFolder(Path.Combine(_tempDir, "does-not-exist")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
