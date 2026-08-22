using Bridge.Emulation;
using Bridge.Emulation.Dat;

namespace Bridge.Tests.Emulation;

public class RomCrc32Tests
{
    [Fact]
    public void ComputeHex_UsesClrmameCrc32Polynomial()
    {
        var bytes = "123456789"u8.ToArray();
        Assert.Equal("CBF43926", RomCrc32.ComputeHex(bytes));
    }
}

public class ClrmameDatParserTests
{
    private const string SampleDat = """
        clrmamepro (
            name "Nintendo - Game Boy"
            description "Nintendo - Game Boy"
        )

        game (
            name "Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe)"
            description "Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe)"
            region "USA"
            rom ( name "Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe).gb" size 1048576 crc 081BEE60 )
        )
        """;

    [Fact]
    public void Parse_IndexesGamesByCrc()
    {
        var table = ClrmameDatParser.Parse(SampleDat);

        var game = Assert.Single(table);
        Assert.Equal("081BEE60", game.Key);
        Assert.Equal("Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe)", game.Value.Name);
        Assert.Equal("USA", game.Value.Region);
        Assert.Equal(1048576u, Assert.Single(game.Value.Roms).Size);
    }

    [Fact]
    public void Parse_ExtractsRegionFromTitleWhenMissingRegionField()
    {
        const string dat = """
            game (
                name "Pokemon - Edicion Amarilla (Spain)"
                rom ( name "Pokemon - Edicion Amarilla (Spain).gb" size 1048576 crc 081BEE60 )
            )
            """;

        var table = ClrmameDatParser.Parse(dat);

        var game = Assert.Single(table);
        Assert.Equal("Spain", game.Value.Region);
    }

    [Fact]
    public void Parse_GameBlocksWithParenthesesInTitle_KeepCorrectCrcOwners()
    {
        const string dat = """
            game (
            	name "Odekake Lester - Lelele no Le (^^; (Japan)"
            	rom ( name "Odekake Lester - Lelele no Le (^^; (Japan).sfc" size 1048576 crc 8D89A8E8 )
            )
            game (
            	name "Super Mario World (USA)"
            	rom ( name "Super Mario World (USA).sfc" size 524288 crc B19ED489 )
            )
            """;

        var table = ClrmameDatParser.Parse(dat);

        Assert.Equal("Odekake Lester - Lelele no Le (^^; (Japan)", table["8D89A8E8"].Name);
        Assert.Equal("Super Mario World (USA)", table["B19ED489"].Name);
    }
}

public class RomDatMatcherTests
{
    [Fact]
    public void TryResolve_UsesRegisteredDatTable()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bridge-rom-dat-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var romPath = Path.Combine(tempDir, "Pokemon Yellow.gb");
            File.WriteAllBytes(romPath, new byte[1048576]);

            var expectedCrc = RomCrc32.ComputeHex(File.ReadAllBytes(romPath));
            var store = new RomDatStore { CacheDirectory = tempDir };
            store.RegisterTable(
                "Nintendo - Game Boy.dat",
                new Dictionary<string, RomDatGame>
                {
                    [expectedCrc] = new RomDatGame(
                        "Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe)",
                        "USA",
                        [new RomDatRom(1048576, expectedCrc)])
                });

            var matcher = new RomDatMatcher(store);

            Assert.True(matcher.TryResolve(romPath, out var canonicalName, out var crcHex));
            Assert.Equal("Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe)", canonicalName);
            Assert.Equal(expectedCrc, crcHex);

            Assert.True(matcher.TryMatch(romPath, out var match));
            Assert.Equal("USA", match!.Region);
            Assert.Equal("Game Boy", match.PlatformName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
