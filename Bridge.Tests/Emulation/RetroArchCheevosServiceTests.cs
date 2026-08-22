using Bridge.Emulation;

namespace Bridge.Tests.Emulation;

public class RetroArchCheevosServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public RetroArchCheevosServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"bridge_cheevos_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task ApplyLaunchConfigAsync_WritesCheevosCredentials()
    {
        var exePath = Path.Combine(_tempRoot, "retroarch.exe");
        await File.WriteAllTextAsync(exePath, string.Empty);
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "retroarch.cfg"), "foo = \"bar\"\n");

        var service = new RetroArchCheevosService();
        await service.ApplyLaunchConfigAsync(
            exePath,
            new RetroArchCheevosCredentials("player", "secret", "", false));

        var config = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "retroarch.cfg"));
        Assert.Contains("cheevos_enable = \"true\"", config);
        Assert.Contains("cheevos_username = \"player\"", config);
        Assert.Contains("cheevos_password = \"secret\"", config);
        Assert.Contains("saveconfig_on_exit = \"true\"", config);
        Assert.DoesNotContain("cheevos_token", config);
    }

    [Fact]
    public async Task ApplyLaunchConfigAsync_PrefersStoredTokenOverPassword()
    {
        var exePath = Path.Combine(_tempRoot, "retroarch.exe");
        await File.WriteAllTextAsync(exePath, string.Empty);

        var service = new RetroArchCheevosService();
        await service.ApplyLaunchConfigAsync(
            exePath,
            new RetroArchCheevosCredentials("player", "secret", "token123", false));

        var config = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "retroarch.cfg"));
        Assert.Contains("cheevos_token = \"token123\"", config);
        Assert.DoesNotContain("cheevos_password", config);
    }

    [Fact]
    public void TryReadBackToken_ReturnsTokenFromConfig()
    {
        var exePath = Path.Combine(_tempRoot, "retroarch.exe");
        File.WriteAllText(Path.Combine(_tempRoot, "retroarch.cfg"), "cheevos_token = \"abc123\"\n");

        var service = new RetroArchCheevosService();
        Assert.True(service.TryReadBackToken(exePath, out var token));
        Assert.Equal("abc123", token);
    }
}
