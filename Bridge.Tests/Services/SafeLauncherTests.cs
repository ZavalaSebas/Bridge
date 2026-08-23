using Bridge.Core.Utilities;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class SafeLauncherTests
{
    [Theory]
    [InlineData("https://store.steampowered.com/app/570", true)]
    [InlineData("steam://rungameid/570", true)]
    [InlineData("com.epicgames.launcher://store/library", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    public void UrlValidator_respects_allowlist(string url, bool expectedSafe)
    {
        Assert.Equal(expectedSafe, UrlValidator.IsSafeToOpen(url));
    }

    [Theory]
    [InlineData(@"""C:\Program Files\Game\uninstall.exe"" /S", @"C:\Program Files\Game\uninstall.exe", "/S")]
    [InlineData("C:\\Game\\uninstall.exe /S", @"C:\Game\uninstall.exe", "/S")]
    [InlineData("cmd.exe /c del game", null, null)]
    [InlineData("powershell -Command Remove-Item game", null, null)]
    public void TryParseUninstallCommand_parses_or_rejects(string command, string? expectedFile, string? expectedArgs)
    {
        var parsed = SafeLauncher.TryParseUninstallCommand(command, out var fileName, out var arguments);

        if (expectedFile is null)
        {
            Assert.False(parsed);
            return;
        }

        Assert.True(parsed);
        Assert.Equal(expectedFile, fileName);
        Assert.Equal(expectedArgs, arguments);
    }

    [Fact]
    public void TryOpenDirectory_ReturnsFalseWhenMissing()
    {
        Assert.False(SafeLauncher.TryOpenDirectory(Path.Combine(Path.GetTempPath(), "bridge-missing-" + Guid.NewGuid())));
        Assert.False(SafeLauncher.TryOpenDirectory(null));
        Assert.False(SafeLauncher.TryOpenDirectory(""));
    }
}
