using System.IO.Compression;
using Bridge.Core.Entities;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class RomLibraryPackServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bridge-rom-pack-" + Guid.NewGuid().ToString("N"));
    private readonly string _ra;
    private readonly string _backups;
    private readonly string _roms;

    public RomLibraryPackServiceTests()
    {
        Directory.CreateDirectory(_root);
        _ra = Path.Combine(_root, "retroarch");
        _backups = Path.Combine(_root, "save-backups");
        _roms = Path.Combine(_root, "roms");
        Directory.CreateDirectory(Path.Combine(_ra, "saves"));
        Directory.CreateDirectory(_roms);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup */ }
    }

    [Fact]
    public void CreateAndImport_RestoresSavesAndSmallRoms()
    {
        var romPath = Path.Combine(_roms, "Tiny.sfc");
        File.WriteAllText(romPath, "rom-bytes");
        File.WriteAllText(Path.Combine(_ra, "saves", "Tiny.srm"), "save");
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = "Tiny",
            Roms = [new GameRom { Path = romPath }]
        };

        var zip = Path.Combine(_root, "pack.zip");
        var created = RomLibraryPackService.Create([game], zip, _ra, _backups, maxRomBytes: 1024);

        Assert.True(created.Success, created.Message);
        Assert.Equal(1, created.GamesWithSaves);
        Assert.Equal(1, created.RomsIncluded);

        File.Delete(romPath);
        File.Delete(Path.Combine(_ra, "saves", "Tiny.srm"));

        var fallback = Path.Combine(_root, "imported");
        var imported = RomLibraryPackService.Import(zip, fallback, _ra);

        Assert.True(imported.Success, imported.Message);
        Assert.Equal(1, imported.SavesRestored);
        Assert.Equal(1, imported.RomsCopied);
        Assert.True(File.Exists(Path.Combine(_ra, "saves", "Tiny.srm")));
        Assert.True(File.Exists(romPath));
    }

    [Fact]
    public void Create_SkipsRomsOverMaxBytes()
    {
        var romPath = Path.Combine(_roms, "Huge.sfc");
        File.WriteAllBytes(romPath, new byte[64]);
        File.WriteAllText(Path.Combine(_ra, "saves", "Huge.srm"), "save");
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = "Huge",
            Roms = [new GameRom { Path = romPath }]
        };

        var zip = Path.Combine(_root, "pack.zip");
        var created = RomLibraryPackService.Create([game], zip, _ra, _backups, maxRomBytes: 8);

        Assert.True(created.Success, created.Message);
        Assert.Equal(1, created.GamesWithSaves);
        Assert.Equal(0, created.RomsIncluded);
        Assert.Equal(1, created.RomsSkipped);

        using var archive = ZipFile.OpenRead(zip);
        Assert.DoesNotContain(archive.Entries, entry => entry.Name.Equals("Huge.sfc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsLibraryBackupZip()
    {
        var zip = Path.Combine(_root, "library.zip");
        var staging = Path.Combine(_root, "staging");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "backup-manifest.json"), "{}");
        ZipFile.CreateFromDirectory(staging, zip);

        var result = RomLibraryPackService.Validate(zip);

        Assert.False(result.Success);
    }

    [Fact]
    public void CreateAndImport_RestoresMappedPcSaveFolder()
    {
        var saves = Path.Combine(_root, "steam-saves");
        Directory.CreateDirectory(saves);
        File.WriteAllText(Path.Combine(saves, "slot.sav"), "cloud");
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = "Steam Title"
        };

        var zip = Path.Combine(_root, "pc-pack.zip");
        var created = RomLibraryPackService.Create(
            [game],
            zip,
            _ra,
            _backups,
            customSaveFolders: new Dictionary<Guid, string> { [game.Id] = saves });

        Assert.True(created.Success, created.Message);
        Assert.Equal(1, created.GamesWithSaves);
        Assert.Equal(0, created.RomsIncluded);

        File.Delete(Path.Combine(saves, "slot.sav"));

        var imported = RomLibraryPackService.Import(zip, Path.Combine(_root, "imported"), _ra);

        Assert.True(imported.Success, imported.Message);
        Assert.Equal(1, imported.SavesRestored);
        Assert.Equal("cloud", File.ReadAllText(Path.Combine(saves, "slot.sav")));
        Assert.Equal(saves, Assert.Contains(game.Id, imported.RestoredSaveFolders!));
    }

    [Fact]
    public void Create_SkipsPcGameWithoutMappedFolder()
    {
        var game = new Game { Id = Guid.NewGuid(), Name = "Unmapped" };
        var zip = Path.Combine(_root, "empty-pc.zip");

        var created = RomLibraryPackService.Create([game], zip, _ra, _backups);

        Assert.True(created.Success, created.Message);
        Assert.Equal(0, created.GamesWithSaves);
    }
}
