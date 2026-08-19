using Bridge.Services;

namespace Bridge.Tests.Services;

public class WindowsStartupRegistrationTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Bridge\Bridge.exe", @"""C:\Program Files\Bridge\Bridge.exe""")]
    [InlineData(@"D:\Bridge.exe", @"""D:\Bridge.exe""")]
    public void FormatLaunchCommand_QuotesExecutablePath(string exePath, string expected) =>
        Assert.Equal(expected, WindowsStartupRegistration.FormatLaunchCommand(exePath));
}
