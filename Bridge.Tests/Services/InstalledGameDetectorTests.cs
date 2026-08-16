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
        var batPath = Path.Combine(_tempDir, "start.bat");
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
    [InlineData("HYP.exe")]
    [InlineData("HYPHelper.exe")]
    [InlineData("HYUpdater.exe")]
    [InlineData("launcher_epic.exe")]
    [InlineData("crashreport.exe")]
    [InlineData("upload_crash.exe")]
    [InlineData("BeyondEditor.exe")]
    [InlineData("ZFGameBrowser.exe")]
    [InlineData("7z.exe")]
    [InlineData("hpatchz.exe")]
    [InlineData("breakpad_server.exe")]
    [InlineData("dotnetfx3.exe")]
    [InlineData("vcredist_x86.exe")]
    [InlineData("Fallout3Launcher.exe")]
    [InlineData("launcher.exe")]
    [InlineData("EasyAntiCheat_Setup.exe")]
    [InlineData("Rockstar-Games-Launcher.exe")]
    [InlineData("UbisoftConnectInstaller.exe")]
    [InlineData("Fallout3 - Garden of Eden Creation Kit.exe")]
    [InlineData("UnrealCEFSubProcess.exe")]
    [InlineData("CrashSender.exe")]
    [InlineData("crashpad_handler.exe")]
    [InlineData("crs-handler.exe")]
    [InlineData("crs-uploader.exe")]
    [InlineData("miniTicketDbg.exe")]
    [InlineData("nw.exe")]
    [InlineData("UE3Redist.exe")]
    [InlineData("D3D11Install_2010.exe")]
    [InlineData("install_pspc_sdk_runtime.bat")]
    [InlineData("install-kbupdate.bat")]
    [InlineData("runme.exe")]
    [InlineData("testapp.exe")]
    [InlineData("clean.bat")]
    [InlineData("show_third_party_software_licenses.bat")]
    [InlineData("Benchmark.exe")]
    [InlineData("ForzaProtocolSelector.exe")]
    [InlineData("NvProfileFixer.exe")]
    [InlineData("msedgewebview2.exe")]
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

    [Theory]
    [InlineData("DaysGone.exe", "Days Gone")]
    [InlineData("AlanWake.exe", "Alan Wake")]
    [InlineData("NARUTO-Win64-Shipping.exe", "NARUTO")]
    [InlineData("GameDevTycoon.exe", "Game Dev Tycoon")]
    [InlineData("watch_dogs.exe", "watch dogs")]
    [InlineData("Fallout3.exe", "Fallout3")]
    public void ScanFolder_ProducesReadableNames(string filename, string expectedName)
    {
        File.WriteAllText(Path.Combine(_tempDir, filename), "x");

        var found = new InstalledGameDetector().ScanFolder(_tempDir);

        var candidate = Assert.Single(found);
        Assert.Equal(expectedName, candidate.Name);
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
