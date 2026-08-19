using Bridge.Emulation;

namespace Bridge.Tests.Emulation;

public class CheatFileParserTests
{
    private const string RealNesCheatFile = """
        cheats = 3

        cheat0_desc = "10 Power Points"
        cheat0_code = "ZESNLLLE"
        cheat0_enable = false

        cheat1_desc = "20 Power Points"
        cheat1_code = "GOSNLLLA"
        cheat1_enable = false

        cheat2_desc = "Infinite Power"
        cheat2_code = "SXVLZXSE+VVOULXVK"
        cheat2_enable = true
        """;

    [Fact]
    public void Parse_RealLibretroDatabaseFile_ReturnsAllCheatsWithCorrectDescAndEnable()
    {
        var result = CheatFileParser.Parse(RealNesCheatFile);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Cheats.Count);
        Assert.Equal("10 Power Points", result.Cheats[0].Description);
        Assert.False(result.Cheats[0].Enabled);
        Assert.Equal("Infinite Power", result.Cheats[2].Description);
        Assert.True(result.Cheats[2].Enabled);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsInvalid()
    {
        var result = CheatFileParser.Parse(string.Empty);

        Assert.False(result.IsValid);
        Assert.Empty(result.Cheats);
    }

    [Fact]
    public void Parse_HeaderCountExceedsActualEntries_ReturnsInvalid()
    {
        var text = """
            cheats = 3

            cheat0_desc = "Only One"
            cheat0_enable = false
            """;

        var result = CheatFileParser.Parse(text);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void SetEnabled_TogglesOnlyTheTargetLine()
    {
        var updated = CheatFileParser.SetEnabled(RealNesCheatFile, 0, true);

        var reparsed = CheatFileParser.Parse(updated);
        Assert.True(reparsed.IsValid);
        Assert.True(reparsed.Cheats[0].Enabled);
        Assert.False(reparsed.Cheats[1].Enabled);
    }

    [Fact]
    public void SetEnabled_IndexNotPresent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CheatFileParser.SetEnabled(RealNesCheatFile, 99, true));
    }
}
