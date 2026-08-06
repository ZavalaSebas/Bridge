using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class RomScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly RomScanner _scanner = new();
    private readonly EmulatorProfile _profile = new() { Id = "profile-1", ImageExtensions = ["nes"] };
    private readonly Guid _emulatorId = Guid.NewGuid();

    public RomScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bridge-romscan-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Scan_OnlyMatchesConfiguredExtension()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Game A.nes"), "rom");
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "not a rom");

        var found = _scanner.Scan(_tempDir, _emulatorId, _profile, existingGames: []);

        var game = Assert.Single(found);
        Assert.Equal("Game A", game.Name);
    }

    [Fact]
    public void Scan_CreatesAnEmulatorPlayActionWithTheGivenIds()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Game A.nes"), "rom");

        var found = _scanner.Scan(_tempDir, _emulatorId, _profile, existingGames: []);

        var action = Assert.Single(Assert.Single(found).GameActions);
        Assert.Equal(GameActionType.Emulator, action.Type);
        Assert.True(action.IsPlayAction);
        Assert.Equal(_emulatorId, action.EmulatorId);
        Assert.Equal(_profile.Id, action.EmulatorProfileId);
    }

    [Fact]
    public void Scan_SkipsFilesAlreadyImported()
    {
        var romPath = Path.Combine(_tempDir, "Game A.nes");
        File.WriteAllText(romPath, "rom");

        var alreadyImported = new Game { Name = "Game A" };
        alreadyImported.Roms.Add(new GameRom { Name = "Game A", Path = romPath });

        var found = _scanner.Scan(_tempDir, _emulatorId, _profile, existingGames: [alreadyImported]);

        Assert.Empty(found);
    }

    [Fact]
    public void Scan_ThrowsForMissingDirectory()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist");

        Assert.Throws<DirectoryNotFoundException>(() =>
            _scanner.Scan(missing, _emulatorId, _profile, existingGames: []));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
