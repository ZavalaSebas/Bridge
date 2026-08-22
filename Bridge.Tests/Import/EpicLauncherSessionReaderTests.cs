using System.Text;
using System.Text.Json;
using Bridge.Import.Epic;

namespace Bridge.Tests.Import;

public class EpicLauncherSessionReaderTests : IDisposable
{
    private readonly string _configDir;

    public EpicLauncherSessionReaderTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), $"bridge-epic-session-{Guid.NewGuid()}");
        Directory.CreateDirectory(_configDir);
    }

    [Fact]
    public void TryReadSession_ReadsPlainJsonRememberMePayload()
    {
        var refreshToken = "refresh-token-123";
        var json = JsonSerializer.Serialize(new[] { new { Token = refreshToken } });
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        WriteIni(encoded);

        var session = EpicLauncherSessionReader.TryReadSession(_configDir);

        Assert.NotNull(session);
        Assert.Equal(refreshToken, session!.RefreshToken);
    }

    [Fact]
    public void TryReadSession_ReturnsNullWhenRememberMeMissing()
    {
        File.WriteAllText(Path.Combine(_configDir, "GameUserSettings.ini"), "[Other]\nData=abc");

        Assert.Null(EpicLauncherSessionReader.TryReadSession(_configDir));
    }

    [Fact]
    public void TryReadSession_ReadsFromWindowsEditorDirectory()
    {
        var refreshToken = "refresh-token-editor";
        var json = JsonSerializer.Serialize(new[] { new { Token = refreshToken } });
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var windowsEditorDir = Path.Combine(_configDir, "WindowsEditor");
        Directory.CreateDirectory(windowsEditorDir);
        File.WriteAllText(
            Path.Combine(windowsEditorDir, "GameUserSettings.ini"),
            $$"""
            [RememberMe]
            Data={{encoded}}
            """);

        var session = EpicLauncherSessionReader.TryReadSession(windowsEditorDir);

        Assert.NotNull(session);
        Assert.Equal(refreshToken, session!.RefreshToken);
    }

    private void WriteIni(string encodedData)
    {
        File.WriteAllText(
            Path.Combine(_configDir, "GameUserSettings.ini"),
            $$"""
            [RememberMe]
            Data={{encodedData}}
            """);
    }

    public void Dispose() => Directory.Delete(_configDir, recursive: true);
}
