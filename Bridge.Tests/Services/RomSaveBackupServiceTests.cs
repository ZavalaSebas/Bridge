using Bridge.Core.Entities;
using Bridge.Emulation;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class RomSaveBackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bridge-rom-save-bak-" + Guid.NewGuid().ToString("N"));
    private readonly string _ra;
    private readonly string _backups;

    public RomSaveBackupServiceTests()
    {
        Directory.CreateDirectory(_root);
        _ra = Path.Combine(_root, "retroarch");
        _backups = Path.Combine(_root, "save-backups");
        Directory.CreateDirectory(Path.Combine(_ra, "saves"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup */ }
    }

    [Fact]
    public void CreateRestore_RoundTripsSramAfterFolderDeleted()
    {
        var srm = Path.Combine(_ra, "saves", "Kirby.srm");
        File.WriteAllText(srm, "progress");
        var game = RomGame("Kirby", @"C:\Roms\Kirby.sfc");

        var created = RomSaveBackupService.Create(game, RomSaveBackupKind.Manual, _ra, _backups);

        Assert.True(created.Success, created.Message);
        Assert.Equal(1, created.FileCount);
        File.Delete(srm);
        Directory.Delete(Path.Combine(_ra, "saves"));

        var snapshot = Assert.Single(RomSaveBackupService.List(game.Id, _backups));
        var restored = RomSaveBackupService.Restore(snapshot.DirectoryPath, @"C:\Roms\Kirby.sfc", _ra);

        Assert.True(restored.Success, restored.Message);
        Assert.True(File.Exists(srm));
        Assert.Equal("progress", File.ReadAllText(srm));
    }

    [Fact]
    public void Create_Automatic_SkipsIdenticalSnapshot()
    {
        File.WriteAllText(Path.Combine(_ra, "saves", "Same.srm"), "data");
        var game = RomGame("Same", @"C:\Roms\Same.sfc");

        Assert.True(RomSaveBackupService.Create(game, RomSaveBackupKind.Automatic, _ra, _backups).Success);
        var second = RomSaveBackupService.Create(game, RomSaveBackupKind.Automatic, _ra, _backups);

        Assert.True(second.Success);
        Assert.True(second.Unchanged);
        Assert.Single(RomSaveBackupService.List(game.Id, _backups));
    }

    [Fact]
    public void Create_Automatic_PrunesOlderThanMax()
    {
        var srm = Path.Combine(_ra, "saves", "Prune.srm");
        var game = RomGame("Prune", @"C:\Roms\Prune.sfc");

        for (var i = 0; i < RomSaveBackupService.MaxAutomaticBackups + 2; i++)
        {
            File.WriteAllText(srm, $"v{i}");
            var result = RomSaveBackupService.Create(game, RomSaveBackupKind.Automatic, _ra, _backups);
            Assert.True(result.Success, result.Message);
            Assert.False(result.Unchanged);
        }

        var remaining = RomSaveBackupService.List(game.Id, _backups)
            .Where(item => item.Kind == RomSaveBackupKind.Automatic)
            .ToList();
        Assert.Equal(RomSaveBackupService.MaxAutomaticBackups, remaining.Count);
    }

    [Fact]
    public void Create_ReturnsFailureWhenNoSaveFiles()
    {
        var game = RomGame("Empty", @"C:\Roms\Empty.sfc");

        var result = RomSaveBackupService.Create(game, RomSaveBackupKind.Manual, _ra, _backups);

        Assert.False(result.Success);
    }

    [Fact]
    public void CreateRestore_RoundTripsCustomSaveFolder()
    {
        var saves = Path.Combine(_root, "pc-saves");
        Directory.CreateDirectory(Path.Combine(saves, "slot1"));
        File.WriteAllText(Path.Combine(saves, "save.dat"), "progress");
        File.WriteAllText(Path.Combine(saves, "slot1", "quick.sav"), "quick");
        var game = new Game { Id = Guid.NewGuid(), Name = "PC Game" };

        var created = RomSaveBackupService.Create(
            game,
            RomSaveBackupKind.Manual,
            backupsRoot: _backups,
            customSaveFolder: saves);

        Assert.True(created.Success, created.Message);
        Assert.Equal(2, created.FileCount);
        File.Delete(Path.Combine(saves, "save.dat"));
        File.Delete(Path.Combine(saves, "slot1", "quick.sav"));

        var snapshot = Assert.Single(RomSaveBackupService.List(game.Id, _backups));
        var restored = RomSaveBackupService.Restore(
            snapshot.DirectoryPath,
            romPath: null,
            customSaveFolder: saves);

        Assert.True(restored.Success, restored.Message);
        Assert.Equal("progress", File.ReadAllText(Path.Combine(saves, "save.dat")));
        Assert.Equal("quick", File.ReadAllText(Path.Combine(saves, "slot1", "quick.sav")));
    }

    [Fact]
    public void Create_FailsForPcGameWithoutFolder()
    {
        var game = new Game { Id = Guid.NewGuid(), Name = "PC Game" };

        var result = RomSaveBackupService.Create(game, RomSaveBackupKind.Manual, backupsRoot: _backups);

        Assert.False(result.Success);
    }

    [Fact]
    public void EnumerateFolderSources_SkipsFilesOverMaxBytes()
    {
        var saves = Path.Combine(_root, "oversized");
        Directory.CreateDirectory(saves);
        File.WriteAllText(Path.Combine(saves, "small.sav"), "ok");
        var huge = Path.Combine(saves, "huge.bin");
        using (var stream = File.Create(huge))
            stream.SetLength(RomSaveBackupService.MaxFolderFileBytes + 1);

        var files = RomSaveBackupService.EnumerateFolderSources(saves);

        Assert.Equal("small.sav", Assert.Single(files).RelativePath);
    }

    private static Game RomGame(string name, string romPath) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Roms = [new GameRom { Path = romPath }]
    };
}
