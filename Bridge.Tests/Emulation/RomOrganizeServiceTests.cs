using Bridge.Emulation;
using System.IO.Compression;

namespace Bridge.Tests.Emulation;

public class RomOrganizeServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bridge-rom-org-" + Guid.NewGuid().ToString("N"));

    public RomOrganizeServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup */ }
    }

    [Fact]
    public void Organize_MovesLooseRomIntoPlatformFolderWithOfficialName()
    {
        var source = Path.Combine(_root, "dump.nes");
        File.WriteAllText(source, "rom");

        var result = RomOrganizeService.Organize(
            [new RomOrganizeTarget(source, "Super Mario Bros. (USA)", "Nintendo Entertainment System", Skip: false)],
            _root);

        var expected = Path.Combine(_root, "Nintendo Entertainment System", "Super Mario Bros. (USA).nes");
        Assert.Single(result.Changes);
        Assert.Equal(expected, result.Changes[0].NewRomPath);
        Assert.True(File.Exists(expected));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public void Organize_MovesAndRenamesSidecarSram()
    {
        var source = Path.Combine(_root, "dump.gba");
        var srm = Path.Combine(_root, "dump.srm");
        File.WriteAllText(source, "rom");
        File.WriteAllText(srm, "save");

        RomOrganizeService.Organize(
            [new RomOrganizeTarget(source, "Metroid Fusion (USA)", "Game Boy Advance", Skip: false)],
            _root);

        var destDir = Path.Combine(_root, "Game Boy Advance");
        Assert.True(File.Exists(Path.Combine(destDir, "Metroid Fusion (USA).gba")));
        Assert.True(File.Exists(Path.Combine(destDir, "Metroid Fusion (USA).srm")));
        Assert.False(File.Exists(srm));
    }

    [Fact]
    public void Organize_SkipsWhenAlreadyInPlace()
    {
        var destDir = Path.Combine(_root, "Game Boy");
        Directory.CreateDirectory(destDir);
        var path = Path.Combine(destDir, "Tetris (USA).gb");
        File.WriteAllText(path, "rom");

        var result = RomOrganizeService.Organize(
            [new RomOrganizeTarget(path, "Tetris (USA)", "Game Boy", Skip: false)],
            _root);

        Assert.Empty(result.Changes);
        Assert.Equal(1, result.Unchanged);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Organize_AddsSuffixWhenTargetExists()
    {
        var destDir = Path.Combine(_root, "Nintendo Entertainment System");
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(destDir, "Clash.nes"), "other");
        var source = Path.Combine(_root, "new.nes");
        File.WriteAllText(source, "rom");

        var result = RomOrganizeService.Organize(
            [new RomOrganizeTarget(source, "Clash", "Nintendo Entertainment System", Skip: false)],
            _root);

        Assert.Equal(
            Path.Combine(destDir, "Clash (2).nes"),
            Assert.Single(result.Changes).NewRomPath);
    }

    [Fact]
    public void Organize_SkipsFilesOutsideScanRoot()
    {
        var outside = Path.Combine(Path.GetTempPath(), "bridge-rom-org-out-" + Guid.NewGuid().ToString("N") + ".nes");
        File.WriteAllText(outside, "rom");
        try
        {
            var result = RomOrganizeService.Organize(
                [new RomOrganizeTarget(outside, "Outside", "Nintendo Entertainment System", Skip: false)],
                _root);

            Assert.Equal(1, result.Skipped);
            Assert.True(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void Organize_RenamesSingleGameZip()
    {
        var zip = Path.Combine(_root, "pack.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            archive.CreateEntry("Inner.sfc");
        }

        var romPath = zip + "#Inner.sfc";
        var result = RomOrganizeService.Organize(
            [new RomOrganizeTarget(romPath, "Chrono Trigger (USA)", "Super Nintendo Entertainment System", Skip: false)],
            _root);

        var destZip = Path.Combine(_root, "Super Nintendo Entertainment System", "Chrono Trigger (USA).zip");
        Assert.Equal(destZip + "#Inner.sfc", Assert.Single(result.Changes).NewRomPath);
        Assert.True(File.Exists(destZip));
        Assert.False(File.Exists(zip));
    }

    [Fact]
    public void Organize_SharedZipMovesWithoutRenamingToOneGame()
    {
        var zip = Path.Combine(_root, "pack.zip");
        File.WriteAllBytes(zip, [1, 2, 3]);
        var first = zip + "#One.sfc";
        var second = zip + "#Two.sfc";

        var result = RomOrganizeService.Organize(
            [
                new RomOrganizeTarget(first, "One (USA)", "Super Nintendo Entertainment System", Skip: false),
                new RomOrganizeTarget(second, "Two (USA)", "Super Nintendo Entertainment System", Skip: false)
            ],
            _root);

        var destZip = Path.Combine(_root, "Super Nintendo Entertainment System", "pack.zip");
        Assert.Equal(2, result.Changes.Count);
        Assert.Contains(result.Changes, change => change.NewRomPath == destZip + "#One.sfc");
        Assert.Contains(result.Changes, change => change.NewRomPath == destZip + "#Two.sfc");
        Assert.True(File.Exists(destZip));
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidCharacters()
    {
        Assert.Equal("Game Subtitle", RomOrganizeService.SanitizeFileName("Game: Subtitle"));
        Assert.Equal("ROM", RomOrganizeService.SanitizeFileName("   "));
    }
}
