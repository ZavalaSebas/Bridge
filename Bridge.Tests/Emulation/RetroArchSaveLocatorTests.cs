using Bridge.Emulation;

namespace Bridge.Tests.Emulation;

public class RetroArchSaveLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bridge-ra-saves-" + Guid.NewGuid().ToString("N"));

    public RetroArchSaveLocatorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup */ }
    }

    [Fact]
    public void TryFind_ReturnsCanonicalSavesEvenWhenFolderMissing()
    {
        var found = RetroArchSaveLocator.TryFind(_root, @"C:\Roms\Missing (USA).sfc");

        Assert.Equal(Path.Combine(_root, "saves"), found);
    }

    [Fact]
    public void TryFind_FindsSrmNextToRom()
    {
        var romDir = Path.Combine(_root, "roms");
        Directory.CreateDirectory(romDir);
        var romPath = Path.Combine(romDir, "Metroid (USA).gba");
        File.WriteAllText(romPath, "rom");
        File.WriteAllText(Path.Combine(romDir, "Metroid (USA).srm"), "sram");

        var found = RetroArchSaveLocator.TryFind(_root, romPath);

        Assert.Equal(romDir, found);
    }

    [Fact]
    public void TryFind_FindsSavestateInStatesFolder()
    {
        var states = Path.Combine(_root, "states");
        Directory.CreateDirectory(states);
        File.WriteAllText(Path.Combine(states, "Kirby.state1"), "slot");

        var found = RetroArchSaveLocator.TryFind(_root, @"C:\Roms\Kirby.sfc");

        Assert.Equal(states, found);
    }

    [Fact]
    public void TryFind_ReturnsFolderContainingSrm()
    {
        var saves = Path.Combine(_root, "saves");
        Directory.CreateDirectory(saves);
        var srm = Path.Combine(saves, "Super Mario World (USA).srm");
        File.WriteAllText(srm, "sram");

        var found = RetroArchSaveLocator.TryFind(_root, @"C:\Roms\Super Mario World (USA).sfc");

        Assert.Equal(saves, found);
    }

    [Fact]
    public void TryFind_FindsSrmInCoreSubfolder()
    {
        var coreSaves = Path.Combine(_root, "saves", "Snes9x");
        Directory.CreateDirectory(coreSaves);
        File.WriteAllText(Path.Combine(coreSaves, "Zelda.srm"), "sram");

        var found = RetroArchSaveLocator.TryFind(_root, @"C:\Roms\Zelda.smc");

        Assert.Equal(coreSaves, found);
    }

    [Fact]
    public void TryFind_HonorsConfiguredSavefileDirectory()
    {
        var custom = Path.Combine(_root, "custom-saves");
        Directory.CreateDirectory(custom);
        File.WriteAllText(Path.Combine(_root, "retroarch.cfg"), $"savefile_directory = \"{custom}\"\n");

        var found = RetroArchSaveLocator.TryFind(_root, null);

        Assert.Equal(custom, found);
    }

    [Fact]
    public void TryFind_UsesArchiveEntryBasename()
    {
        var saves = Path.Combine(_root, "saves");
        Directory.CreateDirectory(saves);
        File.WriteAllText(Path.Combine(saves, "Inner Game.srm"), "sram");

        var found = RetroArchSaveLocator.TryFind(_root, @"C:\Roms\pack.zip#Inner Game.sfc");

        Assert.Equal(saves, found);
    }

    [Fact]
    public void EnumerateSaveFiles_FindsSramAndSavestate()
    {
        var saves = Path.Combine(_root, "saves");
        var states = Path.Combine(_root, "states");
        Directory.CreateDirectory(saves);
        Directory.CreateDirectory(states);
        var srm = Path.Combine(saves, "Zelda.srm");
        var state = Path.Combine(states, "Zelda.state1");
        File.WriteAllText(srm, "sram");
        File.WriteAllText(state, "slot");

        var files = RetroArchSaveLocator.EnumerateSaveFiles(_root, @"C:\Roms\Zelda.sfc");

        Assert.Contains(files, file => file.Path == srm && file.Role == RomSaveRole.Saves);
        Assert.Contains(files, file => file.Path == state && file.Role == RomSaveRole.States);
    }

    [Fact]
    public void EnumerateSaveFiles_FindsSramNextToRom()
    {
        var romDir = Path.Combine(_root, "roms");
        Directory.CreateDirectory(romDir);
        var romPath = Path.Combine(romDir, "Metroid (USA).gba");
        var srm = Path.Combine(romDir, "Metroid (USA).srm");
        File.WriteAllText(romPath, "rom");
        File.WriteAllText(srm, "sram");

        var files = RetroArchSaveLocator.EnumerateSaveFiles(_root, romPath);

        Assert.Contains(files, file => file.Path == srm && file.Role == RomSaveRole.Content);
    }
}
