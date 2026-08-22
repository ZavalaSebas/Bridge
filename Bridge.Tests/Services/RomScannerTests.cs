using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Emulation;
using Bridge.Emulation.Dat;
using System.IO.Compression;

namespace Bridge.Tests.Services;

public class RomScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly RomScanner _scanner = new(RomDatMatcher.Disabled);

    public RomScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bridge-romscan-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Scan_OnlyMatchesSupportedRomExtension()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Game A.nes"), "rom");
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "not a rom");

        var found = _scanner.Scan(_tempDir, existingGames: []);

        var game = Assert.Single(found);
        Assert.Equal("Game A", game.Name);
    }

    [Fact]
    public void Scan_CreatesABridgeManagedRetroArchPlayAction()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Game A.nes"), "rom");

        var found = _scanner.Scan(_tempDir, existingGames: []);

        var action = Assert.Single(Assert.Single(found).GameActions);
        Assert.Equal(GameActionType.Emulator, action.Type);
        Assert.True(action.IsPlayAction);
        Assert.Equal("Bridge RetroArch", action.Name);
    }

    [Fact]
    public void Scan_SkipsFilesAlreadyImported()
    {
        var romPath = Path.Combine(_tempDir, "Game A.nes");
        File.WriteAllText(romPath, "rom");

        var alreadyImported = new Game { Name = "Game A" };
        alreadyImported.Roms.Add(new GameRom { Name = "Game A", Path = romPath });

        var found = _scanner.Scan(_tempDir, existingGames: [alreadyImported]);

        Assert.Empty(found);
    }

    [Fact]
    public void Scan_ThrowsForMissingDirectory()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist");

        Assert.Throws<DirectoryNotFoundException>(() =>
            _scanner.Scan(missing, existingGames: []));
    }

    [Theory]
    [InlineData("Super Mario [U][!]", "Super Mario")]
    [InlineData("Zelda (USA)", "Zelda")]
    [InlineData("Final_Fantasy_VII", "Final Fantasy VII")]
    [InlineData("Metroid_Prime™", "Metroid Prime")]
    [InlineData("Sonic (Europe) (Rev 1)", "Sonic")]
    [InlineData("Mario Kart - Double Dash!!", "Mario Kart - Double Dash!!")]
    public void SanitizeName_StripsTagsAndNormalizes(string raw, string expected)
    {
        Assert.Equal(expected, RomScanner.SanitizeName(raw));
    }

    [Theory]
    [InlineData("Pokemon - Emerald Version", "Pokemon Emerald Version")]
    [InlineData("Pokemon - Emerald Version (USA)", "Pokemon Emerald Version")]
    [InlineData("Super Mario [U][!]", "Super Mario")]
    [InlineData("Zelda (USA)", "Zelda")]
    [InlineData("Mario Kart - Double Dash!!", "Mario Kart Double Dash!!")]
    public void ToSearchName_NormalizesForIgdbSearch(string raw, string expected)
    {
        Assert.Equal(expected, RomScanner.ToSearchName(raw));
    }

    [Fact]
    public void GetMetadataSearchNames_SpanishPokemonTitle_IncludesEnglishVariants()
    {
        var names = RomScanner.GetMetadataSearchNames("Pokemon Amarillo");

        Assert.Contains("Pokemon Amarillo", names);
        Assert.Contains("Pokemon Yellow Version", names);
    }

    [Theory]
    [InlineData(
        "Pokemon - Edicion Amarilla - Edicion Especial Pikachu  Español",
        "Pokemon Yellow Version")]
    [InlineData("Pokemon - Edicion Cristal  Español", "Pokemon Crystal Version")]
    [InlineData("Pokemon - Edicion Platino  Español", "Pokemon Platinum Version")]
    [InlineData("Pokemon Snap  Español", "Pokemon Snap")]
    [InlineData("Alien Storm  Español", "Alien Storm")]
    [InlineData(
        "From TV Animation One Piece - Grand Battle Swan Colosseum (Japan) (Sample)",
        "One Piece Grand Battle Swan Colosseum")]
    public void GetMetadataSearchNames_SpanishRomNames_IncludeIgdbFriendlyCandidate(string raw, string expected)
    {
        var names = RomScanner.GetMetadataSearchNames(raw);

        Assert.Contains(expected, names);
    }

    [Fact]
    public void SanitizeName_StripsTrailingLanguageTag()
    {
        Assert.Equal(
            "Pokemon - Edicion Amarilla - Edicion Especial Pikachu",
            RomScanner.SanitizeName("Pokemon - Edicion Amarilla - Edicion Especial Pikachu [GB] Español"));
    }

    [Fact]
    public void Scan_UsesSanitizedNameForGameAndRom()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Super Mario [U][!].nes"), "rom");

        var found = _scanner.Scan(_tempDir, existingGames: []);

        var game = Assert.Single(found);
        Assert.Equal("Super Mario", game.Name);
        Assert.Equal("Super Mario", Assert.Single(game.Roms).Name);
    }

    [Fact]
    public void Scan_RecursesIntoSubfoldersAndSkipsCompanionFiles()
    {
        var nested = Path.Combine(_tempDir, "Nintendo", "Saves");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Game B.gba"), "rom");
        File.WriteAllText(Path.Combine(nested, "Game B.sav"), "save");

        var found = _scanner.Scan(_tempDir, existingGames: []);

        var game = Assert.Single(found);
        Assert.Equal("Game B", game.Name);
        Assert.EndsWith("Game B.gba", Assert.Single(game.Roms).Path);
    }

    [Fact]
    public void Scan_ImportsRomInsideZipWithRetroArchArchivePath()
    {
        var zipPath = Path.Combine(_tempDir, "Super Mario World (USA).zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("Super Mario World (USA).sfc");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("rom");
        }

        var found = _scanner.Scan(_tempDir, existingGames: []);

        var game = Assert.Single(found);
        Assert.Equal("Super Mario World", game.Name);
        Assert.Equal(
            RomArchivePath.Combine(zipPath, "Super Mario World (USA).sfc"),
            Assert.Single(game.Roms).Path);
    }

    [Fact]
    public void Scan_SkipsZipWithoutSupportedRomEntries()
    {
        var zipPath = Path.Combine(_tempDir, "notes.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("readme.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("not a rom");
        }

        var found = _scanner.Scan(_tempDir, existingGames: []);

        Assert.Empty(found);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
