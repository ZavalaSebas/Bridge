using System.Net;
using System.Net.Http;
using Bridge.Core.Entities;
using Bridge.Emulation;
using Bridge.Tests.Metadata;

namespace Bridge.Tests.Emulation;

public class RetroArchCheatServiceTests : IDisposable
{
    private const string ValidCheatFile = """
        cheats = 1

        cheat0_desc = "Infinite Lives"
        cheat0_code = "AAAAAAAA"
        cheat0_enable = false
        """;

    private readonly string _cheatsDirectory;

    public RetroArchCheatServiceTests()
    {
        _cheatsDirectory = Path.Combine(Path.GetTempPath(), $"bridge_test_cheats_{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_cheatsDirectory))
        {
            Directory.Delete(_cheatsDirectory, recursive: true);
        }
    }

    private static Game MakeGame() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Game"
    };

    private static Game MakeGameWithRom(string romFileName)
    {
        var game = MakeGame();
        game.Roms.Add(new GameRom { Path = Path.Combine(@"C:\roms", romFileName) });
        return game;
    }

    private static RomPlatformDefinition NesPlatform =>
        RomPlatformCatalog.FindByPlatformName("Nintendo Entertainment System")!;

    private static RomPlatformDefinition WonderSwanPlatform =>
        RomPlatformCatalog.FindByPlatformName("WonderSwan / WonderSwan Color")!;

    private RetroArchCheatService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new FakeHttpMessageHandler(responder)), _cheatsDirectory);

    private string GetCoreDir(Game game) =>
        Path.Combine(_cheatsDirectory, game.Id.ToString(), "FCEUmm");

    [Fact]
    public async Task LoadCheatsAsync_UnsupportedPlatform_ReturnsPlatformNotSupportedWithoutHttpCall()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be called"));
        var game = MakeGameWithRom("Test Game.nes");

        var result = await service.LoadCheatsAsync(game, WonderSwanPlatform);

        Assert.Equal(CheatFetchOutcome.PlatformNotSupported, result.Outcome);
    }

    [Fact]
    public async Task LoadCheatsAsync_FetchSucceeds_PersistsFileAndSourceSidecar()
    {
        var service = CreateService(req => req.RequestUri!.AbsoluteUri.Contains("Test%20Game.cht")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ValidCheatFile) }
            : throw new InvalidOperationException($"Unexpected request: {req.RequestUri}"));
        var game = MakeGameWithRom("Test Game.nes");

        var result = await service.LoadCheatsAsync(game, NesPlatform);

        Assert.Equal(CheatFetchOutcome.Success, result.Outcome);
        Assert.Single(result.Cheats);
        Assert.Equal("Infinite Lives", result.Cheats[0].Description);
        Assert.True(File.Exists(Path.Combine(GetCoreDir(game), "Test Game.cht")));
        Assert.True(File.Exists(Path.Combine(GetCoreDir(game), "source.txt")));
    }

    [Fact]
    public async Task LoadCheatsAsync_FetchReturns404_ReturnsNotFound()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var game = MakeGameWithRom("Test Game.nes");

        var result = await service.LoadCheatsAsync(game, NesPlatform);

        Assert.Equal(CheatFetchOutcome.NotFound, result.Outcome);
        Assert.False(Directory.Exists(Path.Combine(_cheatsDirectory, game.Id.ToString())));
    }

    [Fact]
    public async Task LoadCheatsAsync_UsesRomFileNameForDatabaseLookup()
    {
        string? requestedUrl = null;
        var service = CreateService(req =>
        {
            requestedUrl = req.RequestUri!.AbsoluteUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ValidCheatFile) };
        });

        var game = new Game { Id = Guid.NewGuid(), Name = "Super Mario World" };
        game.Roms.Add(new GameRom { Path = @"D:\roms\Super Mario World (USA).sfc" });

        var snes = RomPlatformCatalog.FindByPlatformName("Super Nintendo Entertainment System")!;
        var result = await service.LoadCheatsAsync(game, snes);

        Assert.Equal(CheatFetchOutcome.Success, result.Outcome);
        Assert.Contains("Super%20Mario%20World%20%28USA%29.cht", requestedUrl);
    }

    [Fact]
    public async Task ApplyCheatLaunchOverridesAsync_WritesOverrideFileUnderConfigDirectory()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var game = MakeGameWithRom("Test Game.nes");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"bridge_retroarch_{Guid.NewGuid():N}");
        var configDir = Path.Combine(tempRoot, "config");
        Directory.CreateDirectory(configDir);
        var exePath = Path.Combine(tempRoot, "retroarch.exe");
        await File.WriteAllTextAsync(exePath, string.Empty);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "retroarch.cfg"), "rgui_config_directory = \":config\"\n");

        try
        {
            await service.ApplyCheatLaunchOverridesAsync(
                game,
                NesPlatform,
                exePath,
                Path.Combine(_cheatsDirectory, game.Id.ToString()),
                autoApplyCheatsEnabled: true);

            var overridePath = Path.Combine(configDir, "FCEUmm", "Test Game.cfg");
            Assert.True(File.Exists(overridePath));
            var content = await File.ReadAllTextAsync(overridePath);
            Assert.Contains("cheat_database_path", content);
            Assert.Contains("apply_cheats_after_load = true", content);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
