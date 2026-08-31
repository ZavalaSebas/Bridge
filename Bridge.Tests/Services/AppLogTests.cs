using Bridge.Services;

namespace Bridge.Tests.Services;

public class AppLogTests
{
    [Fact]
    public void LoggingMethods_NeverThrow()
    {
        // AppLog's single hard guarantee: logging a failure can't itself throw
        // (it's called from swallowed-exception paths and global crash handlers).
        var exception = Record.Exception(() =>
        {
            AppLog.Info("unit-test info");
            AppLog.Warn("unit-test warn");
            AppLog.Warn("unit-test warn with exception", new InvalidOperationException("boom"));
            AppLog.Error("unit-test error", new InvalidOperationException("boom"));
        });

        Assert.Null(exception);
    }
}
